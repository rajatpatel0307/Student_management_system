using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;
using System.Data;
using System.Text;
using System.Text.Json;

namespace Student_Management_System.Controllers;

[Authorize]
[PermissionAuthorize("fees.view")]
public sealed class FinanceController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly IFinanceLedgerService _ledger;
    private readonly IInvoiceService _invoices;
    public FinanceController(
        ApplicationDbContext db,
        AuditService audit,
        IFinanceLedgerService ledger,
        IInvoiceService invoices)
    {
        _db = db;
        _audit = audit;
        _ledger = ledger;
        _invoices = invoices;
    }

    [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
    public async Task<IActionResult> Index(
        [FromQuery] ListQuery request,
        FinancialTransactionType? type,
        string? paymentMethod)
    {
        request.Normalize();
        paymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? null : paymentMethod.Trim();
        var query = ApplyFilters(BaseTransactionQuery(), request, type, paymentMethod);
        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)request.PageSize));
        request.Page = Math.Min(request.Page, totalPages);

        var today = DateTime.UtcNow.Date;
        ViewBag.Today = await query
            .Where(t => t.CreatedUtc >= today && t.CreatedUtc < today.AddDays(1))
            .SumAsync(t => (decimal?)t.Amount) ?? 0;
        ViewBag.Gross = await query.Where(t => t.Type == FinancialTransactionType.Payment)
            .SumAsync(t => (decimal?)t.Amount) ?? 0;
        ViewBag.Net = await query.SumAsync(t => (decimal?)t.Amount) ?? 0;
        ViewBag.Reversals = -(await query
            .Where(t => t.Type == FinancialTransactionType.Refund ||
                        t.Type == FinancialTransactionType.Reversal)
            .SumAsync(t => (decimal?)t.Amount) ?? 0);
        ViewBag.Type = type;
        ViewBag.PaymentMethod = paymentMethod;
        ViewBag.PaymentMethods = await _db.FinancialTransactions.AsNoTracking()
            .Select(t => t.PaymentMethod).Distinct().OrderBy(x => x).ToListAsync();

        query = (request.Sort?.ToLowerInvariant(), request.Direction) switch
        {
            ("receipt", "desc") => query.OrderByDescending(t => t.ReceiptNumber),
            ("receipt", _) => query.OrderBy(t => t.ReceiptNumber),
            ("student", "desc") => query.OrderByDescending(t => t.Student.FullName),
            ("student", _) => query.OrderBy(t => t.Student.FullName),
            ("amount", "desc") => query.OrderByDescending(t => t.Amount),
            ("amount", _) => query.OrderBy(t => t.Amount),
            ("date", "asc") => query.OrderBy(t => t.CreatedUtc),
            _ => query.OrderByDescending(t => t.CreatedUtc)
        };

        var items = await query.Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize).ToListAsync();
        return View(new PagedResult<FinancialTransaction>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            Search = request.Search,
            Sort = request.Sort,
            Direction = request.Direction
        });
    }

    [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
    [PermissionAuthorize("fees.collect")]
    public async Task<IActionResult> Collect(int? studentId)
    {
        await LoadOptionsAsync();
        return View(new CollectPaymentViewModel { StudentId = studentId.GetValueOrDefault() });
    }

    [PermissionAuthorize("fees.collect")]
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,Accountant")]
    public async Task<IActionResult> Collect(CollectPaymentViewModel input)
    {
        var student = await _db.Students.AsNoTracking().SingleOrDefaultAsync(s => s.Id == input.StudentId);
        var fee = await _db.Fees.AsNoTracking().SingleOrDefaultAsync(f => f.Id == input.FeeId);
        if (student == null) ModelState.AddModelError(nameof(input.StudentId), "Select a valid student.");
        if (fee == null) ModelState.AddModelError(nameof(input.FeeId), "Select a valid fee.");
        if (student != null && fee != null && student.ClassId != fee.ClassId)
            ModelState.AddModelError(nameof(input.FeeId), "This fee does not apply to the selected student's class.");
        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync();
            return View(input);
        }

        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        FinancialTransaction entry;
        try
        {
            entry = await ExecuteFinancialUnitAsync(async ct =>
            {
                var payment = await _ledger.CollectAsync(input, actor, ct);
                await _invoices.AllocatePaymentAsync(payment.Id, ct);
                _db.OutboxMessages.Add(new OutboxMessage
                {
                    Type = "finance.payment-collected",
                    PayloadJson = JsonSerializer.Serialize(new
                    {
                        TransactionId = payment.Id,
                        payment.ReceiptNumber,
                        payment.StudentId,
                        payment.Amount
                    })
                });
                await _db.SaveChangesAsync(ct);
                return payment;
            }, HttpContext.RequestAborted);
        }
        catch (PaymentRequestConflictException exception)
        {
            return Conflict(exception.Message);
        }
        TempData["Success"] = $"Payment recorded. Receipt: {entry.ReceiptNumber}";
        return RedirectToAction(nameof(Details), new { id = entry.Id });
    }

    [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
    public async Task<IActionResult> Invoices(
        [FromQuery] ListQuery request,
        CancellationToken cancellationToken)
    {
        request.Normalize();
        var query = _db.StudentInvoices.AsNoTracking()
            .Include(i => i.Student).ThenInclude(s => s.Department)
            .Include(i => i.AcademicYear)
            .Include(i => i.Lines)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(i =>
                i.InvoiceNumber.Contains(request.Search) ||
                i.Student.FullName.Contains(request.Search) ||
                i.Student.Email.Contains(request.Search));
        if (request.DepartmentId.HasValue)
            query = query.Where(i => i.Student.DepartmentId == request.DepartmentId);
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<InvoiceStatus>(request.Status, true, out var status))
            query = query.Where(i => i.Status == status);
        ViewBag.Students = await _db.Students.AsNoTracking()
            .Where(s => !s.IsArchived).OrderBy(s => s.FullName).Take(2000).ToListAsync(cancellationToken);
        ViewBag.Years = await _db.AcademicYears.AsNoTracking()
            .Where(y => !y.IsArchived).OrderByDescending(y => y.StartsOn).ToListAsync(cancellationToken);
        ViewBag.Fees = await _db.Fees.AsNoTracking()
            .Where(f => !f.IsArchived).OrderBy(f => f.Term).ToListAsync(cancellationToken);
        return View(await query.OrderByDescending(i => i.IssuedUtc)
            .ToPagedResultAsync(request, cancellationToken));
    }

    [HttpPost, Authorize(Roles = "SuperAdmin,Admin,Accountant")]
    [PermissionAuthorize("fees.adjust")]
    public async Task<IActionResult> IssueInvoice(
        int studentId,
        int academicYearId,
        List<int> feeIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await _invoices.IssueAsync(
                studentId,
                academicYearId,
                feeIds,
                User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                cancellationToken);
            TempData["Success"] = $"Invoice {invoice.InvoiceNumber} issued.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        return RedirectToAction(nameof(Invoices));
    }

    public async Task<IActionResult> Details(long id)
    {
        var entry = await _db.FinancialTransactions.AsNoTracking()
            .Include(t => t.Student).ThenInclude(s => s.Class)
            .Include(t => t.Student).ThenInclude(s => s.Section)
            .Include(t => t.Fee).Include(t => t.ReversesTransaction)
            .SingleOrDefaultAsync(t => t.Id == id);
        if (entry == null) return NotFound();
        if (!await CanViewStudentAsync(entry.StudentId)) return Forbid();
        var returned = entry.Type == FinancialTransactionType.Payment
            ? -(await _db.FinancialTransactions.AsNoTracking()
                .Where(t => t.ReversesTransactionId == id &&
                            t.Status == FinancialTransactionStatus.Completed)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m)
            : 0m;
        ViewBag.HasReversal = returned >= entry.Amount;
        ViewBag.RefundedAmount = returned;
        ViewBag.RefundableAmount = Math.Max(0, entry.Amount - returned);
        ViewBag.Institution = await _db.Institutions.AsNoTracking()
            .Where(institution => institution.IsActive)
            .OrderBy(institution => institution.Id)
            .FirstOrDefaultAsync();
        var feePaid = await _db.FinancialTransactions.AsNoTracking()
            .Where(transaction => transaction.StudentId == entry.StudentId &&
                                  transaction.FeeId == entry.FeeId &&
                                  transaction.Status == FinancialTransactionStatus.Completed)
            .SumAsync(transaction => (decimal?)transaction.Amount) ?? 0m;
        ViewBag.FeePaid = feePaid;
        ViewBag.FeeBalance = Math.Max(0m, entry.Fee.Amount - feePaid);
        return View(entry);
    }

    public async Task<IActionResult> Ledger(
        int studentId,
        [FromQuery] ListQuery request,
        FinancialTransactionType? type)
    {
        if (!await CanViewStudentAsync(studentId)) return Forbid();
        request.Normalize();
        var student = await _db.Students.AsNoTracking().Include(s => s.Class)
            .SingleOrDefaultAsync(s => s.Id == studentId);
        if (student == null) return NotFound();
        var allTransactions = BaseTransactionQuery().Where(t => t.StudentId == studentId);
        var query = ApplyFilters(allTransactions, request, type, null);
        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)request.PageSize));
        request.Page = Math.Min(request.Page, totalPages);
        ViewBag.Student = student;
        var charges = await _db.Fees.AsNoTracking().Include(f => f.Class)
            .Where(f => f.ClassId == student.ClassId)
            .OrderBy(f => f.DueDate).ToListAsync();
        ViewBag.Charges = charges;
        ViewBag.Type = type;
        ViewBag.NetPaid = await allTransactions.SumAsync(t => (decimal?)t.Amount) ?? 0;
        ViewBag.PaymentCount = await allTransactions.CountAsync(t => t.Type == FinancialTransactionType.Payment);
        ViewBag.PaidByFee = await allTransactions.GroupBy(t => t.FeeId)
            .Select(group => new { FeeId = group.Key, Amount = group.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.FeeId, x => x.Amount);
        ViewBag.Balance = await _ledger.GetBalanceAsync(studentId, DateTime.UtcNow);
        var items = await query.OrderByDescending(t => t.CreatedUtc)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync();
        return View(new PagedResult<FinancialTransaction>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            Search = request.Search,
            Sort = "date",
            Direction = "desc"
        });
    }

    [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
    public async Task<FileResult> Export(
        [FromQuery] ListQuery request,
        FinancialTransactionType? type,
        string? paymentMethod)
    {
        request.Normalize();
        paymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? null : paymentMethod.Trim();
        var items = await ApplyFilters(BaseTransactionQuery(), request, type, paymentMethod)
            .OrderByDescending(t => t.CreatedUtc).Take(10000).ToListAsync();
        var csv = new StringBuilder();
        csv.AppendLine("Receipt,Date,Student,Email,Fee,Type,Method,Reference,Amount,Notes");
        foreach (var item in items)
        {
            csv.AppendLine(string.Join(",",
                Csv(item.ReceiptNumber),
                Csv(item.CreatedUtc.ToString("O")),
                Csv(item.Student.FullName),
                Csv(item.Student.Email),
                Csv(item.Fee.Term),
                Csv(item.Type.ToString()),
                Csv(item.PaymentMethod),
                Csv(item.TransactionReference),
                item.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Csv(item.Notes)));
        }
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv",
            $"finance-ledger-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }

    [PermissionAuthorize("fees.refund")]
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,Accountant")]
    public async Task<IActionResult> Reverse(long id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return BadRequest("A reversal reason is required.");
        FinancialTransaction reversal;
        try
        {
            reversal = await ExecuteFinancialUnitAsync(async ct =>
            {
                var result = await _ledger.ReverseAsync(
                    id, reason, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", ct);
                await RefreshAllocatedInvoicesAsync(id, ct);
                _db.OutboxMessages.Add(new OutboxMessage
                {
                    Type = "finance.payment-reversed",
                    PayloadJson = JsonSerializer.Serialize(new { TransactionId = id, ReversalId = result.Id })
                });
                await _db.SaveChangesAsync(ct);
                return result;
            }, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        TempData["Success"] = "Payment reversed through an immutable reversal entry.";
        return RedirectToAction(nameof(Details), new { id = reversal.Id });
    }

    [PermissionAuthorize("fees.refund")]
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,Accountant")]
    public async Task<IActionResult> Refund(
        long id, decimal amount, string reason, string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest("A refund reason is required.");
        try
        {
            var refund = await ExecuteFinancialUnitAsync(async ct =>
            {
                var result = await _ledger.RefundAsync(
                    id, amount, reason,
                    User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
                    idempotencyKey, ct);
                await RefreshAllocatedInvoicesAsync(id, ct);
                _db.OutboxMessages.Add(new OutboxMessage
                {
                    Type = "finance.payment-refunded",
                    PayloadJson = JsonSerializer.Serialize(new
                    {
                        TransactionId = id,
                        RefundId = result.Id,
                        result.Amount
                    })
                });
                await _db.SaveChangesAsync(ct);
                return result;
            }, HttpContext.RequestAborted);
            TempData["Success"] = "Refund recorded through the immutable ledger.";
            return RedirectToAction(nameof(Details), new { id = refund.Id });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(exception.Message); }
    }

    [PermissionAuthorize("fees.adjust")]
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,Accountant")]
    public async Task<IActionResult> Adjust(
        int studentId,
        int feeId,
        decimal amount,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest("An adjustment reason is required.");
        try
        {
            var adjustment = await _ledger.AdjustAsync(
                studentId, feeId, amount, reason,
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
            TempData["Success"] = "Adjustment recorded through the immutable ledger.";
            return RedirectToAction(nameof(Details), new { id = adjustment.Id });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }

    private async Task<bool> CanViewStudentAsync(int studentId)
    {
        if (User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("Accountant")) return true;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.IsInRole("Student"))
            return await _db.Students.AnyAsync(s => s.Id == studentId && s.UserId == userId);
        if (User.IsInRole("Guardian"))
            return await _db.GuardianStudentLinks.AnyAsync(l =>
                l.StudentId == studentId && l.CanViewFinance && l.GuardianProfile.UserId == userId);
        return false;
    }

    private async Task RefreshAllocatedInvoicesAsync(
        long transactionId,
        CancellationToken cancellationToken = default)
    {
        var invoiceIds = await _db.PaymentAllocations.AsNoTracking()
            .Where(a => a.FinancialTransactionId == transactionId)
            .Select(a => a.StudentInvoiceLine.StudentInvoiceId)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var invoiceId in invoiceIds)
            await _invoices.RefreshStatusAsync(invoiceId, cancellationToken);
    }

    private async Task<T> ExecuteFinancialUnitAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
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

    private async Task LoadOptionsAsync()
    {
        ViewBag.Students = await _db.Students.AsNoTracking().Include(s => s.Class)
            .OrderBy(s => s.FullName).ToListAsync();
        ViewBag.Fees = await _db.Fees.AsNoTracking().Include(f => f.Class)
            .OrderBy(f => f.Class!.Name).ThenBy(f => f.Term).ToListAsync();
    }

    private IQueryable<FinancialTransaction> BaseTransactionQuery()
    {
        return _db.FinancialTransactions.AsNoTracking()
            .Include(t => t.Student).ThenInclude(s => s.Class)
            .Include(t => t.Fee).AsQueryable();
    }

    private static IQueryable<FinancialTransaction> ApplyFilters(
        IQueryable<FinancialTransaction> query,
        ListQuery request,
        FinancialTransactionType? type,
        string? paymentMethod)
    {
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search!;
            query = query.Where(t => t.ReceiptNumber.Contains(term) ||
                                     t.Student.FullName.Contains(term) ||
                                     t.Student.Email.Contains(term) ||
                                     t.PaymentMethod.Contains(term) ||
                                     (t.TransactionReference != null && t.TransactionReference.Contains(term)) ||
                                     t.Fee.Term.Contains(term));
        }
        if (type.HasValue) query = query.Where(t => t.Type == type.Value);
        if (!string.IsNullOrWhiteSpace(paymentMethod))
            query = query.Where(t => t.PaymentMethod == paymentMethod);
        if (request.From.HasValue)
            query = query.Where(t => t.CreatedUtc >= request.From.Value.Date);
        if (request.To.HasValue)
        {
            var exclusiveEnd = request.To.Value.Date.AddDays(1);
            query = query.Where(t => t.CreatedUtc < exclusiveEnd);
        }
        return query;
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var safeValue = value[0] is '=' or '+' or '-' or '@' ? $"'{value}" : value;
        return $"\"{safeValue.Replace("\"", "\"\"")}\"";
    }

}
