using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using System.Security.Claims;

namespace Student_Management_System.Services;

public interface IAttendanceService
{
    Task<AttendanceSession> SubmitAsync(
        int classId, int subjectId, int teacherId, DateTime date, string period,
        IReadOnlyCollection<Attendance> records, string actorUserId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, decimal>> GetPercentagesAsync(
        IQueryable<Attendance> query, CancellationToken cancellationToken = default);
}

public sealed class AttendanceService : IAttendanceService
{
    private readonly ApplicationDbContext _db;
    public AttendanceService(ApplicationDbContext db) => _db = db;

    public async Task<AttendanceSession> SubmitAsync(
        int classId,
        int subjectId,
        int teacherId,
        DateTime date,
        string period,
        IReadOnlyCollection<Attendance> records,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        period = period.Trim();
        if (string.IsNullOrWhiteSpace(period) || period.Length > 100) throw new InvalidOperationException("Lecture or period must contain 1 to 100 characters.");
        if (records.Count == 0) throw new InvalidOperationException("Attendance roster is empty.");
        if (records.Any(r => r.Status is not ("Present" or "Absent" or "Late" or "Excused")))
            throw new InvalidOperationException("Attendance contains an unsupported status.");
        var submittedIds = records.Select(r => r.StudentId).Distinct().OrderBy(x => x).ToArray();
        if (submittedIds.Length != records.Count)
            throw new InvalidOperationException("Attendance roster contains duplicate students.");
        var teacher = await _db.Teachers.AsNoTracking().SingleOrDefaultAsync(t => t.Id == teacherId && t.UserId == actorUserId, cancellationToken);
        if (teacher == null) throw new UnauthorizedAccessException("Attendance must be submitted by its assigned teacher.");
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorUserId),
            new Claim(ClaimTypes.Role, teacher.TeacherType == TeacherType.HOD ? "HOD" : "Professor")], "attendance-service"));
        var expectedIds = await (await AcademicRecordScope.EntryStudentsAsync(_db, actor)).AsNoTracking()
            .Where(s => s.ClassId == classId && !s.IsArchived)
            .Select(s => s.Id).OrderBy(x => x).ToArrayAsync(cancellationToken);
        if (!submittedIds.SequenceEqual(expectedIds))
            throw new InvalidOperationException("The submitted roster does not match the active class roster.");
        if (!await _db.Subjects.AnyAsync(s => s.Id == subjectId && s.ClassId == classId, cancellationToken))
            throw new InvalidOperationException("Subject does not belong to the selected class.");
        var subjectAllowed = teacher.TeacherType == TeacherType.HOD
            ? await _db.Subjects.AnyAsync(subject => subject.Id == subjectId && subject.DepartmentId == teacher.DepartmentId && subject.Class.DepartmentId == teacher.DepartmentId, cancellationToken)
            : await _db.TeacherSubjectAssignments.AnyAsync(assignment => assignment.SubjectId == subjectId && assignment.TeacherId == teacherId, cancellationToken);
        if (!subjectAllowed) throw new UnauthorizedAccessException("The subject is outside the teacher's assignment.");

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            if (await _db.AttendanceSessions.AnyAsync(s =>
                    s.ClassId == classId && s.SubjectId == subjectId &&
                    s.SessionDate == date.Date && s.Period == period, cancellationToken))
                throw new InvalidOperationException("This attendance session already exists.");

            var session = new AttendanceSession
            {
                ClassId = classId,
                SubjectId = subjectId,
                TeacherId = teacherId,
                SessionDate = date.Date,
                Period = period,
                Status = AttendanceSessionStatus.Submitted,
                SubmittedUtc = DateTime.UtcNow
            };
            _db.AttendanceSessions.Add(session);
            foreach (var record in records)
            {
                // Only roster identity and status come from the request. Never
                // attach posted navigation graphs or trust record/session IDs.
                _db.Attendances.Add(new Attendance
                {
                    StudentId = record.StudentId, Status = record.Status,
                    AttendanceSession = session, SubjectId = subjectId,
                    TeacherId = teacherId, Date = date.Date, Lecture = period
                });
            }
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = actorUserId,
                Action = $"Submitted attendance session ({records.Count} students)",
                EntityName = nameof(AttendanceSession),
                EntityId = $"{classId}:{subjectId}:{date:yyyy-MM-dd}:{period}"
            });
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return session;
        });
    }

    public async Task<IReadOnlyDictionary<int, decimal>> GetPercentagesAsync(
        IQueryable<Attendance> query,
        CancellationToken cancellationToken = default)
    {
        var weights = await GetWeightsAsync(cancellationToken);
        var lateWeight = weights.Late;
        var excusedWeight = weights.Excused;
        var totals = await query.GroupBy(a => a.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Total = g.Count(),
                Earned = g.Sum(a => a.Status == "Present" ? 1m :
                    a.Status == "Late" ? lateWeight :
                    a.Status == "Excused" ? excusedWeight : 0m)
            })
            .ToListAsync(cancellationToken);
        return totals.ToDictionary(
            x => x.StudentId,
            x => x.Total == 0 ? 0 : Math.Round(x.Earned * 100m / x.Total, 1));
    }

    public async Task<AttendanceWeights> GetWeightsAsync(CancellationToken cancellationToken = default) => new(
        await GetWeightAsync("Attendance.LateWeight", 0.5m, cancellationToken),
        await GetWeightAsync("Attendance.ExcusedWeight", 1m, cancellationToken));

    private async Task<decimal> GetWeightAsync(
        string key, decimal fallback, CancellationToken cancellationToken)
    {
        var value = await _db.SystemSettings.AsNoTracking()
            .Where(s => s.Key == key).Select(s => s.Value)
            .SingleOrDefaultAsync(cancellationToken);
        return decimal.TryParse(value, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            && parsed is >= 0 and <= 1 ? parsed : fallback;
    }
}

public sealed record AttendanceWeights(decimal Late, decimal Excused);
