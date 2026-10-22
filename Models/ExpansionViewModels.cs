using System.ComponentModel.DataAnnotations;

namespace Student_Management_System.Models;

public sealed class CreatePromotionBatchInput
{
    [Required] public int SourceAcademicYearId { get; set; }
    [Required] public int DestinationAcademicYearId { get; set; }
    public int? DepartmentId { get; set; }
    [Required] public int SourceClassId { get; set; }
    [Required] public int SourceSectionId { get; set; }
    public int? DestinationClassId { get; set; }
    public int? DestinationSectionId { get; set; }
}

public sealed class PromotionDecisionInput
{
    public long EntryId { get; set; }
    public StudentLifecycleDecision Decision { get; set; }
    [MaxLength(500)] public string? Reason { get; set; }
}

public sealed class PromotionReviewInput
{
    public long BatchId { get; set; }
    public bool Approve { get; set; }
    [MaxLength(1000)] public string? Comment { get; set; }
    [Required] public string RowVersion { get; set; } = "";
}

public sealed class PromotionDetailsViewModel
{
    public PromotionBatch Batch { get; init; } = null!;
    public IReadOnlyList<PromotionEntry> Entries { get; init; } = [];
}

public sealed class CommunicationInput
{
    [Required, MaxLength(160)] public string Title { get; set; } = "";
    [Required, MaxLength(8000)] public string Body { get; set; } = "";
    [Required, MaxLength(40)] public string Priority { get; set; } = "Normal";
    public DateTime? ScheduledLocal { get; set; }
    public DateTime? ExpiresLocal { get; set; }
    [Required, MaxLength(40)] public string AudienceType { get; set; } = "Role";
    [Required, MaxLength(450)] public string AudienceValue { get; set; } = "";
    public bool InApp { get; set; } = true;
    public bool Email { get; set; }
    public bool Sms { get; set; }
}

public sealed class TimetableEntryInput
{
    public int? Id { get; set; }
    public byte[] RowVersion { get; set; } = [];
    [Required] public int ClassId { get; set; }
    [Required] public int SubjectId { get; set; }
    [Required] public int TeacherId { get; set; }
    [Required, Range(1, 7)] public int DayOfWeek { get; set; }
    [Required] public TimeSpan StartsAt { get; set; }
    [Required] public TimeSpan EndsAt { get; set; }
    public int? RoomId { get; set; }
}

public sealed class ReportCenterQuery : ListQuery
{
    [Required, MaxLength(80)] public string ReportType { get; set; } = "Enrollment";
    [MaxLength(20)] public string Format { get; set; } = "Html";
}

public sealed class DashboardMetric
{
    public required string Label { get; init; }
    public required string Value { get; init; }
    public required string Icon { get; init; }
    public string? Controller { get; init; }
    public string? Action { get; init; }
    public string Tone { get; init; } = "";
}

// Compatibility model for the earlier workspace cards. The analytics canvas
// uses RoleDashboardViewModel from DashboardAnalyticsModels.cs.
public sealed class WorkspaceDashboardViewModel
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<DashboardMetric> Metrics { get; init; } = [];
    public IReadOnlyList<UserNotification> Notifications { get; init; } = [];
    public IReadOnlyList<AuditLog> Activities { get; init; } = [];
}

public sealed class InstitutionInput
{
    public int? Id { get; set; }
    [Required, MaxLength(180)] public string Name { get; set; } = "";
    [Required, MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(500)] public string? Address { get; set; }
    [EmailAddress, MaxLength(256)] public string? Email { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    [Required, MaxLength(80)] public string TimeZoneId { get; set; } = "Asia/Kolkata";
}

public sealed class CampusInput
{
    public int? Id { get; set; }
    [Required] public int InstitutionId { get; set; }
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    [Required, MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(500)] public string? Address { get; set; }
}

public sealed class AcademicSessionInput
{
    public int? Id { get; set; }
    [Required, MaxLength(80)] public string Name { get; set; } = "";
    [DataType(DataType.Date)] public DateTime StartsOn { get; set; }
    [DataType(DataType.Date)] public DateTime EndsOn { get; set; }
    public int? AcademicYearId { get; set; }
    public bool IsCurrent { get; set; }
}

public sealed class CurriculumInput
{
    [Required] public int AcademicProgramId { get; set; }
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    [Required, MaxLength(30)] public string Version { get; set; } = "";
    [Required] public int EffectiveAcademicSessionId { get; set; }
    public IReadOnlyList<int> SubjectIds { get; set; } = [];
}

public sealed class AcademicEventInput
{
    [Required] public int AcademicSessionId { get; set; }
    [Required, MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(1000)] public string? Description { get; set; }
    public AcademicEventKind Kind { get; set; }
    public DateTime StartsLocal { get; set; }
    public DateTime EndsLocal { get; set; }
    public int? DepartmentId { get; set; }
    public bool IsPublished { get; set; }
}
