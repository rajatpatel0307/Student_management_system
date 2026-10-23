using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Student_Management_System.Models;

public enum StudentLifecycleDecision
{
    Pending = 1,
    Promoted,
    Retained,
    Graduated,
    Transferred,
    Withdrawn
}

public enum PromotionBatchStatus
{
    Draft = 1,
    Submitted,
    Approved,
    Rejected,
    Applied,
    Cancelled
}

public enum CommunicationStatus
{
    Draft = 1,
    Scheduled,
    Processing,
    Completed,
    Cancelled
}

public enum DeliveryChannel
{
    InApp = 1,
    Email,
    Sms
}

public enum DeliveryStatus
{
    Pending = 1,
    Processing,
    Delivered,
    Failed,
    Skipped
}

public enum InvoiceStatus
{
    Draft = 1,
    Issued,
    PartiallyPaid,
    Paid,
    Overdue,
    Cancelled
}

public enum AcademicEventKind
{
    Academic = 1,
    Holiday,
    Examination,
    Admission,
    Activity
}

public sealed class Institution
{
    public int Id { get; set; }
    [Required, MaxLength(180)] public string Name { get; set; } = "";
    [Required, MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(500)] public string? Address { get; set; }
    [EmailAddress, MaxLength(256)] public string? Email { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    [MaxLength(80)] public string TimeZoneId { get; set; } = "Asia/Kolkata";
    public bool IsActive { get; set; } = true;
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public sealed class Campus
{
    public int Id { get; set; }
    public int InstitutionId { get; set; }
    public Institution Institution { get; set; } = null!;
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    [Required, MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(500)] public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public sealed class ProgramType
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string Name { get; set; } = "";
    [MaxLength(300)] public string? Description { get; set; }
    public bool UsesCredits { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public sealed class AcademicSession
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string Name { get; set; } = "";
    public DateTime StartsOn { get; set; }
    public DateTime EndsOn { get; set; }
    public int? AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsClosed { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public sealed class AcademicSubjectType
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string Name { get; set; } = "";
    public bool IsPractical { get; set; }
    public bool IsElective { get; set; }
}

public sealed class Curriculum
{
    public int Id { get; set; }
    public int AcademicProgramId { get; set; }
    public AcademicProgram AcademicProgram { get; set; } = null!;
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    public bool IsArchived { get; set; }
    public ICollection<CurriculumVersion> Versions { get; set; } = [];
}

public sealed class CurriculumVersion
{
    public int Id { get; set; }
    public int CurriculumId { get; set; }
    public Curriculum Curriculum { get; set; } = null!;
    [Required, MaxLength(30)] public string Version { get; set; } = "";
    public int EffectiveAcademicSessionId { get; set; }
    public AcademicSession EffectiveAcademicSession { get; set; } = null!;
    public DateTime ApprovedUtc { get; set; }
    [MaxLength(450)] public string? ApprovedByUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<CurriculumSubject> Subjects { get; set; } = [];
}

public sealed class CurriculumSubject
{
    public int Id { get; set; }
    public int CurriculumVersionId { get; set; }
    public CurriculumVersion CurriculumVersion { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public int SemesterNumber { get; set; }
    [Column(TypeName = "decimal(8,2)")] public decimal Credits { get; set; }
    [MaxLength(80)] public string? ElectiveGroup { get; set; }
    public bool IsRequired { get; set; } = true;
}

public sealed class AcademicCalendarEvent
{
    public long Id { get; set; }
    public int AcademicSessionId { get; set; }
    public AcademicSession AcademicSession { get; set; } = null!;
    [Required, MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(1000)] public string? Description { get; set; }
    public AcademicEventKind Kind { get; set; }
    public DateTime StartsUtc { get; set; }
    public DateTime EndsUtc { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public bool IsPublished { get; set; }
}

public sealed class PromotionBatch
{
    public long Id { get; set; }
    [Required, MaxLength(40)] public string BatchNumber { get; set; } = "";
    public int SourceAcademicYearId { get; set; }
    public AcademicYear SourceAcademicYear { get; set; } = null!;
    public int DestinationAcademicYearId { get; set; }
    public AcademicYear DestinationAcademicYear { get; set; } = null!;
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int SourceClassId { get; set; }
    public Class SourceClass { get; set; } = null!;
    public int SourceSectionId { get; set; }
    public Section SourceSection { get; set; } = null!;
    public int? DestinationClassId { get; set; }
    public Class? DestinationClass { get; set; }
    public int? DestinationSectionId { get; set; }
    public Section? DestinationSection { get; set; }
    public PromotionBatchStatus Status { get; set; } = PromotionBatchStatus.Draft;
    [Required, MaxLength(450)] public string CreatedByUserId { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(450)] public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedUtc { get; set; }
    [MaxLength(450)] public string? AppliedByUserId { get; set; }
    public DateTime? AppliedUtc { get; set; }
    [MaxLength(1000)] public string? ReviewComment { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public ICollection<PromotionEntry> Entries { get; set; } = [];
}

public sealed class PromotionEntry
{
    public long Id { get; set; }
    public long PromotionBatchId { get; set; }
    public PromotionBatch PromotionBatch { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public StudentLifecycleDecision Decision { get; set; } = StudentLifecycleDecision.Pending;
    [MaxLength(500)] public string? Reason { get; set; }
    [Column(TypeName = "decimal(6,2)")] public decimal AttendancePercent { get; set; }
    [Column(TypeName = "decimal(6,2)")] public decimal ResultPercent { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal OutstandingBalance { get; set; }
    public bool IsEligible { get; set; }
}

public sealed class AdmissionInquiry
{
    public long Id { get; set; }
    [Required, MaxLength(160)] public string ProspectName { get; set; } = "";
    [EmailAddress, MaxLength(256)] public string? Email { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    public int? AcademicProgramId { get; set; }
    public AcademicProgram? AcademicProgram { get; set; }
    [MaxLength(80)] public string Source { get; set; } = "Direct";
    [MaxLength(40)] public string Status { get; set; } = "Open";
    [MaxLength(450)] public string? AssignedToUserId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ConvertedUtc { get; set; }
    public long? AdmissionApplicationId { get; set; }
    public AdmissionApplication? AdmissionApplication { get; set; }
}

public sealed class AdmissionAppointment
{
    public long Id { get; set; }
    public long AdmissionApplicationId { get; set; }
    public AdmissionApplication AdmissionApplication { get; set; } = null!;
    [Required, MaxLength(40)] public string AppointmentType { get; set; } = "Interview";
    public DateTime StartsUtc { get; set; }
    [MaxLength(160)] public string? Location { get; set; }
    [MaxLength(450)] public string? ReviewerUserId { get; set; }
    [MaxLength(40)] public string Status { get; set; } = "Scheduled";
    [MaxLength(2000)] public string? Outcome { get; set; }
}

public sealed class GuardianLinkHistory
{
    public long Id { get; set; }
    public int GuardianStudentLinkId { get; set; }
    public GuardianStudentLink GuardianStudentLink { get; set; } = null!;
    [Required, MaxLength(40)] public string Action { get; set; } = "";
    [Required, MaxLength(450)] public string ChangedByUserId { get; set; } = "";
    [MaxLength(1000)] public string? Comment { get; set; }
    public DateTime ChangedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class FeeCategory
{
    public int Id { get; set; }
    [Required, MaxLength(30)] public string Code { get; set; } = "";
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    public bool IsRefundable { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class StudentInvoice
{
    public long Id { get; set; }
    [Required, MaxLength(40)] public string InvoiceNumber { get; set; } = "";
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public DateTime IssuedUtc { get; set; }
    public DateTime DueUtc { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    [Column(TypeName = "decimal(18,2)")] public decimal TotalAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal AidAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal PaidAmount { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public ICollection<StudentInvoiceLine> Lines { get; set; } = [];
    public ICollection<StudentAidAllocation> AidAllocations { get; set; } = [];
}

public sealed class StudentInvoiceLine
{
    public long Id { get; set; }
    public long StudentInvoiceId { get; set; }
    public StudentInvoice StudentInvoice { get; set; } = null!;
    public int? FeeId { get; set; }
    public Fee? Fee { get; set; }
    public int? StudentClassEnrollmentId { get; set; }
    public StudentClassEnrollment? StudentClassEnrollment { get; set; }
    public int FeeCategoryId { get; set; }
    public FeeCategory FeeCategory { get; set; } = null!;
    [Required, MaxLength(160)] public string Description { get; set; } = "";
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal DiscountAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal LateFeeAmount { get; set; }
}

public sealed class PaymentAllocation
{
    public long Id { get; set; }
    public long FinancialTransactionId { get; set; }
    public FinancialTransaction FinancialTransaction { get; set; } = null!;
    public long StudentInvoiceLineId { get; set; }
    public StudentInvoiceLine StudentInvoiceLine { get; set; } = null!;
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
}

public sealed class StudentAidAward
{
    public long Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    [Required, MaxLength(40)] public string AwardType { get; set; } = "Scholarship";
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public DateTime StartsOn { get; set; }
    public DateTime? EndsOn { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public ICollection<StudentAidAllocation> Allocations { get; set; } = [];
}

public sealed class StudentAidAllocation
{
    public long Id { get; set; }
    public long StudentAidAwardId { get; set; }
    public StudentAidAward StudentAidAward { get; set; } = null!;
    public long StudentInvoiceId { get; set; }
    public StudentInvoice StudentInvoice { get; set; } = null!;
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    public DateTime AllocatedUtc { get; set; } = DateTime.UtcNow;
    [Required, MaxLength(450)] public string AllocatedByUserId { get; set; } = "";
    public DateTime? ReversedUtc { get; set; }
    [MaxLength(450)] public string? ReversedByUserId { get; set; }
    [MaxLength(500)] public string? ReversalReason { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public sealed class FinancialPeriod
{
    public int Id { get; set; }
    [Required, MaxLength(40)] public string Name { get; set; } = "";
    public DateTime StartsOn { get; set; }
    public DateTime EndsOn { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedUtc { get; set; }
    [MaxLength(450)] public string? ClosedByUserId { get; set; }
}

public sealed class DailyCollectionClose
{
    public long Id { get; set; }
    public DateTime BusinessDate { get; set; }
    [Required, MaxLength(450)] public string CashierUserId { get; set; } = "";
    [Column(TypeName = "decimal(18,2)")] public decimal ExpectedAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CountedAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Variance { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public DateTime ClosedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Room
{
    public int Id { get; set; }
    public int? CampusId { get; set; }
    public Campus? Campus { get; set; }
    [Required, MaxLength(30)] public string Code { get; set; } = "";
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    public int Capacity { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class TimetableSubstitution
{
    public long Id { get; set; }
    public int TimetableEntryId { get; set; }
    public TimetableEntry TimetableEntry { get; set; } = null!;
    public DateTime OccursOn { get; set; }
    public int? SubstituteTeacherId { get; set; }
    public Teacher? SubstituteTeacher { get; set; }
    public int? RoomId { get; set; }
    public Room? Room { get; set; }
    [Required, MaxLength(40)] public string Status { get; set; } = "Substituted";
    [MaxLength(500)] public string? Reason { get; set; }
    [Required, MaxLength(450)] public string CreatedByUserId { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CommunicationMessage
{
    public long Id { get; set; }
    [Required, MaxLength(160)] public string Title { get; set; } = "";
    [Required, MaxLength(8000)] public string Body { get; set; } = "";
    [MaxLength(40)] public string Priority { get; set; } = "Normal";
    public CommunicationStatus Status { get; set; } = CommunicationStatus.Draft;
    public DateTime? ScheduledUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }
    [Required, MaxLength(450)] public string CreatedByUserId { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public ICollection<CommunicationAudience> Audiences { get; set; } = [];
    public ICollection<CommunicationDelivery> Deliveries { get; set; } = [];
}

public sealed class CommunicationAudience
{
    public long Id { get; set; }
    public long CommunicationMessageId { get; set; }
    public CommunicationMessage CommunicationMessage { get; set; } = null!;
    [Required, MaxLength(40)] public string AudienceType { get; set; } = "Role";
    [Required, MaxLength(450)] public string AudienceValue { get; set; } = "";
}

public sealed class CommunicationDelivery
{
    public long Id { get; set; }
    public long CommunicationMessageId { get; set; }
    public CommunicationMessage CommunicationMessage { get; set; } = null!;
    [Required, MaxLength(450)] public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public DeliveryChannel Channel { get; set; }
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? DeliveredUtc { get; set; }
    public DateTime? ReadUtc { get; set; }
    [MaxLength(1000)] public string? LastError { get; set; }
    [MaxLength(64)] public string? ClaimOwner { get; set; }
    public DateTime? ClaimedUtc { get; set; }
    public DateTime? ClaimExpiresUtc { get; set; }
    public DateTime? NextAttemptUtc { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public sealed class OutboxMessage
{
    public long Id { get; set; }
    [Required, MaxLength(100)] public string Type { get; set; } = "";
    [Required] public string PayloadJson { get; set; } = "";
    public DateTime OccurredUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedUtc { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptUtc { get; set; }
    [MaxLength(2000)] public string? LastError { get; set; }
    [MaxLength(64)] public string? ClaimOwner { get; set; }
    public DateTime? ClaimedUtc { get; set; }
    public DateTime? ClaimExpiresUtc { get; set; }
    public bool IsDeadLetter { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public sealed class LibraryCategory
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class LibraryAuthor
{
    public int Id { get; set; }
    [Required, MaxLength(160)] public string Name { get; set; } = "";
}

public sealed class LibraryPublisher
{
    public int Id { get; set; }
    [Required, MaxLength(160)] public string Name { get; set; } = "";
}

public sealed class LibraryAcquisition
{
    public long Id { get; set; }
    public int LibraryBookId { get; set; }
    public LibraryBook LibraryBook { get; set; } = null!;
    [Required, MaxLength(80)] public string Supplier { get; set; } = "";
    public DateTime AcquiredOn { get; set; }
    public int Quantity { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TotalCost { get; set; }
    [MaxLength(100)] public string? Reference { get; set; }
}

public sealed class LibraryBorrowingPolicy
{
    public int Id { get; set; }
    [Required, MaxLength(40)] public string RoleName { get; set; } = "";
    public int MaximumLoans { get; set; }
    public int LoanDays { get; set; }
    public int MaximumRenewals { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal DailyFine { get; set; }
}

public sealed class LibraryStockVerification
{
    public long Id { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    [Required, MaxLength(450)] public string StartedByUserId { get; set; } = "";
    public int ExpectedCopies { get; set; }
    public int LocatedCopies { get; set; }
    public int MissingCopies { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
}

public sealed class HostelBuilding
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(500)] public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class HostelBed
{
    public int Id { get; set; }
    public int HostelRoomId { get; set; }
    public HostelRoom HostelRoom { get; set; } = null!;
    [Required, MaxLength(30)] public string BedNumber { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class CampusServiceTicket
{
    public long Id { get; set; }
    [Required, MaxLength(40)] public string Module { get; set; } = "";
    [Required, MaxLength(160)] public string Title { get; set; } = "";
    [Required, MaxLength(2000)] public string Description { get; set; } = "";
    [MaxLength(40)] public string Priority { get; set; } = "Normal";
    [MaxLength(40)] public string Status { get; set; } = "Open";
    [Required, MaxLength(450)] public string ReportedByUserId { get; set; } = "";
    [MaxLength(450)] public string? AssignedToUserId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedUtc { get; set; }
}

public sealed class TransportVehicle
{
    public int Id { get; set; }
    [Required, MaxLength(30)] public string RegistrationNumber { get; set; } = "";
    [Required, MaxLength(80)] public string Model { get; set; } = "";
    public int Capacity { get; set; }
    public bool IsActive { get; set; } = true;
    [MaxLength(30)] public string? VehicleNumber { get; set; }
    [MaxLength(40)] public string? VehicleType { get; set; }
    public int? TransportDriverId { get; set; }
    public TransportDriver? TransportDriver { get; set; }
    public int? TransportRouteId { get; set; }
    public TransportRoute? TransportRoute { get; set; }
    public DateTime? InsuranceExpiry { get; set; }
    public DateTime? FitnessExpiry { get; set; }
    public DateTime? PermitExpiry { get; set; }
    [MaxLength(40)] public string MaintenanceStatus { get; set; } = "Ready";
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public sealed class TransportStop
{
    public int Id { get; set; }
    public int TransportRouteId { get; set; }
    public TransportRoute TransportRoute { get; set; } = null!;
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    public int Sequence { get; set; }
    public TimeSpan? ScheduledTime { get; set; }
}

public sealed class PlacementInterview
{
    public long Id { get; set; }
    public long PlacementApplicationId { get; set; }
    public PlacementApplication PlacementApplication { get; set; } = null!;
    [Required, MaxLength(80)] public string Stage { get; set; } = "";
    public DateTime ScheduledUtc { get; set; }
    [MaxLength(160)] public string? Location { get; set; }
    [MaxLength(40)] public string Status { get; set; } = "Scheduled";
    [MaxLength(1000)] public string? Feedback { get; set; }
}

public sealed class PlacementOffer
{
    public long Id { get; set; }
    public long PlacementApplicationId { get; set; }
    public PlacementApplication PlacementApplication { get; set; } = null!;
    [Column(TypeName = "decimal(18,2)")] public decimal AnnualCompensation { get; set; }
    public DateTime OfferedUtc { get; set; }
    [MaxLength(40)] public string Status { get; set; } = "Offered";
    public DateTime? JoiningDate { get; set; }
}

public sealed class EmployeeContract
{
    public long Id { get; set; }
    public int EmployeeProfileId { get; set; }
    public EmployeeProfile EmployeeProfile { get; set; } = null!;
    [Required, MaxLength(80)] public string ContractType { get; set; } = "";
    public DateTime StartsOn { get; set; }
    public DateTime? EndsOn { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal BaseSalary { get; set; }
    [MaxLength(500)] public string? DocumentStorageKey { get; set; }
}

public sealed class EmployeePayroll
{
    public long Id { get; set; }
    public int EmployeeProfileId { get; set; }
    public EmployeeProfile EmployeeProfile { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal GrossAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal DeductionAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal NetAmount { get; set; }
    [MaxLength(40)] public string Status { get; set; } = "Draft";
    public DateTime? PaidUtc { get; set; }
}

public sealed class SavedReport
{
    public long Id { get; set; }
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    [Required, MaxLength(80)] public string ReportType { get; set; } = "";
    [Required] public string FilterJson { get; set; } = "{}";
    [Required, MaxLength(450)] public string OwnerUserId { get; set; } = "";
    public bool IsShared { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ReportSchedule
{
    public long Id { get; set; }
    public long SavedReportId { get; set; }
    public SavedReport SavedReport { get; set; } = null!;
    [Required, MaxLength(80)] public string CronExpression { get; set; } = "";
    [Required, MaxLength(256)] public string RecipientEmail { get; set; } = "";
    [Required, MaxLength(20)] public string Format { get; set; } = "Excel";
    public bool IsActive { get; set; } = true;
    public DateTime? NextRunUtc { get; set; }
    public DateTime? LastRunUtc { get; set; }
}
