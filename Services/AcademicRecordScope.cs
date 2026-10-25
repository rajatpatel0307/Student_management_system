using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;

namespace Student_Management_System.Services;

public static class AcademicRecordScope
{
    public static async Task<IQueryable<Student>> EntryStudentsAsync(ApplicationDbContext db, ClaimsPrincipal principal)
    {
        var actor = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var students = db.Students.Where(student => !student.IsArchived);
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(actor)) return students.Where(_ => false);
        if (principal.IsInRole("HOD"))
        {
            var department = await db.Teachers.Where(t => t.UserId == actor && t.TeacherType == TeacherType.HOD)
                .Select(t => t.DepartmentId).SingleOrDefaultAsync();
            return department.HasValue ? StudentRecordScope.ForHod(db, students, actor, department.Value) : students.Where(_ => false);
        }
        if (principal.IsInRole("Professor")) return StudentRecordScope.ForCampuses(db, students, actor)
            .Where(student => student.Class!.TeacherAssignments.Any(assignment => assignment.Teacher.UserId == actor));
        return students.Where(_ => false);
    }

    public static async Task<IQueryable<Attendance>> AttendanceAsync(ApplicationDbContext db, IQueryable<Attendance> query, ClaimsPrincipal principal)
    {
        var actor = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(actor)) return query.Where(_ => false);
        query = query.Where(record => !db.UserPermissions.Any(permission => permission.UserId == actor &&
            permission.Permission.Key == "attendance.view" && !permission.IsGranted));
        if (principal.IsInRole("SuperAdmin")) return query;
        if (principal.IsInRole("Student")) return query.Where(a => !a.Student.IsArchived && a.Student.UserId == actor);
        IQueryable<Student> students;
        if (principal.IsInRole("HOD")) students = await EntryStudentsAsync(db, principal);
        else if (principal.IsInRole("Admin") || principal.IsInRole("Professor"))
            students = StudentRecordScope.ForCampuses(db, db.Students, actor);
        else return query.Where(_ => false);
        var ids = students.Select(student => student.Id);
        query = query.Where(a => ids.Contains(a.StudentId));
        if (principal.IsInRole("Professor")) query = query.Where(a => !a.Student.IsArchived &&
            (a.Teacher!.UserId == actor || a.Subject!.TeacherAssignments.Any(assignment => assignment.Teacher.UserId == actor)));
        return query;
    }
}
