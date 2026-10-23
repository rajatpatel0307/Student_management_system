using System.ComponentModel.DataAnnotations;

namespace Student_Management_System.Models;

// Identity owns credentials; EmployeeProfile owns employment; this extension
// contains only the information specific to managed operational staff.
public sealed class StaffMember
{
    public int Id { get; set; }
    public int EmployeeProfileId { get; set; }
    public EmployeeProfile EmployeeProfile { get; set; } = null!;
    [Required, MaxLength(40)] public string PrimaryRole { get; set; } = "";
    [MaxLength(30)] public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    [MaxLength(1000)] public string? Address { get; set; }
    [MaxLength(200)] public string? EmergencyContact { get; set; }
    [MaxLength(2000)] public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(450)] public string CreatedByUserId { get; set; } = "";
    public bool IsDeleted { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

// Supporting operational requests have a common review lifecycle. Core books,
// vehicles, rooms, payments and student records remain in their normalized tables.
public sealed class StaffWorkItem
{
    public long Id { get; set; }
    public int? CampusId { get; set; }
    public Campus? Campus { get; set; }
    [Required, MaxLength(40)] public string Workspace { get; set; } = "";
    [Required, MaxLength(80)] public string Category { get; set; } = "";
    [Required, MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(2000)] public string? Description { get; set; }
    public int? StudentId { get; set; }
    public Student? Student { get; set; }
    [MaxLength(100)] public string? Reference { get; set; }
    public DateTime? DueOn { get; set; }
    [Required, MaxLength(30)] public string Status { get; set; } = "Pending";
    [MaxLength(1000)] public string? ReviewComment { get; set; }
    [Required, MaxLength(450)] public string CreatedByUserId { get; set; } = "";
    [MaxLength(450)] public string? ReviewedByUserId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public sealed class StaffInput
{
    [Required, StringLength(160)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Phone, StringLength(30)] public string? Phone { get; set; }
    // Optional for older API clients; these values are assigned by the service.
    [StringLength(40)] public string? EmployeeId { get; set; }
    [StringLength(256)] public string? Username { get; set; }
    [Required, StringLength(40)] public string Role { get; set; } = "";
    public int? DepartmentId { get; set; }
    public DateTime JoiningDate { get; set; }
    [StringLength(30)] public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    [StringLength(1000)] public string? Address { get; set; }
    [StringLength(200)] public string? EmergencyContact { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    [StringLength(128, MinimumLength = 10)] public string? Password { get; set; }
    [StringLength(128), Compare(nameof(Password))] public string? ConfirmPassword { get; set; }
    [StringLength(100)] public string? RowVersion { get; set; }
}

public sealed class WorkItemInput
{
    [Required, StringLength(160)] public string Title { get; set; } = "";
    [StringLength(2000)] public string? Description { get; set; }
    [StringLength(100)] public string? Reference { get; set; }
    public int? StudentId { get; set; }
    public DateTime? DueOn { get; set; }
}

public sealed class ReviewWorkInput
{
    [Required, StringLength(30)] public string Status { get; set; } = "";
    [Required, StringLength(1000)] public string Comment { get; set; } = "";
    [Required, StringLength(100)] public string RowVersion { get; set; } = "";
}

public sealed class TransportDriver
{
    public int Id { get; set; }
    [Required, MaxLength(160)] public string FullName { get; set; } = "";
    [Required, MaxLength(30)] public string Phone { get; set; } = "";
    [Required, MaxLength(80)] public string LicenseNumber { get; set; } = "";
    public DateTime LicenseExpiry { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public sealed class StaffExpense
{
    public long Id { get; set; }
    public int? CampusId { get; set; }
    public Campus? Campus { get; set; }
    [Required, MaxLength(160)] public string Title { get; set; } = "";
    [Required, MaxLength(80)] public string Category { get; set; } = "";
    [MaxLength(160)] public string? Vendor { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18,2)")]
    [Range(0.01, 10000000)] public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    [MaxLength(100)] public string? Reference { get; set; }
    [Required, MaxLength(30)] public string Status { get; set; } = "Draft";
    [Required, MaxLength(450)] public string CreatedByUserId { get; set; } = "";
    [MaxLength(450)] public string? ApprovedByUserId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public sealed class StaffMessage
{
    public long Id { get; set; }
    [Required, MaxLength(450)] public string SenderUserId { get; set; } = "";
    public ApplicationUser SenderUser { get; set; } = null!;
    [Required, MaxLength(450)] public string RecipientUserId { get; set; } = "";
    public ApplicationUser RecipientUser { get; set; } = null!;
    [Required, MaxLength(160)] public string Subject { get; set; } = "";
    [Required, MaxLength(4000)] public string Body { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadUtc { get; set; }
}

public sealed class SendStaffMessageInput
{
    [Required, EmailAddress, StringLength(256)] public string RecipientEmail { get; set; } = "";
    [Required, StringLength(160)] public string Subject { get; set; } = "";
    [Required, StringLength(4000)] public string Body { get; set; } = "";
}
