using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Student_Management_System.Models;

public enum AdmissionStage
{
    Draft = 1,
    Submitted,
    UnderReview,
    DocumentsPending,
    Verified,
    EntranceTest,
    Interview,
    Approved,
    OfferSent,
    Enrolled,
    Rejected,
    Waitlisted,
    Withdrawn,
    Cancelled
}

public class AdmissionApplication
{
    public long Id { get; set; }
    public int? CampusId { get; set; }
    public Campus? Campus { get; set; }
    [Required, MaxLength(32)] public string ReferenceNumber { get; set; } = "";
    [Required, MaxLength(160)] public string ApplicantName { get; set; } = "";
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = "";
    [MaxLength(30)] public string? Phone { get; set; }
    [DataType(DataType.Date)] public DateTime DateOfBirth { get; set; }
    [MaxLength(1000)] public string? Address { get; set; }
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int? ClassId { get; set; }
    public Class? Class { get; set; }
    [Required, MaxLength(120)] public string ProgramName { get; set; } = "";
    public int? AcademicProgramId { get; set; }
    public AcademicProgram? AcademicProgram { get; set; }
    public int? AcademicIntakeId { get; set; }
    public AcademicIntake? AcademicIntake { get; set; }
    [MaxLength(450)] public string? AssignedReviewerUserId { get; set; }
    [MaxLength(80)] public string Source { get; set; } = "Direct";
    public AdmissionStage Stage { get; set; } = AdmissionStage.Draft;
    [MaxLength(2000)] public string? InternalNotes { get; set; }
    [MaxLength(2000)] public string? ApplicantNotes { get; set; }
    [MaxLength(450)] public string? CreatedByUserId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public int? EnrolledStudentId { get; set; }
    public Student? EnrolledStudent { get; set; }
    public ICollection<AdmissionStageHistory> StageHistory { get; set; } = [];
    public ICollection<AdmissionDocument> Documents { get; set; } = [];
    public bool IsArchived { get; set; }
    public DateTime? ArchivedUtc { get; set; }
    [MaxLength(450)] public string? ArchivedByUserId { get; set; }
    [MaxLength(500)] public string? ArchiveReason { get; set; }
    public DateTime? RestoredUtc { get; set; }
    [MaxLength(450)] public string? RestoredByUserId { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public class AdmissionStageHistory
{
    public long Id { get; set; }
    public long AdmissionApplicationId { get; set; }
    public AdmissionApplication AdmissionApplication { get; set; } = null!;
    public AdmissionStage FromStage { get; set; }
    public AdmissionStage ToStage { get; set; }
    [MaxLength(1000)] public string? Comment { get; set; }
    [Required, MaxLength(450)] public string ChangedByUserId { get; set; } = "";
    public DateTime ChangedUtc { get; set; } = DateTime.UtcNow;
}

public class GuardianProfile
{
    public int Id { get; set; }
    [Required, MaxLength(450)] public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    [Required, MaxLength(160)] public string FullName { get; set; } = "";
    [Required, MaxLength(40)] public string Relationship { get; set; } = "";
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = "";
    [MaxLength(30)] public string? Phone { get; set; }
    public ICollection<GuardianStudentLink> StudentLinks { get; set; } = [];
}

public class GuardianStudentLink
{
    public int Id { get; set; }
    public int GuardianProfileId { get; set; }
    public GuardianProfile GuardianProfile { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public bool IsPrimary { get; set; }
    public bool CanViewFinance { get; set; } = true;
    public bool CanViewAcademics { get; set; } = true;
    public bool CanReceiveEmergencyContact { get; set; } = true;
    [Required, MaxLength(40)] public string Relationship { get; set; } = "Guardian";
    [MaxLength(500)] public string? CommunicationPreferences { get; set; }
    public DateTime LinkedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UnlinkedUtc { get; set; }
}

public enum FinancialTransactionType
{
    Payment = 1,
    Adjustment,
    Refund,
    Reversal
}

public enum FinancialTransactionStatus
{
    Pending = 1,
    Completed,
    Failed,
    Cancelled
}

public class FinancialTransaction
{
    public long Id { get; set; }
    [Required, MaxLength(40)] public string ReceiptNumber { get; set; } = "";
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int FeeId { get; set; }
    public Fee Fee { get; set; } = null!;
    public FinancialTransactionType Type { get; set; }
    public FinancialTransactionStatus Status { get; set; } = FinancialTransactionStatus.Completed;
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    [Required, MaxLength(40)] public string PaymentMethod { get; set; } = "Cash";
    [MaxLength(100)] public string? TransactionReference { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    [Required, MaxLength(450)] public string CreatedByUserId { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public long? ReversesTransactionId { get; set; }
    public FinancialTransaction? ReversesTransaction { get; set; }
    [MaxLength(100)] public string? IdempotencyKey { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public sealed class ReceiptSequence
{
    public int Id { get; set; }
    [Required, MaxLength(20)] public string Period { get; set; } = "";
    public long NextValue { get; set; } = 1;
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public sealed class AccountActivation
{
    public long Id { get; set; }
    [Required, MaxLength(450)] public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    [Required, MaxLength(64)] public string TokenHash { get; set; } = "";
    public DateTime ExpiresUtc { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UsedUtc { get; set; }
    public DateTime? LastSentUtc { get; set; }
    public int SendCount { get; set; }
    public bool DeliveryPending { get; set; }
}

public enum AttendanceSessionStatus
{
    Draft = 1,
    Submitted,
    Approved,
    CorrectionRequested,
    Corrected
}

public sealed class AttendanceSession
{
    public long Id { get; set; }
    public int ClassId { get; set; }
    public Class Class { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public int? SectionId { get; set; }
    public Section? Section { get; set; }
    public DateTime SessionDate { get; set; }
    public TimeSpan? StartsAt { get; set; }
    public TimeSpan? EndsAt { get; set; }
    [Required, MaxLength(100)] public string Period { get; set; } = "";
    public AttendanceSessionStatus Status { get; set; } = AttendanceSessionStatus.Draft;
    public DateTime? SubmittedUtc { get; set; }
    [MaxLength(450)] public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedUtc { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public ICollection<Attendance> Records { get; set; } = [];
}

public sealed class AttendanceCorrection
{
    public long Id { get; set; }
    public int AttendanceId { get; set; }
    public Attendance Attendance { get; set; } = null!;
    [Required, MaxLength(20)] public string PreviousStatus { get; set; } = "";
    [Required, MaxLength(20)] public string RequestedStatus { get; set; } = "";
    [Required, MaxLength(1000)] public string Reason { get; set; } = "";
    [Required, MaxLength(450)] public string RequestedByUserId { get; set; } = "";
    public DateTime RequestedUtc { get; set; } = DateTime.UtcNow;
    [ConcurrencyCheck] public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    [MaxLength(450)] public string? ReviewedByUserId { get; set; }
    [MaxLength(1000)] public string? ReviewComment { get; set; }
    public DateTime? ReviewedUtc { get; set; }
}

public enum LeaveRequestStatus
{
    Pending = 1,
    Approved,
    Rejected,
    Cancelled
}

public class LeaveRequest
{
    public long Id { get; set; }
    [Required, MaxLength(450)] public string RequesterUserId { get; set; } = "";
    [Required, MaxLength(60)] public string LeaveType { get; set; } = "";
    [DataType(DataType.Date)] public DateTime StartsOn { get; set; }
    [DataType(DataType.Date)] public DateTime EndsOn { get; set; }
    [Required, MaxLength(2000)] public string Reason { get; set; } = "";
    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Pending;
    [MaxLength(450)] public string? ReviewedByUserId { get; set; }
    [MaxLength(1000)] public string? ReviewComment { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedUtc { get; set; }
}

public class UserNotification
{
    public long Id { get; set; }
    [Required, MaxLength(450)] public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    [Required, MaxLength(160)] public string Title { get; set; } = "";
    [Required, MaxLength(1000)] public string Message { get; set; } = "";
    [MaxLength(500)] public string? ActionUrl { get; set; }
    [MaxLength(40)] public string Category { get; set; } = "General";
    public bool IsRead { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadUtc { get; set; }
}

public enum ApprovalStatus
{
    Pending = 1,
    Approved,
    Rejected,
    Cancelled
}

public class ApprovalRequest
{
    public long Id { get; set; }
    [Required, MaxLength(80)] public string WorkflowType { get; set; } = "";
    [Required, MaxLength(100)] public string EntityType { get; set; } = "";
    [Required, MaxLength(100)] public string EntityId { get; set; } = "";
    [Required, MaxLength(450)] public string RequestedByUserId { get; set; } = "";
    [MaxLength(450)] public string? CurrentApproverUserId { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    [MaxLength(2000)] public string? Reason { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DueUtc { get; set; }
    public DateTime? EscalatedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public ICollection<ApprovalStep> Steps { get; set; } = [];
}

public class ApprovalStep
{
    public long Id { get; set; }
    public long ApprovalRequestId { get; set; }
    public ApprovalRequest ApprovalRequest { get; set; } = null!;
    public int Sequence { get; set; }
    [Required, MaxLength(80)] public string ApproverRole { get; set; } = "";
    [MaxLength(450)] public string? ApproverUserId { get; set; }
    [MaxLength(450)] public string? DelegatedToUserId { get; set; }
    public DateTime? DueUtc { get; set; }
    public DateTime? EscalatedUtc { get; set; }
    [MaxLength(450)] public string? ActedByUserId { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    [MaxLength(1000)] public string? Comment { get; set; }
    public DateTime? ActedUtc { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public class LibraryBook
{
    public int Id { get; set; }
    [Required, MaxLength(200)] public string Title { get; set; } = "";
    [Required, MaxLength(160)] public string Author { get; set; } = "";
    [MaxLength(20)] public string? Isbn { get; set; }
    [MaxLength(80)] public string? Category { get; set; }
    [MaxLength(80)] public string? Publisher { get; set; }
    public int? LibraryCategoryId { get; set; }
    public LibraryCategory? LibraryCategory { get; set; }
    public int? LibraryPublisherId { get; set; }
    public LibraryPublisher? LibraryPublisher { get; set; }
    public bool IsArchived { get; set; }
    public ICollection<LibraryBookCopy> Copies { get; set; } = [];
}

public class LibraryBookCopy
{
    public int Id { get; set; }
    public int LibraryBookId { get; set; }
    public LibraryBook LibraryBook { get; set; } = null!;
    [Required, MaxLength(50)] public string Barcode { get; set; } = "";
    [MaxLength(80)] public string? ShelfLocation { get; set; }
    public bool IsAvailable { get; set; } = true;
    public LibraryCopyCondition Condition { get; set; } = LibraryCopyCondition.Good;
    public ICollection<LibraryLoan> Loans { get; set; } = [];
}

public class LibraryLoan
{
    public long Id { get; set; }
    public int LibraryBookCopyId { get; set; }
    public LibraryBookCopy LibraryBookCopy { get; set; } = null!;
    [Required, MaxLength(450)] public string BorrowerUserId { get; set; } = "";
    public ApplicationUser BorrowerUser { get; set; } = null!;
    public DateTime IssuedUtc { get; set; } = DateTime.UtcNow;
    public DateTime DueUtc { get; set; }
    public DateTime? ReturnedUtc { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal FineAmount { get; set; }
    public int RenewalCount { get; set; }
    public DateTime? LastRenewedUtc { get; set; }
}
