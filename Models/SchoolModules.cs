using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Student_Management_System.Models;

public class AcademicYear
{
    public int Id { get; set; }
    [Required, MaxLength(40)] public string Name { get; set; } = "";
    [DataType(DataType.Date)] public DateTime StartsOn { get; set; }
    [DataType(DataType.Date)] public DateTime EndsOn { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedUtc { get; set; }
    [MaxLength(450)] public string? ArchivedByUserId { get; set; }
    [MaxLength(500)] public string? ArchiveReason { get; set; }
    public DateTime? RestoredUtc { get; set; }
    [MaxLength(450)] public string? RestoredByUserId { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public class Fee
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public Class? Class { get; set; }
    [Range(0.01, 10000000)] [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
    [Required, MaxLength(80)] public string Term { get; set; } = "";
    [DataType(DataType.Date)] public DateTime DueDate { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedUtc { get; set; }
    [MaxLength(450)] public string? ArchivedByUserId { get; set; }
    [MaxLength(500)] public string? ArchiveReason { get; set; }
    public DateTime? RestoredUtc { get; set; }
    [MaxLength(450)] public string? RestoredByUserId { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public class Payment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    public int FeeId { get; set; }
    public Fee? Fee { get; set; }
    [Range(0.01, 10000000)] [Column(TypeName = "decimal(18,2)")]
    public decimal AmountPaid { get; set; }
    public DateTime PaymentDateUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string Reference { get; set; } = "";
}

public class TimetableEntry
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public Class? Class { get; set; }
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
    [Range(1, 7)] public int DayOfWeek { get; set; }
    public TimeSpan StartsAt { get; set; }
    public TimeSpan EndsAt { get; set; }
    [MaxLength(80)] public string Room { get; set; } = "";
    public int? RoomId { get; set; }
    public Room? RoomEntity { get; set; }
    public bool IsCancelled { get; set; }
    [MaxLength(500)] public string? CancellationReason { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public class Assignment
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }
    [Required, MaxLength(160)] public string Title { get; set; } = "";
    [Required, MaxLength(4000)] public string Instructions { get; set; } = "";
    public DateTime DueUtc { get; set; }
    public DateTime? PublishUtc { get; set; }
    [MaxLength(40)] public string Status { get; set; } = "Draft";
    [MaxLength(40)] public string SubmissionType { get; set; } = "TextOrFile";
    public int MaximumSubmissions { get; set; } = 1;
    public bool AllowLateSubmission { get; set; }
    [Column(TypeName = "decimal(8,2)")] public decimal LatePenaltyPercent { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public ICollection<AssignmentSubmission> Submissions { get; set; } = [];
    public decimal MaximumScore { get; set; } = 100;
    public ICollection<AssignmentRubricCriterion> Rubric { get; set; } = [];
}

public class AssignmentSubmission
{
    public int Id { get; set; }
    public int AssignmentId { get; set; }
    public Assignment? Assignment { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    [Required, MaxLength(4000)] public string Answer { get; set; } = "";
    public DateTime SubmittedUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(40)] public string EvaluationStatus { get; set; } = "Pending";
    [Column(TypeName = "decimal(18,2)")]
    [Range(typeof(decimal), "0", "1000000")] public decimal? Score { get; set; }
    [MaxLength(1000)] public string? Feedback { get; set; }
    public DateTime? GradedUtc { get; set; }
    [MaxLength(450)] public string? GradedByUserId { get; set; }
    public ICollection<AssignmentSubmissionVersion> Versions { get; set; } = [];
}
