using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using System.Data;

namespace Student_Management_System.Services;

public sealed record FeeBalance(
    decimal Scheduled,
    decimal Paid,
    decimal ReversedOrRefunded,
    decimal Adjustments,
    decimal Outstanding,
    decimal Overdue);

public sealed class PaymentRequestConflictException : InvalidOperationException
{
    public PaymentRequestConflictException()
        : base("The request key is already used by a different payment request.") { }
}

public interface IFinanceLedgerService
{
    Task<FinancialTransaction> CollectAsync(CollectPaymentViewModel input, string actorUserId, CancellationToken cancellationToken = default);
    Task<FinancialTransaction> ReverseAsync(long transactionId, string reason, string actorUserId, CancellationToken cancellationToken = default);
    Task<FinancialTransaction> RefundAsync(long transactionId, decimal amount, string reason, string actorUserId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<FinancialTransaction> AdjustAsync(int studentId, int feeId, decimal amount, string reason, string actorUserId, CancellationToken cancellationToken = default);
    Task<FeeBalance> GetBalanceAsync(int studentId, DateTime asOfUtc, CancellationToken cancellationToken = default);
}

public sealed class FinanceLedgerService : IFinanceLedgerService
{
    private readonly ApplicationDbContext _db;

    public FinanceLedgerService(ApplicationDbContext db) => _db = db;

