using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;

internal static class AcademicScopeVerification
{
    public static async Task RunAsync(ApplicationDbContext db, int campusId, int classId, int sectionId, Action<bool, string> check)
    {
        ApplicationUser User(string id) => new() { Id = id, UserName = id, Email = id + "@test.invalid", FullName = id, PublicUserId = id };
        var actor = User("scope-hod"); var local = User("scope-local"); var foreign = User("scope-foreign");
        actor.PhoneNumber = "8999999995"; local.PhoneNumber = "8999999994"; foreign.PhoneNumber = "8999999993";
        db.Users.AddRange(actor, local, foreign);
        var department = new Department { Name = "Scoped academic department", Code = "SCOPE" };
        var foreignCampus = new Campus { Name = "Other campus", Code = "OTHER", InstitutionId = await db.Campuses.Where(c => c.Id == campusId).Select(c => c.InstitutionId).SingleAsync() };
        db.Teachers.Add(new Teacher { FullName = actor.FullName, Email = actor.Email!, UserId = actor.Id, Department = department, TeacherType = TeacherType.HOD });
        var localStudent = new Student { FullName = local.FullName, Email = local.Email!, UserId = local.Id, ClassId = classId, SectionId = sectionId, Department = department };
        var foreignStudent = new Student { FullName = foreign.FullName, Email = foreign.Email!, UserId = foreign.Id, ClassId = classId, SectionId = sectionId, Department = department };
        db.Students.AddRange(localStudent, foreignStudent);
        db.UserCampusMemberships.AddRange(new UserCampusMembership { UserId = actor.Id, CampusId = campusId }, new UserCampusMembership { UserId = local.Id, CampusId = campusId }, new UserCampusMembership { UserId = foreign.Id, Campus = foreignCampus });
        var subject = new Subject { Name = "Scoped subject", ClassId = classId, Department = department };
        var exam = new Exam { Name = "Scope exam", ExamDate = DateTime.Today, Status = ExamLifecycle.OpenForEntry };
        db.Marks.AddRange(new Mark { Student = localStudent, Subject = subject, Exam = exam, Score = 50 }, new Mark { Student = foreignStudent, Subject = subject, Exam = exam, Score = 50 });
        await db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.Id), new Claim(ClaimTypes.Role, "HOD")], "test"));
        var results = new ResultService(db, new PermissionService(db));
        var visible = await (await results.ScopeAsync(db.Marks, principal)).Select(m => m.StudentId).ToListAsync();
        check(visible.SequenceEqual(new[] { localStudent.Id }), "HOD results must exclude same-department students in another campus.");
        var controller = new MarkController(db, null!, results) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } } };
        check(await controller.Create(new MarkEntryInputModel { StudentId = foreignStudent.Id, SubjectId = subject.Id, ExamId = exam.Id, Score = 75 }) is ForbidResult,
            "Changing a marks-entry student ID must not bypass the HOD campus boundary.");
        var foreignMark = await db.Marks.SingleAsync(m => m.StudentId == foreignStudent.Id);
        check(await controller.Edit(foreignMark.Id) is ForbidResult && await controller.Edit(foreignMark.Id, 75, "Forbidden") is ForbidResult,
            "Both direct edit URLs and posts must enforce HOD record scope.");
        check(await controller.Approve(foreignMark.Id) is ForbidResult, "A direct approval ID must not bypass the HOD campus boundary.");
        foreignCampus.IsActive = false; await db.SaveChangesAsync();
        check((await (await results.ScopeAsync(db.Marks, principal)).CountAsync()) == 1, "Inactive foreign campuses must not introduce result visibility.");
    }
}
