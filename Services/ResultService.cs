using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using System.Security.Claims;

namespace Student_Management_System.Services;

public interface IResultService
{
    Task<IQueryable<Mark>> ScopeAsync(IQueryable<Mark> query, ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<bool> CanEditAsync(ClaimsPrincipal principal, int subjectId, CancellationToken cancellationToken = default);
    Task<string> ResolveGradeAsync(
        decimal score,
        decimal maximumMarks,
        int subjectId,
        CancellationToken cancellationToken = default);
    bool IsVisibleToLearners(Mark mark);
}

public sealed class ResultService : IResultService
{
    private readonly ApplicationDbContext _db;
    private readonly PermissionService _permissions;
    private readonly Dictionary<int, IReadOnlyList<GradeBoundary>> _gradeScales = new();

    public ResultService(ApplicationDbContext db, PermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<IQueryable<Mark>> ScopeAsync(
        IQueryable<Mark> query,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return query.Where(_ => false);
        if (principal.IsInRole("SuperAdmin")) return query;

        if (principal.IsInRole("Admin") || principal.IsInRole("ExaminationOfficer"))
            return await _permissions.HasAsync(principal, "marks.view", cancellationToken)
                ? query
                : query.Where(_ => false);

        if (principal.IsInRole("HOD"))
        {
            var departmentId = await _db.Teachers.AsNoTracking()
                .Where(t => t.UserId == userId && t.TeacherType == TeacherType.HOD)
                .Select(t => t.DepartmentId)
                .SingleOrDefaultAsync(cancellationToken);
            return departmentId.HasValue
                ? query.Where(m => m.Subject.DepartmentId == departmentId ||
                                   m.Student.DepartmentId == departmentId)
                : query.Where(_ => false);
        }

        if (principal.IsInRole("Professor"))
            return query.Where(m =>
                m.Subject.TeacherAssignments.Any(a => a.Teacher.UserId == userId) ||
                (m.Subject.Teacher != null && m.Subject.Teacher.UserId == userId));

        if (principal.IsInRole("Student"))
            return query.Where(m => m.Student.UserId == userId &&
                                    m.IsApproved &&
                                    m.Exam.Status == ExamLifecycle.Published);

        if (principal.IsInRole("Guardian"))
            return query.Where(m => m.IsApproved &&
                                    m.Exam.Status == ExamLifecycle.Published &&
                                    m.Student != null &&
                                    _db.GuardianStudentLinks.Any(link =>
                                        link.StudentId == m.StudentId && link.UnlinkedUtc == null &&
                                        link.CanViewAcademics && link.GuardianProfile.UserId == userId));

        return query.Where(_ => false);
    }

    public async Task<bool> CanEditAsync(
        ClaimsPrincipal principal,
        int subjectId,
        CancellationToken cancellationToken = default)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return false;
        if (principal.IsInRole("SuperAdmin")) return true;
        if (principal.IsInRole("Admin") || principal.IsInRole("ExaminationOfficer"))
            return await _permissions.HasAsync(principal, "marks.enter", cancellationToken);
        if (principal.IsInRole("HOD"))
            return await _db.Subjects.AnyAsync(s => s.Id == subjectId &&
                s.Department!.Teachers.Any(t => t.UserId == userId && t.TeacherType == TeacherType.HOD),
                cancellationToken);
        return principal.IsInRole("Professor") &&
               await _db.TeacherSubjectAssignments.AnyAsync(a =>
                   a.SubjectId == subjectId && a.Teacher.UserId == userId, cancellationToken);
    }

    public bool IsVisibleToLearners(Mark mark) =>
        mark.IsApproved && mark.Exam.Status == ExamLifecycle.Published;

    public async Task<string> ResolveGradeAsync(
        decimal score,
        decimal maximumMarks,
        int subjectId,
        CancellationToken cancellationToken = default)
    {
        if (maximumMarks <= 0 || score < 0 || score > maximumMarks)
            throw new ArgumentOutOfRangeException(nameof(score));

        if (!_gradeScales.TryGetValue(subjectId, out var scale))
        {
            var departmentId = await _db.Subjects.AsNoTracking()
                .Where(s => s.Id == subjectId)
                .Select(s => s.DepartmentId)
                .SingleOrDefaultAsync(cancellationToken);
            scale = await _db.GradeBoundaries.AsNoTracking()
                .Where(g => g.IsActive &&
                            (g.DepartmentId == departmentId || g.DepartmentId == null))
                .OrderByDescending(g => g.DepartmentId.HasValue)
                .ThenByDescending(g => g.MinimumPercent)
                .ToListAsync(cancellationToken);
            _gradeScales[subjectId] = scale;
        }

        var percentage = score * 100m / maximumMarks;
        var boundary = scale.FirstOrDefault(g =>
            percentage >= g.MinimumPercent && percentage <= g.MaximumPercent);
        return boundary?.Grade ?? GradeCalculator.FromScore(score, maximumMarks);
    }
}
