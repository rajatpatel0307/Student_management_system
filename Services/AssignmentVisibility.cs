using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;

namespace Student_Management_System.Services;

public static class AssignmentLifecycle
{
    public const string Draft = "Draft";
    public const string Scheduled = "Scheduled";
    public const string Published = "Published";
    public const string Closed = "Closed";
    public static readonly string[] All = [Draft, Scheduled, Published, Closed];
}

public static class AssignmentVisibility
{
    public static IQueryable<Assignment> VisibleAt(
        this IQueryable<Assignment> query,
        DateTime utcNow) => query.Where(a =>
            a.Status == AssignmentLifecycle.Published ||
            (a.Status == AssignmentLifecycle.Scheduled &&
             a.PublishUtc.HasValue && a.PublishUtc.Value <= utcNow));

    public static IQueryable<Assignment> VisibleToStudent(
        this IQueryable<Assignment> query,
        string userId,
        DateTime utcNow) => query.VisibleAt(utcNow).Where(a =>
            (a.Subject!.Class.Students.Any(s => s.UserId == userId) ||
             a.Subject.StudentEnrollments.Any(e => e.Student.UserId == userId)));

    public static bool IsVisibleToStudent(Assignment assignment, DateTime utcNow) =>
        assignment.Status == AssignmentLifecycle.Published ||
        (assignment.Status == AssignmentLifecycle.Scheduled &&
         assignment.PublishUtc.HasValue && assignment.PublishUtc.Value <= utcNow);
}
