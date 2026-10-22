using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using Student_Management_System.Security;

namespace Student_Management_System.Controllers
{
    [Authorize]
    public class TeachersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditService _audit;
        private readonly IStudentAccountService _accounts;
        private readonly IProfileManagementAuthorizationService _profileAuthorization;

        public TeachersController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            AuditService audit,
            IStudentAccountService accounts,
            IProfileManagementAuthorizationService profileAuthorization)
        {
            _context = context;
            _userManager = userManager;
            _audit = audit;
            _accounts = accounts;
            _profileAuthorization = profileAuthorization;
        }

        // ================= CREATE GET =================
        [Authorize(Roles = "Admin,SuperAdmin")]
        public IActionResult Create()
        {
            ViewBag.Subjects = _context.Subjects.ToList();
            ViewBag.Departments = _context.Departments.ToList();
            return View();
        }

        // ================= CREATE POST =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Create([Bind("FullName,Email,Phone,Qualification,TeacherType,DepartmentId")] Teacher teacher, List<int> SubjectIds)
        {
            // 🔥 REMOVE NAVIGATION VALIDATION
            ModelState.Remove("User");
            ModelState.Remove("Subjects");
            ModelState.Remove("UserId");

            if (!ModelState.IsValid)
            {
                ViewBag.Subjects = _context.Subjects.ToList();
                ViewBag.Departments = _context.Departments.ToList();
                return View(teacher);
            }

            // 🔥 CHECK IF USER ALREADY EXISTS
            var existingUser = await _userManager.FindByEmailAsync(teacher.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("", "User already exists with this email");
                ViewBag.Subjects = _context.Subjects.ToList();
                ViewBag.Departments = _context.Departments.ToList();
                return View(teacher);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            ApplicationUser? createdUser = null;
            var actionResult = await strategy.ExecuteAsync<IActionResult>(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                var user = new ApplicationUser
                {
                    UserName = teacher.Email,
                    Email = teacher.Email,
                    PhoneNumber = teacher.Phone,
                    FullName = teacher.FullName
                };
                var result = await _userManager.CreateAsync(user);

                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }

                    ViewBag.Subjects = _context.Subjects.ToList();
                    ViewBag.Departments = _context.Departments.ToList();
                    return View(teacher);
                }

                // 🔥 ASSIGN ROLE
                var roleResult = await _userManager.AddToRoleAsync(user,
                    teacher.TeacherType == TeacherType.HOD ? "HOD" : "Professor");
                if (!roleResult.Succeeded)
                {
                    foreach (var error in roleResult.Errors)
                        ModelState.AddModelError("", error.Description);
                    await transaction.RollbackAsync();
                    ViewBag.Subjects = _context.Subjects.ToList();
                    ViewBag.Departments = _context.Departments.ToList();
                    return View(teacher);
                }

                // 🔗 LINK TEACHER TO USER
                teacher.UserId = user.Id;

                _context.Teachers.Add(teacher);
                await _context.SaveChangesAsync();

                // 🔥 ASSIGN SUBJECTS
                if (SubjectIds != null && SubjectIds.Any())
                {
                    foreach (var subjectId in SubjectIds)
                    {
                        var subject = await _context.Subjects.FindAsync(subjectId);

                        if (subject != null)
                        {
                            subject.TeacherId = teacher.Id;
                            if (!await _context.TeacherSubjectAssignments.AnyAsync(a => a.TeacherId == teacher.Id && a.SubjectId == subjectId))
                                _context.TeacherSubjectAssignments.Add(new TeacherSubjectAssignment { TeacherId = teacher.Id, SubjectId = subjectId });
                        }
                    }

                    await _context.SaveChangesAsync();
                }
                await transaction.CommitAsync();
                createdUser = user;

                return RedirectToAction(nameof(Index));
            });
            if (createdUser != null)
            {
                var invitation = await _accounts.CreateActivationAsync(createdUser);
                var url = Url.Action("Activate", "Account",
                    new { email = createdUser.Email, token = invitation.RawToken },
                    Request.Scheme);
                var delivered = !string.IsNullOrWhiteSpace(url) &&
                    await _accounts.SendActivationAsync(createdUser, url);
                TempData[delivered ? "Success" : "Warning"] = delivered
                    ? "Teacher account created and the activation invitation was sent."
                    : "Teacher account created, but activation email delivery is pending.";
            }
            return actionResult;
        }

        // ================= INDEX =================
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Index([FromQuery] ListQuery request, CancellationToken cancellationToken)
        {
            request.Normalize();
            var query = _context.Teachers.AsNoTracking()
                .Include(t => t.User).Include(t => t.Department)
                .Include(t => t.ClassAssignments).Include(t => t.SubjectAssignments)
                .Where(t => !t.IsArchived)
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(request.Search))
                query = query.Where(t => t.FullName.Contains(request.Search) || t.Email.Contains(request.Search) || t.Phone.Contains(request.Search));
            if (request.DepartmentId.HasValue) query = query.Where(t => t.DepartmentId == request.DepartmentId);
            if (request.Status == "hod") query = query.Where(t => t.TeacherType == TeacherType.HOD);
            if (request.Status == "professor") query = query.Where(t => t.TeacherType == TeacherType.Professor);
            if (request.Status == "unassigned") query = query.Where(t => !t.ClassAssignments.Any() && !t.SubjectAssignments.Any());
            var now = DateTimeOffset.UtcNow;
            ViewBag.Stats = new[]
            {
                new StatCardViewModel("Total faculty", (await query.CountAsync(cancellationToken)).ToString("N0"), "HODs and Professors", "fa-solid fa-chalkboard-user"),
                new StatCardViewModel("HODs", (await query.CountAsync(t => t.TeacherType == TeacherType.HOD, cancellationToken)).ToString("N0"), "Department leadership", "fa-solid fa-user-tie", "info", "status", "hod"),
                new StatCardViewModel("Professors", (await query.CountAsync(t => t.TeacherType == TeacherType.Professor, cancellationToken)).ToString("N0"), "Teaching faculty", "fa-solid fa-person-chalkboard", "success", "status", "professor"),
                new StatCardViewModel("Unassigned", (await query.CountAsync(t => !t.ClassAssignments.Any() && !t.SubjectAssignments.Any(), cancellationToken)).ToString("N0"), "No class or subject assignment", "fa-solid fa-link-slash", "warning", "status", "unassigned")
            };
            ViewBag.Departments = await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(cancellationToken);
            query = (request.Sort?.ToLowerInvariant(), request.Direction) switch
            {
                ("department", "desc") => query.OrderByDescending(t => t.Department!.Name).ThenBy(t => t.FullName),
                ("department", _) => query.OrderBy(t => t.Department!.Name).ThenBy(t => t.FullName),
                ("type", "desc") => query.OrderByDescending(t => t.TeacherType).ThenBy(t => t.FullName),
                ("type", _) => query.OrderBy(t => t.TeacherType).ThenBy(t => t.FullName),
                ("name", "desc") => query.OrderByDescending(t => t.FullName),
                _ => query.OrderBy(t => t.FullName)
            };
            return View(await query.ToPagedResultAsync(request, cancellationToken));
        }

        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var target = await _context.Teachers.AsNoTracking()
                .Where(t => t.Id == id)
                .Select(t => new { t.UserId, t.DepartmentId })
                .SingleOrDefaultAsync(cancellationToken);
            if (target == null) return NotFound();
            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
            {
                if (User.IsInRole("HOD"))
                {
                    var ownDepartmentId = await _context.Teachers.AsNoTracking()
                        .Where(t => t.UserId == uid && t.TeacherType == TeacherType.HOD)
                        .Select(t => t.DepartmentId).SingleOrDefaultAsync(cancellationToken);
                    if (!ownDepartmentId.HasValue || target.DepartmentId != ownDepartmentId) return Forbid();
                }
                else if (target.UserId != uid)
                {
                    return Forbid();
                }
            }
            var teacher = await _context.Teachers.AsNoTracking().Include(t => t.User).Include(t => t.Department)
                .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
            if (teacher == null) return NotFound();
            var classes = await _context.TeacherClassAssignments.AsNoTracking().Where(a => a.TeacherId == id)
                .Select(a => a.Class).Include(c => c.Department).Include(c => c.Semester).OrderBy(c => c.Name).ToListAsync(cancellationToken);
            var subjects = await _context.TeacherSubjectAssignments.AsNoTracking().Where(a => a.TeacherId == id)
                .Select(a => a.Subject).Include(s => s.Class).OrderBy(s => s.Name).ToListAsync(cancellationToken);
            var classIds = classes.Select(c => c.Id).ToList();
            var subjectIds = subjects.Select(s => s.Id).ToList();
            return View(new TeacherDetailsViewModel
            {
                Teacher = teacher,
                Classes = classes,
                Subjects = subjects,
                Timetable = await _context.TimetableEntries.AsNoTracking().Include(t => t.Subject).Include(t => t.Class)
                    .Where(t => t.TeacherId == id).OrderBy(t => t.DayOfWeek).ThenBy(t => t.StartsAt).ToListAsync(cancellationToken),
                Materials = await _context.StudyMaterials.AsNoTracking().Include(m => m.Subject).Where(m => m.UploadedByTeacherId == id)
                    .OrderByDescending(m => m.CreatedUtc).ToListAsync(cancellationToken),
                Assignments = await _context.Assignments.AsNoTracking().Include(a => a.Subject).Where(a => subjectIds.Contains(a.SubjectId))
                    .OrderByDescending(a => a.CreatedUtc).ToListAsync(cancellationToken),
                LoginHistory = await _context.LoginHistories.AsNoTracking()
                    .Where(l => l.UserId == teacher.UserId || l.Email == teacher.Email)
                    .OrderByDescending(l => l.CreatedUtc).Take(20).ToListAsync(cancellationToken),
                StudentsTaught = await _context.Students.AsNoTracking().CountAsync(s => classIds.Contains(s.ClassId) || s.SubjectEnrollments.Any(e => subjectIds.Contains(e.SubjectId)), cancellationToken),
                AttendanceEntries = await _context.Attendances.AsNoTracking().CountAsync(a => a.TeacherId == id, cancellationToken),
                MarksEntered = await _context.Marks.AsNoTracking().CountAsync(m => m.EnteredByTeacherId == id, cancellationToken)
            });
        }

        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Export([FromQuery] ListQuery request, CancellationToken cancellationToken)
        {
            request.Normalize();
            var query = _context.Teachers.AsNoTracking().Include(t => t.Department).AsQueryable();
            if (!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(t => t.FullName.Contains(request.Search) || t.Email.Contains(request.Search));
            if (request.DepartmentId.HasValue) query = query.Where(t => t.DepartmentId == request.DepartmentId);
            if (request.Status == "hod") query = query.Where(t => t.TeacherType == TeacherType.HOD);
            if (request.Status == "professor") query = query.Where(t => t.TeacherType == TeacherType.Professor);
            var data = await query.OrderBy(t => t.FullName).ToListAsync(cancellationToken);
            var rows = new List<string[]> { new[] { "ID", "Name", "Email", "Phone", "Type", "Department", "Qualification" } };
            rows.AddRange(data.Select(t => new[] { t.Id.ToString(), t.FullName, t.Email, t.Phone, t.TeacherType.ToString(), t.Department?.Name ?? "", t.Qualification }));
            return File(CsvExport.Create(rows), "text/csv", $"faculty-{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        [Authorize(Roles = "Admin")]
        [Authorize(Policy = ProfilePolicies.HodEdit)]
        public async Task<IActionResult> Edit(int id)
        {
            var teacher = await _context.Teachers.AsNoTracking()
                .SingleOrDefaultAsync(t => t.Id == id && t.TeacherType == TeacherType.HOD);
            if (teacher?.UserId == null || !await _profileAuthorization.CanEditAsync(
                    User, teacher.UserId, ManagedProfileKind.Hod, teacher.DepartmentId))
                return NotFound();
            await LoadEditOptionsAsync(id, teacher?.DepartmentId);
            return View(teacher);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        [Authorize(Policy = ProfilePolicies.HodEdit)]
        public async Task<IActionResult> Edit(int id, [Bind("Id,FullName,Email,Phone,Qualification,RowVersion")] Teacher input, List<int> ClassIds, List<int> SubjectIds)
        {
            if (id != input.Id) return NotFound();
            var teacher = await _context.Teachers.Include(t => t.User)
                .SingleOrDefaultAsync(t => t.Id == id && t.TeacherType == TeacherType.HOD);
            if (teacher?.UserId == null || teacher.User == null ||
                !await _profileAuthorization.CanEditAsync(
                    User, teacher.UserId, ManagedProfileKind.Hod, teacher.DepartmentId))
                return NotFound();
            input.TeacherType = teacher.TeacherType;
            input.DepartmentId = teacher.DepartmentId;
            ConcurrencyToken.Apply(_context, teacher, input.RowVersion);
            ModelState.Remove("User"); ModelState.Remove("Subjects"); ModelState.Remove("Classes");
            ModelState.Remove("ClassAssignments"); ModelState.Remove("SubjectAssignments"); ModelState.Remove("UserId");
            ClassIds = ClassIds.Distinct().ToList();
            SubjectIds = SubjectIds.Distinct().ToList();
            var duplicate = await _userManager.FindByEmailAsync(input.Email);
            if (duplicate != null && duplicate.Id != teacher.UserId)
                ModelState.AddModelError(nameof(input.Email), "This email address is already in use.");
            if (input.DepartmentId.HasValue)
            {
                if (await _context.Classes.AnyAsync(c => ClassIds.Contains(c.Id) && c.DepartmentId != input.DepartmentId))
                    ModelState.AddModelError("ClassIds", "Every selected class must belong to the Teacher's department.");
                if (await _context.Subjects.AnyAsync(s => SubjectIds.Contains(s.Id) && s.DepartmentId != null && s.DepartmentId != input.DepartmentId))
                    ModelState.AddModelError("SubjectIds", "Every selected subject must belong to the Teacher's department.");
            }
            if (await _context.Classes.CountAsync(c => ClassIds.Contains(c.Id)) != ClassIds.Count)
                ModelState.AddModelError("ClassIds", "One or more selected classes are invalid.");
            if (await _context.Subjects.CountAsync(s => SubjectIds.Contains(s.Id)) != SubjectIds.Count)
                ModelState.AddModelError("SubjectIds", "One or more selected subjects are invalid.");
            if (!ModelState.IsValid)
            {
                await LoadEditOptionsAsync(id, input.DepartmentId, ClassIds, SubjectIds);
                return View(input);
            }
            teacher.FullName = input.FullName; teacher.Email = input.Email;
            teacher.Phone = input.Phone; teacher.Qualification = input.Qualification;
            var user = teacher.User;
            user.FullName = input.FullName; user.PhoneNumber = input.Phone; user.Email = input.Email;
            user.UserName = input.Email; user.NormalizedEmail = _userManager.NormalizeEmail(input.Email);
            user.NormalizedUserName = _userManager.NormalizeName(input.Email);
            user.SecurityStamp = Guid.NewGuid().ToString();

            var classAssignments = await _context.TeacherClassAssignments.Where(a => a.TeacherId == id).ToListAsync();
            _context.TeacherClassAssignments.RemoveRange(classAssignments.Where(a => !ClassIds.Contains(a.ClassId)));
            var assignedClassIds = classAssignments.Select(a => a.ClassId).ToHashSet();
            _context.TeacherClassAssignments.AddRange(ClassIds.Where(classId => !assignedClassIds.Contains(classId))
                .Select(classId => new TeacherClassAssignment { TeacherId = id, ClassId = classId }));
            var subjectAssignments = await _context.TeacherSubjectAssignments.Where(a => a.TeacherId == id).ToListAsync();
            _context.TeacherSubjectAssignments.RemoveRange(subjectAssignments.Where(a => !SubjectIds.Contains(a.SubjectId)));
            var assignedSubjectIds = subjectAssignments.Select(a => a.SubjectId).ToHashSet();
            _context.TeacherSubjectAssignments.AddRange(SubjectIds.Where(subjectId => !assignedSubjectIds.Contains(subjectId))
                .Select(subjectId => new TeacherSubjectAssignment { TeacherId = id, SubjectId = subjectId }));

            var targetCampusIds = await _context.UserCampusMemberships.AsNoTracking()
                .Where(m => m.UserId == teacher.UserId && m.IsActive)
                .Select(m => m.CampusId).ToArrayAsync();
            _audit.Add("Updated HOD authoritative profile and assignments", nameof(Teacher), id,
                detailsJson: System.Text.Json.JsonSerializer.Serialize(new
                {
                    TargetUserId = teacher.UserId,
                    ChangedFields = new[] { "FullName", "Email", "Phone", "Qualification", "Assignments" },
                    teacher.DepartmentId,
                    CampusIds = targetCampusIds
                }));
            await _context.SaveChangesAsync();
            TempData["Success"] = "HOD profile and assignments updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Delete(int id, string? reason)
        {
            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.Id == id);
            if (teacher == null) return NotFound();
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "An archive reason is required.";
                return RedirectToAction(nameof(Index));
            }
            teacher.IsArchived = true;
            teacher.ArchivedUtc = DateTime.UtcNow;
            teacher.ArchivedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            teacher.ArchiveReason = reason.Trim();
            if (!string.IsNullOrEmpty(teacher.UserId))
            {
                var user = await _userManager.FindByIdAsync(teacher.UserId);
                if (user != null)
                {
                    user.IsActive = false;
                    await _userManager.UpdateAsync(user);
                    await _userManager.UpdateSecurityStampAsync(user);
                }
            }
            await _context.SaveChangesAsync();
            await _audit.RecordAsync("Archived Teacher profile and deactivated login", nameof(Teacher), id);
            TempData["Success"] = "Teacher archived and login access revoked.";
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadEditOptionsAsync(
            int teacherId,
            int? departmentId,
            IEnumerable<int>? selectedClassIds = null,
            IEnumerable<int>? selectedSubjectIds = null)
        {
            ViewBag.Departments = await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
            ViewBag.Classes = await _context.Classes.AsNoTracking().Include(c => c.Department)
                .OrderBy(c => c.Name).ToListAsync();
            ViewBag.Subjects = await _context.Subjects.AsNoTracking().Include(s => s.Class)
                .OrderBy(s => s.Name).ToListAsync();
            ViewBag.SelectedClassIds = (selectedClassIds ??
                await _context.TeacherClassAssignments.Where(a => a.TeacherId == teacherId)
                    .Select(a => a.ClassId).ToListAsync()).ToHashSet();
            ViewBag.SelectedSubjectIds = (selectedSubjectIds ??
                await _context.TeacherSubjectAssignments.Where(a => a.TeacherId == teacherId)
                    .Select(a => a.SubjectId).ToListAsync()).ToHashSet();
        }




    }
}