    public Task<FinancialTransaction> CollectAsync(
        CollectPaymentViewModel input,
        string actorUserId,
        CancellationToken cancellationToken = default) =>
        ExecuteTransactionAsync(async ct =>
        {
            var key = input.IdempotencyKey.Trim();
            var existing = await _db.FinancialTransactions
                .SingleOrDefaultAsync(t => t.IdempotencyKey == key, ct);
            if (existing != null)
            {
                if (existing.Type != FinancialTransactionType.Payment ||
                    existing.Status != FinancialTransactionStatus.Completed ||
                    existing.StudentId != input.StudentId || existing.FeeId != input.FeeId ||
                    existing.Amount != decimal.Round(input.Amount, 2, MidpointRounding.AwayFromZero) ||
                    existing.PaymentMethod != input.PaymentMethod.Trim() ||
                    (existing.TransactionReference ?? "") != (input.TransactionReference?.Trim() ?? "") ||
                    (existing.Notes ?? "") != (input.Notes?.Trim() ?? ""))
                    throw new PaymentRequestConflictException();
                return existing;
            }

            var student = await _db.Students.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == input.StudentId, ct)
                ?? throw new InvalidOperationException("Student not found.");
            var fee = await _db.Fees.AsNoTracking()
                .SingleOrDefaultAsync(f => f.Id == input.FeeId, ct)
                ?? throw new InvalidOperationException("Fee not found.");
            if (student.ClassId != fee.ClassId)
                throw new InvalidOperationException("The fee does not apply to this student's class.");

            var transaction = new FinancialTransaction
            {
                ReceiptNumber = await NextReceiptAsync("RCPT", ct),
                StudentId = input.StudentId,
                FeeId = input.FeeId,
                Type = FinancialTransactionType.Payment,
                Amount = input.Amount,
                PaymentMethod = input.PaymentMethod.Trim(),
                TransactionReference = input.TransactionReference?.Trim(),
                Notes = input.Notes?.Trim(),
                IdempotencyKey = key,
                CreatedByUserId = actorUserId
            };
            _db.FinancialTransactions.Add(transaction);
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = actorUserId,
                Action = "Collected fee payment",
                EntityName = nameof(FinancialTransaction),
                EntityId = transaction.ReceiptNumber
            });
            await _db.SaveChangesAsync(ct);
            return transaction;
        }, cancellationToken);

    public Task<FinancialTransaction> ReverseAsync(
        long transactionId,
        string reason,
        string actorUserId,
        CancellationToken cancellationToken = default) =>
        ExecuteTransactionAsync(async ct =>
        {
            var original = await _db.FinancialTransactions
                .SingleOrDefaultAsync(t => t.Id == transactionId, ct)
                ?? throw new KeyNotFoundException("Payment not found.");
            if (original.Type != FinancialTransactionType.Payment ||
                original.Status != FinancialTransactionStatus.Completed)
                throw new InvalidOperationException("Only completed payments can be reversed.");
            if (await _db.FinancialTransactions.AnyAsync(t =>
                    t.ReversesTransactionId == transactionId &&
                    t.Status == FinancialTransactionStatus.Completed, ct))
                throw new InvalidOperationException(
                    "A payment with a completed refund or reversal cannot be fully reversed.");

            var reversal = new FinancialTransaction
            {
                ReceiptNumber = await NextReceiptAsync("REV", ct),
                StudentId = original.StudentId,
                FeeId = original.FeeId,
                Type = FinancialTransactionType.Reversal,
                Amount = -original.Amount,
                PaymentMethod = original.PaymentMethod,
                TransactionReference = original.TransactionReference,
                Notes = reason.Trim(),
                CreatedByUserId = actorUserId,
                ReversesTransactionId = original.Id,
                IdempotencyKey = $"reversal:{original.Id}"
            };
            _db.FinancialTransactions.Add(reversal);
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = actorUserId,
                Action = "Reversed fee payment",
                EntityName = nameof(FinancialTransaction),
                EntityId = original.Id.ToString()
            });
            await _db.SaveChangesAsync(ct);
            return reversal;
        }, cancellationToken);

    public Task<FinancialTransaction> RefundAsync(
        long transactionId,
        decimal amount,
        string reason,
        string actorUserId,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        ExecuteTransactionAsync(async ct =>
        {
            idempotencyKey = idempotencyKey.Trim();
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new InvalidOperationException("A refund idempotency key is required.");
            var existing = await _db.FinancialTransactions
                .SingleOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, ct);
            if (existing != null)
            {
                if (existing.Type != FinancialTransactionType.Refund ||
                    existing.ReversesTransactionId != transactionId)
                    throw new InvalidOperationException("The request key is already used by another transaction.");
                return existing;
            }

            var original = await _db.FinancialTransactions
                .SingleOrDefaultAsync(t => t.Id == transactionId, ct)
                ?? throw new KeyNotFoundException("Payment not found.");
            if (original.Type != FinancialTransactionType.Payment ||
                original.Status != FinancialTransactionStatus.Completed)
                throw new InvalidOperationException("Only completed payments can be refunded.");
            amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            var returned = -(await _db.FinancialTransactions
                .Where(t => t.ReversesTransactionId == transactionId &&
                            t.Status == FinancialTransactionStatus.Completed &&
                            (t.Type == FinancialTransactionType.Refund ||
                             t.Type == FinancialTransactionType.Reversal))
                .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m);
            var refundable = decimal.Round(original.Amount - returned, 2);
            if (amount <= 0 || amount > refundable)
                throw new InvalidOperationException(
                    $"Refund amount must be positive and cannot exceed the remaining refundable amount ({refundable:0.00}).");

            var refund = new FinancialTransaction
            {
                ReceiptNumber = await NextReceiptAsync("REF", ct),
                StudentId = original.StudentId,
                FeeId = original.FeeId,
                Type = FinancialTransactionType.Refund,
                Amount = -amount,
                PaymentMethod = original.PaymentMethod,
                TransactionReference = original.TransactionReference,
                Notes = reason.Trim(),
                CreatedByUserId = actorUserId,
                ReversesTransactionId = original.Id,
                IdempotencyKey = idempotencyKey,
                Status = FinancialTransactionStatus.Completed
            };
            _db.FinancialTransactions.Add(refund);
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = actorUserId,
                Action = "Refunded fee payment",
                EntityName = nameof(FinancialTransaction),
                EntityId = original.Id.ToString()
            });
            await _db.SaveChangesAsync(ct);
            return refund;
        }, cancellationToken);

    public Task<FinancialTransaction> AdjustAsync(
        int studentId,
        int feeId,
        decimal amount,
        string reason,
        string actorUserId,
        CancellationToken cancellationToken = default) =>
        ExecuteTransactionAsync(async ct =>
        {
            if (amount == 0)
                throw new InvalidOperationException("Adjustment amount cannot be zero.");
            var valid = await _db.Students.AnyAsync(s =>
                s.Id == studentId &&
                _db.Fees.Any(f => f.Id == feeId && f.ClassId == s.ClassId), ct);
            if (!valid)
                throw new InvalidOperationException(
                    "The fee does not apply to this student.");
            var adjustment = new FinancialTransaction
            {
                ReceiptNumber = await NextReceiptAsync("ADJ", ct),
                StudentId = studentId,
                FeeId = feeId,
                Type = FinancialTransactionType.Adjustment,
                Amount = amount,
                PaymentMethod = "Adjustment",
                Notes = reason.Trim(),
                CreatedByUserId = actorUserId,
                IdempotencyKey = $"adjustment:{Guid.NewGuid():N}"
            };
            _db.FinancialTransactions.Add(adjustment);
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = actorUserId,
                Action = "Created fee adjustment",
                EntityName = nameof(FinancialTransaction),
                EntityId = adjustment.ReceiptNumber
            });
            await _db.SaveChangesAsync(ct);
            return adjustment;
        }, cancellationToken);

    public async Task<FeeBalance> GetBalanceAsync(
        int studentId,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default)
    {
        if (!await _db.Students.AsNoTracking().AnyAsync(
                s => s.Id == studentId, cancellationToken))
            throw new KeyNotFoundException("Student not found.");
        var invoices = _db.StudentInvoices.AsNoTracking().Where(i =>
            i.StudentId == studentId && i.Status != InvoiceStatus.Cancelled);
        var scheduled = await invoices.SumAsync(
            i => (decimal?)(i.TotalAmount - i.AidAmount), cancellationToken) ?? 0m;
        var remainingInvoices = invoices.Select(i => new
        {
            i.DueUtc,
            Remaining = i.TotalAmount - i.AidAmount - i.PaidAmount > 0
                ? i.TotalAmount - i.AidAmount - i.PaidAmount : 0m
        });
        var outstanding = await remainingInvoices.SumAsync(
            i => (decimal?)i.Remaining, cancellationToken) ?? 0m;
        var overdue = await remainingInvoices.Where(i => i.DueUtc < asOfUtc)
            .SumAsync(i => (decimal?)i.Remaining, cancellationToken) ?? 0m;
        var ledger = _db.FinancialTransactions.AsNoTracking().Where(t =>
            t.StudentId == studentId &&
            t.Status == FinancialTransactionStatus.Completed);
        var paid = await ledger.Where(t => t.Type == FinancialTransactionType.Payment)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0;
        var reversed = -(await ledger.Where(t =>
                t.Type == FinancialTransactionType.Reversal || t.Type == FinancialTransactionType.Refund)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0);
        var adjustments = await ledger.Where(t => t.Type == FinancialTransactionType.Adjustment)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0;
        return new FeeBalance(
            scheduled, paid, reversed, adjustments,
            outstanding, overdue);
    }

    private async Task<string> NextReceiptAsync(string prefix, CancellationToken cancellationToken)
    {
        var period = DateTime.UtcNow.ToString("yyyyMM");
        var sequence = await _db.ReceiptSequences.SingleOrDefaultAsync(s => s.Period == period, cancellationToken);
        if (sequence == null)
        {
            sequence = new ReceiptSequence { Period = period, NextValue = 2 };
            _db.ReceiptSequences.Add(sequence);
            return $"{prefix}-{period}-000001";
        }
        var number = sequence.NextValue++;
        return $"{prefix}-{period}-{number:000000}";
    }

    private async Task<T> ExecuteTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        if (_db.Database.CurrentTransaction != null)
            return await operation(cancellationToken);

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
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
