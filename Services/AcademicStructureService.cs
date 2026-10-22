using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;

namespace Student_Management_System.Services;

public interface IAcademicStructureService
{
    Task<Institution> SaveInstitutionAsync(InstitutionInput input, string actorUserId, CancellationToken cancellationToken = default);
    Task<Campus> SaveCampusAsync(CampusInput input, string actorUserId, CancellationToken cancellationToken = default);
    Task<AcademicSession> SaveSessionAsync(AcademicSessionInput input, string actorUserId, CancellationToken cancellationToken = default);
    Task<Curriculum> CreateCurriculumAsync(CurriculumInput input, string actorUserId, CancellationToken cancellationToken = default);
    Task<AcademicCalendarEvent> CreateEventAsync(AcademicEventInput input, string actorUserId, CancellationToken cancellationToken = default);
}

public sealed class AcademicStructureService : IAcademicStructureService
{
    private readonly ApplicationDbContext _db;

    public AcademicStructureService(ApplicationDbContext db) => _db = db;

    public async Task<Institution> SaveInstitutionAsync(
        InstitutionInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await _db.Institutions.AnyAsync(i =>
                i.Code == code && (!input.Id.HasValue || i.Id != input.Id), cancellationToken))
            throw new InvalidOperationException("Institution code already exists.");
        var entity = input.Id.HasValue
            ? await _db.Institutions.SingleOrDefaultAsync(i => i.Id == input.Id, cancellationToken)
                ?? throw new KeyNotFoundException("Institution not found.")
            : new Institution();
        entity.Name = input.Name.Trim();
        entity.Code = code;
        entity.Address = input.Address?.Trim();
        entity.Email = input.Email?.Trim();
        entity.Phone = input.Phone?.Trim();
        entity.TimeZoneId = input.TimeZoneId.Trim();
        if (!input.Id.HasValue) _db.Institutions.Add(entity);
        AddAudit(actorUserId, input.Id.HasValue ? "Updated institution" : "Created institution", nameof(Institution), input.Id);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<Campus> SaveCampusAsync(
        CampusInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await _db.Institutions.AnyAsync(i => i.Id == input.InstitutionId && i.IsActive, cancellationToken))
            throw new InvalidOperationException("Select an active institution.");
        var code = input.Code.Trim().ToUpperInvariant();
        if (await _db.Campuses.AnyAsync(c =>
                c.InstitutionId == input.InstitutionId &&
                c.Code == code &&
                (!input.Id.HasValue || c.Id != input.Id), cancellationToken))
            throw new InvalidOperationException("Campus code already exists for this institution.");
        var entity = input.Id.HasValue
            ? await _db.Campuses.SingleOrDefaultAsync(c => c.Id == input.Id, cancellationToken)
                ?? throw new KeyNotFoundException("Campus not found.")
            : new Campus();
        entity.InstitutionId = input.InstitutionId;
        entity.Name = input.Name.Trim();
        entity.Code = code;
        entity.Address = input.Address?.Trim();
        if (!input.Id.HasValue) _db.Campuses.Add(entity);
        AddAudit(actorUserId, input.Id.HasValue ? "Updated campus" : "Created campus", nameof(Campus), input.Id);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<AcademicSession> SaveSessionAsync(
        AcademicSessionInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (input.EndsOn.Date <= input.StartsOn.Date)
            throw new InvalidOperationException("Academic session end date must follow its start date.");
        if (await _db.AcademicSessions.AnyAsync(s =>
                (!input.Id.HasValue || s.Id != input.Id) &&
                s.StartsOn < input.EndsOn.Date &&
                input.StartsOn.Date < s.EndsOn, cancellationToken))
            throw new InvalidOperationException("Academic session dates overlap an existing session.");
        var entity = input.Id.HasValue
            ? await _db.AcademicSessions.SingleOrDefaultAsync(s => s.Id == input.Id, cancellationToken)
                ?? throw new KeyNotFoundException("Academic session not found.")
            : new AcademicSession();
        if (input.IsCurrent)
        {
            var current = await _db.AcademicSessions
                .Where(s => s.IsCurrent && (!input.Id.HasValue || s.Id != input.Id))
                .ToListAsync(cancellationToken);
            foreach (var session in current) session.IsCurrent = false;
        }
        entity.Name = input.Name.Trim();
        entity.StartsOn = input.StartsOn.Date;
        entity.EndsOn = input.EndsOn.Date;
        entity.AcademicYearId = input.AcademicYearId;
        entity.IsCurrent = input.IsCurrent;
        if (!input.Id.HasValue) _db.AcademicSessions.Add(entity);
        AddAudit(actorUserId, input.Id.HasValue ? "Updated academic session" : "Created academic session", nameof(AcademicSession), input.Id);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<Curriculum> CreateCurriculumAsync(
        CurriculumInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await _db.AcademicPrograms.AnyAsync(p =>
                p.Id == input.AcademicProgramId && p.IsActive, cancellationToken))
            throw new InvalidOperationException("Select an active academic program.");
        if (!await _db.AcademicSessions.AnyAsync(s =>
                s.Id == input.EffectiveAcademicSessionId && !s.IsClosed, cancellationToken))
            throw new InvalidOperationException("Select an open academic session.");
        var subjectIds = input.SubjectIds.Distinct().ToArray();
        var validSubjects = await _db.Subjects.CountAsync(s =>
            subjectIds.Contains(s.Id) && !s.IsArchived, cancellationToken);
        if (subjectIds.Length == 0 || validSubjects != subjectIds.Length)
            throw new InvalidOperationException("Select one or more active subjects.");
        if (await _db.Curricula.AnyAsync(c =>
                c.AcademicProgramId == input.AcademicProgramId &&
                c.Name == input.Name.Trim(), cancellationToken))
            throw new InvalidOperationException("A curriculum with this name already exists for the program.");

        var curriculum = new Curriculum
        {
            AcademicProgramId = input.AcademicProgramId,
            Name = input.Name.Trim(),
            Versions =
            [
                new CurriculumVersion
                {
                    Version = input.Version.Trim(),
                    EffectiveAcademicSessionId = input.EffectiveAcademicSessionId,
                    ApprovedUtc = DateTime.UtcNow,
                    ApprovedByUserId = actorUserId,
                    Subjects = subjectIds.Select(subjectId => new CurriculumSubject
                    {
                        SubjectId = subjectId,
                        IsRequired = true
                    }).ToList()
                }
            ]
        };
        _db.Curricula.Add(curriculum);
        AddAudit(actorUserId, "Created curriculum and version", nameof(Curriculum), input.Name);
        await _db.SaveChangesAsync(cancellationToken);
        return curriculum;
    }

