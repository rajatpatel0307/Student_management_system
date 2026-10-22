using System.ComponentModel.DataAnnotations;
using Student_Management_System.Services;

namespace Student_Management_System.Models;

public sealed class AssignmentInputModel : IValidatableObject
{
    public int Id { get; set; }
    [Required] public int SubjectId { get; set; }
    [Required, MaxLength(160)] public string Title { get; set; } = "";
    [Required, MaxLength(4000)] public string Instructions { get; set; } = "";
    public DateTime DueUtc { get; set; }
    public DateTime? PublishUtc { get; set; }
    [Required, MaxLength(40)] public string Status { get; set; } = AssignmentLifecycle.Draft;
    [Required, MaxLength(40)] public string SubmissionType { get; set; } = "TextOrFile";
    [Range(1, 20)] public int MaximumSubmissions { get; set; } = 1;
    public bool AllowLateSubmission { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal LatePenaltyPercent { get; set; }
    [Range(typeof(decimal), "0.01", "1000000")] public decimal MaximumScore { get; set; } = 100;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!AssignmentLifecycle.All.Contains(Status, StringComparer.Ordinal))
            yield return new ValidationResult("Select a supported lifecycle state.", [nameof(Status)]);
        if (Status == AssignmentLifecycle.Scheduled && !PublishUtc.HasValue)
            yield return new ValidationResult("A scheduled assignment requires a publication time.", [nameof(PublishUtc)]);
        if (Status == AssignmentLifecycle.Draft && PublishUtc.HasValue)
            yield return new ValidationResult("Draft assignments cannot have an active publication time.", [nameof(PublishUtc)]);
        if (PublishUtc.HasValue && DueUtc <= PublishUtc.Value)
            yield return new ValidationResult("The due time must be after the publication time.", [nameof(DueUtc)]);
        if (!AllowLateSubmission && LatePenaltyPercent != 0)
            yield return new ValidationResult("A late penalty requires late submissions to be enabled.", [nameof(LatePenaltyPercent)]);
    }

    public static AssignmentInputModel FromEntity(Assignment assignment) => new()
    {
        Id = assignment.Id,
        SubjectId = assignment.SubjectId,
        Title = assignment.Title,
        Instructions = assignment.Instructions,
        DueUtc = assignment.DueUtc,
        PublishUtc = assignment.PublishUtc,
        Status = assignment.Status,
        SubmissionType = assignment.SubmissionType,
        MaximumSubmissions = assignment.MaximumSubmissions,
        AllowLateSubmission = assignment.AllowLateSubmission,
        LatePenaltyPercent = assignment.LatePenaltyPercent,
        MaximumScore = assignment.MaximumScore
    };
}

public sealed class ExamEditInputModel : IValidatableObject
{
    public int Id { get; set; }
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    [Required, MaxLength(50)] public string ExamType { get; set; } = "";
    public DateTime ExamDate { get; set; }
    [Range(typeof(decimal), "0.01", "1000000")] public decimal MaximumMarks { get; set; }
    [Range(typeof(decimal), "0", "1000000")] public decimal PassMarks { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal WeightPercent { get; set; }
    public DateTime? EntryOpensUtc { get; set; }
    public DateTime? EntryClosesUtc { get; set; }
    public DateTime? PublicationScheduledUtc { get; set; }
    public bool IsSupplementary { get; set; }
    [Required] public byte[] RowVersion { get; set; } = [];
    public ExamLifecycle CurrentStatus { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PassMarks > MaximumMarks)
            yield return new ValidationResult("Pass marks cannot exceed maximum marks.", [nameof(PassMarks)]);
        if (EntryOpensUtc.HasValue && EntryClosesUtc.HasValue && EntryClosesUtc <= EntryOpensUtc)
            yield return new ValidationResult("Entry closing time must be after entry opening time.", [nameof(EntryClosesUtc)]);
        if (PublicationScheduledUtc.HasValue && EntryClosesUtc.HasValue && PublicationScheduledUtc < EntryClosesUtc)
            yield return new ValidationResult("Publication cannot be scheduled before mark entry closes.", [nameof(PublicationScheduledUtc)]);
    }

    public static ExamEditInputModel FromEntity(Exam exam) => new()
    {
        Id = exam.Id,
        Name = exam.Name,
        ExamType = exam.ExamType,
        ExamDate = exam.ExamDate,
        MaximumMarks = exam.MaximumMarks,
        PassMarks = exam.PassMarks,
        WeightPercent = exam.WeightPercent,
        EntryOpensUtc = exam.EntryOpensUtc,
        EntryClosesUtc = exam.EntryClosesUtc,
        PublicationScheduledUtc = exam.PublicationScheduledUtc,
        IsSupplementary = exam.IsSupplementary,
        RowVersion = exam.RowVersion,
        CurrentStatus = exam.Status
    };
}
