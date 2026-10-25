using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;

namespace Student_Management_System.Services;

public static class StudentRecordScope
{
    public static IQueryable<Student> ForCampuses(ApplicationDbContext db, IQueryable<Student> students, string? actor)
    {
        if (string.IsNullOrWhiteSpace(actor)) return students.Where(_ => false);
        var campuses = db.UserCampusMemberships.Where(m => m.UserId == actor && m.IsActive && m.Campus.IsActive && m.Campus.Institution.IsActive)
            .Select(m => m.CampusId);
        return students.Where(student => student.UserId != null && db.UserCampusMemberships.Any(m =>
            m.UserId == student.UserId && m.IsActive && campuses.Contains(m.CampusId)));
    }
    // Directory, profile, photo, and transcript must share the same HOD boundary.
    public static IQueryable<Student> ForHod(ApplicationDbContext db,
        IQueryable<Student> students, string? actorId, int departmentId)
        => ForHod(students, db.UserCampusMemberships.AsNoTracking(), actorId, departmentId);

    public static IQueryable<Student> ForHod(IQueryable<Student> students,
        IQueryable<UserCampusMembership> memberships, string? actorId, int departmentId)
    {
        if (string.IsNullOrWhiteSpace(actorId)) return students.Where(_ => false);
        var campuses = memberships
            .Where(m => m.UserId == actorId && m.IsActive &&
                        m.Campus.IsActive && m.Campus.Institution.IsActive)
            .Select(m => m.CampusId);
        return students.Where(s => s.DepartmentId == departmentId && s.UserId != null &&
            memberships.Any(m => m.UserId == s.UserId && m.IsActive &&
                campuses.Contains(m.CampusId)));
    }
}
