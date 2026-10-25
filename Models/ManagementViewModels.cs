using System.ComponentModel.DataAnnotations;

namespace Student_Management_System.Models;

public class ListQuery
{
    public string? Search { get; set; }
    public string? Sort { get; set; }
    public string Direction { get; set; } = "asc";
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(10, 100)] public int PageSize { get; set; } = 20;
    public int? DepartmentId { get; set; }
    public int? ClassId { get; set; }
    public int? SectionId { get; set; }
    public int? SemesterId { get; set; }
    public int? AcademicYearId { get; set; }
    public int? SubjectId { get; set; }
    public int? ExamId { get; set; }
    public int? TeacherId { get; set; }
    [DataType(DataType.Date)] public DateTime? From { get; set; }
    [DataType(DataType.Date)] public DateTime? To { get; set; }
    public string? Status { get; set; }

    public void Normalize()
    {
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
        Sort = string.IsNullOrWhiteSpace(Sort) ? null : Sort.Trim();
        Direction = string.Equals(Direction, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
        Page = Math.Max(1, Page);
        PageSize = new[] { 10, 20, 50, 100 }.Contains(PageSize) ? PageSize : 20;
    }
}

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public string? Search { get; init; }
    public string? Sort { get; init; }
    public string Direction { get; init; } = "asc";
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public int FirstItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int LastItem => Math.Min(Page * PageSize, TotalCount);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public sealed record StatCardViewModel(
    string Label,
    string Value,
    string SupportingText,
    string Icon,
    string Tone = "",
    string? FilterName = null,
    string? FilterValue = null);

public sealed class ManagementHeaderViewModel
{
    public string Eyebrow { get; init; } = "Management";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string? PrimaryAction { get; init; }
    public string? PrimaryLabel { get; init; }
    public string? PrimaryController { get; init; }
    public string? ExportAction { get; init; }
    public string? ExportController { get; init; }
    public string? PeriodLabel { get; init; }
}

public sealed class StudentDetailsViewModel
{
    public PagedResult<Attendance> AttendancePage { get; init; } = new();
    public PagedResult<Mark> MarksPage { get; init; } = new();
    public int PresentCount { get; init; }
    public int AbsentCount { get; init; }
    public int LateCount { get; init; }
    public int ExcusedCount { get; init; }
    public Student Student { get; init; } = null!;
    public IReadOnlyList<StudentClassEnrollment> Enrollments { get; init; } = Array.Empty<StudentClassEnrollment>();
    public IReadOnlyList<StudentSubjectEnrollment> Subjects { get; init; } = Array.Empty<StudentSubjectEnrollment>();
    public IReadOnlyList<Attendance> Attendance { get; init; } = Array.Empty<Attendance>();
    public IReadOnlyList<Mark> Marks { get; init; } = Array.Empty<Mark>();
    public IReadOnlyList<Assignment> Assignments { get; init; } = Array.Empty<Assignment>();
    public IReadOnlyList<AssignmentSubmission> Submissions { get; init; } = Array.Empty<AssignmentSubmission>();
    public IReadOnlyList<TimetableEntry> Timetable { get; init; } = Array.Empty<TimetableEntry>();
    public IReadOnlyList<LoginHistory> LoginHistory { get; init; } = Array.Empty<LoginHistory>();
    public IReadOnlyList<GuardianStudentLink> Guardians { get; init; } = [];
    public IReadOnlyList<FinancialTransaction> FinanceTransactions { get; init; } = [];
    public IReadOnlyList<StudentInvoice> Invoices { get; init; } = [];
    public IReadOnlyList<LibraryLoan> LibraryLoans { get; init; } = [];
    public IReadOnlyList<LeaveRequest> LeaveRequests { get; init; } = [];
    public IReadOnlyList<AdmissionDocument> Documents { get; init; } = [];
    public IReadOnlyList<UserNotification> Notifications { get; init; } = [];
    public IReadOnlyList<AuditLog> ActivityHistory { get; init; } = [];
    public bool CanViewPersonal { get; init; }
    public bool CanViewFinance { get; init; }
    public bool CanViewSecurity { get; init; }
    public double AttendancePercentage { get; init; }
    public double AverageScore { get; init; }
}

public sealed class TeacherDetailsViewModel
{
    public Teacher Teacher { get; init; } = null!;
    public IReadOnlyList<Class> Classes { get; init; } = Array.Empty<Class>();
    public IReadOnlyList<Subject> Subjects { get; init; } = Array.Empty<Subject>();
    public IReadOnlyList<TimetableEntry> Timetable { get; init; } = Array.Empty<TimetableEntry>();
    public IReadOnlyList<StudyMaterial> Materials { get; init; } = Array.Empty<StudyMaterial>();
    public IReadOnlyList<Assignment> Assignments { get; init; } = Array.Empty<Assignment>();
    public IReadOnlyList<LoginHistory> LoginHistory { get; init; } = Array.Empty<LoginHistory>();
    public int StudentsTaught { get; init; }
    public int AttendanceEntries { get; init; }
    public int MarksEntered { get; init; }
}

