using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Security;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;

namespace Student_Management_System.Services;

public interface IDashboardAnalyticsService
{
    Task<RoleDashboardViewModel> GetAsync(
        ClaimsPrincipal user,
        string dashboardRole,
        DashboardFilterInput filter,
        CancellationToken cancellationToken = default);
}

public sealed class DashboardAnalyticsService(
    ApplicationDbContext db,
    ICampusScopeService campusScope,
    PermissionService permissions,
    ILogger<DashboardAnalyticsService> logger) : IDashboardAnalyticsService
{
    private readonly ApplicationDbContext _db = db;
    private readonly ICampusScopeService _campusScope = campusScope;
    private readonly PermissionService _permissions = permissions;
    private readonly ILogger<DashboardAnalyticsService> _logger = logger;

    public async Task<RoleDashboardViewModel> GetAsync(
        ClaimsPrincipal user,
        string dashboardRole,
        DashboardFilterInput filter,
        CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
        var range = DashboardMath.ResolveRange(filter, DateTime.UtcNow);
        if (!await _permissions.HasAsync(user, PermissionKeys.DashboardView, cancellationToken))
            throw new UnauthorizedAccessException();
        var campusIds = await ResolveCampusScopeAsync(user, filter.CampusId, cancellationToken);
        try
        {
            var role = dashboardRole.Trim();
            var result = role switch
            {
                "SuperAdmin" when user.IsInRole("SuperAdmin") =>
                    await ExecutiveAsync(user, filter, range, campusIds, cancellationToken),
                "Admin" when user.IsInRole("Admin") =>
                    await AdminAsync(user, filter, range, campusIds, cancellationToken),
                "HOD" when user.IsInRole("HOD") =>
                    await HodAsync(userId, filter, range, campusIds, cancellationToken),
                "Professor" when user.IsInRole("Professor") =>
                    await ProfessorAsync(userId, filter, range, campusIds, cancellationToken),
                "Student" when user.IsInRole("Student") =>
                    await StudentAsync(userId, filter, range, campusIds, cancellationToken),
                "Accountant" when user.IsInRole("Accountant") =>
                    await FinanceAsync(user, filter, range, campusIds, cancellationToken),
                "AdmissionOfficer" when user.IsInRole("AdmissionOfficer") =>
                    await AdmissionsAsync(user, filter, range, campusIds, cancellationToken),
                "Librarian" when user.IsInRole("Librarian") =>
                    await LibraryAsync(user, filter, range, campusIds, cancellationToken),
                "ExaminationOfficer" when user.IsInRole("ExaminationOfficer") =>
                    await ExaminationAsync(user, filter, range, campusIds, cancellationToken),
                "Operations" when user.IsInRole("TransportManager") ||
                                  user.IsInRole("HostelManager") ||
                                  user.IsInRole("PlacementOfficer") ||
                                  user.IsInRole("Employee") =>
                    await OperationsAsync(userId, filter, range, campusIds, cancellationToken),
                _ => throw new UnauthorizedAccessException()
            };
            return result;
        }
        finally
        {
            watch.Stop();
            _logger.LogInformation(
                "Dashboard analytics completed for role {Role} in {ElapsedMilliseconds} ms over {RangeDays} days.",
                dashboardRole, watch.ElapsedMilliseconds, range.Days);
        }
    }

    private async Task<RoleDashboardViewModel> ExecutiveAsync(
        ClaimsPrincipal user,
        DashboardFilterInput filter,
        DashboardDateRange range,
        int[] campusIds,
        CancellationToken token)
    {
        if (!await _permissions.HasAsync(user, "reports.view", token))
            throw new UnauthorizedAccessException();
        var studentQuery = ScopedStudents(campusIds, filter).Where(s => s.User != null && s.User.IsActive);
        var teacherQuery = ScopedTeachers(campusIds, filter).Where(t => t.User.IsActive);
        var invoiceQuery = ScopedInvoices(campusIds, filter)
            .Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft);
        var transactionQuery = ScopedTransactions(campusIds, filter)
            .Where(t => t.Status == FinancialTransactionStatus.Completed &&
                        t.CreatedUtc >= range.FromUtc && t.CreatedUtc < range.ToExclusiveUtc);
        var attendanceQuery = ScopedAttendance(campusIds, filter)
            .Where(a => a.Date >= range.FromUtc && a.Date < range.ToExclusiveUtc);
        var attendanceTotals = await attendanceQuery.GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Present = g.Count(a => a.Status == "Present" || a.Status == "Late" || a.Status == "Excused") })
            .SingleOrDefaultAsync(token);
        var collections = await SignedCollectionAsync(transactionQuery, token);
        var outstanding = await OutstandingAsync(invoiceQuery, token);
        var previousAdmissions = await ScopedAdmissions(campusIds, filter)
            .CountAsync(a => a.CreatedUtc >= range.PreviousFromUtc && a.CreatedUtc < range.PreviousToExclusiveUtc, token);
        var admissions = await ScopedAdmissions(campusIds, filter)
            .CountAsync(a => a.CreatedUtc >= range.FromUtc && a.CreatedUtc < range.ToExclusiveUtc, token);
        var failedLogins = await _db.LoginHistories.AsNoTracking()
            .CountAsync(l => !l.Succeeded && l.CreatedUtc >= range.FromUtc && l.CreatedUtc < range.ToExclusiveUtc, token);
        var campusComparison = await BuildCampusComparisonAsync(campusIds, token);
        var enrollment = await BuildAdmissionsTrendAsync(campusIds, filter, range, token);
        var finance = await BuildFinanceTrendAsync(campusIds, filter, range, token);
        var attendance = await BuildAttendanceTrendAsync(campusIds, filter, range, token);
        var funnel = await BuildAdmissionsFunnelAsync(campusIds, filter, range, token);
        var campuses = await CampusOptionsAsync(campusIds, token);
        var departments = await DepartmentOptionsAsync(campusIds, token);
        return new RoleDashboardViewModel
        {
            Role = "SuperAdmin", Eyebrow = "Institution intelligence",
            Title = "Executive dashboard",
            Description = "Institution-wide academic, financial, operational and security signals from authoritative records.",
            ScopeDescription = "All explicitly authorized active campuses",
            RefreshedUtc = DateTime.UtcNow, Filter = filter, Range = range,
            Campuses = campuses, Departments = departments,
            Kpis =
            [
                Kpi("Active students", await studentQuery.CountAsync(token), "fa-user-graduate", "info", "Active student identities in scope", "/Students"),
                Kpi("Active professors & staff", await teacherQuery.CountAsync(token), "fa-chalkboard-user", "success", "Active academic staff in scope", "/Teachers"),
                Kpi("Active campuses", campusIds.Length, "fa-building", "primary", "Campuses included in this view"),
                Kpi("Departments", await _db.Departments.AsNoTracking().CountAsync(d => !d.IsArchived, token), "fa-building-columns", "primary", "Active academic departments"),
                Kpi("Admissions", admissions, "fa-user-check", "info", range.Label, "/Admissions", previousAdmissions),
                MoneyKpi("Net collections", collections, "fa-rupee-sign", "success", "Completed payments less refunds and reversals", "/Finance"),
                MoneyKpi("Outstanding fees", outstanding, "fa-file-invoice-dollar", "warning", "Issued invoice balance after aid and allocations", "/Finance/Invoices"),
                PercentKpi("Attendance rate", attendanceTotals == null ? 0 : DashboardMath.Percentage(attendanceTotals.Present, attendanceTotals.Total), "fa-calendar-check", "success", "Present, late and excused attendance in period"),
                Kpi("Pending approvals", await _db.ApprovalRequests.AsNoTracking().CountAsync(a => a.Status == ApprovalStatus.Pending, token), "fa-list-check", "warning", "Institution workflows awaiting action", "/Approvals"),
                Kpi("Security alerts", failedLogins, "fa-shield-halved", "danger", "Failed sign-ins in selected period", "/SuperAdmin/LoginHistory")
            ],
            Charts = [enrollment, campusComparison, finance, attendance, funnel],
            Alerts = await SystemAlertsAsync(range, token),
            PrimaryActionUrl = "/Reports", PrimaryActionLabel = "Open report center"
        };
    }

    private async Task<RoleDashboardViewModel> AdminAsync(
        ClaimsPrincipal user,
        DashboardFilterInput filter,
        DashboardDateRange range,
        int[] campusIds,
        CancellationToken token)
    {
        if (campusIds.Length == 0) throw new UnauthorizedAccessException();
        var students = ScopedStudents(campusIds, filter).Where(s => s.User != null && s.User.IsActive);
        var staff = ScopedTeachers(campusIds, filter).Where(t => t.User.IsActive);
        var attendanceQuery = ScopedAttendance(campusIds, filter)
            .Where(a => a.Date >= range.FromUtc && a.Date < range.ToExclusiveUtc);
        var attendanceTotals = await attendanceQuery.GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Present = g.Count(a => a.Status == "Present" || a.Status == "Late" || a.Status == "Excused") })
            .SingleOrDefaultAsync(token);
        var collections = await SignedCollectionAsync(ScopedTransactions(campusIds, filter)
            .Where(t => t.Status == FinancialTransactionStatus.Completed && t.CreatedUtc >= range.FromUtc && t.CreatedUtc < range.ToExclusiveUtc), token);
        var outstanding = await OutstandingAsync(ScopedInvoices(campusIds, filter)
            .Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft), token);
        DashboardChart[] charts =
        [
            await BuildAdmissionsTrendAsync(campusIds, filter, range, token),
            await BuildAttendanceTrendAsync(campusIds, filter, range, token),
            await BuildFinanceTrendAsync(campusIds, filter, range, token),
            await BuildDepartmentPerformanceAsync(campusIds, filter, token),
            await BuildProfessorWorkloadAsync(campusIds, filter.DepartmentId, token)
        ];
        return new RoleDashboardViewModel
        {
            Role = "Admin", Eyebrow = "Campus administration", Title = "Campus dashboard",
            Description = "Operational, academic and financial performance for your assigned campuses only.",
            ScopeDescription = await CampusScopeLabelAsync(campusIds, token), Filter = filter, Range = range,
            RefreshedUtc = DateTime.UtcNow,
            Campuses = await CampusOptionsAsync(campusIds, token),
            Departments = await DepartmentOptionsAsync(campusIds, token),
            Classes = await ClassOptionsAsync(campusIds, filter.DepartmentId, token),
            Kpis =
            [
                Kpi("Active students", await students.CountAsync(token), "fa-user-graduate", "info", "Students with active identities", "/Students"),
                Kpi("Active staff", await staff.CountAsync(token), "fa-users", "success", "Academic staff in assigned campuses", "/Teachers"),
                Kpi("Classes", await ScopedClasses(campusIds, filter).CountAsync(token), "fa-school", "primary", "Classes represented in assigned campuses", "/Classes"),
                PercentKpi("Attendance", attendanceTotals == null ? 0 : DashboardMath.Percentage(attendanceTotals.Present, attendanceTotals.Total), "fa-calendar-check", "success", range.Label, "/Attendance"),
                MoneyKpi("Collections", collections, "fa-rupee-sign", "success", range.Label, "/Finance"),
                MoneyKpi("Outstanding", outstanding, "fa-file-invoice-dollar", "warning", "Open issued invoices", "/Finance/Invoices"),
                Kpi("Admissions in review", await ScopedAdmissions(campusIds, filter).CountAsync(a => a.Stage == AdmissionStage.Submitted || a.Stage == AdmissionStage.UnderReview, token), "fa-list-check", "warning", "Campus-scoped applications awaiting review", "/Admissions"),
                Kpi("Open service requests", await ScopedServiceTickets(campusIds).CountAsync(t => t.Status != "Resolved" && t.Status != "Closed", token), "fa-screwdriver-wrench", "danger", "Unresolved campus requests", "/Operations")
            ],
            Charts = charts,
            Alerts = await CampusAlertsAsync(campusIds, token),
            PrimaryActionUrl = "/Reports", PrimaryActionLabel = "Campus reports"
        };
    }

    private async Task<RoleDashboardViewModel> HodAsync(
        string userId,
        DashboardFilterInput filter,
        DashboardDateRange range,
        int[] campusIds,
        CancellationToken token)
    {
        var hod = await _db.Teachers.AsNoTracking()
            .Where(t => t.UserId == userId && t.TeacherType == TeacherType.HOD && !t.IsArchived && t.User.IsActive)
            .Select(t => new { t.Id, t.DepartmentId, DepartmentName = t.Department != null ? t.Department.Name : "Unassigned" })
            .SingleOrDefaultAsync(token);
        if (hod?.DepartmentId == null || filter.DepartmentId.HasValue && filter.DepartmentId != hod.DepartmentId)
            throw new UnauthorizedAccessException();
        filter.DepartmentId = hod.DepartmentId;
        var students = ScopedStudents(campusIds, filter);
        var professors = ScopedTeachers(campusIds, filter).Where(t => t.TeacherType == TeacherType.Professor);
        var attendanceQuery = ScopedAttendance(campusIds, filter)
            .Where(a => a.Date >= range.FromUtc && a.Date < range.ToExclusiveUtc);
        var totals = await attendanceQuery.GroupBy(_ => 1).Select(g => new
        {
            Total = g.Count(), Present = g.Count(a => a.Status == "Present" || a.Status == "Late" || a.Status == "Excused")
        }).SingleOrDefaultAsync(token);
        var publishedMarks = ScopedMarks(campusIds, filter).Where(m => m.Exam.Status == ExamLifecycle.Published && !m.IsWithheld);
        var markStats = await publishedMarks.GroupBy(_ => 1).Select(g => new
        {
            Average = g.Average(m => m.Score * 100m / m.Exam.MaximumMarks),
            Pass = g.Count(m => m.Score >= m.Exam.PassMarks), Total = g.Count()
        }).SingleOrDefaultAsync(token);
        return new RoleDashboardViewModel
        {
            Role = "HOD", Eyebrow = "Department intelligence", Title = hod.DepartmentName,
            Description = "Department performance, workload and student support signals within your active campuses.",
            ScopeDescription = $"{hod.DepartmentName} · {await CampusScopeLabelAsync(campusIds, token)}",
            Filter = filter, Range = range, RefreshedUtc = DateTime.UtcNow,
            Classes = await ClassOptionsAsync(campusIds, hod.DepartmentId, token),
            Subjects = await SubjectOptionsAsync(campusIds, hod.DepartmentId, null, token),
            Kpis =
            [
                Kpi("Students", await students.CountAsync(token), "fa-user-graduate", "info", "Active department learners", "/Students"),
                Kpi("Professors", await professors.CountAsync(token), "fa-chalkboard-user", "success", "Department professors in campus scope", "/HodManagement/Professors"),
                PercentKpi("Attendance", totals == null ? 0 : DashboardMath.Percentage(totals.Present, totals.Total), "fa-calendar-check", "success", range.Label, "/Attendance"),
                PercentKpi("Average marks", markStats?.Average ?? 0, "fa-chart-line", "info", "Published results only", "/Mark"),
                PercentKpi("Pass rate", markStats == null ? 0 : DashboardMath.Percentage(markStats.Pass, markStats.Total), "fa-circle-check", "success", "Published, non-withheld results"),
                Kpi("Pending corrections", await _db.AttendanceCorrections.AsNoTracking().CountAsync(c => c.Status == ApprovalStatus.Pending && c.Attendance.Student.DepartmentId == hod.DepartmentId && _db.UserCampusMemberships.Any(m => m.UserId == c.Attendance.Student.UserId && m.IsActive && campusIds.Contains(m.CampusId)), token), "fa-clock-rotate-left", "warning", "Attendance changes awaiting review", "/Attendance/Sessions")
            ],
            Charts =
            [
                await BuildAttendanceTrendAsync(campusIds, filter, range, token),
                await BuildSubjectPerformanceAsync(campusIds, filter, token),
                await BuildProfessorWorkloadAsync(campusIds, hod.DepartmentId, token),
                await BuildAssignmentStatusAsync(campusIds, filter, null, token),
                await BuildExamLifecycleAsync(campusIds, filter, token)
            ],
            Alerts = await DepartmentRisksAsync(campusIds, hod.DepartmentId.Value, range, token),
            PrimaryActionUrl = "/HodManagement/Professors", PrimaryActionLabel = "Manage professors"
        };
    }

    private async Task<RoleDashboardViewModel> ProfessorAsync(
        string userId,
        DashboardFilterInput filter,
        DashboardDateRange range,
        int[] campusIds,
        CancellationToken token)
    {
        var teacher = await _db.Teachers.AsNoTracking()
            .Where(t => t.UserId == userId && t.TeacherType == TeacherType.Professor && !t.IsArchived && t.User.IsActive)
            .Select(t => new { t.Id, t.FullName, t.DepartmentId }).SingleOrDefaultAsync(token)
            ?? throw new UnauthorizedAccessException();
        var classIds = await _db.TeacherClassAssignments.AsNoTracking().Where(a => a.TeacherId == teacher.Id &&
                a.Class.Students.Any(s => s.UserId != null && _db.UserCampusMemberships.Any(m =>
                    m.UserId == s.UserId && m.IsActive && campusIds.Contains(m.CampusId))))
            .Select(a => a.ClassId).Distinct().ToArrayAsync(token);
        var subjectIds = await _db.TeacherSubjectAssignments.AsNoTracking().Where(a => a.TeacherId == teacher.Id &&
                a.Subject.Class.Students.Any(s => s.UserId != null && _db.UserCampusMemberships.Any(m =>
                    m.UserId == s.UserId && m.IsActive && campusIds.Contains(m.CampusId))))
            .Select(a => a.SubjectId).Distinct().ToArrayAsync(token);
        if (filter.ClassId.HasValue && !classIds.Contains(filter.ClassId.Value) ||
            filter.SubjectId.HasValue && !subjectIds.Contains(filter.SubjectId.Value))
            throw new UnauthorizedAccessException();
        var scopedStudents = ScopedStudents(campusIds, filter).Where(s => classIds.Contains(s.ClassId));
        var attendance = ScopedAttendance(campusIds, filter).Where(a => classIds.Contains(a.Student.ClassId) &&
            (!a.TeacherId.HasValue || a.TeacherId == teacher.Id) && a.Date >= range.FromUtc && a.Date < range.ToExclusiveUtc);
        var attendanceTotals = await attendance.GroupBy(_ => 1).Select(g => new
        {
            Total = g.Count(), Present = g.Count(a => a.Status == "Present" || a.Status == "Late" || a.Status == "Excused")
        }).SingleOrDefaultAsync(token);
        var todayNumber = DateTime.Today.DayOfWeek == DayOfWeek.Sunday
            ? 7
            : (int)DateTime.Today.DayOfWeek;
        var todayClasses = await _db.TimetableEntries.AsNoTracking().CountAsync(t =>
            t.TeacherId == teacher.Id && !t.IsCancelled && t.DayOfWeek == todayNumber, token);
        var assignmentQuery = _db.Assignments.AsNoTracking().Where(a => subjectIds.Contains(a.SubjectId));
        var pendingGrading = await _db.AssignmentSubmissions.AsNoTracking().CountAsync(s =>
            s.Assignment != null && subjectIds.Contains(s.Assignment.SubjectId) && s.EvaluationStatus == "Pending", token);
        return new RoleDashboardViewModel
        {
            Role = "Professor", Eyebrow = "Teaching analytics", Title = $"Welcome, {teacher.FullName}",
            Description = "Attendance, assessment and workload analytics for your assigned classes and subjects only.",
            ScopeDescription = $"{classIds.Length} assigned classes · {subjectIds.Length} assigned subjects",
            Filter = filter, Range = range, RefreshedUtc = DateTime.UtcNow,
            Classes = await _db.Classes.AsNoTracking().Where(c => classIds.Contains(c.Id)).OrderBy(c => c.Name)
                .Select(c => new DashboardOption(c.Id.ToString(), c.Name)).ToListAsync(token),
            Subjects = await _db.Subjects.AsNoTracking().Where(s => subjectIds.Contains(s.Id)).OrderBy(s => s.Name)
                .Select(s => new DashboardOption(s.Id.ToString(), s.Name)).ToListAsync(token),
            Kpis =
            [
                Kpi("Today’s classes", todayClasses, "fa-calendar-day", "info", "Scheduled, non-cancelled periods", "/Timetable"),
                Kpi("Assigned students", await scopedStudents.CountAsync(token), "fa-user-graduate", "primary", "Students in your assigned classes"),
                Kpi("Pending grading", pendingGrading, "fa-pen-to-square", "warning", "Submissions awaiting evaluation", "/Assignment"),
                PercentKpi("Average attendance", attendanceTotals == null ? 0 : DashboardMath.Percentage(attendanceTotals.Present, attendanceTotals.Total), "fa-calendar-check", "success", range.Label, "/Attendance"),
                Kpi("Missing submissions", await assignmentQuery
                    .SelectMany(a => a.Subject!.Class.Students.Where(s => !a.Submissions.Any(submission => submission.StudentId == s.Id)))
                    .CountAsync(token), "fa-file-circle-exclamation", "danger", "Eligible class students without a submission"),
                Kpi("Upcoming deadlines", await assignmentQuery.CountAsync(a => a.Status == "Published" && a.DueUtc >= DateTime.UtcNow && a.DueUtc < DateTime.UtcNow.AddDays(14), token), "fa-hourglass-half", "warning", "Published assignments due in 14 days", "/Assignment")
            ],
            Charts =
            [
                await BuildProfessorAttendanceTrendAsync(teacher.Id, classIds, campusIds, range, token),
                await BuildAssignmentStatusAsync(campusIds, filter, subjectIds, token),
                await BuildMarksHistogramAsync(campusIds, filter, subjectIds, token),
                await BuildSubjectPerformanceAsync(campusIds, filter, token, subjectIds)
            ],
            Alerts = await ProfessorRisksAsync(campusIds, classIds, subjectIds, range, token),
            Timeline = await ProfessorTimelineAsync(teacher.Id, subjectIds, token),
            PrimaryActionUrl = "/Attendance/SelectClass", PrimaryActionLabel = "Take attendance"
        };
    }

    private async Task<RoleDashboardViewModel> StudentAsync(
        string userId,
        DashboardFilterInput filter,
        DashboardDateRange range,
        int[] campusIds,
        CancellationToken token)
    {
        var student = await _db.Students.AsNoTracking().Where(s => s.UserId == userId && !s.IsArchived)
            .Select(s => new { s.Id, s.FullName, s.ClassId, s.DepartmentId }).SingleOrDefaultAsync(token)
            ?? throw new UnauthorizedAccessException();
        if (filter.ClassId.HasValue && filter.ClassId != student.ClassId ||
            filter.DepartmentId.HasValue && filter.DepartmentId != student.DepartmentId)
            throw new UnauthorizedAccessException();
        filter.ClassId = student.ClassId;
        filter.DepartmentId = student.DepartmentId;
        var attendance = _db.Attendances.AsNoTracking().Where(a => a.StudentId == student.Id &&
            a.Date >= range.FromUtc && a.Date < range.ToExclusiveUtc);
        var attendanceTotals = await attendance.GroupBy(_ => 1).Select(g => new
        {
            Total = g.Count(), Present = g.Count(a => a.Status == "Present" || a.Status == "Late" || a.Status == "Excused")
        }).SingleOrDefaultAsync(token);
        var marks = _db.Marks.AsNoTracking().Where(m => m.StudentId == student.Id &&
            m.Exam.Status == ExamLifecycle.Published && m.IsApproved && !m.IsWithheld);
        var average = await marks.AverageAsync(m => (decimal?)(m.Score * 100m / m.Exam.MaximumMarks), token) ?? 0;
        var subjectIds = await _db.StudentSubjectEnrollments.AsNoTracking().Where(e => e.StudentId == student.Id)
            .Select(e => e.SubjectId).Union(_db.Subjects.AsNoTracking().Where(s => s.ClassId == student.ClassId).Select(s => s.Id))
            .Distinct().ToArrayAsync(token);
        var visibleAssignments = _db.Assignments.AsNoTracking().VisibleAt(DateTime.UtcNow)
            .Where(a => subjectIds.Contains(a.SubjectId));
        var submittedIds = _db.AssignmentSubmissions.AsNoTracking().Where(s => s.StudentId == student.Id).Select(s => s.AssignmentId);
        var outstanding = await OutstandingAsync(_db.StudentInvoices.AsNoTracking().Where(i => i.StudentId == student.Id &&
            i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft), token);
        return new RoleDashboardViewModel
        {
            Role = "Student", Eyebrow = "My learning", Title = $"Welcome, {student.FullName}",
            Description = "Your personal attendance, published results, coursework and fee position.",
            ScopeDescription = "Only your authenticated student record",
            Filter = filter, Range = range, RefreshedUtc = DateTime.UtcNow,
            Kpis =
            [
                PercentKpi("Attendance", attendanceTotals == null ? 0 : DashboardMath.Percentage(attendanceTotals.Present, attendanceTotals.Total), "fa-calendar-check", "success", range.Label),
                PercentKpi("Published average", average, "fa-chart-line", "info", "Approved, published and non-withheld results only"),
                Kpi("Pending assignments", await visibleAssignments.CountAsync(a => a.DueUtc >= DateTime.UtcNow && !submittedIds.Contains(a.Id), token), "fa-list-check", "warning", "Visible assignments without your submission", "/Assignment"),
                Kpi("Completed assignments", await visibleAssignments.CountAsync(a => submittedIds.Contains(a.Id), token), "fa-circle-check", "success", "Visible assignments submitted"),
                MoneyKpi("Outstanding fees", outstanding, "fa-wallet", "warning", "Issued invoice balance after aid and payments", "/Finance"),
                Kpi("Active library loans", await _db.LibraryLoans.AsNoTracking().CountAsync(l => l.BorrowerUserId == userId && l.ReturnedUtc == null, token), "fa-book", "info", "Books currently issued to you", "/Library/MyLoans")
            ],
            Charts =
            [
                await BuildStudentAttendanceTrendAsync(student.Id, range, token),
                await BuildStudentSubjectPerformanceAsync(student.Id, token),
                await BuildStudentAssignmentStatusAsync(student.Id, subjectIds, token),
                await BuildStudentFeeStatusAsync(student.Id, token)
            ],
            Alerts = await StudentRisksAsync(student.Id, subjectIds, range, token),
            Timeline = await StudentTimelineAsync(student.Id, student.ClassId, subjectIds, token),
            PrimaryActionUrl = "/Assignment", PrimaryActionLabel = "View assignments"
        };
    }

    private async Task<RoleDashboardViewModel> FinanceAsync(
        ClaimsPrincipal user,
        DashboardFilterInput filter,
        DashboardDateRange range,
        int[] campusIds,
        CancellationToken token)
    {
        if (!await _permissions.HasAsync(user, "fees.view", token)) throw new UnauthorizedAccessException();
        var transactions = ScopedTransactions(campusIds, filter).Where(t =>
            t.Status == FinancialTransactionStatus.Completed && t.CreatedUtc >= range.FromUtc && t.CreatedUtc < range.ToExclusiveUtc);
        var invoices = ScopedInvoices(campusIds, filter).Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft);
        var today = DateTime.UtcNow.Date;
        return new RoleDashboardViewModel
        {
            Role = "Accountant", Eyebrow = "Financial control", Title = "Finance dashboard",
            Description = "Posted ledger performance, receivables, refunds and reconciliation within your campuses.",
            ScopeDescription = await CampusScopeLabelAsync(campusIds, token), Filter = filter, Range = range,
            RefreshedUtc = DateTime.UtcNow, Campuses = await CampusOptionsAsync(campusIds, token),
            Departments = await DepartmentOptionsAsync(campusIds, token),
            Kpis =
            [
                MoneyKpi("Collections today", await SignedCollectionAsync(ScopedTransactions(campusIds, filter).Where(t => t.Status == FinancialTransactionStatus.Completed && t.CreatedUtc >= today && t.CreatedUtc < today.AddDays(1)), token), "fa-cash-register", "success", "Completed ledger entries today", "/Finance"),
                MoneyKpi("Net collections", await SignedCollectionAsync(transactions, token), "fa-rupee-sign", "success", range.Label, "/Finance"),
                MoneyKpi("Invoiced", await invoices.SumAsync(i => (decimal?)i.TotalAmount, token) ?? 0, "fa-file-invoice", "info", "Issued, paid and overdue invoices"),
                MoneyKpi("Outstanding", await OutstandingAsync(invoices, token), "fa-file-invoice-dollar", "warning", "Unpaid balance", "/Finance/Invoices"),
                MoneyKpi("Refunds & reversals", Math.Abs(await transactions.Where(t => t.Type == FinancialTransactionType.Refund || t.Type == FinancialTransactionType.Reversal).SumAsync(t => (decimal?)t.Amount, token) ?? 0), "fa-rotate-left", "danger", "Completed refund and reversal entries"),
                Kpi("Refunds & reversals", await transactions.CountAsync(t => t.Type == FinancialTransactionType.Refund || t.Type == FinancialTransactionType.Reversal, token), "fa-scale-unbalanced", "warning", "Completed scoped return entries")
            ],
            Charts =
            [
                await BuildFinanceTrendAsync(campusIds, filter, range, token),
                await BuildPaymentMethodsAsync(campusIds, filter, range, token),
                await BuildOutstandingAgingAsync(campusIds, filter, token),
                await BuildInvoiceStatusAsync(campusIds, filter, token)
            ],
            Alerts = await FinanceAlertsAsync(campusIds, filter, range, token),
            PrimaryActionUrl = "/Finance/Collect", PrimaryActionLabel = "Collect payment"
        };
    }

    private async Task<RoleDashboardViewModel> AdmissionsAsync(
        ClaimsPrincipal user,
        DashboardFilterInput filter,
        DashboardDateRange range,
        int[] campusIds,
        CancellationToken token)
    {
        if (!await _permissions.HasAsync(user, "admission.view", token)) throw new UnauthorizedAccessException();
        var applications = ScopedAdmissions(campusIds, filter);
        var period = applications.Where(a => a.CreatedUtc >= range.FromUtc && a.CreatedUtc < range.ToExclusiveUtc);
        return new RoleDashboardViewModel
        {
            Role = "AdmissionOfficer", Eyebrow = "Admissions intelligence", Title = "Admissions dashboard",
            Description = "Pipeline, demand and processing analytics for authorized campuses.",
            ScopeDescription = await CampusScopeLabelAsync(campusIds, token), Filter = filter, Range = range,
            RefreshedUtc = DateTime.UtcNow, Campuses = await CampusOptionsAsync(campusIds, token),
            Departments = await DepartmentOptionsAsync(campusIds, token),
            Kpis =
            [
                Kpi("Applications", await period.CountAsync(token), "fa-file-signature", "info", range.Label, "/Admissions"),
                Kpi("Awaiting review", await applications.CountAsync(a => a.Stage == AdmissionStage.Submitted || a.Stage == AdmissionStage.UnderReview, token), "fa-hourglass-half", "warning", "Submitted or under review", "/Admissions"),
                Kpi("Approved", await period.CountAsync(a => a.Stage == AdmissionStage.Approved || a.Stage == AdmissionStage.Enrolled, token), "fa-user-check", "success", "Approved or enrolled in period"),
                Kpi("Pending documents", await applications.SelectMany(a => a.Documents)
                    .CountAsync(d => d.Status == DocumentVerificationStatus.Pending, token), "fa-file-circle-exclamation", "danger", "Application documents awaiting verification"),
                PercentKpi("Conversion", DashboardMath.Percentage(await period.CountAsync(a => a.Stage == AdmissionStage.Enrolled, token), await period.CountAsync(token)), "fa-arrow-trend-up", "success", "Enrolled applications divided by applications created")
            ],
            Charts =
            [
                await BuildAdmissionsTrendAsync(campusIds, filter, range, token),
                await BuildAdmissionsFunnelAsync(campusIds, filter, range, token),
                await BuildAdmissionDemandAsync(campusIds, filter, range, token),
                await BuildAdmissionSourcesAsync(campusIds, filter, range, token)
            ],
            Alerts = await AdmissionAlertsAsync(campusIds, filter, token),
            PrimaryActionUrl = "/Admissions", PrimaryActionLabel = "Review applications"
        };
    }

    private async Task<RoleDashboardViewModel> LibraryAsync(
        ClaimsPrincipal user,
        DashboardFilterInput filter,
        DashboardDateRange range,
        int[] campusIds,
        CancellationToken token)
    {
        if (!await _permissions.HasAsync(user, "library.audit", token)) throw new UnauthorizedAccessException();
        var loans = ScopedLoans(campusIds).Where(l => l.IssuedUtc >= range.FromUtc && l.IssuedUtc < range.ToExclusiveUtc);
        var active = ScopedLoans(campusIds).Where(l => l.ReturnedUtc == null);
        return new RoleDashboardViewModel
        {
            Role = "Librarian", Eyebrow = "Library circulation", Title = "Library dashboard",
            Description = "Circulation, returns, overdue items and fine activity for borrowers in your campuses.",
            ScopeDescription = await CampusScopeLabelAsync(campusIds, token) + " · catalog ownership is not campus-tagged",
            Filter = filter, Range = range, RefreshedUtc = DateTime.UtcNow,
            Kpis =
            [
                Kpi("Loans in period", await loans.CountAsync(token), "fa-book-open-reader", "info", range.Label, "/Library"),
                Kpi("Currently borrowed", await active.CountAsync(token), "fa-book", "primary", "Unreturned items in scope"),
                Kpi("Overdue", await active.CountAsync(l => l.DueUtc < DateTime.UtcNow, token), "fa-clock", "danger", "Unreturned items past due date"),
                MoneyKpi("Assessed fines", await active.SumAsync(l => (decimal?)l.FineAmount, token) ?? 0, "fa-rupee-sign", "warning", "Fine amount on active scoped loans"),
                Kpi("Returned", await loans.CountAsync(l => l.ReturnedUtc != null, token), "fa-rotate-left", "success", "Items returned during selected issue period")
            ],
            Charts =
            [
                await BuildLibraryTrendAsync(campusIds, range, token),
                await BuildOverdueAgingAsync(campusIds, token),
                await BuildLibraryCategoriesAsync(campusIds, range, token),
                await BuildLibraryConditionAsync(campusIds, token)
            ],
            Alerts = await LibraryAlertsAsync(campusIds, token),
            PrimaryActionUrl = "/Library", PrimaryActionLabel = "Manage circulation"
        };
    }

    private async Task<RoleDashboardViewModel> ExaminationAsync(
        ClaimsPrincipal user,
        DashboardFilterInput filter,
        DashboardDateRange range,
        int[] campusIds,
        CancellationToken token)
    {
        if (!await _permissions.HasAsync(user, "marks.view", token)) throw new UnauthorizedAccessException();
        var marks = ScopedMarks(campusIds, filter);
        var published = marks.Where(m => m.Exam.Status == ExamLifecycle.Published && !m.IsWithheld);
        var stats = await published.GroupBy(_ => 1).Select(g => new
        {
            Average = g.Average(m => m.Score * 100m / m.Exam.MaximumMarks),
            Passed = g.Count(m => m.Score >= m.Exam.PassMarks),
            Total = g.Count()
        }).SingleOrDefaultAsync(token);
        return new RoleDashboardViewModel
        {
            Role = "ExaminationOfficer", Eyebrow = "Assessment control", Title = "Examination dashboard",
            Description = "Exam lifecycle, evaluation completion and published-result quality for authorized campuses.",
            ScopeDescription = await CampusScopeLabelAsync(campusIds, token), Filter = filter, Range = range,
            RefreshedUtc = DateTime.UtcNow, Departments = await DepartmentOptionsAsync(campusIds, token),
            Kpis =
            [
                Kpi("Scheduled exams", await _db.Exams.AsNoTracking().CountAsync(e =>
                    e.ExamDate >= range.FromUtc && e.ExamDate < range.ToExclusiveUtc &&
                    e.Marks.Any(m => m.Student.UserId != null && _db.UserCampusMemberships.Any(cm => cm.UserId == m.Student.UserId && cm.IsActive && campusIds.Contains(cm.CampusId))), token),
                    "fa-calendar-days", "info", range.Label, "/Exam"),
                Kpi("Awaiting publication", await _db.Exams.AsNoTracking().CountAsync(e =>
                    e.Status == ExamLifecycle.Locked &&
                    e.Marks.Any(m => m.Student.UserId != null && _db.UserCampusMemberships.Any(cm => cm.UserId == m.Student.UserId && cm.IsActive && campusIds.Contains(cm.CampusId))), token),
                    "fa-lock", "warning", "Scoped locked result sets"),
                PercentKpi("Published average", stats?.Average ?? 0, "fa-chart-line", "info", "Published non-withheld results"),
                PercentKpi("Pass rate", stats == null ? 0 : DashboardMath.Percentage(stats.Passed, stats.Total), "fa-circle-check", "success", "Published non-withheld results"),
                Kpi("Withheld results", await marks.CountAsync(m => m.IsWithheld, token), "fa-circle-pause", "danger", "Results explicitly withheld")
            ],
            Charts =
            [
                await BuildExamLifecycleAsync(campusIds, filter, token),
                await BuildSubjectPerformanceAsync(campusIds, filter, token),
                await BuildMarksHistogramAsync(campusIds, filter, null, token),
                await BuildGradeDistributionAsync(campusIds, filter, token)
            ],
            Alerts = await ExaminationAlertsAsync(campusIds, filter, token),
            PrimaryActionUrl = "/Exam", PrimaryActionLabel = "Manage examinations"
        };
    }

    private async Task<RoleDashboardViewModel> OperationsAsync(
        string userId,
        DashboardFilterInput filter,
        DashboardDateRange range,
        int[] campusIds,
        CancellationToken token)
    {
        var rooms = _db.Rooms.AsNoTracking().Where(r => r.IsActive && r.CampusId.HasValue && campusIds.Contains(r.CampusId.Value));
        var tickets = ScopedServiceTickets(campusIds);
        var scopedClassIds = ScopedClasses(campusIds, filter).Select(c => c.Id);
        var timetablePeriods = await _db.TimetableEntries.AsNoTracking()
            .CountAsync(t => scopedClassIds.Contains(t.ClassId) && !t.IsCancelled, token);
        return new RoleDashboardViewModel
        {
            Role = "Operations", Eyebrow = "Campus operations", Title = "Operations dashboard",
            Description = "Rooms, schedules, service requests and operational capacity within your campuses.",
            ScopeDescription = await CampusScopeLabelAsync(campusIds, token), Filter = filter, Range = range,
            RefreshedUtc = DateTime.UtcNow,
            Kpis =
            [
                Kpi("Active rooms", await rooms.CountAsync(token), "fa-door-open", "info", "Campus-tagged active rooms"),
                Kpi("Open requests", await tickets.CountAsync(t => t.Status != "Resolved" && t.Status != "Closed", token), "fa-screwdriver-wrench", "warning", "Scoped unresolved service tickets", "/Operations"),
                Kpi("High priority", await tickets.CountAsync(t => t.Priority == "High" && t.Status != "Resolved" && t.Status != "Closed", token), "fa-triangle-exclamation", "danger", "High-priority open requests"),
                Kpi("Timetable periods", timetablePeriods, "fa-calendar-days", "success", "Active periods in campus-scoped classes"),
                Kpi("Resolved requests", await tickets.CountAsync(t => t.Status == "Resolved" || t.Status == "Closed", token), "fa-circle-check", "primary", "Campus-scoped resolved service requests")
            ],
            Charts =
            [
                await BuildRoomUtilizationAsync(campusIds, token),
                await BuildTicketStatusAsync(campusIds, token),
                await BuildTicketAgingAsync(campusIds, token),
                await BuildTimetableDensityAsync(campusIds, token)
            ],
            Alerts = await OperationsAlertsAsync(campusIds, token),
            PrimaryActionUrl = "/Operations", PrimaryActionLabel = "Open operations center"
        };
    }

    // Query roots always apply campus/user scope before aggregation.
    private IQueryable<Student> ScopedStudents(int[] campuses, DashboardFilterInput filter)
    {
        var query = _db.Students.AsNoTracking().Where(s => !s.IsArchived && s.UserId != null &&
            _db.UserCampusMemberships.Any(m => m.UserId == s.UserId && m.IsActive && campuses.Contains(m.CampusId)));
        if (filter.DepartmentId.HasValue) query = query.Where(s => s.DepartmentId == filter.DepartmentId);
        if (filter.ClassId.HasValue) query = query.Where(s => s.ClassId == filter.ClassId);
        return query;
    }

    private IQueryable<Teacher> ScopedTeachers(int[] campuses, DashboardFilterInput filter)
    {
        var query = _db.Teachers.AsNoTracking().Where(t => !t.IsArchived &&
            _db.UserCampusMemberships.Any(m => m.UserId == t.UserId && m.IsActive && campuses.Contains(m.CampusId)));
        if (filter.DepartmentId.HasValue) query = query.Where(t => t.DepartmentId == filter.DepartmentId);
        return query;
    }

    private IQueryable<Class> ScopedClasses(int[] campuses, DashboardFilterInput filter)
    {
        var query = _db.Classes.AsNoTracking().Where(c => !c.IsArchived &&
            (c.Students.Any(s => s.UserId != null && _db.UserCampusMemberships.Any(m => m.UserId == s.UserId && m.IsActive && campuses.Contains(m.CampusId))) ||
             c.Teacher != null && _db.UserCampusMemberships.Any(m => m.UserId == c.Teacher.UserId && m.IsActive && campuses.Contains(m.CampusId))));
        if (filter.DepartmentId.HasValue) query = query.Where(c => c.DepartmentId == filter.DepartmentId);
        if (filter.ClassId.HasValue) query = query.Where(c => c.Id == filter.ClassId);
        return query;
    }

    private IQueryable<Attendance> ScopedAttendance(int[] campuses, DashboardFilterInput filter)
    {
        var query = _db.Attendances.AsNoTracking().Where(a => a.Student.UserId != null &&
            _db.UserCampusMemberships.Any(m => m.UserId == a.Student.UserId && m.IsActive && campuses.Contains(m.CampusId)));
        if (filter.DepartmentId.HasValue) query = query.Where(a => a.Student.DepartmentId == filter.DepartmentId);
        if (filter.ClassId.HasValue) query = query.Where(a => a.Student.ClassId == filter.ClassId);
        if (filter.SubjectId.HasValue) query = query.Where(a => a.SubjectId == filter.SubjectId);
        return query;
    }

    private IQueryable<Mark> ScopedMarks(int[] campuses, DashboardFilterInput filter)
    {
        var query = _db.Marks.AsNoTracking().Where(m => m.Student.UserId != null &&
            _db.UserCampusMemberships.Any(x => x.UserId == m.Student.UserId && x.IsActive && campuses.Contains(x.CampusId)));
        if (filter.DepartmentId.HasValue) query = query.Where(m => m.Student.DepartmentId == filter.DepartmentId);
        if (filter.ClassId.HasValue) query = query.Where(m => m.Student.ClassId == filter.ClassId);
        if (filter.SubjectId.HasValue) query = query.Where(m => m.SubjectId == filter.SubjectId);
        return query;
    }

    private IQueryable<StudentInvoice> ScopedInvoices(int[] campuses, DashboardFilterInput filter)
    {
        var query = _db.StudentInvoices.AsNoTracking().Where(i => i.Student.UserId != null &&
            _db.UserCampusMemberships.Any(m => m.UserId == i.Student.UserId && m.IsActive && campuses.Contains(m.CampusId)));
        if (filter.DepartmentId.HasValue) query = query.Where(i => i.Student.DepartmentId == filter.DepartmentId);
        if (filter.ClassId.HasValue) query = query.Where(i => i.Student.ClassId == filter.ClassId);
        return query;
    }

    private IQueryable<FinancialTransaction> ScopedTransactions(int[] campuses, DashboardFilterInput filter)
    {
        var query = _db.FinancialTransactions.AsNoTracking().Where(t => t.Student.UserId != null &&
            _db.UserCampusMemberships.Any(m => m.UserId == t.Student.UserId && m.IsActive && campuses.Contains(m.CampusId)));
        if (filter.DepartmentId.HasValue) query = query.Where(t => t.Student.DepartmentId == filter.DepartmentId);
        if (filter.ClassId.HasValue) query = query.Where(t => t.Student.ClassId == filter.ClassId);
        return query;
    }

    private IQueryable<AdmissionApplication> ScopedAdmissions(int[] campuses, DashboardFilterInput filter)
    {
        var query = _db.AdmissionApplications.AsNoTracking().Where(a => !a.IsArchived && a.CampusId.HasValue && campuses.Contains(a.CampusId.Value));
        if (filter.DepartmentId.HasValue) query = query.Where(a => a.DepartmentId == filter.DepartmentId);
        return query;
    }

    private IQueryable<LibraryLoan> ScopedLoans(int[] campuses) =>
        _db.LibraryLoans.AsNoTracking().Where(l => _db.UserCampusMemberships.Any(m =>
            m.UserId == l.BorrowerUserId && m.IsActive && campuses.Contains(m.CampusId)));

    private IQueryable<CampusServiceTicket> ScopedServiceTickets(int[] campuses) =>
        _db.CampusServiceTickets.AsNoTracking().Where(t => _db.UserCampusMemberships.Any(m =>
            m.UserId == t.ReportedByUserId && m.IsActive && campuses.Contains(m.CampusId)));

    private async Task<int[]> ResolveCampusScopeAsync(ClaimsPrincipal user, int? requested, CancellationToken token)
    {
        var allowed = await _campusScope.AllowedCampusIdsAsync(user, token);
        if (!user.IsInRole("SuperAdmin") && allowed.Length == 0)
            throw new DashboardSetupRequiredException();
        if (requested.HasValue)
        {
            if (!allowed.Contains(requested.Value)) throw new UnauthorizedAccessException();
            return [requested.Value];
        }
        return allowed;
    }

    private async Task<IReadOnlyList<DashboardOption>> CampusOptionsAsync(int[] ids, CancellationToken token) =>
        await _db.Campuses.AsNoTracking().Where(c => ids.Contains(c.Id) && c.IsActive).OrderBy(c => c.Name)
            .Select(c => new DashboardOption(c.Id.ToString(), c.Name)).ToListAsync(token);

    private async Task<IReadOnlyList<DashboardOption>> DepartmentOptionsAsync(int[] campuses, CancellationToken token) =>
        await _db.Departments.AsNoTracking().Where(d => !d.IsArchived &&
            (d.Students.Any(s => s.UserId != null && _db.UserCampusMemberships.Any(m => m.UserId == s.UserId && m.IsActive && campuses.Contains(m.CampusId))) ||
             d.Teachers.Any(t => _db.UserCampusMemberships.Any(m => m.UserId == t.UserId && m.IsActive && campuses.Contains(m.CampusId)))))
            .OrderBy(d => d.Name).Select(d => new DashboardOption(d.Id.ToString(), d.Name)).ToListAsync(token);

    private async Task<IReadOnlyList<DashboardOption>> ClassOptionsAsync(int[] campuses, int? departmentId, CancellationToken token)
    {
        var filter = new DashboardFilterInput { DepartmentId = departmentId };
        return await ScopedClasses(campuses, filter).OrderBy(c => c.Name)
            .Select(c => new DashboardOption(c.Id.ToString(), c.Name)).ToListAsync(token);
    }

    private async Task<IReadOnlyList<DashboardOption>> SubjectOptionsAsync(int[] campuses, int? departmentId, int? classId, CancellationToken token)
    {
        var query = _db.Subjects.AsNoTracking().Where(s => !s.IsArchived &&
            (s.Class.Students.Any(st => st.UserId != null && _db.UserCampusMemberships.Any(m => m.UserId == st.UserId && m.IsActive && campuses.Contains(m.CampusId))) ||
             s.TeacherAssignments.Any(a => _db.UserCampusMemberships.Any(m => m.UserId == a.Teacher.UserId && m.IsActive && campuses.Contains(m.CampusId)))));
        if (departmentId.HasValue) query = query.Where(s => s.DepartmentId == departmentId);
        if (classId.HasValue) query = query.Where(s => s.ClassId == classId);
        return await query.OrderBy(s => s.Name).Select(s => new DashboardOption(s.Id.ToString(), s.Name)).ToListAsync(token);
    }

    private async Task<string> CampusScopeLabelAsync(int[] campuses, CancellationToken token)
    {
        var names = await _db.Campuses.AsNoTracking().Where(c => campuses.Contains(c.Id)).OrderBy(c => c.Name).Select(c => c.Name).ToListAsync(token);
        return names.Count == 0 ? "No active campus scope" : string.Join(", ", names);
    }

    private static DashboardKpi Kpi(string title, int value, string icon, string tone, string explanation, string? url = null, int? previous = null) =>
        new(title, value.ToString("N0", CultureInfo.CurrentCulture), icon, tone, explanation, value, previous,
            previous.HasValue ? DashboardMath.ChangePercent(value, previous.Value) : null, url);

    private static DashboardKpi MoneyKpi(string title, decimal value, string icon, string tone, string explanation, string? url = null) =>
        new(title, DashboardMath.Money(value).ToString("C0", CultureInfo.CurrentCulture), icon, tone, explanation, value, null, null, url, "currency");

    private static DashboardKpi PercentKpi(string title, decimal value, string icon, string tone, string explanation, string? url = null) =>
        new(title, $"{Math.Round(value, 1):0.0}%", icon, tone, explanation, value, null, null, url, "percent");

    private static DashboardChart Chart(string id, string title, string description, string type,
        IReadOnlyList<string> labels, IReadOnlyList<DashboardChartDataset> datasets,
        string format = "number", bool stacked = false, bool horizontal = false, string? url = null) =>
        new() { Id = id, Title = title, Description = description, Type = type, Labels = labels,
            Datasets = datasets, Format = format, Stacked = stacked, Horizontal = horizontal,
            DrillDownUrl = url, AccessibleSummary = $"{title}. {description}" };

    private static DashboardChartDataset Series(string label, IEnumerable<decimal?> values, string tone, string? type = null, bool fill = false) =>
        new() { Label = label, Values = values.ToArray(), Tone = tone, Type = type, Fill = fill };

    private static async Task<decimal> SignedCollectionAsync(IQueryable<FinancialTransaction> query, CancellationToken token) =>
        DashboardMath.Money(await query.SumAsync(t => (decimal?)(
            t.Type == FinancialTransactionType.Payment || t.Type == FinancialTransactionType.Adjustment
                ? t.Amount : -Math.Abs(t.Amount)), token) ?? 0m);

    private static async Task<decimal> OutstandingAsync(IQueryable<StudentInvoice> query, CancellationToken token) =>
        DashboardMath.Money(await query
            .Where(i => i.TotalAmount - i.AidAmount - i.PaidAmount > 0)
            .SumAsync(i => (decimal?)(i.TotalAmount - i.AidAmount - i.PaidAmount), token) ?? 0m);

    private async Task<DashboardChart> BuildAdmissionsTrendAsync(int[] campuses, DashboardFilterInput filter, DashboardDateRange range, CancellationToken token)
    {
        var rows = await ScopedAdmissions(campuses, filter).Where(a => a.CreatedUtc >= range.FromUtc && a.CreatedUtc < range.ToExclusiveUtc)
            .GroupBy(a => new { a.CreatedUtc.Year, a.CreatedUtc.Month }).Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count(), Enrolled = g.Count(a => a.Stage == AdmissionStage.Enrolled), Rejected = g.Count(a => a.Stage == AdmissionStage.Rejected) })
            .OrderBy(x => x.Year).ThenBy(x => x.Month).ToListAsync(token);
        var labels = rows.Select(x => new DateTime(x.Year, x.Month, 1).ToString("MMM yy")).ToArray();
        return Chart("enrollment-trend", "Enrollment trend", "Applications, enrollments and rejections by month.", "line", labels,
            [Series("Applications", rows.Select(x => (decimal?)x.Count), "primary", fill: true), Series("Enrolled", rows.Select(x => (decimal?)x.Enrolled), "success"), Series("Rejected", rows.Select(x => (decimal?)x.Rejected), "danger")], url: "/Admissions");
    }

    private async Task<DashboardChart> BuildCampusComparisonAsync(int[] campuses, CancellationToken token)
    {
        var rows = await _db.Campuses.AsNoTracking().Where(c => campuses.Contains(c.Id)).OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Name,
                Students = _db.Students.Count(s => s.UserId != null && !s.IsArchived && _db.UserCampusMemberships.Any(m => m.UserId == s.UserId && m.CampusId == c.Id && m.IsActive)),
                Staff = _db.Teachers.Count(t => !t.IsArchived && _db.UserCampusMemberships.Any(m => m.UserId == t.UserId && m.CampusId == c.Id && m.IsActive))
            }).ToListAsync(token);
        return Chart("campus-comparison", "Campus comparison", "Active student and academic-staff population; metrics use the same count scale.", "bar", rows.Select(x => x.Name).ToArray(),
            [Series("Students", rows.Select(x => (decimal?)x.Students), "primary"), Series("Staff", rows.Select(x => (decimal?)x.Staff), "success")], url: "/Reports");
    }

    private async Task<DashboardChart> BuildFinanceTrendAsync(int[] campuses, DashboardFilterInput filter, DashboardDateRange range, CancellationToken token)
    {
        var rows = await ScopedTransactions(campuses, filter).Where(t => t.Status == FinancialTransactionStatus.Completed && t.CreatedUtc >= range.FromUtc && t.CreatedUtc < range.ToExclusiveUtc)
            .GroupBy(t => t.CreatedUtc.Date).Select(g => new { Day = g.Key,
                Collected = g.Sum(t => t.Type == FinancialTransactionType.Payment || t.Type == FinancialTransactionType.Adjustment ? t.Amount : 0),
                Returned = g.Sum(t => t.Type == FinancialTransactionType.Refund || t.Type == FinancialTransactionType.Reversal ? Math.Abs(t.Amount) : 0) })
            .OrderBy(x => x.Day).ToListAsync(token);
        return Chart("finance-trend", "Fee performance", "Completed ledger collections and refunds/reversals by day.", "line", rows.Select(x => x.Day.ToString("dd MMM")).ToArray(),
            [Series("Collected", rows.Select(x => (decimal?)x.Collected), "success", fill: true), Series("Refunded / reversed", rows.Select(x => (decimal?)x.Returned), "danger")], "currency", url: "/Finance");
    }

    private async Task<DashboardChart> BuildAttendanceTrendAsync(int[] campuses, DashboardFilterInput filter, DashboardDateRange range, CancellationToken token)
    {
        var rows = await ScopedAttendance(campuses, filter).Where(a => a.Date >= range.FromUtc && a.Date < range.ToExclusiveUtc)
            .GroupBy(a => a.Date).Select(g => new { Day = g.Key, Present = g.Count(a => a.Status == "Present"), Absent = g.Count(a => a.Status == "Absent"), Late = g.Count(a => a.Status == "Late"), Leave = g.Count(a => a.Status == "Leave" || a.Status == "Excused") })
            .OrderBy(x => x.Day).ToListAsync(token);
        return Chart("attendance-trend", "Attendance trend", "Recorded attendance statuses over the selected period.", "line", rows.Select(x => x.Day.ToString("dd MMM")).ToArray(),
            [Series("Present", rows.Select(x => (decimal?)x.Present), "success", fill: true), Series("Absent", rows.Select(x => (decimal?)x.Absent), "danger"), Series("Late", rows.Select(x => (decimal?)x.Late), "warning"), Series("Leave", rows.Select(x => (decimal?)x.Leave), "info")], stacked: false, url: "/Attendance");
    }

    private async Task<DashboardChart> BuildAdmissionsFunnelAsync(int[] campuses, DashboardFilterInput filter, DashboardDateRange range, CancellationToken token)
    {
        var query = ScopedAdmissions(campuses, filter).Where(a => a.CreatedUtc >= range.FromUtc && a.CreatedUtc < range.ToExclusiveUtc);
        var rows = await query.GroupBy(a => a.Stage).Select(g => new { Stage = g.Key, Count = g.Count() }).ToListAsync(token);
        var stages = Enum.GetValues<AdmissionStage>();
        return Chart("admissions-funnel", "Admissions funnel", "Application lifecycle counts for records created in the selected period.", "bar",
            stages.Select(s => s.ToString()).ToArray(), [Series("Applications", stages.Select(s => (decimal?)(rows.FirstOrDefault(x => x.Stage == s)?.Count ?? 0)), "primary")], horizontal: true, url: "/Admissions");
    }

    private async Task<DashboardChart> BuildDepartmentPerformanceAsync(int[] campuses, DashboardFilterInput filter, CancellationToken token)
    {
        var query = ScopedMarks(campuses, filter).Where(m => m.Exam.Status == ExamLifecycle.Published && !m.IsWithheld);
        var rows = await query.GroupBy(m => new { m.Student.DepartmentId, Name = m.Student.Department != null ? m.Student.Department.Name : "Unassigned" })
            .Select(g => new { g.Key.Name, Average = g.Average(m => m.Score * 100m / m.Exam.MaximumMarks) }).OrderBy(x => x.Name).ToListAsync(token);
        return Chart("department-performance", "Academic performance", "Average percentage from published, non-withheld results.", "bar", rows.Select(x => x.Name).ToArray(), [Series("Average", rows.Select(x => (decimal?)x.Average), "info")], "percent", url: "/Reports");
    }

    private async Task<DashboardChart> BuildSubjectPerformanceAsync(int[] campuses, DashboardFilterInput filter, CancellationToken token, int[]? allowedSubjectIds = null)
    {
        var query = ScopedMarks(campuses, filter).Where(m => m.Exam.Status == ExamLifecycle.Published && !m.IsWithheld);
        if (allowedSubjectIds != null) query = query.Where(m => allowedSubjectIds.Contains(m.SubjectId));
        var rows = await query.GroupBy(m => new { m.SubjectId, m.Subject.Name }).Select(g => new
        { g.Key.Name, Average = g.Average(m => m.Score * 100m / m.Exam.MaximumMarks), PassRate = g.Count(m => m.Score >= m.Exam.PassMarks) * 100m / g.Count() })
            .OrderBy(x => x.Name).Take(20).ToListAsync(token);
        return Chart("subject-performance", "Subject performance", "Published-result average and pass rate by subject.", "bar", rows.Select(x => x.Name).ToArray(),
            [Series("Average", rows.Select(x => (decimal?)x.Average), "primary"), Series("Pass rate", rows.Select(x => (decimal?)x.PassRate), "success")], "percent", url: "/Mark");
    }

    private async Task<DashboardChart> BuildProfessorWorkloadAsync(int[] campuses, int? departmentId, CancellationToken token)
    {
        var filter = new DashboardFilterInput { DepartmentId = departmentId };
        var rows = await ScopedTeachers(campuses, filter).Where(t => t.TeacherType == TeacherType.Professor)
            .Select(t => new { t.FullName, Classes = t.ClassAssignments.Count, Subjects = t.SubjectAssignments.Count, Periods = _db.TimetableEntries.Count(e => e.TeacherId == t.Id && !e.IsCancelled) })
            .OrderByDescending(x => x.Periods).ThenBy(x => x.FullName).Take(20).ToListAsync(token);
        return Chart("professor-workload", "Professor workload", "Assigned classes, subjects and active timetable periods.", "bar", rows.Select(x => x.FullName).ToArray(),
            [Series("Classes", rows.Select(x => (decimal?)x.Classes), "primary"), Series("Subjects", rows.Select(x => (decimal?)x.Subjects), "info"), Series("Periods", rows.Select(x => (decimal?)x.Periods), "warning")], stacked: true, horizontal: true, url: "/HodManagement/Professors");
    }

    private async Task<DashboardChart> BuildAssignmentStatusAsync(int[] campuses, DashboardFilterInput filter, int[]? subjectIds, CancellationToken token)
    {
        var query = _db.Assignments.AsNoTracking().Where(a => a.Subject != null &&
            a.Subject.Class.Students.Any(s => s.UserId != null && _db.UserCampusMemberships.Any(m => m.UserId == s.UserId && m.IsActive && campuses.Contains(m.CampusId))));
        if (filter.DepartmentId.HasValue) query = query.Where(a => a.Subject!.DepartmentId == filter.DepartmentId);
        if (filter.ClassId.HasValue) query = query.Where(a => a.Subject!.ClassId == filter.ClassId);
        if (filter.SubjectId.HasValue) query = query.Where(a => a.SubjectId == filter.SubjectId);
        if (subjectIds != null) query = query.Where(a => subjectIds.Contains(a.SubjectId));
        var rows = await query.GroupBy(a => a.Status).Select(g => new { Status = g.Key, Count = g.Count() }).OrderBy(x => x.Status).ToListAsync(token);
        return Chart("assignment-status", "Assignment lifecycle", "Explicit Draft, Scheduled and Published states; dates never publish Drafts implicitly.", "doughnut", rows.Select(x => x.Status).ToArray(), [Series("Assignments", rows.Select(x => (decimal?)x.Count), "primary")], url: "/Assignment");
    }

    private async Task<DashboardChart> BuildExamLifecycleAsync(int[] campuses, DashboardFilterInput filter, CancellationToken token)
    {
        var scopedExamIds = ScopedMarks(campuses, filter).Select(m => m.ExamId).Distinct();
        var rows = await _db.Exams.AsNoTracking().Where(e => scopedExamIds.Contains(e.Id)).GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).OrderBy(x => x.Status).ToListAsync(token);
        return Chart("exam-lifecycle", "Exam lifecycle", "Assessment lifecycle counts represented by scoped student results.", "doughnut", rows.Select(x => x.Status.ToString()).ToArray(), [Series("Exams", rows.Select(x => (decimal?)x.Count), "info")], url: "/Exam");
    }

    private async Task<DashboardChart> BuildProfessorAttendanceTrendAsync(int teacherId, int[] classIds, int[] campuses, DashboardDateRange range, CancellationToken token)
    {
        var filter = new DashboardFilterInput();
        var rows = await ScopedAttendance(campuses, filter).Where(a => classIds.Contains(a.Student.ClassId) && (!a.TeacherId.HasValue || a.TeacherId == teacherId) && a.Date >= range.FromUtc && a.Date < range.ToExclusiveUtc)
            .GroupBy(a => a.Date).Select(g => new { Day = g.Key, Rate = g.Count(a => a.Status == "Present" || a.Status == "Late" || a.Status == "Excused") * 100m / g.Count() })
            .OrderBy(x => x.Day).ToListAsync(token);
        return Chart("class-attendance", "Class attendance", "Attendance rate across assigned classes.", "line", rows.Select(x => x.Day.ToString("dd MMM")).ToArray(), [Series("Attendance", rows.Select(x => (decimal?)x.Rate), "success", fill: true)], "percent", url: "/Attendance");
    }

    private async Task<DashboardChart> BuildMarksHistogramAsync(int[] campuses, DashboardFilterInput filter, int[]? subjectIds, CancellationToken token)
    {
        var query = ScopedMarks(campuses, filter).Where(m => m.Exam.Status == ExamLifecycle.Published && !m.IsWithheld);
        if (subjectIds != null) query = query.Where(m => subjectIds.Contains(m.SubjectId));
        var rows = await query.Select(m => m.Score * 100m / m.Exam.MaximumMarks).GroupBy(p => p < 40 ? 0 : p < 50 ? 1 : p < 60 ? 2 : p < 70 ? 3 : p < 80 ? 4 : p < 90 ? 5 : 6)
            .Select(g => new { Bucket = g.Key, Count = g.Count() }).ToListAsync(token);
        string[] labels = ["0–39", "40–49", "50–59", "60–69", "70–79", "80–89", "90–100"];
        return Chart("marks-histogram", "Score distribution", "Published, non-withheld score percentages.", "bar", labels,
            [Series("Results", Enumerable.Range(0, labels.Length).Select(i => (decimal?)(rows.FirstOrDefault(x => x.Bucket == i)?.Count ?? 0)), "primary")], url: "/Mark");
    }

    private async Task<DashboardChart> BuildStudentAttendanceTrendAsync(int studentId, DashboardDateRange range, CancellationToken token)
    {
        var rows = await _db.Attendances.AsNoTracking().Where(a => a.StudentId == studentId && a.Date >= range.FromUtc && a.Date < range.ToExclusiveUtc)
            .GroupBy(a => new { a.Date.Year, a.Date.Month }).Select(g => new { g.Key.Year, g.Key.Month, Rate = g.Count(a => a.Status == "Present" || a.Status == "Late" || a.Status == "Excused") * 100m / g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month).ToListAsync(token);
        return Chart("my-attendance", "Monthly attendance", "Your recorded attendance by month.", "line", rows.Select(x => new DateTime(x.Year, x.Month, 1).ToString("MMM yy")).ToArray(), [Series("Attendance", rows.Select(x => (decimal?)x.Rate), "success", fill: true)], "percent");
    }

    private async Task<DashboardChart> BuildStudentSubjectPerformanceAsync(int studentId, CancellationToken token)
    {
        var rows = await _db.Marks.AsNoTracking().Where(m => m.StudentId == studentId && m.Exam.Status == ExamLifecycle.Published && m.IsApproved && !m.IsWithheld)
            .GroupBy(m => new { m.SubjectId, m.Subject.Name }).Select(g => new { g.Key.Name, Average = g.Average(m => m.Score * 100m / m.Exam.MaximumMarks) })
            .OrderBy(x => x.Name).ToListAsync(token);
        return Chart("my-subject-performance", "Subject performance", "Your approved, published result average by subject.", "bar", rows.Select(x => x.Name).ToArray(), [Series("Average", rows.Select(x => (decimal?)x.Average), "primary")], "percent", horizontal: true);
    }

    private async Task<DashboardChart> BuildStudentAssignmentStatusAsync(int studentId, int[] subjectIds, CancellationToken token)
    {
        var visible = _db.Assignments.AsNoTracking().VisibleAt(DateTime.UtcNow).Where(a => subjectIds.Contains(a.SubjectId) && a.DueUtc >= DateTime.UtcNow);
        var total = await visible.CountAsync(token);
        var submitted = await visible.CountAsync(a => a.Submissions.Any(s => s.StudentId == studentId), token);
        var graded = await visible.CountAsync(a => a.Submissions.Any(s => s.StudentId == studentId && s.Score.HasValue), token);
        return Chart("my-assignments", "Assignment completion", "Your visible assignment submission and grading status.", "doughnut", ["Not submitted", "Submitted", "Graded"],
            [Series("Assignments", [(decimal?)Math.Max(0, total - submitted), Math.Max(0, submitted - graded), graded], "primary")]);
    }

    private async Task<DashboardChart> BuildStudentFeeStatusAsync(int studentId, CancellationToken token)
    {
        var totals = await _db.StudentInvoices.AsNoTracking().Where(i => i.StudentId == studentId && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft)
            .GroupBy(_ => 1).Select(g => new { Billed = g.Sum(i => i.TotalAmount), Aid = g.Sum(i => i.AidAmount), Paid = g.Sum(i => i.PaidAmount) }).SingleOrDefaultAsync(token);
        var outstanding = totals == null ? 0 : Math.Max(0, totals.Billed - totals.Aid - totals.Paid);
        return Chart("my-fees", "Fee position", "Your issued invoice allocations, aid and outstanding balance.", "doughnut", ["Paid", "Aid", "Outstanding"],
            [Series("Amount", [(decimal?)(totals?.Paid ?? 0), totals?.Aid ?? 0, outstanding], "info")], "currency");
    }

    private async Task<DashboardChart> BuildPaymentMethodsAsync(int[] campuses, DashboardFilterInput filter, DashboardDateRange range, CancellationToken token)
    {
        var rows = await ScopedTransactions(campuses, filter).Where(t => t.Status == FinancialTransactionStatus.Completed && t.Type == FinancialTransactionType.Payment && t.CreatedUtc >= range.FromUtc && t.CreatedUtc < range.ToExclusiveUtc)
            .GroupBy(t => t.PaymentMethod).Select(g => new { Method = g.Key, Amount = g.Sum(t => t.Amount) }).OrderByDescending(x => x.Amount).ToListAsync(token);
        return Chart("payment-methods", "Payment methods", "Completed payment amount by method.", "doughnut", rows.Select(x => x.Method).ToArray(), [Series("Amount", rows.Select(x => (decimal?)x.Amount), "primary")], "currency", url: "/Finance");
    }

    private async Task<DashboardChart> BuildOutstandingAgingAsync(int[] campuses, DashboardFilterInput filter, CancellationToken token)
    {
        var today = DateTime.UtcNow.Date;
        var rows = await ScopedInvoices(campuses, filter).Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Paid)
            .Select(i => new { i.DueUtc, Balance = i.TotalAmount - i.AidAmount - i.PaidAmount }).Where(i => i.Balance > 0)
            .GroupBy(i => i.DueUtc >= today ? 0 : i.DueUtc >= today.AddDays(-30) ? 1 : i.DueUtc >= today.AddDays(-60) ? 2 : i.DueUtc >= today.AddDays(-90) ? 3 : 4)
            .Select(g => new { Bucket = g.Key, Amount = g.Sum(i => i.Balance) }).ToListAsync(token);
        string[] labels = ["Current", "1–30 days", "31–60 days", "61–90 days", "90+ days"];
        return Chart("outstanding-aging", "Outstanding-fee aging", "Open invoice balance grouped by days past due.", "bar", labels,
            [Series("Outstanding", Enumerable.Range(0, labels.Length).Select(i => (decimal?)(rows.FirstOrDefault(x => x.Bucket == i)?.Amount ?? 0)), "warning")], "currency", stacked: true, horizontal: true, url: "/Finance/Invoices");
    }

    private async Task<DashboardChart> BuildInvoiceStatusAsync(int[] campuses, DashboardFilterInput filter, CancellationToken token)
    {
        var rows = await ScopedInvoices(campuses, filter).GroupBy(i => i.Status).Select(g => new { Status = g.Key, Amount = g.Sum(i => i.TotalAmount), Count = g.Count() }).OrderBy(x => x.Status).ToListAsync(token);
        return Chart("invoice-status", "Invoice status", "Authoritative invoice count by lifecycle status.", "bar", rows.Select(x => x.Status.ToString()).ToArray(), [Series("Invoices", rows.Select(x => (decimal?)x.Count), "info")], url: "/Finance/Invoices");
    }

    private async Task<DashboardChart> BuildAdmissionDemandAsync(int[] campuses, DashboardFilterInput filter, DashboardDateRange range, CancellationToken token)
    {
        var rows = await ScopedAdmissions(campuses, filter).Where(a => a.CreatedUtc >= range.FromUtc && a.CreatedUtc < range.ToExclusiveUtc)
            .GroupBy(a => a.ProgramName).Select(g => new { Program = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).Take(15).ToListAsync(token);
        return Chart("admission-demand", "Course demand", "Applications by requested program.", "bar", rows.Select(x => x.Program).ToArray(), [Series("Applications", rows.Select(x => (decimal?)x.Count), "primary")], horizontal: true, url: "/Admissions");
    }

    private async Task<DashboardChart> BuildAdmissionSourcesAsync(int[] campuses, DashboardFilterInput filter, DashboardDateRange range, CancellationToken token)
    {
        var rows = await ScopedAdmissions(campuses, filter).Where(a => a.CreatedUtc >= range.FromUtc && a.CreatedUtc < range.ToExclusiveUtc)
            .GroupBy(a => a.Source).Select(g => new { Source = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).ToListAsync(token);
        return Chart("admission-sources", "Application sources", "Applications by recorded source.", "doughnut", rows.Select(x => x.Source).ToArray(), [Series("Applications", rows.Select(x => (decimal?)x.Count), "info")]);
    }

    private async Task<DashboardChart> BuildLibraryTrendAsync(int[] campuses, DashboardDateRange range, CancellationToken token)
    {
        var rows = await ScopedLoans(campuses).Where(l => l.IssuedUtc >= range.FromUtc && l.IssuedUtc < range.ToExclusiveUtc)
            .GroupBy(l => l.IssuedUtc.Date).Select(g => new { Day = g.Key, Borrowed = g.Count(), Returned = g.Count(l => l.ReturnedUtc != null) }).OrderBy(x => x.Day).ToListAsync(token);
        return Chart("library-trend", "Borrow and return trend", "Scoped circulation by issue date.", "line", rows.Select(x => x.Day.ToString("dd MMM")).ToArray(),
            [Series("Borrowed", rows.Select(x => (decimal?)x.Borrowed), "primary", fill: true), Series("Returned", rows.Select(x => (decimal?)x.Returned), "success")]);
    }

    private async Task<DashboardChart> BuildOverdueAgingAsync(int[] campuses, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var rows = await ScopedLoans(campuses).Where(l => l.ReturnedUtc == null && l.DueUtc < now)
            .GroupBy(l => l.DueUtc >= now.AddDays(-7) ? 0 : l.DueUtc >= now.AddDays(-30) ? 1 : l.DueUtc >= now.AddDays(-60) ? 2 : 3)
            .Select(g => new { Bucket = g.Key, Count = g.Count() }).ToListAsync(token);
        string[] labels = ["1–7 days", "8–30 days", "31–60 days", "60+ days"];
        return Chart("overdue-aging", "Overdue aging", "Unreturned scoped loans by days overdue.", "bar", labels,
            [Series("Loans", Enumerable.Range(0, labels.Length).Select(i => (decimal?)(rows.FirstOrDefault(x => x.Bucket == i)?.Count ?? 0)), "danger")], horizontal: true);
    }

    private async Task<DashboardChart> BuildLibraryCategoriesAsync(int[] campuses, DashboardDateRange range, CancellationToken token)
    {
        var rows = await ScopedLoans(campuses).Where(l => l.IssuedUtc >= range.FromUtc && l.IssuedUtc < range.ToExclusiveUtc)
            .GroupBy(l => l.LibraryBookCopy.LibraryBook.Category ?? "Uncategorized").Select(g => new { Category = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(12).ToListAsync(token);
        return Chart("library-categories", "Borrowing by category", "Scoped loan activity by recorded book category.", "bar", rows.Select(x => x.Category).ToArray(), [Series("Loans", rows.Select(x => (decimal?)x.Count), "info")], horizontal: true);
    }

    private async Task<DashboardChart> BuildLibraryConditionAsync(int[] campuses, CancellationToken token)
    {
        var scopedCopyIds = ScopedLoans(campuses).Select(l => l.LibraryBookCopyId).Distinct();
        var rows = await _db.LibraryBookCopies.AsNoTracking().Where(c => scopedCopyIds.Contains(c.Id)).GroupBy(c => c.Condition)
            .Select(g => new { Condition = g.Key, Count = g.Count() }).OrderBy(x => x.Condition).ToListAsync(token);
        return Chart("library-condition", "Item condition", "Condition of items represented in scoped circulation.", "doughnut", rows.Select(x => x.Condition.ToString()).ToArray(), [Series("Items", rows.Select(x => (decimal?)x.Count), "warning")]);
    }

    private async Task<DashboardChart> BuildGradeDistributionAsync(int[] campuses, DashboardFilterInput filter, CancellationToken token)
    {
        var rows = await ScopedMarks(campuses, filter).Where(m => m.Exam.Status == ExamLifecycle.Published && !m.IsWithheld)
            .GroupBy(m => m.Grade).Select(g => new { Grade = g.Key, Count = g.Count() }).OrderBy(x => x.Grade).ToListAsync(token);
        return Chart("grade-distribution", "Grade distribution", "Published, non-withheld results by recorded grade.", "bar", rows.Select(x => string.IsNullOrWhiteSpace(x.Grade) ? "Ungraded" : x.Grade).ToArray(), [Series("Results", rows.Select(x => (decimal?)x.Count), "primary")]);
    }

    private async Task<DashboardChart> BuildRoomUtilizationAsync(int[] campuses, CancellationToken token)
    {
        var rows = await _db.Rooms.AsNoTracking().Where(r => r.IsActive && r.CampusId.HasValue && campuses.Contains(r.CampusId.Value))
            .Select(r => new { r.Name, r.Capacity, Periods = _db.TimetableEntries.Count(t => t.RoomId == r.Id && !t.IsCancelled) })
            .OrderByDescending(x => x.Periods).Take(20).ToListAsync(token);
        return Chart("room-utilization", "Room utilization", "Active timetable periods and capacity for campus-tagged rooms.", "bar", rows.Select(x => x.Name).ToArray(),
            [Series("Periods", rows.Select(x => (decimal?)x.Periods), "primary"), Series("Capacity", rows.Select(x => (decimal?)x.Capacity), "info")]);
    }

    private async Task<DashboardChart> BuildTicketStatusAsync(int[] campuses, CancellationToken token)
    {
        var rows = await ScopedServiceTickets(campuses).GroupBy(t => t.Status).Select(g => new { Status = g.Key, Count = g.Count() }).OrderBy(x => x.Status).ToListAsync(token);
        return Chart("ticket-status", "Service request status", "Scoped campus-service requests by status.", "doughnut", rows.Select(x => x.Status).ToArray(), [Series("Requests", rows.Select(x => (decimal?)x.Count), "info")]);
    }

    private async Task<DashboardChart> BuildTicketAgingAsync(int[] campuses, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var rows = await ScopedServiceTickets(campuses).Where(t => t.Status != "Resolved" && t.Status != "Closed")
            .GroupBy(t => t.CreatedUtc >= now.AddDays(-7) ? 0 : t.CreatedUtc >= now.AddDays(-30) ? 1 : t.CreatedUtc >= now.AddDays(-60) ? 2 : 3)
            .Select(g => new { Bucket = g.Key, Count = g.Count() }).ToListAsync(token);
        string[] labels = ["0–7 days", "8–30 days", "31–60 days", "60+ days"];
        return Chart("ticket-aging", "Request aging", "Open service requests by age.", "bar", labels,
            [Series("Requests", Enumerable.Range(0, labels.Length).Select(i => (decimal?)(rows.FirstOrDefault(x => x.Bucket == i)?.Count ?? 0)), "warning")], horizontal: true);
    }

    private async Task<DashboardChart> BuildTimetableDensityAsync(int[] campuses, CancellationToken token)
    {
        var scopedClassIds = ScopedClasses(campuses, new DashboardFilterInput()).Select(c => c.Id);
        var rows = await _db.TimetableEntries.AsNoTracking().Where(t => scopedClassIds.Contains(t.ClassId) && !t.IsCancelled)
            .GroupBy(t => t.DayOfWeek).Select(g => new { Day = g.Key, Count = g.Count() }).OrderBy(x => x.Day).ToListAsync(token);
        var days = Enumerable.Range(1, 7).ToArray();
        return Chart("timetable-density", "Scheduling density", "Active timetable periods by weekday.", "bar", days.Select(d => ((DayOfWeek)(d % 7)).ToString()).ToArray(),
            [Series("Periods", days.Select(d => (decimal?)(rows.FirstOrDefault(x => x.Day == d)?.Count ?? 0)), "primary")]);
    }

    // Explainable lists never make automated disciplinary decisions.
    private async Task<IReadOnlyList<DashboardRiskItem>> DepartmentRisksAsync(int[] campuses, int departmentId, DashboardDateRange range, CancellationToken token)
    {
        var rows = await ScopedAttendance(campuses, new DashboardFilterInput { DepartmentId = departmentId })
            .Where(a => a.Date >= range.FromUtc && a.Date < range.ToExclusiveUtc)
            .GroupBy(a => new { a.StudentId, a.Student.FullName }).Select(g => new { g.Key.StudentId, g.Key.FullName, Total = g.Count(), Present = g.Count(a => a.Status == "Present" || a.Status == "Late" || a.Status == "Excused") })
            .Where(x => x.Total >= 3 && x.Present * 100m / x.Total < 75m).OrderBy(x => x.FullName).Take(8).ToListAsync(token);
        return rows.Select(x => new DashboardRiskItem(x.FullName, $"Attendance is {DashboardMath.Percentage(x.Present, x.Total):0.0}% across {x.Total} recorded sessions; review context before action.", "warning", $"/Students/Details/{x.StudentId}")).ToArray();
    }

    private async Task<IReadOnlyList<DashboardRiskItem>> ProfessorRisksAsync(int[] campuses, int[] classIds, int[] subjectIds, DashboardDateRange range, CancellationToken token)
    {
        var rows = await ScopedAttendance(campuses, new DashboardFilterInput()).Where(a => classIds.Contains(a.Student.ClassId) && a.Date >= range.FromUtc && a.Date < range.ToExclusiveUtc)
            .GroupBy(a => new { a.StudentId, a.Student.FullName }).Select(g => new { g.Key.StudentId, g.Key.FullName, Total = g.Count(), Present = g.Count(a => a.Status == "Present" || a.Status == "Late" || a.Status == "Excused") })
            .Where(x => x.Total >= 3 && x.Present * 100m / x.Total < 75m).OrderBy(x => x.FullName).Take(8).ToListAsync(token);
        return rows.Select(x => new DashboardRiskItem(x.FullName, $"Attendance is {DashboardMath.Percentage(x.Present, x.Total):0.0}% in assigned classes; this is a support signal, not a disciplinary decision.", "warning")).ToArray();
    }

    private async Task<IReadOnlyList<DashboardRiskItem>> StudentRisksAsync(int studentId, int[] subjectIds, DashboardDateRange range, CancellationToken token)
    {
        List<DashboardRiskItem> result = [];
        var totals = await _db.Attendances.AsNoTracking().Where(a => a.StudentId == studentId && a.Date >= range.FromUtc && a.Date < range.ToExclusiveUtc)
            .GroupBy(_ => 1).Select(g => new { Total = g.Count(), Present = g.Count(a => a.Status == "Present" || a.Status == "Late" || a.Status == "Excused") }).SingleOrDefaultAsync(token);
        if (totals is { Total: >= 3 } && DashboardMath.Percentage(totals.Present, totals.Total) < 75)
            result.Add(new("Attendance needs attention", $"Your attendance is {DashboardMath.Percentage(totals.Present, totals.Total):0.0}% in this period. Contact your academic team if records need clarification.", "warning"));
        var missing = await _db.Assignments.AsNoTracking().VisibleAt(DateTime.UtcNow).Where(a => subjectIds.Contains(a.SubjectId) && a.DueUtc < DateTime.UtcNow && !a.Submissions.Any(s => s.StudentId == studentId)).CountAsync(token);
        if (missing > 0) result.Add(new("Missing coursework", $"{missing} visible assignments are past due without a recorded submission.", "danger", "/Assignment"));
        return result;
    }

    private async Task<IReadOnlyList<DashboardRiskItem>> SystemAlertsAsync(DashboardDateRange range, CancellationToken token)
    {
        var deliveryFailures = await _db.OutboxMessages.AsNoTracking()
            .CountAsync(o => o.IsDeadLetter || o.AttemptCount >= 5 && o.ProcessedUtc == null, token);
        var failedSignIns = await _db.LoginHistories.AsNoTracking()
            .CountAsync(l => !l.Succeeded && l.CreatedUtc >= range.FromUtc && l.CreatedUtc < range.ToExclusiveUtc, token);
        return
        [
            new("Background delivery failures", $"{deliveryFailures} messages require operational review.", "warning"),
            new("Failed sign-ins", $"{failedSignIns} failed attempts in the selected period.", "danger", "/SuperAdmin/LoginHistory")
        ];
    }

    private async Task<IReadOnlyList<DashboardRiskItem>> CampusAlertsAsync(int[] campuses, CancellationToken token)
    {
        var openHighPriority = await ScopedServiceTickets(campuses)
            .CountAsync(t => t.Priority == "High" && t.Status != "Resolved" && t.Status != "Closed", token);
        return [new("Open high-priority requests", $"{openHighPriority} high-priority campus requests are unresolved.", "warning", "/Operations")];
    }

    private async Task<IReadOnlyList<DashboardRiskItem>> FinanceAlertsAsync(int[] campuses, DashboardFilterInput filter, DashboardDateRange range, CancellationToken token)
    {
        var reversals = await ScopedTransactions(campuses, filter).CountAsync(t =>
            t.Status == FinancialTransactionStatus.Completed &&
            (t.Type == FinancialTransactionType.Refund || t.Type == FinancialTransactionType.Reversal) &&
            Math.Abs(t.Amount) >= 10000 && t.CreatedUtc >= range.FromUtc && t.CreatedUtc < range.ToExclusiveUtc, token);
        return [new("High-value reversals", $"{reversals} completed entries require routine review.", "warning", "/Finance")];
    }

    private async Task<IReadOnlyList<DashboardRiskItem>> AdmissionAlertsAsync(int[] campuses, DashboardFilterInput filter, CancellationToken token)
    {
        var pending = await ScopedAdmissions(campuses, filter)
            .SelectMany(a => a.Documents)
            .CountAsync(d => d.Status == DocumentVerificationStatus.Pending, token);
        return [new("Pending documents", $"{pending} documents await verification.", "warning", "/Admissions")];
    }

    private async Task<IReadOnlyList<DashboardRiskItem>> LibraryAlertsAsync(int[] campuses, CancellationToken token)
    {
        var overdue = await ScopedLoans(campuses)
            .CountAsync(l => l.ReturnedUtc == null && l.DueUtc < DateTime.UtcNow.AddDays(-60), token);
        return [new("Long-overdue loans", $"{overdue} items are overdue by more than 60 days.", "danger", "/Library")];
    }

    private async Task<IReadOnlyList<DashboardRiskItem>> ExaminationAlertsAsync(int[] campuses, DashboardFilterInput filter, CancellationToken token)
    {
        var withheld = await ScopedMarks(campuses, filter).CountAsync(m => m.IsWithheld, token);
        return [new("Withheld results", $"{withheld} result records are withheld and excluded from student analytics.", "warning", "/Mark")];
    }

    private async Task<IReadOnlyList<DashboardRiskItem>> OperationsAlertsAsync(int[] campuses, CancellationToken token)
    {
        var openHighPriority = await ScopedServiceTickets(campuses)
            .CountAsync(t => t.Priority == "High" && t.Status != "Resolved" && t.Status != "Closed", token);
        return [new("High-priority requests", $"{openHighPriority} requests require attention.", "danger", "/Operations")];
    }

    private async Task<IReadOnlyList<DashboardRiskItem>> ProfessorTimelineAsync(int teacherId, int[] subjectIds, CancellationToken token)
    {
        var timetable = await _db.TimetableEntries.AsNoTracking().Where(t => t.TeacherId == teacherId && !t.IsCancelled).OrderBy(t => t.DayOfWeek).ThenBy(t => t.StartsAt).Take(5)
            .Select(t => new DashboardRiskItem(t.Subject!.Name, $"{((DayOfWeek)(t.DayOfWeek % 7))} · {t.StartsAt:hh\\:mm}–{t.EndsAt:hh\\:mm}", "info", null)).ToListAsync(token);
        var deadlines = await _db.Assignments.AsNoTracking().Where(a => subjectIds.Contains(a.SubjectId) && a.Status == "Published" && a.DueUtc >= DateTime.UtcNow).OrderBy(a => a.DueUtc).Take(5)
            .Select(a => new DashboardRiskItem(a.Title, $"Due {a.DueUtc:u}", "warning", $"/Assignment/Details/{a.Id}")).ToListAsync(token);
        return timetable.Concat(deadlines).Take(8).ToArray();
    }

    private async Task<IReadOnlyList<DashboardRiskItem>> StudentTimelineAsync(int studentId, int classId, int[] subjectIds, CancellationToken token)
    {
        var assignments = await _db.Assignments.AsNoTracking().VisibleAt(DateTime.UtcNow).Where(a => subjectIds.Contains(a.SubjectId) && a.DueUtc >= DateTime.UtcNow).OrderBy(a => a.DueUtc).Take(6)
            .Select(a => new DashboardRiskItem(a.Title, $"Assignment due {a.DueUtc:u}", "warning", $"/Assignment/Details/{a.Id}")).ToListAsync(token);
        var exams = await _db.Exams.AsNoTracking().Where(e => e.Status != ExamLifecycle.Draft &&
                e.ExamDate >= DateTime.UtcNow && e.Marks.Any(m => m.StudentId == studentId))
            .OrderBy(e => e.ExamDate).Take(4)
            .Select(e => new DashboardRiskItem(e.Name, $"Exam on {e.ExamDate:dd MMM yyyy}", "info", null)).ToListAsync(token);
        return assignments.Concat(exams).Take(8).ToArray();
    }
}
