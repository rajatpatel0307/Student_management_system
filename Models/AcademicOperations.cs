using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Student_Management_System.Models;

public sealed class GradeBoundary
{
    public int Id { get; set; }
    [Required, MaxLength(20)] public string Grade { get; set; } = "";
    [Range(0, 100)] public decimal MinimumPercent { get; set; }
    [Range(0, 100)] public decimal MaximumPercent { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ResultLifecycleHistory
{
    public long Id { get; set; }
    public int ExamId { get; set; }
    public Exam Exam { get; set; } = null!;
    public ExamLifecycle FromStatus { get; set; }
    public ExamLifecycle ToStatus { get; set; }
    [Required, MaxLength(450)] public string ChangedByUserId { get; set; } = "";
    [MaxLength(1000)] public string? Comment { get; set; }
    public DateTime ChangedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ResultCorrectionRequest
{
    public long Id { get; set; }
    public int MarkId { get; set; }
    public Mark Mark { get; set; } = null!;
    public decimal PreviousScore { get; set; }
    public decimal RequestedScore { get; set; }
    [Required, MaxLength(1000)] public string Reason { get; set; } = "";
    [Required, MaxLength(450)] public string RequestedByUserId { get; set; } = "";
    [ConcurrencyCheck] public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    [MaxLength(450)] public string? ReviewedByUserId { get; set; }
    public DateTime RequestedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedUtc { get; set; }
}

public sealed class AcademicProgram
{
    public int Id { get; set; }
    [Required, MaxLength(30)] public string Code { get; set; } = "";
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int? ProgramTypeId { get; set; }
    public ProgramType? ProgramType { get; set; }
    public int? CampusId { get; set; }
    public Campus? Campus { get; set; }
    [Range(1, 12)] public int DurationSemesters { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<AcademicIntake> Intakes { get; set; } = [];
}

public sealed class AcademicIntake
{
    public int Id { get; set; }
    public int AcademicProgramId { get; set; }
    public AcademicProgram AcademicProgram { get; set; } = null!;
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    [Range(1, 100000)] public int SeatCapacity { get; set; }
    public int ReservedSeats { get; set; }
    public DateTime OpensUtc { get; set; }
    public DateTime ClosesUtc { get; set; }
}

public enum DocumentVerificationStatus
{
    Pending = 1,
    Verified,
    Rejected
}

public sealed class AdmissionDocument
{
    public long Id { get; set; }
    public long AdmissionApplicationId { get; set; }
    public AdmissionApplication AdmissionApplication { get; set; } = null!;
    [Required, MaxLength(80)] public string DocumentType { get; set; } = "";
    [Required, MaxLength(500)] public string StorageKey { get; set; } = "";
    [Required, MaxLength(255)] public string OriginalFileName { get; set; } = "";
    [Required, MaxLength(100)] public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public DocumentVerificationStatus Status { get; set; } = DocumentVerificationStatus.Pending;
    [MaxLength(450)] public string? VerifiedByUserId { get; set; }
    public DateTime? VerifiedUtc { get; set; }
    [MaxLength(1000)] public string? VerificationComment { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AssignmentRubricCriterion
{
    public int Id { get; set; }
    public int AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    [Range(0.01, 1000000)] public decimal MaximumScore { get; set; }
    [MaxLength(1000)] public string? Description { get; set; }
}

public sealed class AssignmentSubmissionVersion
{
    public long Id { get; set; }
    public int AssignmentSubmissionId { get; set; }
    public AssignmentSubmission AssignmentSubmission { get; set; } = null!;
    public int VersionNumber { get; set; }
    [Required, MaxLength(4000)] public string Answer { get; set; } = "";
    [MaxLength(500)] public string? AttachmentStorageKey { get; set; }
    public DateTime SubmittedUtc { get; set; } = DateTime.UtcNow;
}

public enum LibraryCopyCondition
{
    Good = 1,
    Damaged,
    Lost,
    Withdrawn
}

public sealed class LibraryReservation
{
    public long Id { get; set; }
    public int LibraryBookId { get; set; }
    public LibraryBook LibraryBook { get; set; } = null!;
    [Required, MaxLength(450)] public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public DateTime ReservedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresUtc { get; set; }
    public DateTime? FulfilledUtc { get; set; }
    public DateTime? CancelledUtc { get; set; }
}

public sealed class LibraryFineSettlement
{
    public long Id { get; set; }
    public long LibraryLoanId { get; set; }
    public LibraryLoan LibraryLoan { get; set; } = null!;
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    [Required, MaxLength(40)] public string Method { get; set; } = "Cash";
    [MaxLength(100)] public string? Reference { get; set; }
    [Required, MaxLength(450)] public string CollectedByUserId { get; set; } = "";
    public DateTime SettledUtc { get; set; } = DateTime.UtcNow;
}

public sealed class TransportRoute
{
    public int Id { get; set; }
    [Required, MaxLength(30)] public string Code { get; set; } = "";
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    [Required, MaxLength(1000)] public string Stops { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class TransportEnrollment
{
    public long Id { get; set; }
    public int TransportRouteId { get; set; }
    public TransportRoute TransportRoute { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    [Required, MaxLength(120)] public string BoardingStop { get; set; } = "";
    public DateTime StartsOn { get; set; }
    public DateTime? EndsOn { get; set; }
}

public sealed class HostelRoom
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string HostelName { get; set; } = "";
    [Required, MaxLength(30)] public string RoomNumber { get; set; } = "";
    [Range(1, 20)] public int Capacity { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class HostelAllocation
{
    public long Id { get; set; }
    public int HostelRoomId { get; set; }
    public HostelRoom HostelRoom { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public DateTime StartsOn { get; set; }
    public DateTime? EndsOn { get; set; }
}

public sealed class PlacementDrive
{
    public long Id { get; set; }
    [Required, MaxLength(160)] public string Employer { get; set; } = "";
    [Required, MaxLength(160)] public string RoleTitle { get; set; } = "";
    public DateTime ApplicationDeadlineUtc { get; set; }
    [MaxLength(2000)] public string? Eligibility { get; set; }
}

public sealed class PlacementApplication
{
    public long Id { get; set; }
    public long PlacementDriveId { get; set; }
    public PlacementDrive PlacementDrive { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    [Required, MaxLength(40)] public string Status { get; set; } = "Applied";
    public DateTime AppliedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class EmployeeProfile
{
    public int Id { get; set; }
    [Required, MaxLength(450)] public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    [Required, MaxLength(40)] public string EmployeeNumber { get; set; } = "";
    [Required, MaxLength(120)] public string JobTitle { get; set; } = "";
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public DateTime JoinedOn { get; set; }
    public bool IsActive { get; set; } = true;
}
