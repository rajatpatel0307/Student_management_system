using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using System.Data;
using System.Text.Json;

namespace Student_Management_System.Services;

public interface IInvoiceService
{
    Task<StudentInvoice> IssueAsync(
        int studentId,
        int academicYearId,
        IReadOnlyCollection<int> feeIds,
        string actorUserId,
        CancellationToken cancellationToken = default);
    Task AllocatePaymentAsync(
        long financialTransactionId,
        CancellationToken cancellationToken = default);
    Task RefreshStatusAsync(long invoiceId, CancellationToken cancellationToken = default);
}

public sealed class InvoiceService : IInvoiceService
{
    private readonly ApplicationDbContext _db;

    public InvoiceService(ApplicationDbContext db) => _db = db;

    public Task<StudentInvoice> IssueAsync(
        int studentId,
        int academicYearId,
        IReadOnlyCollection<int> feeIds,
        string actorUserId,
        CancellationToken cancellationToken = default) =>
        ExecuteSerializableAsync(async ct =>
        {
            if (feeIds.Count == 0)
                throw new InvalidOperationException("Select at least one fee.");
            if (!await _db.AcademicYears.AnyAsync(
                    y => y.Id == academicYearId && !y.IsArchived, ct))
                throw new InvalidOperationException("Select a valid academic year.");

            var student = await _db.Students.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == studentId && !s.IsArchived, ct)
                ?? throw new KeyNotFoundException("Student not found.");
            var enrollment = await _db.StudentClassEnrollments.AsNoTracking()
                .Where(e => e.StudentId == studentId &&
                            e.AcademicYearId == academicYearId &&
                            e.ClassId == student.ClassId)
                .OrderByDescending(e => e.IsActive)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException(
                    "The student has no matching class enrollment for the selected academic year.");
            var fees = await _db.Fees.AsNoTracking()
                .Where(f => feeIds.Contains(f.Id) &&
                            f.ClassId == enrollment.ClassId &&
                            !f.IsArchived)
                .OrderBy(f => f.DueDate).ThenBy(f => f.Id)
                .ToListAsync(ct);
            if (fees.Count != feeIds.Distinct().Count())
                throw new InvalidOperationException("One or more fees are invalid for this enrollment.");

            var defaultCategory = await _db.FeeCategories.SingleOrDefaultAsync(
                c => c.Code == "TUITION", ct);
            if (defaultCategory == null)
            {
                defaultCategory = new FeeCategory
                {
                    Code = "TUITION",
                    Name = "Tuition and academic fees"
                };
                _db.FeeCategories.Add(defaultCategory);
            }

            var invoice = new StudentInvoice
            {
                InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..32],
                StudentId = studentId,
                AcademicYearId = academicYearId,
                IssuedUtc = DateTime.UtcNow,
                DueUtc = DateTime.SpecifyKind(fees.Max(f => f.DueDate), DateTimeKind.Utc),
                Status = InvoiceStatus.Issued,
                TotalAmount = decimal.Round(fees.Sum(f => f.Amount), 2),
                Lines = fees.Select(fee => new StudentInvoiceLine
                {
                    FeeId = fee.Id,
                    StudentClassEnrollmentId = enrollment.Id,
                    FeeCategory = defaultCategory,
                    Description = fee.Term,
                    Amount = decimal.Round(fee.Amount, 2)
                }).ToList()
            };
            _db.StudentInvoices.Add(invoice);

