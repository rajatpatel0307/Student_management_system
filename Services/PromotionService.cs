using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using System.Data;
using System.Text.Json;

namespace Student_Management_System.Services;

public interface IPromotionService
{
    Task<PromotionBatch> CreateDraftAsync(
        CreatePromotionBatchInput input,
        string actorUserId,
        CancellationToken cancellationToken = default);
    Task UpdateDecisionsAsync(
        long batchId,
        IReadOnlyCollection<PromotionDecisionInput> decisions,
        string actorUserId,
        CancellationToken cancellationToken = default);
    Task SubmitAsync(long batchId, string actorUserId, CancellationToken cancellationToken = default);
    Task ReviewAsync(
        PromotionReviewInput input,
        string actorUserId,
        CancellationToken cancellationToken = default);
    Task ApplyAsync(long batchId, string actorUserId, CancellationToken cancellationToken = default);
}

public sealed class PromotionService : IPromotionService
{
    private readonly ApplicationDbContext _db;
    private readonly IAttendanceService _attendance;
    private readonly IApprovalWorkflowService _approvals;

    public PromotionService(
        ApplicationDbContext db,
        IAttendanceService attendance,
        IApprovalWorkflowService approvals)
    {
        _db = db;
        _attendance = attendance;
        _approvals = approvals;
    }

