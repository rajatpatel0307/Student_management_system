using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using System.Security.Claims;

namespace Student_Management_System.Services;

public sealed record ReportDataset(
    string Title,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    string ScopeDescription);

public interface IReportingService
{
    Task<ReportDataset> BuildAsync(
        ReportCenterQuery query,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}

public sealed class ReportingService : IReportingService
{
    private readonly ApplicationDbContext _db;

    public ReportingService(ApplicationDbContext db) => _db = db;

    public async Task<ReportDataset> BuildAsync(
        ReportCenterQuery query,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        query.Normalize();
        EnsureReportAccess(query.ReportType, user);
        return query.ReportType switch
        {
            "Enrollment" => await EnrollmentAsync(query, user, cancellationToken),
            "Attendance" => await AttendanceAsync(query, user, cancellationToken),
            "Results" => await ResultsAsync(query, user, cancellationToken),
            "Finance" => await FinanceAsync(query, user, cancellationToken),
            "Admissions" => await AdmissionsAsync(query, user, cancellationToken),
            "Workload" => await WorkloadAsync(query, user, cancellationToken),
            "Library" => await LibraryAsync(query, user, cancellationToken),
            "LowAttendance" => await LowAttendanceAsync(query, user, cancellationToken),
            "AssignmentCompletion" => await AssignmentCompletionAsync(query, user, cancellationToken),
            "Leave" => await LeaveAsync(query, user, cancellationToken),
            "SeatUtilization" => await SeatUtilizationAsync(query, user, cancellationToken),
            "LoginHistory" => await LoginHistoryAsync(query, user, cancellationToken),
            "AuditActivity" => await AuditActivityAsync(query, user, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported report type.")
        };
    }

    private async Task<ReportDataset> EnrollmentAsync(
        ReportCenterQuery request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var query = _db.StudentClassEnrollments.AsNoTracking()
            .Include(e => e.Student).ThenInclude(s => s.Department)
            .Include(e => e.Class).Include(e => e.Section).Include(e => e.AcademicYear)
            .AsQueryable();
        query = await ScopeEnrollmentsAsync(query, user, cancellationToken);
        if (request.DepartmentId.HasValue)
            query = query.Where(e => e.Student.DepartmentId == request.DepartmentId);
        if (request.ClassId.HasValue) query = query.Where(e => e.ClassId == request.ClassId);
        if (request.SectionId.HasValue) query = query.Where(e => e.SectionId == request.SectionId);
        if (request.AcademicYearId.HasValue) query = query.Where(e => e.AcademicYearId == request.AcademicYearId);
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(e => e.Student.FullName.Contains(request.Search) ||
                                     e.Student.Email.Contains(request.Search));
        var data = await query.OrderBy(e => e.Student.FullName).Take(10000)
            .Select(e => new
            {
                e.Student.AdmissionNumber,
                e.Student.RollNumber,
                e.Student.FullName,
                e.Student.Email,
                Department = e.Student.Department != null ? e.Student.Department.Name : "",
                Class = e.Class.Name,
                Section = e.Section.Name,
                Year = e.AcademicYear.Name,
                e.IsActive
            }).ToListAsync(cancellationToken);
        return Dataset("Student enrollment",
            ["Admission number", "Roll number", "Student", "Email", "Department", "Class", "Section", "Academic year", "Status"],
            data.Select(x => (IReadOnlyList<string>)
                [x.AdmissionNumber ?? "", x.RollNumber ?? "", x.FullName, x.Email, x.Department,
                 x.Class, x.Section, x.Year, x.IsActive ? "Active" : "Historical"]),
            user.IsInRole("HOD") ? "Authorized department" : "Authorized institution records");
    }

    private async Task<ReportDataset> AttendanceAsync(
        ReportCenterQuery request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var query = _db.Attendances.AsNoTracking()
            .Include(a => a.Student).ThenInclude(s => s.Department)
            .Include(a => a.Subject).Include(a => a.Teacher).AsQueryable();
        query = await ScopeAttendanceAsync(query, user, cancellationToken);
        if (request.DepartmentId.HasValue)
            query = query.Where(a => a.Student.DepartmentId == request.DepartmentId);
        if (request.ClassId.HasValue) query = query.Where(a => a.Student.ClassId == request.ClassId);
        if (request.SubjectId.HasValue) query = query.Where(a => a.SubjectId == request.SubjectId);
        if (request.From.HasValue) query = query.Where(a => a.Date >= request.From.Value.Date);
        if (request.To.HasValue) query = query.Where(a => a.Date <= request.To.Value.Date);
        var data = await query.OrderByDescending(a => a.Date).Take(50000)
            .Select(a => new
            {
                a.Date, Student = a.Student.FullName,
                Department = a.Student.Department != null ? a.Student.Department.Name : "",
                Subject = a.Subject != null ? a.Subject.Name : "",
                Teacher = a.Teacher != null ? a.Teacher.FullName : "",
                a.Lecture, a.Status
            }).ToListAsync(cancellationToken);
        return Dataset("Attendance records",
            ["Date", "Student", "Department", "Subject", "Teacher", "Period", "Status"],
            data.Select(x => (IReadOnlyList<string>)
                [x.Date.ToString("yyyy-MM-dd"), x.Student, x.Department, x.Subject,
                 x.Teacher, x.Lecture ?? "", x.Status]),
            user.IsInRole("HOD") ? "Authorized department" :
            user.IsInRole("Professor") ? "Assigned teaching records" : "Authorized institution records");
    }

    private async Task<ReportDataset> ResultsAsync(
        ReportCenterQuery request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var query = _db.Marks.AsNoTracking()
            .Include(m => m.Student).ThenInclude(s => s.Department)
            .Include(m => m.Subject).Include(m => m.Exam)
            .Where(m => m.IsApproved && m.Exam.Status == ExamLifecycle.Published);
        if (user.IsInRole("HOD"))
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            query = query.Where(m => m.Student.Department!.HodTeacher!.UserId == userId);
        }
        else if (user.IsInRole("Professor"))
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            query = query.Where(m => m.Subject.TeacherAssignments.Any(a => a.Teacher.UserId == userId));
        }
        if (request.DepartmentId.HasValue)
            query = query.Where(m => m.Student.DepartmentId == request.DepartmentId);
        if (request.SubjectId.HasValue) query = query.Where(m => m.SubjectId == request.SubjectId);
        if (request.ExamId.HasValue) query = query.Where(m => m.ExamId == request.ExamId);
        var data = await query.OrderBy(m => m.Student.FullName).Take(50000)
            .Select(m => new
            {
                Student = m.Student.FullName,
                Department = m.Student.Department != null ? m.Student.Department.Name : "",
                Subject = m.Subject.Name,
                Exam = m.Exam.Name,
                m.Score, m.Exam.MaximumMarks, m.Grade, m.ResultStatus
            }).ToListAsync(cancellationToken);
        return Dataset("Published examination results",
            ["Student", "Department", "Subject", "Exam", "Score", "Maximum", "Grade", "Status"],
            data.Select(x => (IReadOnlyList<string>)
                [x.Student, x.Department, x.Subject, x.Exam, x.Score.ToString("0.##"),
                 x.MaximumMarks.ToString("0.##"), x.Grade, x.ResultStatus]),
            user.IsInRole("Professor") ? "Assigned subjects; published results only" :
            user.IsInRole("HOD") ? "Authorized department; published results only" :
            "Authorized published results");
    }