    public async Task<AcademicCalendarEvent> CreateEventAsync(
        AcademicEventInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var startsUtc = input.StartsLocal.ToUniversalTime();
        var endsUtc = input.EndsLocal.ToUniversalTime();
        if (endsUtc <= startsUtc)
            throw new InvalidOperationException("Calendar event end must follow its start.");
        if (!await _db.AcademicSessions.AnyAsync(s =>
                s.Id == input.AcademicSessionId &&
                startsUtc.Date >= s.StartsOn.Date &&
                endsUtc.Date <= s.EndsOn.Date.AddDays(1), cancellationToken))
            throw new InvalidOperationException("Event dates must be inside the selected academic session.");
        var entity = new AcademicCalendarEvent
        {
            AcademicSessionId = input.AcademicSessionId,
            Title = input.Title.Trim(),
            Description = input.Description?.Trim(),
            Kind = input.Kind,
            StartsUtc = startsUtc,
            EndsUtc = endsUtc,
            DepartmentId = input.DepartmentId,
            IsPublished = input.IsPublished
        };
        _db.AcademicCalendarEvents.Add(entity);
        AddAudit(actorUserId, "Created academic calendar event", nameof(AcademicCalendarEvent), input.Title);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private void AddAudit(
        string userId,
        string action,
        string entityName,
        object? entityId) =>
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId?.ToString()
        });
}