    public async Task<PromotionBatch> CreateDraftAsync(
        CreatePromotionBatchInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (input.SourceAcademicYearId == input.DestinationAcademicYearId)
            throw new InvalidOperationException("Source and destination sessions must differ.");
        var sourceValid = await _db.Sections.AsNoTracking().AnyAsync(s =>
            s.Id == input.SourceSectionId && s.ClassId == input.SourceClassId,
            cancellationToken);
        if (!sourceValid)
            throw new InvalidOperationException("Source section does not belong to the source class.");
        if (input.DestinationClassId.HasValue != input.DestinationSectionId.HasValue)
            throw new InvalidOperationException("Destination class and section must be selected together.");
        if (input.DestinationClassId.HasValue &&
            !await _db.Sections.AsNoTracking().AnyAsync(s =>
                s.Id == input.DestinationSectionId &&
                s.ClassId == input.DestinationClassId, cancellationToken))
            throw new InvalidOperationException("Destination section does not belong to the destination class.");

        var students = await _db.StudentClassEnrollments.AsNoTracking()
            .Where(e => e.AcademicYearId == input.SourceAcademicYearId &&
                        e.ClassId == input.SourceClassId &&
                        e.SectionId == input.SourceSectionId &&
                        e.IsActive &&
                        !e.Student.IsArchived)
            .Select(e => e.Student)
            .OrderBy(s => s.FullName)
            .ToListAsync(cancellationToken);
        if (students.Count == 0)
            throw new InvalidOperationException("No eligible active students were found.");

        var studentIds = students.Select(s => s.Id).ToArray();
        var attendance = await _attendance.GetPercentagesAsync(
            _db.Attendances.AsNoTracking().Where(a => studentIds.Contains(a.StudentId)),
            cancellationToken);
        var resultPercentages = await _db.Marks.AsNoTracking()
            .Where(m => studentIds.Contains(m.StudentId) &&
                        m.IsApproved &&
                        m.Exam.Status == ExamLifecycle.Published &&
                        m.Exam.MaximumMarks > 0)
            .GroupBy(m => m.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Percent = g.Average(m => m.Score * 100m / m.Exam.MaximumMarks)
            })
            .ToDictionaryAsync(x => x.StudentId, x => x.Percent, cancellationToken);
        var scheduled = await _db.Fees.AsNoTracking()
            .Where(f => f.ClassId == input.SourceClassId && !f.IsArchived)
            .SumAsync(f => (decimal?)f.Amount, cancellationToken) ?? 0;
        var netPayments = await _db.FinancialTransactions.AsNoTracking()
            .Where(t => studentIds.Contains(t.StudentId))
            .GroupBy(t => t.StudentId)
            .Select(g => new { StudentId = g.Key, Amount = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.StudentId, x => x.Amount, cancellationToken);
        var attendanceThreshold = await ReadDecimalSettingAsync(
            "Promotion.MinimumAttendancePercent", 75m, cancellationToken);
        var resultThreshold = await ReadDecimalSettingAsync(
            "Promotion.MinimumResultPercent", 40m, cancellationToken);
        var requireFees = await ReadBooleanSettingAsync(
            "Promotion.RequireFeesCleared", true, cancellationToken);

        var batch = new PromotionBatch
        {
            BatchNumber = $"PRM-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..31],
            SourceAcademicYearId = input.SourceAcademicYearId,
            DestinationAcademicYearId = input.DestinationAcademicYearId,
            DepartmentId = input.DepartmentId,
            SourceClassId = input.SourceClassId,
            SourceSectionId = input.SourceSectionId,
            DestinationClassId = input.DestinationClassId,
            DestinationSectionId = input.DestinationSectionId,
            CreatedByUserId = actorUserId,
            Entries = students.Select(student =>
            {
                var attendancePercent = attendance.GetValueOrDefault(student.Id);
                var resultPercent = resultPercentages.GetValueOrDefault(student.Id);
                var outstanding = Math.Max(0, scheduled - netPayments.GetValueOrDefault(student.Id));
                return new PromotionEntry
                {
                    StudentId = student.Id,
                    AttendancePercent = attendancePercent,
                    ResultPercent = resultPercent,
                    OutstandingBalance = outstanding,
                    IsEligible = attendancePercent >= attendanceThreshold &&
                                 resultPercent >= resultThreshold &&
                                 (!requireFees || outstanding == 0),
                    Decision = attendancePercent >= attendanceThreshold &&
                               resultPercent >= resultThreshold &&
                               (!requireFees || outstanding == 0)
                        ? StudentLifecycleDecision.Promoted
                        : StudentLifecycleDecision.Retained
                };
            }).ToList()
        };
        _db.PromotionBatches.Add(batch);
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = actorUserId,
            Action = $"Created promotion batch with {batch.Entries.Count} students",
            EntityName = nameof(PromotionBatch),
            EntityId = batch.BatchNumber
        });
        await _db.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task UpdateDecisionsAsync(
        long batchId,
        IReadOnlyCollection<PromotionDecisionInput> decisions,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _db.PromotionBatches.Include(b => b.Entries)
            .SingleOrDefaultAsync(b => b.Id == batchId, cancellationToken)
            ?? throw new KeyNotFoundException("Promotion batch not found.");
        if (batch.Status != PromotionBatchStatus.Draft)
            throw new InvalidOperationException("Only draft batches can be edited.");
        if (decisions.Count != batch.Entries.Count ||
            decisions.Select(d => d.EntryId).Distinct().Count() != decisions.Count)
            throw new InvalidOperationException("Every promotion entry must have exactly one decision.");
        var byId = decisions.ToDictionary(d => d.EntryId);
        foreach (var entry in batch.Entries)
        {
            if (!byId.TryGetValue(entry.Id, out var decision))
                throw new InvalidOperationException("A promotion entry is missing.");
            if (decision.Decision == StudentLifecycleDecision.Pending)
                throw new InvalidOperationException("Pending is not a final promotion decision.");
            entry.Decision = decision.Decision;
            entry.Reason = string.IsNullOrWhiteSpace(decision.Reason) ? null : decision.Reason.Trim();
            if (!entry.IsEligible && decision.Decision == StudentLifecycleDecision.Promoted &&
                string.IsNullOrWhiteSpace(entry.Reason))
                throw new InvalidOperationException("Promoting an ineligible student requires a reason.");
        }
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = actorUserId,
            Action = "Updated promotion decisions",
            EntityName = nameof(PromotionBatch),
            EntityId = batch.Id.ToString()
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SubmitAsync(
        long batchId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _db.PromotionBatches.Include(b => b.Entries)
            .SingleOrDefaultAsync(b => b.Id == batchId, cancellationToken)
            ?? throw new KeyNotFoundException("Promotion batch not found.");
        if (batch.Status != PromotionBatchStatus.Draft)
            throw new InvalidOperationException("Only draft batches can be submitted.");
        if (batch.Entries.Count == 0 || batch.Entries.Any(e => e.Decision == StudentLifecycleDecision.Pending))
            throw new InvalidOperationException("Every student requires a decision.");
        batch.Status = PromotionBatchStatus.Submitted;
        await _db.SaveChangesAsync(cancellationToken);
        await _approvals.RequestAsync(
            "StudentPromotion",
            nameof(PromotionBatch),
            batch.Id.ToString(),
            $"Promotion batch {batch.BatchNumber}",
            actorUserId,
            ["HOD", "Admin"],
            DateTime.UtcNow.AddDays(7),
            cancellationToken);
    }

    public async Task ReviewAsync(
        PromotionReviewInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _db.PromotionBatches
            .SingleOrDefaultAsync(b => b.Id == input.BatchId, cancellationToken)
            ?? throw new KeyNotFoundException("Promotion batch not found.");
        if (batch.Status != PromotionBatchStatus.Submitted)
            throw new InvalidOperationException("Only submitted batches can be reviewed.");
        if (Convert.TryFromBase64String(input.RowVersion, new Span<byte>(new byte[input.RowVersion.Length]), out _))
            _db.Entry(batch).Property(b => b.RowVersion).OriginalValue = Convert.FromBase64String(input.RowVersion);
        batch.Status = input.Approve ? PromotionBatchStatus.Approved : PromotionBatchStatus.Rejected;
        batch.ReviewedByUserId = actorUserId;
        batch.ReviewedUtc = DateTime.UtcNow;
        batch.ReviewComment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim();
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = actorUserId,
            Action = input.Approve ? "Approved promotion batch" : "Rejected promotion batch",
            EntityName = nameof(PromotionBatch),
            EntityId = batch.Id.ToString()
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyAsync(
        long batchId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var batch = await _db.PromotionBatches
                .Include(b => b.Entries).ThenInclude(e => e.Student)
                .SingleOrDefaultAsync(b => b.Id == batchId, cancellationToken)
                ?? throw new KeyNotFoundException("Promotion batch not found.");
            if (batch.Status != PromotionBatchStatus.Approved)
                throw new InvalidOperationException("Only approved batches can be applied.");
            if (batch.Entries.Any(e =>
                    e.Decision == StudentLifecycleDecision.Promoted) &&
                (!batch.DestinationClassId.HasValue || !batch.DestinationSectionId.HasValue))
                throw new InvalidOperationException("Promoted students require a destination class and section.");

            foreach (var entry in batch.Entries)
            {
                var current = await _db.StudentClassEnrollments.SingleOrDefaultAsync(e =>
                    e.StudentId == entry.StudentId &&
                    e.AcademicYearId == batch.SourceAcademicYearId &&
                    e.IsActive, cancellationToken);
                if (current != null) current.IsActive = false;
                switch (entry.Decision)
                {
                    case StudentLifecycleDecision.Promoted:
                        if (await _db.StudentClassEnrollments.AnyAsync(e =>
                                e.StudentId == entry.StudentId &&
                                e.AcademicYearId == batch.DestinationAcademicYearId,
                                cancellationToken))
                            throw new InvalidOperationException(
                                $"{entry.Student.FullName} already has a destination enrollment.");
                        _db.StudentClassEnrollments.Add(new StudentClassEnrollment
                        {
                            StudentId = entry.StudentId,
                            ClassId = batch.DestinationClassId!.Value,
                            SectionId = batch.DestinationSectionId!.Value,
                            AcademicYearId = batch.DestinationAcademicYearId,
                            IsActive = true
                        });
                        entry.Student.ClassId = batch.DestinationClassId.Value;
                        entry.Student.SectionId = batch.DestinationSectionId.Value;
                        entry.Student.StudentStatus = "Active";
                        break;
                    case StudentLifecycleDecision.Retained:
                        if (current != null) current.IsActive = true;
                        entry.Student.StudentStatus = "Active";
                        break;
                    case StudentLifecycleDecision.Graduated:
                        entry.Student.StudentStatus = "Graduated";
                        break;
                    case StudentLifecycleDecision.Transferred:
                        entry.Student.StudentStatus = "Transferred";
                        break;
                    case StudentLifecycleDecision.Withdrawn:
                        entry.Student.StudentStatus = "Withdrawn";
                        break;
                }
                if (!string.IsNullOrWhiteSpace(entry.Student.UserId))
                {
                    _db.UserNotifications.Add(new UserNotification
                    {
                        UserId = entry.Student.UserId,
                        Title = "Academic lifecycle updated",
                        Message = $"Your academic status is now {entry.Decision}.",
                        Category = "Academic",
                        ActionUrl = $"/Students/Details/{entry.StudentId}"
                    });
                }
            }
            batch.Status = PromotionBatchStatus.Applied;
            batch.AppliedByUserId = actorUserId;
            batch.AppliedUtc = DateTime.UtcNow;
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = actorUserId,
                Action = $"Applied promotion batch ({batch.Entries.Count} students)",
                EntityName = nameof(PromotionBatch),
                EntityId = batch.Id.ToString()
            });
            _db.OutboxMessages.Add(new OutboxMessage
            {
                Type = "PromotionApplied",
                PayloadJson = JsonSerializer.Serialize(new { batch.Id, batch.BatchNumber })
            });
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private async Task<decimal> ReadDecimalSettingAsync(
        string key,
        decimal fallback,
        CancellationToken cancellationToken)
    {
        var value = await _db.SystemSettings.AsNoTracking()
            .Where(s => s.Key == key).Select(s => s.Value)
            .SingleOrDefaultAsync(cancellationToken);
        return decimal.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private async Task<bool> ReadBooleanSettingAsync(
        string key,
        bool fallback,
        CancellationToken cancellationToken)
    {
        var value = await _db.SystemSettings.AsNoTracking()
            .Where(s => s.Key == key).Select(s => s.Value)
            .SingleOrDefaultAsync(cancellationToken);
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