    private async Task<ReportDataset> FinanceAsync(
        ReportCenterQuery request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!user.IsInRole("SuperAdmin") && !user.IsInRole("Admin") && !user.IsInRole("Accountant"))
            throw new UnauthorizedAccessException();
        var query = _db.FinancialTransactions.AsNoTracking()
            .Include(t => t.Student).ThenInclude(s => s.Department)
            .Include(t => t.Fee).AsQueryable();
        if (request.DepartmentId.HasValue)
            query = query.Where(t => t.Student.DepartmentId == request.DepartmentId);
        if (request.From.HasValue) query = query.Where(t => t.CreatedUtc >= request.From.Value.Date);
        if (request.To.HasValue) query = query.Where(t => t.CreatedUtc < request.To.Value.Date.AddDays(1));
        var data = await query.OrderByDescending(t => t.CreatedUtc).Take(50000)
            .Select(t => new
            {
                t.ReceiptNumber, Student = t.Student.FullName,
                Department = t.Student.Department != null ? t.Student.Department.Name : "",
                Fee = t.Fee.Term, t.Type, t.Amount, t.PaymentMethod,
                t.TransactionReference, t.CreatedUtc
            }).ToListAsync(cancellationToken);
        return Dataset("Immutable finance ledger",
            ["Receipt", "Student", "Department", "Fee", "Type", "Amount", "Method", "Reference", "Created UTC"],
            data.Select(x => (IReadOnlyList<string>)
                [x.ReceiptNumber, x.Student, x.Department, x.Fee, x.Type.ToString(),
                 x.Amount.ToString("0.00"), x.PaymentMethod, x.TransactionReference ?? "",
                 x.CreatedUtc.ToString("O")]),
            "Authorized immutable ledger entries");
    }

    private async Task<ReportDataset> AdmissionsAsync(
        ReportCenterQuery request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!user.IsInRole("SuperAdmin") && !user.IsInRole("Admin") &&
            !user.IsInRole("AdmissionOfficer"))
            throw new UnauthorizedAccessException();
        var query = _db.AdmissionApplications.AsNoTracking()
            .Include(a => a.Department).Include(a => a.AcademicYear).AsQueryable();
        if (request.DepartmentId.HasValue) query = query.Where(a => a.DepartmentId == request.DepartmentId);
        if (request.AcademicYearId.HasValue) query = query.Where(a => a.AcademicYearId == request.AcademicYearId);
        if (request.From.HasValue) query = query.Where(a => a.CreatedUtc >= request.From.Value.Date);
        if (request.To.HasValue) query = query.Where(a => a.CreatedUtc < request.To.Value.Date.AddDays(1));
        var data = await query.OrderByDescending(a => a.CreatedUtc).Take(20000)
            .Select(a => new
            {
                a.ReferenceNumber, a.ApplicantName, a.ProgramName,
                Department = a.Department.Name, Year = a.AcademicYear.Name,
                a.Source, a.Stage, a.CreatedUtc
            }).ToListAsync(cancellationToken);
        return Dataset("Admission conversion",
            ["Reference", "Applicant", "Program", "Department", "Academic year", "Source", "Stage", "Created UTC"],
            data.Select(x => (IReadOnlyList<string>)
                [x.ReferenceNumber, x.ApplicantName, x.ProgramName, x.Department,
                 x.Year, x.Source, x.Stage.ToString(), x.CreatedUtc.ToString("O")]),
            "Authorized admissions records; contact data excluded");
    }

    private async Task<ReportDataset> WorkloadAsync(
        ReportCenterQuery request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var query = _db.Teachers.AsNoTracking().Where(t => !t.IsArchived);
        if (user.IsInRole("HOD"))
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            query = query.Where(t => t.Department!.HodTeacher!.UserId == userId);
        }
        if (request.DepartmentId.HasValue) query = query.Where(t => t.DepartmentId == request.DepartmentId);
        var data = await query.OrderBy(t => t.FullName)
            .Select(t => new
            {
                t.FullName,
                Department = t.Department != null ? t.Department.Name : "",
                t.TeacherType,
                Classes = t.ClassAssignments.Count,
                Subjects = t.SubjectAssignments.Count,
                Periods = _db.TimetableEntries.Count(e => e.TeacherId == t.Id)
            }).ToListAsync(cancellationToken);
        return Dataset("Professor workload",
            ["Professor", "Department", "Type", "Classes", "Subjects", "Weekly periods"],
            data.Select(x => (IReadOnlyList<string>)
                [x.FullName, x.Department, x.TeacherType.ToString(),
                 x.Classes.ToString(), x.Subjects.ToString(), x.Periods.ToString()]),
            user.IsInRole("HOD") ? "Authorized department" : "Authorized faculty records");
    }

    private async Task<ReportDataset> LibraryAsync(
        ReportCenterQuery request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!user.IsInRole("SuperAdmin") && !user.IsInRole("Admin") &&
            !user.IsInRole("Librarian"))
            throw new UnauthorizedAccessException();
        var query = _db.LibraryLoans.AsNoTracking()
            .Include(l => l.LibraryBookCopy).ThenInclude(c => c.LibraryBook)
            .Include(l => l.BorrowerUser).AsQueryable();
        if (request.From.HasValue) query = query.Where(l => l.IssuedUtc >= request.From.Value.Date);
        if (request.To.HasValue) query = query.Where(l => l.IssuedUtc < request.To.Value.Date.AddDays(1));
        var data = await query.OrderByDescending(l => l.IssuedUtc).Take(50000)
            .Select(l => new
            {
                l.LibraryBookCopy.Barcode,
                Book = l.LibraryBookCopy.LibraryBook.Title,
                Borrower = l.BorrowerUser.Email ?? l.BorrowerUser.UserName ?? "",
                l.IssuedUtc, l.DueUtc, l.ReturnedUtc, l.FineAmount
            }).ToListAsync(cancellationToken);
        return Dataset("Library circulation",
            ["Barcode", "Book", "Borrower", "Issued UTC", "Due UTC", "Returned UTC", "Fine"],
            data.Select(x => (IReadOnlyList<string>)
                [x.Barcode, x.Book, x.Borrower, x.IssuedUtc.ToString("O"),
                 x.DueUtc.ToString("O"), x.ReturnedUtc?.ToString("O") ?? "",
                 x.FineAmount.ToString("0.00")]),
            "Authorized circulation records");
    }

    private async Task<ReportDataset> LowAttendanceAsync(
        ReportCenterQuery request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var query = _db.Attendances.AsNoTracking()
            .Include(a => a.Student).ThenInclude(s => s.Department)
            .AsQueryable();
        query = await ScopeAttendanceAsync(query, user, cancellationToken);
        if (request.DepartmentId.HasValue)
            query = query.Where(a => a.Student.DepartmentId == request.DepartmentId);
        if (request.ClassId.HasValue)
            query = query.Where(a => a.Student.ClassId == request.ClassId);
        if (request.From.HasValue) query = query.Where(a => a.Date >= request.From.Value.Date);
        if (request.To.HasValue) query = query.Where(a => a.Date <= request.To.Value.Date);
        var data = await query.GroupBy(a => new
            {
                a.StudentId, a.Student.FullName,
                Department = a.Student.Department != null
                    ? a.Student.Department.Name : ""
            })
            .Select(g => new
            {
                g.Key.StudentId, g.Key.FullName, g.Key.Department,
                Sessions = g.Count(),
                Present = g.Count(a => a.Status == "Present" ||
                                       a.Status == "Late" ||
                                       a.Status == "Excused")
            })
            .Where(x => x.Sessions > 0 && x.Present * 100m / x.Sessions < 75m)
            .OrderBy(x => x.FullName).Take(10000)
            .ToListAsync(cancellationToken);
        return Dataset("Low attendance",
            ["Student", "Department", "Attended", "Sessions", "Percentage"],
            data.Select(x => (IReadOnlyList<string>)
                [x.FullName, x.Department, x.Present.ToString(),
                 x.Sessions.ToString(), (x.Present * 100m / x.Sessions).ToString("0.0")]),
            "Authorized students below the configured review threshold");
    }

    private async Task<ReportDataset> AssignmentCompletionAsync(
        ReportCenterQuery request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = _db.Assignments.AsNoTracking()
            .Include(a => a.Subject).ThenInclude(s => s!.Class)
            .AsQueryable();
        if (user.IsInRole("Professor"))
            query = query.Where(a =>
                a.Subject!.TeacherAssignments.Any(t => t.Teacher.UserId == userId));
        if (user.IsInRole("HOD"))
            query = query.Where(a =>
                a.Subject!.Department!.HodTeacher!.UserId == userId);
        if (request.ClassId.HasValue)
            query = query.Where(a => a.Subject!.ClassId == request.ClassId);
        if (request.SubjectId.HasValue)
            query = query.Where(a => a.SubjectId == request.SubjectId);
        var data = await query.OrderByDescending(a => a.DueUtc).Take(10000)
            .Select(a => new
            {
                a.Title, Subject = a.Subject!.Name,
                Class = a.Subject.Class.Name, a.DueUtc,
                Eligible = a.Subject.Class.Students.Count +
                    a.Subject.StudentEnrollments.Count,
                Submitted = a.Submissions.Count,
                Evaluated = a.Submissions.Count(s => s.Score.HasValue)
            }).ToListAsync(cancellationToken);
        return Dataset("Assignment completion",
            ["Assignment", "Subject", "Class", "Due UTC", "Eligible", "Submitted", "Evaluated"],
            data.Select(x => (IReadOnlyList<string>)
                [x.Title, x.Subject, x.Class, x.DueUtc.ToString("O"),
                 x.Eligible.ToString(), x.Submitted.ToString(), x.Evaluated.ToString()]),
            user.IsInRole("Professor") ? "Assigned subjects" :
            user.IsInRole("HOD") ? "Authorized department" : "Authorized records");
    }

    private async Task<ReportDataset> LeaveAsync(
        ReportCenterQuery request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var query = _db.LeaveRequests.AsNoTracking().AsQueryable();
        if (user.IsInRole("HOD"))
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var departmentId = await _db.Teachers.AsNoTracking()
                .Where(t => t.UserId == userId && t.TeacherType == TeacherType.HOD)
                .Select(t => t.DepartmentId)
                .SingleOrDefaultAsync(cancellationToken);
            if (!departmentId.HasValue)
                query = query.Where(_ => false);
            else
                query = query.Where(l =>
                    _db.Students.Any(s => s.UserId == l.RequesterUserId &&
                                          s.DepartmentId == departmentId) ||
                    _db.Teachers.Any(t => t.UserId == l.RequesterUserId &&
                                          t.DepartmentId == departmentId));
        }
        else if (!user.IsInRole("SuperAdmin") && !user.IsInRole("Admin"))
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            query = query.Where(l => l.RequesterUserId == userId);
        }
        if (request.From.HasValue) query = query.Where(l => l.EndsOn >= request.From.Value.Date);
        if (request.To.HasValue) query = query.Where(l => l.StartsOn <= request.To.Value.Date);
        var data = await query.OrderByDescending(l => l.CreatedUtc).Take(20000)
            .Select(l => new
            {
                Requester = _db.Users.Where(u => u.Id == l.RequesterUserId)
                    .Select(u => u.FullName).FirstOrDefault() ?? l.RequesterUserId,
                l.StartsOn, l.EndsOn,
                l.Reason, l.Status, l.CreatedUtc
            }).ToListAsync(cancellationToken);
        return Dataset("Leave requests",
            ["Requester", "Starts", "Ends", "Reason", "Status", "Created UTC"],
            data.Select(x => (IReadOnlyList<string>)
                [x.Requester, x.StartsOn.ToString("yyyy-MM-dd"),
                 x.EndsOn.ToString("yyyy-MM-dd"), x.Reason,
                 x.Status.ToString(), x.CreatedUtc.ToString("O")]),
            "Authorized leave records");
    }

    private async Task<ReportDataset> SeatUtilizationAsync(
        ReportCenterQuery request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!user.IsInRole("SuperAdmin") && !user.IsInRole("Admin") &&
            !user.IsInRole("AdmissionOfficer"))
            throw new UnauthorizedAccessException();
        var query = _db.AcademicIntakes.AsNoTracking()
            .Include(i => i.AcademicProgram).Include(i => i.AcademicYear)
            .AsQueryable();
        if (request.AcademicYearId.HasValue)
            query = query.Where(i => i.AcademicYearId == request.AcademicYearId);
        var data = await query.OrderByDescending(i => i.AcademicYear.StartsOn)
            .Select(i => new
            {
                Program = i.AcademicProgram.Name, Year = i.AcademicYear.Name,
                i.SeatCapacity, i.ReservedSeats,
                Enrolled = _db.AdmissionApplications.Count(a =>
                    a.AcademicIntakeId == i.Id && a.Stage == AdmissionStage.Enrolled)
            }).ToListAsync(cancellationToken);
        return Dataset("Seat utilization",
            ["Program", "Academic year", "Capacity", "Reserved", "Enrolled", "Available"],
            data.Select(x => (IReadOnlyList<string>)
                [x.Program, x.Year, x.SeatCapacity.ToString(),
                 x.ReservedSeats.ToString(), x.Enrolled.ToString(),
                 Math.Max(0, x.SeatCapacity - x.ReservedSeats - x.Enrolled).ToString()]),
            "Authorized intake capacity");
    }

    private async Task<ReportDataset> LoginHistoryAsync(
        ReportCenterQuery request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!user.IsInRole("SuperAdmin") && !user.IsInRole("Admin"))
            throw new UnauthorizedAccessException();
        var query = _db.LoginHistories.AsNoTracking().AsQueryable();
        if (request.From.HasValue) query = query.Where(l => l.CreatedUtc >= request.From.Value.Date);
        if (request.To.HasValue) query = query.Where(l => l.CreatedUtc < request.To.Value.Date.AddDays(1));
        var data = await query.OrderByDescending(l => l.CreatedUtc).Take(50000)
            .Select(l => new { l.Email, l.CreatedUtc, l.IpAddress, l.Succeeded })
            .ToListAsync(cancellationToken);
        return Dataset("Login history",
            ["Account", "Timestamp UTC", "IP address", "Outcome"],
            data.Select(x => (IReadOnlyList<string>)
                [x.Email, x.CreatedUtc.ToString("O"), x.IpAddress ?? "",
                 x.Succeeded ? "Success" : "Failed"]),
            "Privileged security records");
    }

    private async Task<ReportDataset> AuditActivityAsync(
        ReportCenterQuery request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!user.IsInRole("SuperAdmin") && !user.IsInRole("Admin"))
            throw new UnauthorizedAccessException();
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (request.From.HasValue) query = query.Where(a => a.CreatedUtc >= request.From.Value.Date);
        if (request.To.HasValue) query = query.Where(a => a.CreatedUtc < request.To.Value.Date.AddDays(1));
        var data = await query.OrderByDescending(a => a.CreatedUtc).Take(50000)
            .Select(a => new
            {
                a.CreatedUtc, a.UserId, a.Action, a.EntityName, a.EntityId
            }).ToListAsync(cancellationToken);
        return Dataset("Audit activity",
            ["Timestamp UTC", "Actor", "Action", "Entity", "Entity ID"],
            data.Select(x => (IReadOnlyList<string>)
                [x.CreatedUtc.ToString("O"), x.UserId ?? "", x.Action,
                 x.EntityName ?? "", x.EntityId ?? ""]),
            "Privileged immutable audit records");
    }

    private async Task<IQueryable<StudentClassEnrollment>> ScopeEnrollmentsAsync(
        IQueryable<StudentClassEnrollment> query,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!user.IsInRole("HOD")) return query;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var departmentId = await _db.Teachers.AsNoTracking()
            .Where(t => t.UserId == userId && t.TeacherType == TeacherType.HOD)
            .Select(t => t.DepartmentId).SingleOrDefaultAsync(cancellationToken);
        return departmentId.HasValue
            ? query.Where(e => e.Student.DepartmentId == departmentId)
            : query.Where(_ => false);
    }

    private async Task<IQueryable<Attendance>> ScopeAttendanceAsync(
        IQueryable<Attendance> query,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (user.IsInRole("Professor"))
            return query.Where(a => a.Teacher!.UserId == userId ||
                                    a.Subject!.TeacherAssignments.Any(x => x.Teacher.UserId == userId));
        if (user.IsInRole("HOD"))
        {
            var departmentId = await _db.Teachers.AsNoTracking()
                .Where(t => t.UserId == userId && t.TeacherType == TeacherType.HOD)
                .Select(t => t.DepartmentId).SingleOrDefaultAsync(cancellationToken);
            return departmentId.HasValue
                ? query.Where(a => a.Student.DepartmentId == departmentId)
                : query.Where(_ => false);
        }
        return query;
    }

    private static ReportDataset Dataset(
        string title,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows,
        string scope) =>
            new(title, headers, rows.ToList(), scope);

    private static void EnsureReportAccess(string reportType, ClaimsPrincipal user)
    {
        if (user.IsInRole("SuperAdmin") || user.IsInRole("Admin")) return;
        var permitted = reportType switch
        {
            "Enrollment" or "Workload" => user.IsInRole("HOD"),
            "Attendance" or "LowAttendance" or "Results" or
            "AssignmentCompletion" or "Leave" =>
                user.IsInRole("HOD") || user.IsInRole("Professor"),
            "Finance" => user.IsInRole("Accountant"),
            "Admissions" or "SeatUtilization" => user.IsInRole("AdmissionOfficer"),
            "Library" => user.IsInRole("Librarian"),
            "LoginHistory" or "AuditActivity" => false,
            _ => false
        };
        if (!permitted) throw new UnauthorizedAccessException();
    }
}
