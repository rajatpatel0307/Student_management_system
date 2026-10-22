using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;

namespace Student_Management_System.Services;

public interface ITimetableService
{
    Task<IReadOnlyList<string>> ValidateAsync(
        TimetableEntryInput input,
        CancellationToken cancellationToken = default);
    Task<TimetableEntry> SaveAsync(
        TimetableEntryInput input,
        string actorUserId,
        CancellationToken cancellationToken = default);
    Task<TimetableSubstitution> CreateSubstitutionAsync(
        int timetableEntryId,
        DateTime occursOn,
        int? teacherId,
        int? roomId,
        string status,
        string? reason,
        string actorUserId,
        CancellationToken cancellationToken = default);
}

public sealed class TimetableService : ITimetableService
{
    private readonly ApplicationDbContext _db;

    public TimetableService(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<string>> ValidateAsync(
        TimetableEntryInput input,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        if (input.Id.HasValue && input.RowVersion.Length == 0)
            errors.Add("The original record version is required. Reload the timetable period and try again.");
        if (input.EndsAt <= input.StartsAt)
            errors.Add("End time must be after start time.");
        if (!await _db.Subjects.AsNoTracking().AnyAsync(s =>
                s.Id == input.SubjectId &&
                s.ClassId == input.ClassId &&
                !s.IsArchived, cancellationToken))
            errors.Add("The subject does not belong to the selected class.");
        if (!await _db.Teachers.AsNoTracking().AnyAsync(t =>
                t.Id == input.TeacherId && !t.IsArchived, cancellationToken))
            errors.Add("Select an active Teacher.");
        if (input.RoomId.HasValue &&
            !await _db.Rooms.AsNoTracking().AnyAsync(r =>
                r.Id == input.RoomId && r.IsActive, cancellationToken))
            errors.Add("Select an active room.");

        var overlap = _db.TimetableEntries.AsNoTracking().Where(t =>
            (!input.Id.HasValue || t.Id != input.Id) &&
            t.DayOfWeek == input.DayOfWeek &&
            t.StartsAt < input.EndsAt &&
            input.StartsAt < t.EndsAt);
        if (await overlap.AnyAsync(t => t.ClassId == input.ClassId, cancellationToken))
            errors.Add("The class already has an overlapping period.");
        if (await overlap.AnyAsync(t => t.TeacherId == input.TeacherId, cancellationToken))
            errors.Add("The Teacher already has an overlapping period.");
        if (input.RoomId.HasValue &&
            await overlap.AnyAsync(t => t.RoomId == input.RoomId, cancellationToken))
            errors.Add("The room already has an overlapping period.");
        return errors;
    }

    public async Task<TimetableEntry> SaveAsync(
        TimetableEntryInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var errors = await ValidateAsync(input, cancellationToken);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));
        var entry = input.Id.HasValue
            ? await _db.TimetableEntries.SingleOrDefaultAsync(
                t => t.Id == input.Id, cancellationToken)
                ?? throw new KeyNotFoundException("Timetable period not found.")
            : new TimetableEntry();
        if (input.Id.HasValue)
            ConcurrencyToken.Apply(_db, entry, input.RowVersion);
        entry.ClassId = input.ClassId;
        entry.SubjectId = input.SubjectId;
        entry.TeacherId = input.TeacherId;
        entry.DayOfWeek = input.DayOfWeek;
        entry.StartsAt = input.StartsAt;
        entry.EndsAt = input.EndsAt;
        entry.RoomId = input.RoomId;
        entry.Room = input.RoomId.HasValue
            ? await _db.Rooms.Where(r => r.Id == input.RoomId)
                .Select(r => r.Name).SingleAsync(cancellationToken)
            : "";
        if (!input.Id.HasValue) _db.TimetableEntries.Add(entry);
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = actorUserId,
            Action = input.Id.HasValue
                ? "Updated timetable period"
                : "Created timetable period",
            EntityName = nameof(TimetableEntry),
            EntityId = input.Id?.ToString() ?? $"{input.ClassId}:{input.DayOfWeek}:{input.StartsAt}"
        });
        await _db.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<TimetableSubstitution> CreateSubstitutionAsync(
        int timetableEntryId,
        DateTime occursOn,
        int? teacherId,
        int? roomId,
        string status,
        string? reason,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (occursOn.Date < DateTime.UtcNow.Date)
            throw new InvalidOperationException("Past periods cannot be substituted.");
        var entry = await _db.TimetableEntries.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == timetableEntryId, cancellationToken)
            ?? throw new KeyNotFoundException("Timetable period not found.");
        if ((int)occursOn.DayOfWeek != entry.DayOfWeek % 7)
            throw new InvalidOperationException("The date does not match the timetable day.");
        if (await _db.TimetableSubstitutions.AnyAsync(s =>
                s.TimetableEntryId == timetableEntryId &&
                s.OccursOn == occursOn.Date, cancellationToken))
            throw new InvalidOperationException("A substitution already exists for this period.");

        if (teacherId.HasValue)
        {
            var conflicts = await _db.TimetableEntries.AsNoTracking().AnyAsync(t =>
                t.TeacherId == teacherId &&
                t.DayOfWeek == entry.DayOfWeek &&
                t.StartsAt < entry.EndsAt &&
                entry.StartsAt < t.EndsAt &&
                t.Id != timetableEntryId, cancellationToken);
            if (conflicts)
                throw new InvalidOperationException("The substitute Teacher has a timetable conflict.");
        }
        if (roomId.HasValue)
        {
            var conflicts = await _db.TimetableEntries.AsNoTracking().AnyAsync(t =>
                t.RoomId == roomId &&
                t.DayOfWeek == entry.DayOfWeek &&
                t.StartsAt < entry.EndsAt &&
                entry.StartsAt < t.EndsAt &&
                t.Id != timetableEntryId, cancellationToken);
            if (conflicts)
                throw new InvalidOperationException("The substitute room has a timetable conflict.");
        }

        var substitution = new TimetableSubstitution
        {
            TimetableEntryId = timetableEntryId,
            OccursOn = occursOn.Date,
            SubstituteTeacherId = teacherId,
            RoomId = roomId,
            Status = status.Trim(),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            CreatedByUserId = actorUserId
        };
        _db.TimetableSubstitutions.Add(substitution);
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = actorUserId,
            Action = $"Created timetable {status}",
            EntityName = nameof(TimetableSubstitution),
            EntityId = $"{timetableEntryId}:{occursOn:yyyy-MM-dd}"
        });
        await _db.SaveChangesAsync(cancellationToken);
        return substitution;
    }
}
