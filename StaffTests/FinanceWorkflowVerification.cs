using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Controllers;
using Student_Management_System.Models;
using Student_Management_System.Services;

internal static class FinanceWorkflowVerification
{
    public static async Task RunAsync(ApplicationDbContext db, string actorId, Action<bool, string> check)
    {
        var student = await db.Students.SingleAsync(s => s.UserId == "scope-local");
        var year = new AcademicYear { Name = "Finance workflow year", StartsOn = DateTime.Today, EndsOn = DateTime.Today.AddYears(1) };
        var fee = new Fee { ClassId = student.ClassId, Term = "Finance workflow fee", Amount = 100, DueDate = DateTime.Today.AddDays(30) };
        db.StudentClassEnrollments.Add(new StudentClassEnrollment { StudentId = student.Id, ClassId = student.ClassId, SectionId = student.SectionId, AcademicYear = year });
        db.Fees.Add(fee); await db.SaveChangesAsync();
        var invoices = new InvoiceService(db); var ledger = new FinanceLedgerService(db);
        var invoice = await invoices.IssueAsync(student.Id, year.Id, [fee.Id], actorId);
        check(invoice.TotalAmount == 100 && invoice.Lines.Count == 1 && invoice.Lines.Single().FeeId == fee.Id, "Invoice issuance must retain authoritative fee and enrollment relationships.");
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId), new Claim(ClaimTypes.Role, "SuperAdmin")], "test")) };
        var controller = new FinanceController(db, new AuditService(db, new HttpContextAccessor { HttpContext = http }), ledger, invoices)
        { ControllerContext = new ControllerContext { HttpContext = http }, TempData = new TempDataDictionary(http, new MemoryTempData()) };
        var input = new CollectPaymentViewModel { StudentId = student.Id, FeeId = fee.Id, Amount = 60, PaymentMethod = "Cash", IdempotencyKey = "phase4-payment-1" };
        check(await controller.Collect(input) is RedirectToActionResult, "Authorized payment collection must commit through the real ledger/invoice unit.");
        var payment = await db.FinancialTransactions.AsNoTracking().SingleAsync(t => t.IdempotencyKey == input.IdempotencyKey);
        async Task<decimal> Paid() => await db.StudentInvoices.AsNoTracking().Where(i => i.Id == invoice.Id).Select(i => i.PaidAmount).SingleAsync();
        check(await Paid() == 60 && await db.PaymentAllocations.AnyAsync(a => a.FinancialTransactionId == payment.Id), "Payment collection must allocate and refresh the invoice.");
        check(await controller.Collect(input) is RedirectToActionResult && await db.FinancialTransactions.CountAsync(t => t.IdempotencyKey == input.IdempotencyKey) == 1 && await Paid() == 60,
            "A repeated payment key must not duplicate ledger or invoice totals.");
        input.Amount = 61;
        check(await controller.Collect(input) is ConflictObjectResult && await Paid() == 60, "A changed payment with a reused key must conflict without changing totals.");
        check(await controller.Refund(payment.Id, 20, "Fixture refund", "phase4-refund-1") is RedirectToActionResult && await Paid() == 40, "A partial refund must reduce invoice payment contribution.");
        check(await controller.Refund(payment.Id, 20, "Fixture refund", "phase4-refund-1") is RedirectToActionResult && await Paid() == 40, "A repeated refund must be idempotent.");
        check(await controller.Refund(payment.Id, 41, "Excess refund", "phase4-refund-excess") is ConflictObjectResult && await Paid() == 40, "Refunds must not exceed the remaining payment.");
        // The failed unit clears tracking; re-use only stable fixture identifiers.
        var next = new CollectPaymentViewModel { StudentId = student.Id, FeeId = fee.Id, Amount = 40, PaymentMethod = "Cash", IdempotencyKey = "phase4-payment-2" };
        check(await controller.Collect(next) is RedirectToActionResult && await Paid() == 80, "A second payment must accumulate the correct invoice balance.");
        var second = await db.FinancialTransactions.AsNoTracking().SingleAsync(t => t.IdempotencyKey == next.IdempotencyKey);
        check(await controller.Reverse(second.Id, "Fixture reversal") is RedirectToActionResult && await Paid() == 40, "Reversing a payment must restore the invoice's prior balance.");
        check(await controller.Reverse(second.Id, "Repeated reversal") is ConflictObjectResult && await Paid() == 40, "A second reversal must be rejected without changing balances.");
        check(await db.FinancialTransactions.AsNoTracking().Where(t => t.Id == payment.Id).Select(t => t.Amount).SingleAsync() == 60 &&
              await db.PaymentAllocations.AsNoTracking().Where(a => a.FinancialTransactionId == payment.Id).SumAsync(a => a.Amount) == 60,
            "Refunds/reversals must preserve immutable original payment and allocation records.");
    }
    private sealed class MemoryTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
