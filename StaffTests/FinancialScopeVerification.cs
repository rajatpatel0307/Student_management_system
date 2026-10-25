using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Controllers;
using Student_Management_System.Models;
using Student_Management_System.Services;

internal static class FinancialScopeVerification
{
    public static async Task RunAsync(ApplicationDbContext db, string actorId, Action<bool, string> check)
    {
        var own = await db.Students.SingleAsync(student => student.UserId == "scope-local");
        var foreign = await db.Students.SingleAsync(student => student.UserId == "scope-foreign");
        var campus = await db.Campuses.SingleAsync(c => c.Code == "OTHER"); campus.IsActive = true;
        var fee = new Fee { ClassId = own.ClassId, Term = "Scoped finance fee", Amount = 100, DueDate = DateTime.Today.AddDays(30) };
        var ownPayment = new FinancialTransaction { StudentId = own.Id, Fee = fee, ReceiptNumber = "SCOPE-OWN", Amount = 50, Type = FinancialTransactionType.Payment, CreatedByUserId = actorId };
        var foreignPayment = new FinancialTransaction { StudentId = foreign.Id, Fee = fee, ReceiptNumber = "SCOPE-FOREIGN", Amount = 50, Type = FinancialTransactionType.Payment, CreatedByUserId = actorId };
        db.FinancialTransactions.AddRange(ownPayment, foreignPayment); await db.SaveChangesAsync();
        var year = await db.AcademicYears.FirstAsync();
        db.StudentInvoices.Add(new StudentInvoice { InvoiceNumber = "SCOPE-INVOICE", StudentId = own.Id, AcademicYearId = year.Id, IssuedUtc = DateTime.UtcNow, DueUtc = DateTime.UtcNow.AddDays(30), TotalAmount = 100, Status = InvoiceStatus.Issued });
        await db.SaveChangesAsync();
        ClaimsPrincipal Principal(string role) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId), new Claim(ClaimTypes.Role, role)], "test"));
        var accountant = Principal("Accountant");
        var controller = new FinanceController(db, null!, null!, null!) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = accountant } } };
        var page = (PagedResult<FinancialTransaction>)((ViewResult)await controller.Index(new ListQuery(), null, null)).Model!;
        check(page.Items.Count == 1 && page.Items.Single().Id == ownPayment.Id, "Accountant listings must exclude another active campus's transactions.");
        check(await controller.Details(foreignPayment.Id) is ForbidResult && await controller.Details(ownPayment.Id) is ViewResult,
            "Receipt details must enforce campus scope independently of visible links.");
        var count = await db.FinancialTransactions.CountAsync();
        check(await controller.Collect(new CollectPaymentViewModel { StudentId = foreign.Id, FeeId = fee.Id, Amount = 10 }) is ForbidResult &&
              await controller.Adjust(foreign.Id, fee.Id, 1, "Denied adjustment") is ForbidResult &&
              await controller.Reverse(foreignPayment.Id, "Denied reversal") is ForbidResult &&
              await controller.Refund(foreignPayment.Id, 1, "Denied refund", "denied-idempotency") is ForbidResult &&
              await controller.IssueInvoice(foreign.Id, 1, [fee.Id], CancellationToken.None) is ForbidResult,
            "Changed IDs must not permit cross-campus collection, adjustment, reversal, refund or invoicing.");
        check(await db.FinancialTransactions.CountAsync() == count, "Denied finance commands must not create ledger rows.");
        var reports = new ReportingService(db);
        var report = await reports.BuildAsync(new ReportCenterQuery { ReportType = "Finance" }, accountant);
        check(report.Rows.Count == 1 && report.Rows.Single().Contains("SCOPE-OWN"), "Finance reports must use the same campus boundary as the ledger.");
        var profile = await db.GuardianProfiles.FirstAsync(g => g.UserId == actorId);
        var link = new GuardianStudentLink { GuardianProfileId = profile.Id, StudentId = own.Id, CanViewFinance = true, UnlinkedUtc = DateTime.UtcNow };
        db.GuardianStudentLinks.Add(link); await db.SaveChangesAsync();
        controller.ControllerContext.HttpContext.User = Principal("Guardian");
        check(await controller.Details(ownPayment.Id) is ForbidResult, "Revoked guardian links must not allow receipt access even when their old finance flag is true.");
        link.UnlinkedUtc = null; await db.SaveChangesAsync();
        check(await controller.Details(ownPayment.Id) is ViewResult && await controller.Details(foreignPayment.Id) is ForbidResult,
            "An active guardian finance link must allow only its linked student's records.");
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, own.UserId!), new Claim(ClaimTypes.Role, "Student")], "test"));
        check(await controller.Details(foreignPayment.Id) is ForbidResult, "Student receipt access must reject another student's identifier.");

        var librarian = Principal("Librarian");
        var reportController = new ReportsController(db, reports) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = librarian } } };
        check(await reportController.Index(new ReportCenterQuery(), CancellationToken.None) is ViewResult && ((ReportCenterQuery)reportController.ViewBag.Query).ReportType == "Library",
            "A librarian opening Reports without filters must get an allowed default instead of forbidden Enrollment.");
        var saved = new SavedReport { Name = "Private library filter", OwnerUserId = foreign.UserId!, ReportType = "Library", FilterJson = JsonSerializer.Serialize(new ReportCenterQuery { ReportType = "Library" }) };
        db.SavedReports.Add(saved); await db.SaveChangesAsync();
        check(await reportController.Index(new ReportCenterQuery(), CancellationToken.None, saved.Id) is NotFoundResult, "Private saved filters must not load by changing the saved-report identifier.");
        saved.IsShared = true; await db.SaveChangesAsync();
        check(await reportController.Index(new ReportCenterQuery(), CancellationToken.None, saved.Id) is ViewResult, "An explicitly shared saved filter must remain usable with current role checks.");
        check(ReportingService.AvailableReportTypes(Principal("ExaminationOfficer")).SequenceEqual(new[] { "Results" }), "Examination officers must have a relevant results report entry point.");
    }
}