public sealed class DepartmentDetailsViewModel
{
    public Department Department { get; init; } = null!;
    public IReadOnlyList<Teacher> Teachers { get; init; } = Array.Empty<Teacher>();
    public IReadOnlyList<Student> Students { get; init; } = Array.Empty<Student>();
    public IReadOnlyList<Class> Classes { get; init; } = Array.Empty<Class>();
    public IReadOnlyList<Subject> Subjects { get; init; } = Array.Empty<Subject>();
    public IReadOnlyList<Notice> Announcements { get; init; } = Array.Empty<Notice>();
    public IReadOnlyList<AuditLog> Activities { get; init; } = Array.Empty<AuditLog>();
    public int SectionCount { get; init; }
    public double AttendancePercentage { get; init; }
    public double AverageScore { get; init; }
}

public sealed class ClassDetailsViewModel
{
    public Class Class { get; init; } = null!;
    public IReadOnlyList<Student> Students { get; init; } = Array.Empty<Student>();
    public IReadOnlyList<Subject> Subjects { get; init; } = Array.Empty<Subject>();
    public IReadOnlyList<Section> Sections { get; init; } = Array.Empty<Section>();
    public IReadOnlyList<Teacher> Teachers { get; init; } = Array.Empty<Teacher>();
    public IReadOnlyList<TimetableEntry> Timetable { get; init; } = Array.Empty<TimetableEntry>();
    public IReadOnlyList<AuditLog> Activities { get; init; } = Array.Empty<AuditLog>();
    public double AttendancePercentage { get; init; }
    public double AverageScore { get; init; }
}

public sealed class SubjectDetailsViewModel
{
    public Subject Subject { get; init; } = null!;
    public IReadOnlyList<Teacher> Teachers { get; init; } = Array.Empty<Teacher>();
    public IReadOnlyList<Student> Students { get; init; } = Array.Empty<Student>();
    public IReadOnlyList<Attendance> Attendance { get; init; } = Array.Empty<Attendance>();
    public IReadOnlyList<Mark> Marks { get; init; } = Array.Empty<Mark>();
    public IReadOnlyList<Assignment> Assignments { get; init; } = Array.Empty<Assignment>();
    public IReadOnlyList<StudyMaterial> Materials { get; init; } = Array.Empty<StudyMaterial>();
    public IReadOnlyList<Exam> Exams { get; init; } = Array.Empty<Exam>();
    public double AttendancePercentage { get; init; }
    public double AverageScore { get; init; }
}

public sealed class ExamDetailsViewModel
{
    public Exam Exam { get; init; } = null!;
    public IReadOnlyList<Mark> Marks { get; init; } = Array.Empty<Mark>();
    public int Passed { get; init; }
    public int Failed { get; init; }
    public double AverageScore { get; init; }
}

public sealed class TranscriptViewModel
{
    public Student Student { get; init; } = null!;
    public IReadOnlyList<Mark> Marks { get; init; } = [];
    public DateTime GeneratedUtc { get; init; } = DateTime.UtcNow;
    public decimal WeightedAveragePercent { get; init; }
}

public sealed class AssignmentDetailsViewModel
{
    public Assignment Assignment { get; init; } = null!;
    public IReadOnlyList<AssignmentSubmission> Submissions { get; init; } = Array.Empty<AssignmentSubmission>();
    public int EligibleStudents { get; init; }
}

public sealed class SectionDetailsViewModel
{
    public Section Section { get; init; } = null!;
    public IReadOnlyList<Student> Students { get; init; } = Array.Empty<Student>();
    public IReadOnlyList<Subject> Subjects { get; init; } = Array.Empty<Subject>();
    public IReadOnlyList<TimetableEntry> Timetable { get; init; } = Array.Empty<TimetableEntry>();
    public double AttendancePercentage { get; init; }
    public double AverageScore { get; init; }
}

public sealed class AcademicYearDetailsViewModel
{
    public AcademicYear AcademicYear { get; init; } = null!;
    public IReadOnlyList<Semester> Semesters { get; init; } = Array.Empty<Semester>();
    public IReadOnlyList<StudentClassEnrollment> Enrollments { get; init; } = Array.Empty<StudentClassEnrollment>();
    public IReadOnlyList<Exam> Exams { get; init; } = Array.Empty<Exam>();
}

public sealed class SemesterDetailsViewModel
{
    public Semester Semester { get; init; } = null!;
    public IReadOnlyList<Class> Classes { get; init; } = Array.Empty<Class>();
    public IReadOnlyList<Student> Students { get; init; } = Array.Empty<Student>();
    public IReadOnlyList<Subject> Subjects { get; init; } = Array.Empty<Subject>();
}

public sealed class AdminEditInputModel
{
    [Required, MaxLength(450)] public string Id { get; set; } = "";
    [Required, MaxLength(160)] public string FullName { get; set; } = "";
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = "";
    [MaxLength(30)] public string? PhoneNumber { get; set; }
    [Required] public string ConcurrencyStamp { get; set; } = "";
}

public sealed class AdminDetailsViewModel
{
    public ApplicationUser Admin { get; init; } = null!;
    public IReadOnlyList<Permission> Permissions { get; init; } = Array.Empty<Permission>();
    public IReadOnlyList<AuditLog> Activities { get; init; } = Array.Empty<AuditLog>();
    public IReadOnlyList<LoginHistory> LoginHistory { get; init; } = Array.Empty<LoginHistory>();
}

public sealed record ArchivedRecordViewModel(
    string Entity,
    long Id,
    string Name,
    DateTime? ArchivedUtc,
    string? Reason);
