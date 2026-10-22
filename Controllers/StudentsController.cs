using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;

namespace Student_Management_System.Controllers
{
    [Authorize]
    public class StudentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPrivateFileStorage _privateFiles;
        private readonly AuditService _audit;
        private readonly ILogger<StudentsController> _logger;
        private readonly IResultService _results;
        private readonly IStudentAccountService _accounts;

        public StudentsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IPrivateFileStorage privateFiles,
            AuditService audit,
            ILogger<StudentsController> logger,
            IResultService results,
            IStudentAccountService accounts)
        {
            _context = context;
            _userManager = userManager;
            _privateFiles = privateFiles;
            _audit = audit;
            _logger = logger;
            _results = results;
            _accounts = accounts;
        }

        // ================= INDEX =================
        [Authorize(Roles = "Admin,SuperAdmin,HOD")]
        public async Task<IActionResult> Index([FromQuery] ListQuery request, CancellationToken cancellationToken)
        {
            request.Normalize();
            var query = _context.Students
                .Include(s => s.Class)
                .Include(s => s.Section)
                .Include(s => s.Department)
                .Include(s => s.Semester)
                .Include(s => s.User)
                .AsNoTracking()
                .Where(s => !s.IsArchived)
                .AsQueryable();
            if (User.IsInRole("HOD"))
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var departmentId = await _context.Teachers.Where(t => t.UserId == userId && t.TeacherType == TeacherType.HOD)
                    .Select(t => t.DepartmentId).SingleOrDefaultAsync();
                if (!departmentId.HasValue) return Forbid();
                var campusIds = _context.UserCampusMemberships.AsNoTracking()
                    .Where(m => m.UserId == userId && m.IsActive)
                    .Select(m => m.CampusId);
                query = query.Where(s => s.DepartmentId == departmentId &&
                    s.UserId != null && _context.UserCampusMemberships.Any(m =>
                        m.UserId == s.UserId && m.IsActive && campusIds.Contains(m.CampusId)));
            }
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = request.Search;
                query = query.Where(s => s.FullName.Contains(term) || s.Email.Contains(term) ||
                    (s.Phone != null && s.Phone.Contains(term)));
            }
            if (request.DepartmentId.HasValue) query = query.Where(s => s.DepartmentId == request.DepartmentId);
            if (request.ClassId.HasValue) query = query.Where(s => s.ClassId == request.ClassId);
            if (request.SectionId.HasValue) query = query.Where(s => s.SectionId == request.SectionId);
            if (request.SemesterId.HasValue) query = query.Where(s => s.SemesterId == request.SemesterId);
            if (request.Status == "active")
                query = query.Where(s => s.User != null && (!s.User.LockoutEnd.HasValue || s.User.LockoutEnd <= DateTimeOffset.UtcNow));
            else if (request.Status == "inactive")
                query = query.Where(s => s.User == null || (s.User.LockoutEnd.HasValue && s.User.LockoutEnd > DateTimeOffset.UtcNow));
            else if (request.Status == "unassigned")
                query = query.Where(s => s.DepartmentId == null || s.SemesterId == null);
            else if (request.Status == "low-attendance")
            {
                var lowAttendanceStudentIds = _context.Attendances.AsNoTracking()
                    .GroupBy(a => a.StudentId)
                    .Where(g => g.Count() > 0 && (g.Count(a => a.Status == "Present") * 100.0 / g.Count()) < 75)
                    .Select(g => g.Key);
                query = query.Where(s => lowAttendanceStudentIds.Contains(s.Id));
            }

            var filteredIds = query.Select(s => s.Id);
            var attendanceRates = _context.Attendances.AsNoTracking()
                .Where(a => filteredIds.Contains(a.StudentId))
                .GroupBy(a => a.StudentId)
                .Select(g => new { Total = g.Count(), Present = g.Count(a => a.Status == "Present") });
            var now = DateTimeOffset.UtcNow;
            ViewBag.Stats = new[]
            {
                new StatCardViewModel("Total students", (await query.CountAsync(cancellationToken)).ToString("N0"), "Across the current filters", "fa-solid fa-user-graduate"),
                new StatCardViewModel("Active accounts", (await query.CountAsync(s => s.User != null && (!s.User.LockoutEnd.HasValue || s.User.LockoutEnd <= now), cancellationToken)).ToString("N0"), "Login is available", "fa-solid fa-circle-check", "success", "status", "active"),
                new StatCardViewModel("Inactive accounts", (await query.CountAsync(s => s.User == null || (s.User.LockoutEnd.HasValue && s.User.LockoutEnd > now), cancellationToken)).ToString("N0"), "Locked or not linked", "fa-solid fa-user-lock", "warning", "status", "inactive"),
                new StatCardViewModel("Low attendance", (await attendanceRates.CountAsync(x => x.Total > 0 && (x.Present * 100.0 / x.Total) < 75, cancellationToken)).ToString("N0"), "Below the 75% threshold", "fa-solid fa-triangle-exclamation", "danger", "status", "low-attendance")
            };
            ViewBag.Departments = await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(cancellationToken);
            ViewBag.Classes = await _context.Classes.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
            ViewBag.Sections = await _context.Sections.AsNoTracking().OrderBy(s => s.Name).ToListAsync(cancellationToken);
            ViewBag.Semesters = await _context.Semesters.AsNoTracking().OrderBy(s => s.Number).ToListAsync(cancellationToken);

            query = (request.Sort?.ToLowerInvariant(), request.Direction) switch
            {
                ("email", "desc") => query.OrderByDescending(s => s.Email),
                ("email", _) => query.OrderBy(s => s.Email),
                ("class", "desc") => query.OrderByDescending(s => s.Class!.Name).ThenBy(s => s.FullName),
                ("class", _) => query.OrderBy(s => s.Class!.Name).ThenBy(s => s.FullName),
                ("department", "desc") => query.OrderByDescending(s => s.Department!.Name).ThenBy(s => s.FullName),
                ("department", _) => query.OrderBy(s => s.Department!.Name).ThenBy(s => s.FullName),
                ("name", "desc") => query.OrderByDescending(s => s.FullName),
                _ => query.OrderBy(s => s.FullName)
            };
            return View(await query.ToPagedResultAsync(request, cancellationToken));
        }

        // ================= DETAILS =================
        public async Task<IActionResult> Details(
            int? id,
            CancellationToken cancellationToken)
        {
            var allowedStudents = await BuildAllowedStudentsQueryAsync(
                id,
                cancellationToken);
            if (allowedStudents == null) return NotFound();
            var student = await allowedStudents
                .Include(s => s.User)
                .Include(s => s.Class).ThenInclude(c => c!.Department)
                .Include(s => s.Section)
                .Include(s => s.Department)
                .Include(s => s.AcademicProgram)
                .Include(s => s.Semester).ThenInclude(s => s!.AcademicYear)
                .SingleOrDefaultAsync(cancellationToken);
            if (student == null) return NotFound();
            var viewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var professorView = User.IsInRole("Professor");

            var attendance = await _context.Attendances.AsNoTracking()
                .Include(a => a.Subject).Include(a => a.Teacher)
                .Where(a => a.StudentId == student.Id &&
                    (!professorView ||
                     a.Teacher!.UserId == viewerId ||
                     a.Subject!.TeacherAssignments.Any(t => t.Teacher.UserId == viewerId)))
                .OrderByDescending(a => a.Date)
                .ToListAsync(cancellationToken);
            var marksQuery = _context.Marks.AsNoTracking()
                .Include(m => m.Subject).Include(m => m.Exam).Include(m => m.EnteredByTeacher)
                .Where(m => m.StudentId == student.Id);
            marksQuery = await _results.ScopeAsync(marksQuery, User, cancellationToken);
            var marks = await marksQuery
                .OrderByDescending(m => m.Exam.ExamDate)
                .ToListAsync(cancellationToken);
            var enrollments = await _context.StudentClassEnrollments.AsNoTracking()
                .Include(e => e.Class).Include(e => e.Section).Include(e => e.AcademicYear)
                .Where(e => e.StudentId == student.Id)
                .OrderByDescending(e => e.AcademicYear.StartsOn)
                .ToListAsync(cancellationToken);
            var subjects = await _context.StudentSubjectEnrollments.AsNoTracking()
                .Include(e => e.Subject).ThenInclude(s => s.Teacher)
                .Where(e => e.StudentId == student.Id &&
                    (!professorView ||
                     e.Subject.TeacherAssignments.Any(t => t.Teacher.UserId == viewerId)))
                .OrderBy(e => e.Subject.Name)
                .ToListAsync(cancellationToken);
            var assignmentQuery = _context.Assignments.AsNoTracking().Include(a => a.Subject)
                .Where(a =>
                    (a.Subject!.ClassId == student.ClassId ||
                     a.Subject.StudentEnrollments.Any(e => e.StudentId == student.Id)) &&
                    (!professorView ||
                     a.Subject.TeacherAssignments.Any(t => t.Teacher.UserId == viewerId)));
            if (User.IsInRole("Student"))
                assignmentQuery = assignmentQuery.VisibleAt(DateTime.UtcNow);
            var assignments = await assignmentQuery
                .OrderByDescending(a => a.DueUtc)
                .ToListAsync(cancellationToken);
            var submissions = await _context.AssignmentSubmissions.AsNoTracking()
                .Where(s => s.StudentId == student.Id &&
                            assignments.Select(a => a.Id).Contains(s.AssignmentId))
                .ToListAsync(cancellationToken);
            var timetable = await _context.TimetableEntries.AsNoTracking()
                .Include(t => t.Subject).Include(t => t.Teacher)
                .Where(t => t.ClassId == student.ClassId)
                .OrderBy(t => t.DayOfWeek)
                .ThenBy(t => t.StartsAt)
                .ToListAsync(cancellationToken);
            var isSelf = !string.IsNullOrWhiteSpace(student.UserId) &&
                         student.UserId == viewerId;
            var guardianLink = User.IsInRole("Guardian")
                ? await _context.GuardianStudentLinks.AsNoTracking()
                    .SingleOrDefaultAsync(l =>
                        l.StudentId == student.Id &&
                        l.UnlinkedUtc == null &&
                        l.GuardianProfile.UserId == viewerId,
                        cancellationToken)
                : null;
            var canViewPersonal = User.IsInRole("SuperAdmin") ||
                                  User.IsInRole("Admin") ||
                                  User.IsInRole("HOD") ||
                                  isSelf ||
                                  guardianLink != null;
            var canViewFinance = User.IsInRole("SuperAdmin") ||
                                 User.IsInRole("Admin") ||
                                 User.IsInRole("Accountant") ||
                                 isSelf ||
                                 guardianLink?.CanViewFinance == true;
            var canViewSecurity = User.IsInRole("SuperAdmin") ||
                                  User.IsInRole("Admin") ||
                                  isSelf;
            var guardianLinks = canViewPersonal
                ? await _context.GuardianStudentLinks.AsNoTracking()
                    .Include(l => l.GuardianProfile).ThenInclude(g => g.User)
                    .Where(l => l.StudentId == student.Id && l.UnlinkedUtc == null)
                    .OrderByDescending(l => l.IsPrimary)
                    .ToListAsync(cancellationToken)
                : [];
            var financeTransactions = canViewFinance
                ? await _context.FinancialTransactions.AsNoTracking()
                    .Include(t => t.Fee)
                    .Where(t => t.StudentId == student.Id)
                    .OrderByDescending(t => t.CreatedUtc)
                    .Take(250)
                    .ToListAsync(cancellationToken)
                : [];
            var invoices = canViewFinance
                ? await _context.StudentInvoices.AsNoTracking()
                    .Include(i => i.AcademicYear).Include(i => i.Lines)
                    .Where(i => i.StudentId == student.Id)
                    .OrderByDescending(i => i.IssuedUtc)
                    .Take(100)
                    .ToListAsync(cancellationToken)
                : [];
            var libraryLoans = !professorView ? await _context.LibraryLoans.AsNoTracking()
                .Include(l => l.LibraryBookCopy).ThenInclude(c => c.LibraryBook)
                .Where(l => l.BorrowerUserId == student.UserId)
                .OrderByDescending(l => l.IssuedUtc)
                .Take(100)
                .ToListAsync(cancellationToken) : [];
            var leaveRequests = !professorView ? await _context.LeaveRequests.AsNoTracking()
                .Where(l => l.RequesterUserId == student.UserId)
                .OrderByDescending(l => l.CreatedUtc)
                .Take(100)
                .ToListAsync(cancellationToken) : [];
            var documents = canViewPersonal
                ? await _context.AdmissionDocuments.AsNoTracking()
                    .Where(d => d.AdmissionApplication.EnrolledStudentId == student.Id)
                    .OrderByDescending(d => d.CreatedUtc)
                    .ToListAsync(cancellationToken)
                : [];
            var notifications = canViewSecurity && !string.IsNullOrWhiteSpace(student.UserId)
                ? await _context.UserNotifications.AsNoTracking()
                    .Where(n => n.UserId == student.UserId)
                    .OrderByDescending(n => n.CreatedUtc)
                    .Take(100)
                    .ToListAsync(cancellationToken)
                : [];
            var activities = canViewSecurity
                ? await _context.AuditLogs.AsNoTracking()
                    .Where(a =>
                        (a.EntityName == nameof(Student) && a.EntityId == student.Id.ToString()) ||
                        (student.UserId != null && a.UserId == student.UserId))
                    .OrderByDescending(a => a.CreatedUtc)
                    .Take(100)
                    .ToListAsync(cancellationToken)
                : [];
            return View(new StudentDetailsViewModel
            {
                Student = student,
                Attendance = attendance,
                Marks = marks,
                Enrollments = enrollments,
                Subjects = subjects,
                Assignments = assignments,
                Submissions = submissions,
                Timetable = timetable,
                LoginHistory = canViewSecurity
                    ? await _context.LoginHistories.AsNoTracking()
                        .Where(l => l.UserId == student.UserId || l.Email == student.Email)
                        .OrderByDescending(l => l.CreatedUtc)
                        .Take(20)
                        .ToListAsync(cancellationToken)
                    : [],
                Guardians = guardianLinks,
                FinanceTransactions = financeTransactions,
                Invoices = invoices,
                LibraryLoans = libraryLoans,
                LeaveRequests = leaveRequests,
                Documents = documents,
                Notifications = notifications,
                ActivityHistory = activities,
                CanViewPersonal = canViewPersonal,
                CanViewFinance = canViewFinance,
                CanViewSecurity = canViewSecurity,
                AttendancePercentage = attendance.Count == 0 ? 0 : attendance.Count(a => a.Status == "Present") * 100.0 / attendance.Count,
                AverageScore = marks.Count == 0 ? 0 : (double)marks.Average(m => m.Score)
            });
        }

        [HttpGet]
        public async Task<IActionResult> Photo(
            int id,
            CancellationToken cancellationToken)
        {
            var allowedStudents = await BuildAllowedStudentsQueryAsync(
                id,
                cancellationToken);
            if (allowedStudents == null) return NotFound();
            var student = await allowedStudents
                .Select(s => new { s.Id, s.PhotoPath })
                .SingleOrDefaultAsync(cancellationToken);
            if (student == null || string.IsNullOrWhiteSpace(student.PhotoPath))
                return NotFound();

            var openedFile = await _privateFiles.OpenReadAsync(
                student.PhotoPath,
                PrivateFileKind.StudentPhoto,
                cancellationToken);
            if (openedFile == null) return NotFound();

            SetPrivateNoStoreHeaders();
            Response.Headers.ContentDisposition =
                $"inline; filename=\"student-{student.Id}-photo{openedFile.Extension}\"";
            return File(
                openedFile.Stream,
                openedFile.ContentType,
                enableRangeProcessing: false);
        }

        public async Task<IActionResult> Transcript(int id, CancellationToken cancellationToken)
        {
            var allowed = await BuildAllowedStudentsQueryAsync(id, cancellationToken);
            if (allowed == null) return NotFound();
            var student = await allowed.Include(s => s.Class).Include(s => s.Department)
                .SingleOrDefaultAsync(cancellationToken);
            if (student == null) return NotFound();
            var marks = await _context.Marks.AsNoTracking()
                .Include(m => m.Exam).Include(m => m.Subject)
                .Where(m => m.StudentId == id && m.IsApproved &&
                            m.Exam.Status == ExamLifecycle.Published)
                .OrderBy(m => m.Exam.ExamDate).ThenBy(m => m.Subject.Name)
                .ToListAsync(cancellationToken);
            var weighted = marks.Count == 0 ? 0 :
                marks.Sum(m => (m.Score / m.Exam.MaximumMarks) * m.Exam.WeightPercent) /
                Math.Max(1, marks.Sum(m => m.Exam.WeightPercent)) * 100m;
            Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
            return View(new TranscriptViewModel
            {
                Student = student,
                Marks = marks,
                WeightedAveragePercent = Math.Round(weighted, 2)
            });
        }

        [Authorize(Roles = "Admin,SuperAdmin,HOD")]
        public async Task<IActionResult> Export([FromQuery] ListQuery request, CancellationToken cancellationToken)
        {
            request.Normalize();
            var query = _context.Students.AsNoTracking()
                .Include(s => s.Department).Include(s => s.Class).Include(s => s.Section).Include(s => s.Semester)
                .AsQueryable();
            if (User.IsInRole("HOD"))
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var departmentId = await _context.Teachers.Where(t => t.UserId == uid && t.TeacherType == TeacherType.HOD)
                    .Select(t => t.DepartmentId).SingleOrDefaultAsync(cancellationToken);
                if (!departmentId.HasValue) return Forbid();
                query = query.Where(s => s.DepartmentId == departmentId);
            }
            if (!string.IsNullOrWhiteSpace(request.Search))
                query = query.Where(s => s.FullName.Contains(request.Search) || s.Email.Contains(request.Search));
            if (request.DepartmentId.HasValue) query = query.Where(s => s.DepartmentId == request.DepartmentId);
            if (request.ClassId.HasValue) query = query.Where(s => s.ClassId == request.ClassId);
            if (request.SectionId.HasValue) query = query.Where(s => s.SectionId == request.SectionId);
            var data = await query.OrderBy(s => s.FullName).ToListAsync(cancellationToken);
            var rows = new List<string[]> { new[] { "Student ID", "Name", "Email", "Phone", "Department", "Class", "Section", "Semester" } };
            rows.AddRange(data.Select(s => new[] { s.Id.ToString(), s.FullName, s.Email, s.Phone ?? "", s.Department?.Name ?? "", s.Class?.Name ?? "", s.Section?.Name ?? "", s.Semester?.Name ?? "" }));
            return File(CsvExport.Create(rows), "text/csv", $"students-{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        // ================= CREATE GET =================
        [Authorize(Roles = "Admin,SuperAdmin,HOD")]
        public IActionResult Create()
        {
            LoadStudentDropdowns();
            return View();
        }

        // ================= CREATE POST =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin,HOD")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> Create(
            [Bind("FullName,Email,Phone,DOB,Address,AdmissionNumber,RollNumber,StudentStatus,AdmissionDate,ExpectedGraduationDate,EmergencyContactName,EmergencyContactPhone,BloodGroup,ClassId,SectionId,DepartmentId,SemesterId,AcademicProgramId")] Student student,
            IFormFile? PhotoFile,
            CancellationToken cancellationToken)
        {
            // 🔥 REMOVE NAVIGATION VALIDATION
            ModelState.Remove("User");
            ModelState.Remove("Class");
            ModelState.Remove("Section");
            ModelState.Remove("Department");
            ModelState.Remove("Semester");
            if (User.IsInRole("HOD"))
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
                student.DepartmentId = await _context.Teachers.Where(t => t.UserId == uid && t.TeacherType == TeacherType.HOD)
                    .Select(t => t.DepartmentId).SingleOrDefaultAsync();
                if (!student.DepartmentId.HasValue ||
                    !await _context.Classes.AnyAsync(c => c.Id == student.ClassId && c.DepartmentId == student.DepartmentId))
                    return Forbid();
            }

            if (!ModelState.IsValid)
            {
                LoadStudentDropdowns(student);
                return View(student);
            }

            // 🔥 CHECK USER EXISTS
            var existingUser = await _userManager.FindByEmailAsync(student.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("", "User already exists with this email");
                LoadStudentDropdowns(student);
                return View(student);
            }

            StoredPrivateFile? storedPhoto = null;
            if (PhotoFile != null)
            {
                try
                {
                    storedPhoto = await _privateFiles.SaveStudentPhotoAsync(
                        PhotoFile,
                        cancellationToken);
                }
                catch (PrivateFileValidationException exception)
                {
                    ModelState.AddModelError(nameof(PhotoFile), exception.Message);
                    LoadStudentDropdowns(student);
                    return View(student);
                }
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            var committed = false;
            ApplicationUser? createdUser = null;
            try
            {
                var result = await strategy.ExecuteAsync<IActionResult>(async () =>
                {
                    await using var transaction =
                        await _context.Database.BeginTransactionAsync(cancellationToken);
                    var user = new ApplicationUser
                    {
                        UserName = student.Email,
                        Email = student.Email,
                        PhoneNumber = student.Phone,
                        FullName = student.FullName
                    };
                    var identityResult = await _userManager.CreateAsync(user);

                    if (!identityResult.Succeeded)
                    {
                        foreach (var error in identityResult.Errors)
                            ModelState.AddModelError("", error.Description);
                        LoadStudentDropdowns(student);
                        return View(student);
                    }

                    var roleResult =
                        await _userManager.AddToRoleAsync(user, "Student");
                    if (!roleResult.Succeeded)
                    {
                        foreach (var error in roleResult.Errors)
                            ModelState.AddModelError("", error.Description);
                        await transaction.RollbackAsync(cancellationToken);
                        LoadStudentDropdowns(student);
                        return View(student);
                    }

                    student.UserId = user.Id;
                    student.PhotoPath = storedPhoto?.StorageKey;

                    _context.Students.Add(student);
                    await _context.SaveChangesAsync(cancellationToken);
                    var currentYear = await _context.AcademicYears
                        .Where(y => y.IsCurrent)
                        .Select(y => (int?)y.Id)
                        .SingleOrDefaultAsync(cancellationToken);
                    if (currentYear.HasValue)
                    {
                        _context.StudentClassEnrollments.Add(
                            new StudentClassEnrollment
                            {
                                StudentId = student.Id,
                                ClassId = student.ClassId,
                                SectionId = student.SectionId,
                                AcademicYearId = currentYear.Value
                            });
                        var subjectIds = await _context.Subjects
                            .Where(s => s.ClassId == student.ClassId)
                            .Select(s => s.Id)
                            .ToListAsync(cancellationToken);
                        _context.StudentSubjectEnrollments.AddRange(
                            subjectIds.Select(subjectId =>
                                new StudentSubjectEnrollment
                                {
                                    StudentId = student.Id,
                                    SubjectId = subjectId
                                }));
                        await _context.SaveChangesAsync(cancellationToken);
                    }

                    await transaction.CommitAsync(cancellationToken);
                    committed = true;
                    createdUser = user;
                    return RedirectToAction(nameof(Index));
                });

                if (committed && createdUser != null)
                {
                    var invitation = await _accounts.CreateActivationAsync(
                        createdUser, cancellationToken);
                    var url = Url.Action("Activate", "Account",
                        new { email = createdUser.Email, token = invitation.RawToken },
                        Request.Scheme);
                    var delivered = !string.IsNullOrWhiteSpace(url) &&
                        await _accounts.SendActivationAsync(
                            createdUser, url, cancellationToken);
                    TempData[delivered ? "Success" : "Warning"] = delivered
                        ? "Student account created and the activation invitation was sent."
                        : "Student account created, but activation email delivery is pending.";
                }
                if (!committed && storedPhoto != null)
                    await TryDeletePrivateFileAsync(
                        storedPhoto.StorageKey,
                        PrivateFileKind.StudentPhoto);
                return result;
            }
            catch
            {
                if (!committed && storedPhoto != null)
                    await TryDeletePrivateFileAsync(
                        storedPhoto.StorageKey,
                        PrivateFileKind.StudentPhoto);
                throw;
            }
        }

        // ================= EDIT GET =================
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var student = await _context.Students.Include(s => s.User).SingleOrDefaultAsync(s => s.Id == id);
            if (student == null) return NotFound();
            LoadStudentDropdowns(student);
            return View(student);
        }

        // ================= EDIT POST =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,FullName,Email,Phone,DOB,Address,AdmissionNumber,RollNumber,StudentStatus,AdmissionDate,ExpectedGraduationDate,EmergencyContactName,EmergencyContactPhone,BloodGroup,ClassId,SectionId,DepartmentId,SemesterId,AcademicProgramId,RowVersion")] Student student,
            IFormFile? PhotoFile,
            CancellationToken cancellationToken)
        {
            if (id != student.Id) return NotFound();

            ModelState.Remove("User"); ModelState.Remove("Class"); ModelState.Remove("Section");
            ModelState.Remove("Department"); ModelState.Remove("Semester");

            var classRecord = await _context.Classes.AsNoTracking().SingleOrDefaultAsync(c => c.Id == student.ClassId);
            if (classRecord == null)
                ModelState.AddModelError(nameof(student.ClassId), "Select a valid class.");
            var sectionValid = await _context.Sections.AsNoTracking()
                .AnyAsync(s => s.Id == student.SectionId && s.ClassId == student.ClassId);
            if (!sectionValid)
                ModelState.AddModelError(nameof(student.SectionId), "The selected section does not belong to this class.");
            if (student.DepartmentId.HasValue && classRecord?.DepartmentId != student.DepartmentId)
                ModelState.AddModelError(nameof(student.DepartmentId), "The selected department must match the class department.");
            if (student.SemesterId.HasValue &&
                !await _context.Semesters.AsNoTracking().AnyAsync(s => s.Id == student.SemesterId &&
                    (!student.DepartmentId.HasValue || s.DepartmentId == student.DepartmentId)))
                ModelState.AddModelError(nameof(student.SemesterId), "Select a semester from the student's department.");

            var duplicateUser = await _userManager.FindByEmailAsync(student.Email);
            var existing = await _context.Students.Include(s => s.User).SingleOrDefaultAsync(s => s.Id == id);
            if (existing == null) return NotFound();
            ConcurrencyToken.Apply(_context, existing, student.RowVersion);
            if (duplicateUser != null && duplicateUser.Id != existing.UserId)
                ModelState.AddModelError(nameof(student.Email), "This email address is already in use.");

            StoredPrivateFile? replacementPhoto = null;
            if (PhotoFile != null && ModelState.IsValid)
            {
                try
                {
                    replacementPhoto = await _privateFiles.SaveStudentPhotoAsync(
                        PhotoFile,
                        cancellationToken);
                }
                catch (PrivateFileValidationException exception)
                {
                    ModelState.AddModelError(nameof(PhotoFile), exception.Message);
                }
            }

            if (ModelState.IsValid)
            {
                var oldPhotoPath = existing.PhotoPath;
                var strategy = _context.Database.CreateExecutionStrategy();
                var committed = false;
                try
                {
                    var result = await strategy.ExecuteAsync<IActionResult>(async () =>
                    {
                        await using var transaction =
                            await _context.Database.BeginTransactionAsync(cancellationToken);
                        if (replacementPhoto != null)
                            existing.PhotoPath = replacementPhoto.StorageKey;
                        existing.FullName = student.FullName;
                        existing.Email = student.Email;
                        existing.Phone = student.Phone;
                        existing.DOB = student.DOB;
                        existing.Address = student.Address;
                        existing.ClassId = student.ClassId;
                        existing.SectionId = student.SectionId;
                        existing.DepartmentId = student.DepartmentId;
                        existing.SemesterId = student.SemesterId;

                        var linkedUser = existing.User;
                        if (linkedUser != null)
                        {
                            linkedUser.FullName = student.FullName;
                            linkedUser.Email = student.Email;
                            linkedUser.UserName = student.Email;
                            linkedUser.NormalizedEmail =
                                _userManager.NormalizeEmail(student.Email);
                            linkedUser.NormalizedUserName =
                                _userManager.NormalizeName(student.Email);
                            linkedUser.PhoneNumber = student.Phone;
                            var identityResult =
                                await _userManager.UpdateAsync(linkedUser);
                            if (!identityResult.Succeeded)
                            {
                                foreach (var error in identityResult.Errors)
                                    ModelState.AddModelError("", error.Description);
                                await transaction.RollbackAsync(cancellationToken);
                                LoadStudentDropdowns(student);
                                return View(student);
                            }
                        }

                        await _context.SaveChangesAsync(cancellationToken);

                        var currentYearId = await _context.AcademicYears
                            .Where(y => y.IsCurrent)
                            .Select(y => (int?)y.Id)
                            .SingleOrDefaultAsync(cancellationToken);
                        if (currentYearId.HasValue)
                        {
                            var enrollment = await _context.StudentClassEnrollments
                                .SingleOrDefaultAsync(
                                    e => e.StudentId == id &&
                                         e.AcademicYearId == currentYearId,
                                    cancellationToken);
                            if (enrollment == null)
                                _context.StudentClassEnrollments.Add(
                                    new StudentClassEnrollment
                                    {
                                        StudentId = id,
                                        AcademicYearId = currentYearId.Value,
                                        ClassId = student.ClassId,
                                        SectionId = student.SectionId,
                                        IsActive = true
                                    });
                            else
                            {
                                enrollment.ClassId = student.ClassId;
                                enrollment.SectionId = student.SectionId;
                                enrollment.IsActive = true;
                            }
                        }

                        var validSubjectIds = await _context.Subjects
                            .Where(s => s.ClassId == student.ClassId)
                            .Select(s => s.Id)
                            .ToListAsync(cancellationToken);
                        var currentSubjectEnrollments =
                            await _context.StudentSubjectEnrollments
                                .Where(e => e.StudentId == id)
                                .ToListAsync(cancellationToken);
                        _context.StudentSubjectEnrollments.RemoveRange(
                            currentSubjectEnrollments.Where(
                                e => !validSubjectIds.Contains(e.SubjectId)));
                        var enrolledIds = currentSubjectEnrollments
                            .Select(e => e.SubjectId)
                            .ToHashSet();
                        _context.StudentSubjectEnrollments.AddRange(
                            validSubjectIds
                                .Where(subjectId => !enrolledIds.Contains(subjectId))
                                .Select(subjectId =>
                                    new StudentSubjectEnrollment
                                    {
                                        StudentId = id,
                                        SubjectId = subjectId
                                    }));

                        await _context.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        committed = true;
                        TempData["Success"] = "Student updated successfully.";
                        return RedirectToAction(nameof(Details), new { id });
                    });

                    if (replacementPhoto != null)
                    {
                        if (committed)
                            await TryDeletePrivateFileAsync(
                                oldPhotoPath,
                                PrivateFileKind.StudentPhoto);
                        else
                            await TryDeletePrivateFileAsync(
                                replacementPhoto.StorageKey,
                                PrivateFileKind.StudentPhoto);
                    }
                    if (committed)
                        await _audit.RecordAsync(
                            "Updated student profile and academic placement",
                            nameof(Student),
                            id);
                    return result;
                }
                catch
                {
                    if (!committed && replacementPhoto != null)
                        await TryDeletePrivateFileAsync(
                            replacementPhoto.StorageKey,
                            PrivateFileKind.StudentPhoto);
                    throw;
                }
            }

            if (replacementPhoto != null)
                await TryDeletePrivateFileAsync(
                    replacementPhoto.StorageKey,
                    PrivateFileKind.StudentPhoto);

            LoadStudentDropdowns(student);
            return View(student);
        }

        // ================= DELETE =================
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();

            return View(student);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> DeleteConfirmed(
            int id,
            string? reason,
            CancellationToken cancellationToken)
        {
            var student = await _context.Students.FindAsync(
                [id],
                cancellationToken);
            if (student == null) return NotFound();
            if (string.IsNullOrWhiteSpace(reason))
            {
                ModelState.AddModelError(nameof(reason), "An archive reason is required.");
                return View("Delete", student);
            }
            student.IsArchived = true;
            student.ArchivedUtc = DateTime.UtcNow;
            student.ArchivedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            student.ArchiveReason = reason.Trim();
            if (!string.IsNullOrEmpty(student.UserId))
            {
                var user = await _userManager.FindByIdAsync(student.UserId);
                if (user != null)
                {
                    user.IsActive = false;
                    await _userManager.UpdateAsync(user);
                    await _userManager.UpdateSecurityStampAsync(user);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await _audit.RecordAsync("Archived Student profile and deactivated login", nameof(Student), id);
            TempData["Success"] = "Student archived. Historical records and the private photo were preserved.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<IQueryable<Student>?> BuildAllowedStudentsQueryAsync(
            int? id,
            CancellationToken cancellationToken)
        {
            IQueryable<Student> allowedStudents =
                _context.Students.AsNoTracking();
            if (User.IsInRole("Admin") || User.IsInRole("SuperAdmin"))
                return allowedStudents.Where(s => id.HasValue && s.Id == id);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (User.IsInRole("HOD"))
            {
                var departmentId = await _context.Teachers
                    .Where(t => t.UserId == userId &&
                                t.TeacherType == TeacherType.HOD)
                    .Select(t => t.DepartmentId)
                    .SingleOrDefaultAsync(cancellationToken);
                if (!id.HasValue || !departmentId.HasValue)
                    return null;
                return allowedStudents.Where(
                    s => s.Id == id &&
                         s.DepartmentId == departmentId);
            }

            if (User.IsInRole("Professor"))
            {
                if (!id.HasValue)
                    return null;
                return allowedStudents.Where(
                    s => s.Id == id &&
                         (s.Class!.TeacherAssignments.Any(
                              a => a.Teacher.UserId == userId) ||
                          s.SubjectEnrollments.Any(
                              e => e.Subject.TeacherAssignments.Any(
                                  a => a.Teacher.UserId == userId))));
            }

            return allowedStudents.Where(s => s.UserId == userId);
        }

        private void SetPrivateNoStoreHeaders()
        {
            Response.Headers.CacheControl =
                "private, no-store, no-cache, max-age=0";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";
            Response.Headers.XContentTypeOptions = "nosniff";
        }

        private async Task TryDeletePrivateFileAsync(
            string? storageKey,
            PrivateFileKind kind)
        {
            try
            {
                await _privateFiles.DeleteAsync(
                    storageKey,
                    kind,
                    CancellationToken.None);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(
                    exception,
                    "The obsolete private file {StorageKey} could not be removed.",
                    storageKey);
            }
        }

        private void LoadStudentDropdowns(Student? selected = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var departmentId = User.IsInRole("HOD")
                ? _context.Teachers.Where(t => t.UserId == userId && t.TeacherType == TeacherType.HOD)
                    .Select(t => t.DepartmentId).SingleOrDefault()
                : selected?.DepartmentId;
            var restrictToDepartment = User.IsInRole("HOD") && departmentId.HasValue;
            ViewBag.Classes = new SelectList(
                _context.Classes.Where(c => !restrictToDepartment || c.DepartmentId == departmentId).OrderBy(c => c.Name),
                "Id", "Name", selected?.ClassId);
            ViewBag.Sections = new SelectList(
                _context.Sections.Where(s => !restrictToDepartment || s.Class.DepartmentId == departmentId).OrderBy(s => s.Name),
                "Id", "Name", selected?.SectionId);
            ViewBag.Departments = new SelectList(
                _context.Departments.Where(d => !restrictToDepartment || d.Id == departmentId).OrderBy(d => d.Name),
                "Id", "Name", departmentId);
            ViewBag.Semesters = new SelectList(
                _context.Semesters.Where(s => !restrictToDepartment || s.DepartmentId == departmentId).OrderBy(s => s.Number),
                "Id", "Name", selected?.SemesterId);
        }
    }
}
