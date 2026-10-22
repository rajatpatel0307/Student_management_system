using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;

namespace Student_Management_System.Controllers
{
    [Authorize]
    public class ExamController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;
        private readonly IResultService _results;
        public ExamController(ApplicationDbContext db, AuditService audit, IResultService results)
        { _db = db; _audit = audit; _results = results; }
        [Authorize(Roles = "Admin,SuperAdmin,HOD,ExaminationOfficer")]
        public async Task<IActionResult> Index([FromQuery] ListQuery request, CancellationToken cancellationToken)
        {
            request.Normalize();
            var query = _db.Exams.AsNoTracking().Include(x => x.Marks).AsQueryable();
            if (User.IsInRole("HOD"))
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                query = query.Where(e => e.Marks.Any(m =>
                    m.Student.Department!.Teachers.Any(t =>
                        t.UserId == userId && t.TeacherType == TeacherType.HOD)));
            }
            if (!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(e => e.Name.Contains(request.Search) || e.ExamType.Contains(request.Search));
            if (request.Status == "upcoming") query = query.Where(e => e.ExamDate >= DateTime.Today);
            if (request.Status == "completed") query = query.Where(e => e.ExamDate < DateTime.Today);
            if (request.Status == "published") query = query.Where(e => e.Status == ExamLifecycle.Published);
            ViewBag.Stats = new[]
            {
                new StatCardViewModel("Exams", (await query.CountAsync(cancellationToken)).ToString("N0"), "Across current filters", "fa-solid fa-file-pen"),
                new StatCardViewModel("Upcoming", (await query.CountAsync(e => e.ExamDate >= DateTime.Today, cancellationToken)).ToString("N0"), "Scheduled from today", "fa-regular fa-calendar", "info", "status", "upcoming"),
                new StatCardViewModel("Completed", (await query.CountAsync(e => e.ExamDate < DateTime.Today, cancellationToken)).ToString("N0"), "Exam date has passed", "fa-solid fa-check", "success", "status", "completed"),
                new StatCardViewModel("Published", (await query.CountAsync(e => e.Status == ExamLifecycle.Published, cancellationToken)).ToString("N0"), "Results visible to learners", "fa-solid fa-circle-check", "success", "status", "published")
            };
            return View(await query.OrderByDescending(e => e.ExamDate).ToPagedResultAsync(request, cancellationToken));
        }
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            var exam = await _db.Exams.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
            if (exam == null) return NotFound();
            var marksQuery = _db.Marks.AsNoTracking().Include(m => m.Student).Include(m => m.Subject)
                .Include(m => m.EnteredByTeacher).Where(m => m.ExamId == id);
            marksQuery = await _results.ScopeAsync(marksQuery, User, cancellationToken);
            var marks = await marksQuery.OrderByDescending(m => m.Score).ToListAsync(cancellationToken);
            if ((User.IsInRole("Professor") || User.IsInRole("Student") || User.IsInRole("HOD")) && marks.Count == 0)
                return NotFound();
            return View(new ExamDetailsViewModel {
                Exam = exam,
                Marks = marks,
                Passed = marks.Count(m => m.Score >= exam.PassMarks),
                Failed = marks.Count(m => m.Score < exam.PassMarks),
                AverageScore = marks.Count == 0
                    ? 0
                    : (double)marks.Average(m => m.Score * 100m / exam.MaximumMarks)
            });
        }
        [PermissionAuthorize("marks.approve")]
        [Authorize(Roles = "Admin,SuperAdmin,ExaminationOfficer")]
        public IActionResult Create() => View();
        [PermissionAuthorize("marks.approve")]
        [HttpPost, Authorize(Roles = "Admin,SuperAdmin,ExaminationOfficer")]
        public async Task<IActionResult> Create([Bind("Name,ExamDate,ExamType,Status,MaximumMarks,PassMarks,WeightPercent,EntryOpensUtc,EntryClosesUtc,PublicationScheduledUtc,IsSupplementary")] Exam exam)
        {
            ValidateExam(exam);
            if (exam.Status is ExamLifecycle.Locked or ExamLifecycle.Published or ExamLifecycle.Archived)
                ModelState.AddModelError(nameof(exam.Status),
                    "Create the exam as Draft or OpenForEntry, then use the moderated lifecycle actions.");
            if (!ModelState.IsValid) return View(exam);
            _db.Add(exam);
            await _db.SaveChangesAsync();
            await _audit.RecordAsync("Created exam", nameof(Exam), exam.Id);
            return RedirectToAction(nameof(Index));
        }
        [PermissionAuthorize("marks.approve")]
        [Authorize(Roles = "Admin,SuperAdmin,ExaminationOfficer")]
        public async Task<IActionResult> Edit(int id)
        {
            var exam = await _db.Exams.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id);
            return exam == null ? NotFound() : View(ExamEditInputModel.FromEntity(exam));
        }

        [PermissionAuthorize("marks.approve")]
        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,SuperAdmin,ExaminationOfficer")]
        public async Task<IActionResult> Edit(int id, ExamEditInputModel input)
        {
            if (id != input.Id) return NotFound();
            var exam = await _db.Exams.SingleOrDefaultAsync(e => e.Id == id);
            if (exam == null) return NotFound();
            ConcurrencyToken.Apply(_db, exam, input.RowVersion);
            input.CurrentStatus = exam.Status;
            if (!ModelState.IsValid) return View(input);
            if (exam.Status is ExamLifecycle.Published or ExamLifecycle.Archived)
                return Conflict("Published or archived exams require the correction workflow.");
            exam.Name = input.Name.Trim();
            exam.ExamType = input.ExamType.Trim();
            exam.ExamDate = input.ExamDate;
            exam.MaximumMarks = input.MaximumMarks;
            exam.PassMarks = input.PassMarks;
            exam.WeightPercent = input.WeightPercent;
            exam.EntryOpensUtc = input.EntryOpensUtc;
            exam.EntryClosesUtc = input.EntryClosesUtc;
            exam.PublicationScheduledUtc = input.PublicationScheduledUtc;
            exam.IsSupplementary = input.IsSupplementary;
            // Lifecycle state is intentionally preserved. Locking, publishing,
            // reopening and archiving are controlled by explicit actions.
            _audit.Add("Updated exam metadata", nameof(Exam), id,
                detailsJson: System.Text.Json.JsonSerializer.Serialize(new
                {
                    PreservedStatus = exam.Status.ToString(),
                    exam.EntryOpensUtc,
                    exam.EntryClosesUtc,
                    exam.PublicationScheduledUtc,
                    exam.IsSupplementary
                }));
            await _db.SaveChangesAsync();
            TempData["Success"] = "Exam updated successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [PermissionAuthorize("results.publish")]
        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,SuperAdmin,ExaminationOfficer")]
        public async Task<IActionResult> SetLocked(int id, bool locked)
        {
            var exam = await _db.Exams.FindAsync(id);
            if (exam == null) return NotFound();
            if (exam.Status == ExamLifecycle.Published)
                return Conflict("Published exams cannot be reopened without an authorized correction.");
            var previousStatus = exam.Status;
            exam.Status = locked ? ExamLifecycle.Locked : ExamLifecycle.OpenForEntry;
            _db.ResultLifecycleHistories.Add(new ResultLifecycleHistory
            {
                ExamId = id,
                FromStatus = previousStatus,
                ToStatus = exam.Status,
                ChangedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""
            });
            await _db.SaveChangesAsync();
            await _audit.RecordAsync(locked ? "Locked exam" : "Unlocked exam", nameof(Exam), id);
            TempData["Success"] = locked ? "Exam locked for moderation." : "Exam opened for mark entry.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [PermissionAuthorize("results.publish")]
        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,SuperAdmin,ExaminationOfficer")]
        public async Task<IActionResult> Publish(int id)
        {
            var exam = await _db.Exams.Include(e => e.Marks).SingleOrDefaultAsync(e => e.Id == id);
            if (exam == null) return NotFound();
            if (exam.Status != ExamLifecycle.Locked)
                return Conflict("Lock and moderate the exam before publishing.");
            if (exam.Marks.Count == 0 || exam.Marks.Any(m => !m.IsApproved))
                return Conflict("Every mark must be approved before publication.");
            exam.Status = ExamLifecycle.Published;
            _db.ResultLifecycleHistories.Add(new ResultLifecycleHistory
            {
                ExamId = id,
                FromStatus = ExamLifecycle.Locked,
                ToStatus = ExamLifecycle.Published,
                ChangedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""
            });
            await _db.SaveChangesAsync();
            await _audit.RecordAsync("Published exam results", nameof(Exam), id);
            TempData["Success"] = "Results published.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [PermissionAuthorize("marks.approve")]
        [Authorize(Roles = "SuperAdmin,Admin,ExaminationOfficer")]
        public async Task<IActionResult> GradeScale(int? departmentId)
        {
            ViewBag.Departments = await _db.Departments.AsNoTracking()
                .Where(d => !d.IsArchived).OrderBy(d => d.Name).ToListAsync();
            ViewBag.DepartmentId = departmentId;
            var query = _db.GradeBoundaries.AsNoTracking()
                .Include(g => g.Department).AsQueryable();
            query = departmentId.HasValue
                ? query.Where(g => g.DepartmentId == departmentId)
                : query.Where(g => g.DepartmentId == null);
            return View(await query.OrderByDescending(g => g.MinimumPercent).ToListAsync());
        }

        [PermissionAuthorize("marks.approve")]
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin,Admin,ExaminationOfficer")]
        public async Task<IActionResult> SaveGradeBoundary(GradeBoundary input)
        {
            ModelState.Remove(nameof(GradeBoundary.Department));
            if (input.MinimumPercent > input.MaximumPercent)
                ModelState.AddModelError("", "Minimum percentage cannot exceed maximum percentage.");
            if (input.DepartmentId.HasValue &&
                !await _db.Departments.AnyAsync(d =>
                    d.Id == input.DepartmentId && !d.IsArchived))
                ModelState.AddModelError(nameof(input.DepartmentId), "Select an active department.");
            if (await _db.GradeBoundaries.AnyAsync(g =>
                    g.Id != input.Id && g.DepartmentId == input.DepartmentId &&
                    g.IsActive &&
                    input.MinimumPercent <= g.MaximumPercent &&
                    input.MaximumPercent >= g.MinimumPercent))
                ModelState.AddModelError("", "This range overlaps an active grade boundary.");
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(GradeScale),
                    new { departmentId = input.DepartmentId });
            }

            GradeBoundary boundary;
            if (input.Id == 0)
            {
                boundary = new GradeBoundary();
                _db.GradeBoundaries.Add(boundary);
            }
            else
            {
                boundary = await _db.GradeBoundaries.SingleOrDefaultAsync(g => g.Id == input.Id)
                    ?? throw new InvalidOperationException("Grade boundary was not found.");
            }
            boundary.Grade = input.Grade.Trim();
            boundary.MinimumPercent = input.MinimumPercent;
            boundary.MaximumPercent = input.MaximumPercent;
            boundary.DepartmentId = input.DepartmentId;
            boundary.IsActive = true;
            await _db.SaveChangesAsync();
            await _audit.RecordAsync("Saved grade boundary", nameof(GradeBoundary), boundary.Id);
            TempData["Success"] = "Grade boundary saved.";
            return RedirectToAction(nameof(GradeScale),
                new { departmentId = boundary.DepartmentId });
        }

        [PermissionAuthorize("marks.approve")]
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin,Admin,ExaminationOfficer")]
        public async Task<IActionResult> DeactivateGradeBoundary(int id)
        {
            var boundary = await _db.GradeBoundaries.SingleOrDefaultAsync(g => g.Id == id);
            if (boundary == null) return NotFound();
            boundary.IsActive = false;
            await _db.SaveChangesAsync();
            await _audit.RecordAsync("Deactivated grade boundary", nameof(GradeBoundary), id);
            TempData["Success"] = "Grade boundary deactivated.";
            return RedirectToAction(nameof(GradeScale),
                new { departmentId = boundary.DepartmentId });
        }

        private void ValidateExam(Exam exam)
        {
            if (exam.MaximumMarks <= 0)
                ModelState.AddModelError(nameof(exam.MaximumMarks), "Maximum marks must be greater than zero.");
            if (exam.PassMarks < 0 || exam.PassMarks > exam.MaximumMarks)
                ModelState.AddModelError(nameof(exam.PassMarks), "Pass marks must be between zero and maximum marks.");
            if (exam.WeightPercent < 0 || exam.WeightPercent > 100)
                ModelState.AddModelError(nameof(exam.WeightPercent), "Weight must be between 0 and 100.");
        }
    }
}