            var remainingInvoiceAmount = invoice.TotalAmount;
            var awards = await _db.StudentAidAwards
                .Include(a => a.Allocations)
                .Where(a => a.StudentId == studentId &&
                            a.Status == ApprovalStatus.Approved &&
                            a.StartsOn <= DateTime.UtcNow.Date &&
                            (!a.EndsOn.HasValue || a.EndsOn >= DateTime.UtcNow.Date))
                .OrderBy(a => a.EndsOn).ThenBy(a => a.Id)
                .ToListAsync(ct);
            foreach (var award in awards)
            {
                var consumed = award.Allocations
                    .Where(a => !a.ReversedUtc.HasValue)
                    .Sum(a => a.Amount);
                var available = decimal.Round(Math.Max(0, award.Amount - consumed), 2);
                var amount = Math.Min(available, remainingInvoiceAmount);
                if (amount <= 0) continue;
                invoice.AidAllocations.Add(new StudentAidAllocation
                {
                    StudentAidAward = award,
                    Amount = amount,
                    AllocatedByUserId = actorUserId
                });
                remainingInvoiceAmount -= amount;
            }
            invoice.AidAmount = decimal.Round(
                invoice.AidAllocations.Sum(a => a.Amount), 2);
            if (invoice.Lines.Count > 0)
                invoice.Lines.First().DiscountAmount = invoice.AidAmount;

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = actorUserId,
                Action = "Issued student invoice and allocated approved aid",
                EntityName = nameof(StudentInvoice),
                EntityId = invoice.InvoiceNumber
            });
            _db.OutboxMessages.Add(new OutboxMessage
            {
                Type = "finance.invoice-issued",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    invoice.InvoiceNumber,
                    invoice.StudentId,
                    invoice.AcademicYearId,
                    invoice.TotalAmount,
                    invoice.AidAmount
                })
            });
            await _db.SaveChangesAsync(ct);
            return invoice;
        }, cancellationToken);

    public Task AllocatePaymentAsync(
        long financialTransactionId,
        CancellationToken cancellationToken = default) =>
        ExecuteSerializableAsync(async ct =>
        {
            var payment = await _db.FinancialTransactions.AsNoTracking()
                .SingleOrDefaultAsync(t =>
                    t.Id == financialTransactionId &&
                    t.Type == FinancialTransactionType.Payment &&
                    t.Status == FinancialTransactionStatus.Completed, ct)
                ?? throw new InvalidOperationException("Completed payment transaction not found.");
            if (await _db.PaymentAllocations.AnyAsync(
                    a => a.FinancialTransactionId == financialTransactionId, ct))
                return true;

            var invoices = await _db.StudentInvoices
                .Include(i => i.Lines)
                .Where(i => i.StudentId == payment.StudentId &&
                            i.Status != InvoiceStatus.Cancelled &&
                            i.Status != InvoiceStatus.Paid)
                .OrderBy(i => i.DueUtc).ThenBy(i => i.Id)
                .ToListAsync(ct);
            var remaining = decimal.Round(payment.Amount, 2);
            foreach (var invoice in invoices)
            {
                foreach (var line in invoice.Lines
                             .OrderByDescending(l => l.FeeId == payment.FeeId)
                             .ThenBy(l => l.Id))
                {
                    var already = await _db.PaymentAllocations
                        .Where(a => a.StudentInvoiceLineId == line.Id)
                        .SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;
                    var lineBalance = decimal.Round(Math.Max(0,
                        line.Amount + line.LateFeeAmount -
                        line.DiscountAmount - already), 2);
                    if (lineBalance == 0 || remaining == 0) continue;
                    var allocated = Math.Min(lineBalance, remaining);
                    _db.PaymentAllocations.Add(new PaymentAllocation
                    {
                        FinancialTransactionId = payment.Id,
                        StudentInvoiceLineId = line.Id,
                        Amount = allocated
                    });
                    remaining -= allocated;
                }
                if (remaining == 0) break;
            }
            await _db.SaveChangesAsync(ct);
            foreach (var invoiceId in invoices.Select(i => i.Id))
                await RefreshStatusCoreAsync(invoiceId, ct);
            await _db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

    public Task RefreshStatusAsync(
        long invoiceId,
        CancellationToken cancellationToken = default) =>
        ExecuteSerializableAsync(async ct =>
        {
            await RefreshStatusCoreAsync(invoiceId, ct);
            await _db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

    private async Task RefreshStatusCoreAsync(long invoiceId, CancellationToken ct)
    {
        var invoice = await _db.StudentInvoices
            .SingleOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new KeyNotFoundException("Invoice not found.");
        if (invoice.Status == InvoiceStatus.Cancelled) return;

        var targetAllocations = await _db.PaymentAllocations.AsNoTracking()
            .Where(a => a.StudentInvoiceLine.StudentInvoiceId == invoiceId)
            .Select(a => new
            {
                a.Id,
                a.Amount,
                a.FinancialTransactionId
            })
            .ToListAsync(ct);
        var paymentIds = targetAllocations.Select(a => a.FinancialTransactionId)
            .Distinct().ToArray();
        decimal paid = 0m;
        foreach (var paymentId in paymentIds)
        {
            var payment = await _db.FinancialTransactions.AsNoTracking()
                .SingleAsync(t => t.Id == paymentId, ct);
            if (payment.Status != FinancialTransactionStatus.Completed) continue;
            var returned = -(await _db.FinancialTransactions.AsNoTracking()
                .Where(t => t.ReversesTransactionId == paymentId &&
                            t.Status == FinancialTransactionStatus.Completed &&
                            (t.Type == FinancialTransactionType.Refund ||
                             t.Type == FinancialTransactionType.Reversal))
                .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m);
            var remaining = decimal.Round(Math.Max(0, payment.Amount - returned), 2);
            var allAllocations = await _db.PaymentAllocations.AsNoTracking()
                .Where(a => a.FinancialTransactionId == paymentId)
                .OrderBy(a => a.Id)
                .Select(a => new
                {
                    a.Id,
                    a.Amount,
                    InvoiceId = a.StudentInvoiceLine.StudentInvoiceId
                })
                .ToListAsync(ct);
            foreach (var allocation in allAllocations)
            {
                var contribution = Math.Min(allocation.Amount, remaining);
                if (allocation.InvoiceId == invoiceId) paid += contribution;
                remaining -= contribution;
                if (remaining <= 0) break;
            }
        }
        invoice.PaidAmount = decimal.Round(paid, 2);
        UpdateStatus(invoice);
    }

    private static void UpdateStatus(StudentInvoice invoice)
    {
        var due = decimal.Round(Math.Max(0,
            invoice.TotalAmount - invoice.AidAmount), 2);
        invoice.Status = invoice.PaidAmount >= due
            ? InvoiceStatus.Paid
            : invoice.PaidAmount > 0
                ? InvoiceStatus.PartiallyPaid
                : invoice.DueUtc < DateTime.UtcNow
                    ? InvoiceStatus.Overdue
                    : InvoiceStatus.Issued;
    }

    private async Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        if (_db.Database.CurrentTransaction != null)
            return await operation(cancellationToken);
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            try
            {
                var result = await operation(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
