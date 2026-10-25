using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Student_Management_System.Controllers;
using Student_Management_System.Models;
using Student_Management_System.Services;

internal static class StudentHistoryVerification
{
    public static async Task RunAsync(ApplicationDbContext db, int studentId, Action<bool, string> check)
    {
        var subject = await db.Subjects.FirstAsync();
        db.Attendances.AddRange(Enumerable.Range(1, 55).Select(index => new Attendance { StudentId = studentId, SubjectId = subject.Id,
            Date = DateTime.Today.AddDays(-index), Lecture = "History fixture", Status = index <= 20 ? "Present" : index <= 40 ? "Late" : "Excused" }));
        db.Marks.AddRange(Enumerable.Range(1, 55).Select(index => new Mark { StudentId = studentId, SubjectId = subject.Id, Score = 20, Grade = "D",
            Exam = new Exam { Name = $"History exam {index}", ExamDate = DateTime.Today.AddDays(-index), MaximumMarks = 40, PassMarks = 16 } }));
        db.SystemSettings.AddRange(new SystemSetting { Key = "Attendance.LateWeight", Value = "0.25" }, new SystemSetting { Key = "Attendance.ExcusedWeight", Value = "0.75" });
        await db.SaveChangesAsync();
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "history-superadmin"), new Claim(ClaimTypes.Role, "SuperAdmin")], "test")) };
        var controller = new StudentsController(db, null!, null!, null!, NullLogger<StudentsController>.Instance,
            new ResultService(db, new PermissionService(db)), null!, new CampusScopeService(db))
        { ControllerContext = new ControllerContext { HttpContext = context } };
        var first = (StudentDetailsViewModel)((ViewResult)await controller.Details(studentId, CancellationToken.None)).Model!;
        var second = (StudentDetailsViewModel)((ViewResult)await controller.Details(studentId, CancellationToken.None, attendancePage: 2, marksPage: 2)).Model!;
        check(first.Attendance.Count == 20 && second.Attendance.Count == 20 && second.AttendancePage.TotalCount == 55,
            "Student attendance history must be SQL-paged with full-history totals.");
        check(!first.Attendance.Select(a => a.Id).Intersect(second.Attendance.Select(a => a.Id)).Any(), "Independent attendance pages must not overlap.");
        check(first.Marks.Count == 20 && second.Marks.Count == 20 && second.MarksPage.TotalCount == 56,
            "Student result history must be SQL-paged with authorized full-history totals.");
        check(first.AttendancePercentage == 65.9 && first.AttendancePercentage == second.AttendancePercentage && second.PresentCount == 20 && second.LateCount == 20 && second.ExcusedCount == 15,
            "Profile metrics must use configured attendance weights across all history, independent of page selection.");
        check(Math.Abs(first.AverageScore - 50.214285) < 0.001 && first.AverageScore == second.AverageScore,
            "Profile average percentages must normalize each exam's maximum over full authorized history.");
        var servicePercent = await new AttendanceService(db).GetPercentagesAsync(db.Attendances.Where(a => a.StudentId == studentId));
        check((double)servicePercent[studentId] == first.AttendancePercentage, "The student profile and attendance service must agree on configured weights.");
        var directory = (PagedResult<Student>)((ViewResult)await controller.Index(new ListQuery { Search = "Test Resident", Status = "low-attendance" }, CancellationToken.None)).Model!;
        check(directory.Items.Any(s => s.Id == studentId), "The directory low-attendance filter must use the same configured weights as the profile.");
    }
}
