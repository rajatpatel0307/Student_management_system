using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;

internal static class AttendanceScopeVerification
{
    public static async Task RunAsync(ApplicationDbContext db, Action<bool, string> check)
    {
        var teacher = await db.Teachers.SingleAsync(t => t.UserId == "scope-hod");
        var own = await db.Students.SingleAsync(s => s.UserId == "scope-local");
        var foreign = await db.Students.SingleAsync(s => s.UserId == "scope-foreign");
        var classroom = new Class { Name = "Z attendance scope", DepartmentId = teacher.DepartmentId, TeacherId = teacher.Id };
        var subject = new Subject { Name = "Z attendance scope", Class = classroom, DepartmentId = teacher.DepartmentId };
        db.Subjects.Add(subject); await db.SaveChangesAsync();
        var previousClass = own.ClassId;
        own.ClassId = classroom.Id; foreign.ClassId = classroom.Id; await db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, teacher.UserId!), new Claim(ClaimTypes.Role, "HOD")], "test"));
        var service = new AttendanceService(db);
        async Task Reject(Func<Task> action, string message)
        {
            try { await action(); } catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException) { check(true, message); return; }
            check(false, message);
        }
        await Reject(() => service.SubmitAsync(classroom.Id, subject.Id, teacher.Id, DateTime.Today, "Scope", [new Attendance { StudentId = foreign.Id, Status = "Present" }], teacher.UserId!), "Foreign attendance rosters must be rejected.");
        await Reject(() => service.SubmitAsync(classroom.Id, subject.Id, teacher.Id, DateTime.Today, "Scope", [new Attendance { StudentId = own.Id, Status = "Present" }], "another-user"), "A forged teacher identity must be rejected.");
        await Reject(() => service.SubmitAsync(classroom.Id, subject.Id, teacher.Id, DateTime.Today, "Scope", [new Attendance { StudentId = own.Id, Status = "Present" }, new Attendance { StudentId = own.Id, Status = "Absent" }], teacher.UserId!), "Duplicate attendance rosters must be rejected.");
        var session = await service.SubmitAsync(classroom.Id, subject.Id, teacher.Id, DateTime.Today, "Scope", [new Attendance { Id = int.MaxValue, StudentId = own.Id, Status = "Late", Date = DateTime.MinValue, TeacherId = -1, Lecture = "Forged", Student = new Student { FullName = "Injected graph" } }], teacher.UserId!);
        var record = await db.Attendances.SingleAsync(a => a.AttendanceSessionId == session.Id);
        check(record.Id != int.MaxValue && record.TeacherId == teacher.Id && record.Date == DateTime.Today && record.Lecture == "Scope", "Attendance persistence must ignore posted IDs and metadata.");
        check(!await db.Students.AnyAsync(s => s.FullName == "Injected graph"), "Posted attendance navigation graphs must never be persisted.");
        var other = new Attendance { StudentId = foreign.Id, SubjectId = subject.Id, TeacherId = teacher.Id, Date = DateTime.Today, Status = "Absent", AttendanceSessionId = session.Id };
        db.Attendances.Add(other); await db.SaveChangesAsync();
        var http = new DefaultHttpContext { User = principal };
        var controller = new AttendanceController(db, new AuditService(db, new HttpContextAccessor { HttpContext = http }), service)
        { ControllerContext = new ControllerContext { HttpContext = http } };
        check(await controller.Edit(other.Id) is ForbidResult && await controller.Edit(other.Id, "Present", null) is ForbidResult, "Direct attendance edit URLs and posts must enforce campus scope.");
        check(await controller.ApproveSession(session.Id, session.RowVersion, default) is ForbidResult, "Whole-session approval must reject a mixed-campus roster.");
        var page = (PagedResult<AttendanceSession>)((ViewResult)await controller.Sessions(default)).Model!;
        check(!page.Items.Any(s => s.Id == session.Id), "Session listings must exclude partially visible rosters.");
        var original = record.UpdatedUtc;
        record.Status = "Present"; record.UpdatedUtc = DateTime.UtcNow; await db.SaveChangesAsync();
        check(await controller.Edit(record.Id, "Absent", "Stale edit", original, true) is ConflictObjectResult, "A stale attendance edit must return a conflict.");
        db.ChangeTracker.Clear();
        check(await db.Attendances.Where(a => a.Id == record.Id).Select(a => a.Status).SingleAsync() == "Present", "A stale edit must preserve the committed attendance status.");
        // Restore the pre-existing academic fixture for later native-page tests.
        own = await db.Students.SingleAsync(s => s.Id == own.Id); foreign = await db.Students.SingleAsync(s => s.Id == foreign.Id);
        own.ClassId = previousClass; foreign.ClassId = previousClass; await db.SaveChangesAsync();
    }
}
