using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Controllers;
using Student_Management_System.Models;
using Student_Management_System.Services;

internal static class AdmissionWorkflowVerification
{
    public static async Task RunAsync(ApplicationDbContext db, UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles, int campusId, string actorId, Action<bool, string> check)
    {
        if (!await roles.RoleExistsAsync("Student")) await roles.CreateAsync(new IdentityRole("Student"));
        var teacher = await db.Teachers.FirstAsync();
        var department = new Department { Code = "WFLOW", Name = "Workflow department" };
        var classroom = new Class { Name = "Workflow class", Department = department, TeacherId = teacher.Id };
        var section = new Section { Name = "Workflow section", Class = classroom };
        db.Sections.Add(section); await db.SaveChangesAsync();
        var application = new AdmissionApplication { ReferenceNumber = "WORKFLOW-1", ApplicantName = "Workflow candidate", Email = "workflow-candidate@test.invalid", Phone = "8999999986",
            DateOfBirth = DateTime.Today.AddYears(-18), DepartmentId = department.Id, ClassId = classroom.Id, CampusId = campusId,
            AcademicYearId = await db.AcademicYears.Select(y => y.Id).FirstAsync(), ProgramName = "Workflow program", Stage = AdmissionStage.Submitted };
        db.AdmissionApplications.Add(application); await db.SaveChangesAsync();
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId), new Claim(ClaimTypes.Role, "SuperAdmin")], "test")) };
        var controller = new AdmissionsController(db, users, new AuditService(db, new HttpContextAccessor { HttpContext = http }), new UnavailableInitialDelivery(), null!, new CampusScopeService(db))
        { ControllerContext = new ControllerContext { HttpContext = http }, TempData = new TempDataDictionary(http, new MemoryTempData()) };
        check(await controller.Enroll(application.Id, section.Id) is UnprocessableEntityObjectResult, "Enrollment must not bypass approval.");
        application.Stage = AdmissionStage.Approved; await db.SaveChangesAsync();
        check(await controller.Enroll(application.Id, section.Id) is RedirectToActionResult, "Committed enrollment must survive immediate invitation setup failure.");
        var student = await db.Students.AsNoTracking().Include(s => s.User).SingleAsync(s => s.Id == application.EnrolledStudentId);
        var account = student.User ?? throw new InvalidOperationException("The enrolled student's Identity account is missing.");
        check(account.PhoneNumber == application.Phone && student.ClassId == classroom.Id && student.SectionId == section.Id,
            "Enrollment must synchronize the supplied Identity phone and authoritative placement.");
        check(await db.StudentClassEnrollments.AnyAsync(e => e.StudentId == student.Id && e.AcademicYearId == application.AcademicYearId) &&
            await db.UserCampusMemberships.AnyAsync(m => m.UserId == student.UserId && m.CampusId == campusId), "Enrollment must persist its class history and campus membership.");
        check(await db.OutboxMessages.AnyAsync(m => m.Type == AccountActivationRequests.MessageType && m.PayloadJson.Contains(student.UserId!)) && controller.TempData.ContainsKey("Warning"),
            "Committed enrollment must retain durable invitation recovery and accurate delivery feedback.");
        check(await controller.Enroll(application.Id, section.Id) is UnprocessableEntityObjectResult && await db.Students.CountAsync(s => s.UserId == student.UserId) == 1,
            "Repeated enrollment must not duplicate students.");
    }
    private sealed class MemoryTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
    private sealed class UnavailableInitialDelivery : IStudentAccountService
    {
        public Task<ActivationInvitation> CreateActivationAsync(ApplicationUser user, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Fixture interruption");
        public Task<bool> SendActivationAsync(ApplicationUser user, string url, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IdentityResult> ActivateAsync(string email, string token, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
