using System.ComponentModel.DataAnnotations;

namespace Student_Management_System.Models;

public sealed class AdmissionDashboardViewModel
{
    public PagedResult<AdmissionApplication> Pagination { get; init; } = new();
    public IReadOnlyList<AdmissionApplication> Applications { get; init; } = [];
    public int Total { get; init; }
    public int New { get; init; }
    public int UnderReview { get; init; }
    public int DocumentsPending { get; init; }
    public int Approved { get; init; }
    public int Enrolled { get; init; }
}

public sealed class GuardianStudentViewModel
{
    public Student Student { get; init; } = null!;
    public IReadOnlyList<Attendance> Attendance { get; init; } = [];
    public IReadOnlyList<Mark> Marks { get; init; } = [];
    public IReadOnlyList<Assignment> Assignments { get; init; } = [];
    public IReadOnlyList<FinancialTransaction> Transactions { get; init; } = [];
    public IReadOnlyList<StudentInvoice> Invoices { get; init; } = [];
    public IReadOnlyList<LibraryLoan> LibraryLoans { get; init; } = [];
    public IReadOnlyList<Notice> Notices { get; init; } = [];
    public decimal OutstandingBalance { get; init; }
    public int OverdueLibraryLoans { get; init; }
    public decimal AttendancePercentage { get; init; }
}

public sealed class CollectPaymentViewModel
{
    [Required] public int StudentId { get; set; }
    [Required] public int FeeId { get; set; }
    [Range(0.01, 10000000)] public decimal Amount { get; set; }
    [Required, MaxLength(40)] public string PaymentMethod { get; set; } = "Cash";
    [MaxLength(100)] public string? TransactionReference { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");
}

public sealed class CreateApprovalRequestViewModel
{
    [System.ComponentModel.DataAnnotations.Required]
    public string WorkflowType { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required,
     System.ComponentModel.DataAnnotations.StringLength(100)]
    public string EntityType { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required,
     System.ComponentModel.DataAnnotations.StringLength(100)]
    public string EntityId { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required,
     System.ComponentModel.DataAnnotations.StringLength(2000)]
    public string Reason { get; set; } = "";
}

public sealed record SearchResultViewModel(
    string Group,
    string Title,
    string Subtitle,
    string Controller,
    string Action,
    object RouteId);
