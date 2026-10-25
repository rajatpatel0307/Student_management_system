using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;

namespace Student_Management_System.Services;

public static class FinancialRecordScope
{
    public static IQueryable<Student> Students(ApplicationDbContext db, ClaimsPrincipal principal)
    {
        var query = db.Students.AsNoTracking();
        var actor = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(actor)) return query.Where(_ => false);
        if (principal.IsInRole("SuperAdmin")) return query;
        if (principal.IsInRole("Admin") || principal.IsInRole("Accountant"))
        {
            return StudentRecordScope.ForCampuses(db, query, actor);
        }
        if (principal.IsInRole("Student")) return query.Where(student => student.UserId == actor);
        if (principal.IsInRole("Guardian")) return query.Where(student => db.GuardianStudentLinks.Any(link =>
            link.StudentId == student.Id && link.UnlinkedUtc == null && link.CanViewFinance && link.GuardianProfile.UserId == actor));
        return query.Where(_ => false);
    }
}
