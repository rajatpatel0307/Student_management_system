using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using System.Security.Claims;
using Student_Management_System.Services;

[Authorize]
public class MarkController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;
    private readonly IResultService _results;

    public MarkController(ApplicationDbContext context, AuditService audit, IResultService results)
    {
        _context = context;
        _audit = audit;
        _results = results;
    }

    [Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> Create(int? subjectId, int? examId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var subjects = await (User.IsInRole("HOD")
            ? _context.Subjects.Where(s => s.Department!.Teachers.Any(t => t.UserId == userId && t.TeacherType == TeacherType.HOD))
            : _context.Subjects.Where(s => s.TeacherAssignments.Any(a => a.Teacher.UserId == userId)))
            .Include(s => s.Class)
            .OrderBy(s => s.Name)
            .ToListAsync();
        var exams = await _context.Exams
            .Where(e => e.Status == ExamLifecycle.OpenForEntry)
            .OrderByDescending(e => e.ExamDate).ToListAsync();
        ViewBag.Subjects = subjects;
        ViewBag.Exams = exams;

        var model = new MarksEntryViewModel
        {
            SubjectId = subjectId ?? subjects.FirstOrDefault()?.Id ?? 0,
            ExamId = examId ?? exams.FirstOrDefault()?.Id ?? 0
        };
        if (model.SubjectId == 0 || model.ExamId == 0 || subjects.All(s => s.Id != model.SubjectId))
            return View(model);

        var subject = subjects.Single(s => s.Id == model.SubjectId);
        var students = await _context.Students.Include(s => s.Section)
            .Where(s => s.ClassId == subject.ClassId || s.SubjectEnrollments.Any(e => e.SubjectId == subject.Id))
            .OrderBy(s => s.FullName)
            .ToListAsync();
        var existing = await _context.Marks
            .Where(m => m.SubjectId == model.SubjectId && m.ExamId == model.ExamId)
            .ToDictionaryAsync(m => m.StudentId);
        model.Entries = students.Select(student =>
        {
            existing.TryGetValue(student.Id, out var mark);
            return new StudentMarkEntryViewModel
            {
                StudentId = student.Id,
                StudentName = student.FullName,
                SectionName = student.Section?.Name ?? "Unassigned",
                Score = mark?.Score,
                Remarks = mark?.Remarks,
                IsApproved = mark?.IsApproved ?? false
            };
        }).ToList();
        ViewBag.SelectedSubject = subject;
        ViewBag.SelectedExam = exams.FirstOrDefault(e => e.Id == model.ExamId);
        if (ViewBag.SelectedExam is Exam selectedExam)
        {
            model.MaximumMarks = selectedExam.MaximumMarks;
            model.PassingMarks = selectedExam.PassMarks;
        }
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> SaveBulk(MarksEntryViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var subject = await _context.Subjects.Include(s => s.Class).SingleOrDefaultAsync(s => s.Id == model.SubjectId);
        var allowed = subject != null && await _results.CanEditAsync(User, model.SubjectId);
        if (!allowed || subject == null) return Forbid();

        var exam = await _context.Exams.FindAsync(model.ExamId);
        if (exam == null) return BadRequest("Exam not found.");
        if (exam.Status != ExamLifecycle.OpenForEntry)
            return Conflict("This exam is not open for mark entry.");
        model.MaximumMarks = exam.MaximumMarks;
        model.PassingMarks = exam.PassMarks;
        if (model.Entries.Any(e => e.Score < 0 || e.Score > exam.MaximumMarks))
            ModelState.AddModelError("", $"Scores must be between 0 and {exam.MaximumMarks:0.##}.");

        var studentIds = model.Entries.Select(e => e.StudentId).Distinct().ToList();
        var allowedStudentIds = await _context.Students
            .Where(s => studentIds.Contains(s.Id) && (s.ClassId == subject.ClassId || s.SubjectEnrollments.Any(e => e.SubjectId == subject.Id)))
            .Select(s => s.Id)
            .ToListAsync();
        if (allowedStudentIds.Count != studentIds.Count) return Forbid();
        if (!ModelState.IsValid)
        {
            TempData["Error"] = $"Some marks were invalid. Scores must be between 0 and {exam.MaximumMarks:0.##}.";
            return RedirectToAction(nameof(Create), new { subjectId = model.SubjectId, examId = model.ExamId });
        }

        var teacherId = await _context.Teachers.Where(t => t.UserId == userId).Select(t => (int?)t.Id).SingleOrDefaultAsync();
        var existing = await _context.Marks
            .Where(m => m.SubjectId == model.SubjectId && m.ExamId == model.ExamId && studentIds.Contains(m.StudentId))
            .ToDictionaryAsync(m => m.StudentId);
        foreach (var entry in model.Entries.Where(e => e.Score.HasValue))
        {
            if (existing.TryGetValue(entry.StudentId, out var mark))
            {
                if (mark.IsApproved) continue;
                mark.Score = entry.Score!.Value;
                mark.Grade = await _results.ResolveGradeAsync(
                    mark.Score, exam.MaximumMarks, model.SubjectId);
                mark.Remarks = entry.Remarks?.Trim();
                mark.EnteredByTeacherId = teacherId;
                mark.UpdatedUtc = DateTime.UtcNow;
            }
            else
            {
                _context.Marks.Add(new Mark
                {
                    StudentId = entry.StudentId,
                    SubjectId = model.SubjectId,
                    ExamId = model.ExamId,
                    Score = entry.Score!.Value,
                    Grade = await _results.ResolveGradeAsync(
                        entry.Score.Value, exam.MaximumMarks, model.SubjectId),
                    Remarks = entry.Remarks?.Trim(),
                    EnteredByTeacherId = teacherId
                });
            }
        }
        await _context.SaveChangesAsync();
        await _audit.RecordAsync(
            $"Saved {model.Entries.Count(e => e.Score.HasValue)} marks for exam {model.ExamId}",
            nameof(Mark),
            model.SubjectId);
        TempData["Success"] = "Marks saved successfully.";
        return RedirectToAction(nameof(Create), new { subjectId = model.SubjectId, examId = model.ExamId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> Create(Mark mark)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var allowed = await _results.CanEditAsync(User, mark.SubjectId) &&
            await _context.Subjects.AnyAsync(s => s.Id == mark.SubjectId &&
                (s.Class.Students.Any(st => st.Id == mark.StudentId) ||
                 s.StudentEnrollments.Any(e => e.StudentId == mark.StudentId)));
        if (!allowed) return Forbid();
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var exam = await _context.Exams.FindAsync(mark.ExamId);
        if (exam == null) return BadRequest("Exam not found.");
        if (exam.Status != ExamLifecycle.OpenForEntry)
            return Conflict("This exam is not open for mark entry.");
        if (mark.Score < 0 || mark.Score > exam.MaximumMarks)
            return UnprocessableEntity($"Score must be between 0 and {exam.MaximumMarks:0.##}.");
        mark.Grade = await _results.ResolveGradeAsync(
            mark.Score, exam.MaximumMarks, mark.SubjectId);
        mark.EnteredByTeacherId = await _context.Teachers.Where(t => t.UserId == userId).Select(t => (int?)t.Id).SingleOrDefaultAsync();
        var existing = await _context.Marks.SingleOrDefaultAsync(m =>
            m.StudentId == mark.StudentId && m.SubjectId == mark.SubjectId && m.ExamId == mark.ExamId);
        if (existing == null) _context.Marks.Add(mark);
        else
        {
            existing.Score = mark.Score; existing.Grade = mark.Grade;
            existing.Remarks = mark.Remarks; existing.EnteredByTeacherId = mark.EnteredByTeacherId;
            existing.UpdatedUtc = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
        await _audit.RecordAsync(
            existing == null ? "Created marks record" : "Updated marks record",
            nameof(Mark),
            existing?.Id ?? mark.Id);
        TempData["Success"] = "Marks saved successfully.";

        return User.IsInRole("HOD")
            ? RedirectToAction("Index", "HodDashboard")
            : RedirectToAction("Index", "ProfessorDashboard");
    }

    [PermissionAuthorize("marks.approve")]
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,ExaminationOfficer,HOD")]
    public async Task<IActionResult> Approve(int id)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var mark = await _context.Marks.Include(m => m.Student).Include(m => m.Exam)
            .SingleOrDefaultAsync(m => m.Id == id);
        if (mark == null) return NotFound();
        if (User.IsInRole("HOD"))
        {
            var departmentId = await _context.Teachers
                .Where(t => t.UserId == uid && t.TeacherType == TeacherType.HOD)
                .Select(t => t.DepartmentId).SingleOrDefaultAsync();
            if (!departmentId.HasValue || mark.Student.DepartmentId != departmentId)
                return Forbid();
        }
        if (mark.Exam.Status is not (ExamLifecycle.OpenForEntry or ExamLifecycle.Locked))
            return Conflict("Marks can only be approved before publication.");
        mark.IsApproved = true;
        mark.ApprovedByUserId = uid;
        mark.ApprovedUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _audit.RecordAsync("Approved marks record", nameof(Mark), id);
        TempData["Success"] = "Marks approved successfully.";
        return RedirectToAction("Index", "HodDashboard");
    }

    public async Task<IActionResult> Details(int id)
    {
        var mark = await _context.Marks.AsNoTracking().Include(m => m.Student).ThenInclude(s => s.Class)
            .Include(m => m.Subject).Include(m => m.Exam).Include(m => m.EnteredByTeacher)
            .SingleOrDefaultAsync(m => m.Id == id);
        if (mark == null) return NotFound();
        var visible = await _results.ScopeAsync(_context.Marks.Where(m => m.Id == id), User);
        if (!await visible.AnyAsync()) return NotFound();
        ViewBag.Corrections = await _context.ResultCorrectionRequests.AsNoTracking()
            .Where(r => r.MarkId == id)
            .OrderByDescending(r => r.RequestedUtc)
            .ToListAsync();
        return View(mark);
    }

    [Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> Edit(int id)
    {
        var mark = await _context.Marks.AsNoTracking().Include(m => m.Student)
            .Include(m => m.Subject).Include(m => m.Exam).SingleOrDefaultAsync(m => m.Id == id);
        if (mark == null) return NotFound();
        if (!await _results.CanEditAsync(User, mark.SubjectId) ||
            mark.Exam.Status != ExamLifecycle.OpenForEntry || mark.IsApproved) return Forbid();
        return View(mark);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> Edit(int id, decimal score, string? remarks)
    {
        var mark = await _context.Marks.Include(m => m.Student).Include(m => m.Subject)
            .Include(m => m.Exam).SingleOrDefaultAsync(m => m.Id == id);
        if (mark == null) return NotFound();
        if (!await _results.CanEditAsync(User, mark.SubjectId) ||
            mark.Exam.Status != ExamLifecycle.OpenForEntry || mark.IsApproved) return Forbid();
        if (score < 0 || score > mark.Exam.MaximumMarks)
        {
            ModelState.AddModelError(nameof(score), $"Score must be between 0 and {mark.Exam.MaximumMarks:0.##}.");
            return View(mark);
        }
        mark.Score = score;
        mark.Grade = await _results.ResolveGradeAsync(
            score, mark.Exam.MaximumMarks, mark.SubjectId);
        mark.Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
        mark.UpdatedUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _audit.RecordAsync("Updated marks record", nameof(Mark), id);
        TempData["Success"] = "Marks updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Admin,SuperAdmin,HOD,Professor,Student,Guardian,ExaminationOfficer")]
    public async Task<IActionResult> Index([FromQuery] ListQuery request, CancellationToken cancellationToken)
    {
        request.Normalize();
        var query = _context.Marks.AsNoTracking()
            .Include(m => m.Student).ThenInclude(s => s.Class)
            .Include(m => m.Student).ThenInclude(s => s.Section)
            .Include(m => m.Student).ThenInclude(s => s.Department)
            .Include(m => m.Subject).Include(m => m.Exam).Include(m => m.EnteredByTeacher).AsQueryable();
        query = await _results.ScopeAsync(query, User, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(m => m.Student.FullName.Contains(request.Search) || m.Subject.Name.Contains(request.Search) || m.Exam.Name.Contains(request.Search));
        if (request.DepartmentId.HasValue) query = query.Where(m => m.Student.DepartmentId == request.DepartmentId);
        if (request.ClassId.HasValue) query = query.Where(m => m.Student.ClassId == request.ClassId);
        if (request.SectionId.HasValue) query = query.Where(m => m.Student.SectionId == request.SectionId);
        if (request.SubjectId.HasValue) query = query.Where(m => m.SubjectId == request.SubjectId);
        if (request.ExamId.HasValue) query = query.Where(m => m.ExamId == request.ExamId);
        if (request.TeacherId.HasValue) query = query.Where(m => m.EnteredByTeacherId == request.TeacherId);
        if (request.Status == "passed") query = query.Where(m => m.Score >= m.Exam.PassMarks);
        if (request.Status == "failed") query = query.Where(m => m.Score < m.Exam.PassMarks);
        if (request.Status == "published") query = query.Where(m => m.IsApproved && m.Exam.Status == ExamLifecycle.Published);
        if (request.Status == "draft") query = query.Where(m => !m.IsApproved || m.Exam.Status != ExamLifecycle.Published);
        var count = await query.CountAsync(cancellationToken);
        ViewBag.Stats = new[]
        {
            new StatCardViewModel("Marks records", count.ToString("N0"), "Across current filters", "fa-solid fa-square-poll-vertical"),
            new StatCardViewModel("Passed", (await query.CountAsync(m => m.Score >= m.Exam.PassMarks, cancellationToken)).ToString("N0"), "At or above the configured pass mark", "fa-solid fa-circle-check", "success", "status", "passed"),
            new StatCardViewModel("Failed", (await query.CountAsync(m => m.Score < m.Exam.PassMarks, cancellationToken)).ToString("N0"), "Below the configured pass mark", "fa-solid fa-circle-xmark", "danger", "status", "failed"),
            new StatCardViewModel(
                "Average score",
                count == 0
                    ? "—"
                    : $"{await query.AverageAsync(m => m.Score * 100m / m.Exam.MaximumMarks, cancellationToken):0.0}%",
                "Normalized using each exam maximum",
                "fa-solid fa-chart-line",
                "info")
        };
        ViewBag.Departments = await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(cancellationToken);
        ViewBag.Classes = await _context.Classes.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        ViewBag.Subjects = await _context.Subjects.AsNoTracking().OrderBy(s => s.Name).ToListAsync(cancellationToken);
        ViewBag.Exams = await _context.Exams.AsNoTracking().OrderByDescending(e => e.ExamDate).ToListAsync(cancellationToken);
        return View(await query.OrderByDescending(m => m.UpdatedUtc).ThenBy(m => m.Student.FullName).ToPagedResultAsync(request, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> RequestCorrection(int id, decimal score, string reason)
    {
        var mark = await _context.Marks.Include(m => m.Exam).SingleOrDefaultAsync(m => m.Id == id);
        if (mark == null) return NotFound();
        if (!await _results.CanEditAsync(User, mark.SubjectId)) return Forbid();
        if (mark.Exam.Status != ExamLifecycle.Published)
            return Conflict("Use normal editing before publication.");
        if (score < 0 || score > mark.Exam.MaximumMarks || string.IsNullOrWhiteSpace(reason))
            return UnprocessableEntity("Provide a valid corrected score and reason.");
        if (await _context.ResultCorrectionRequests.AnyAsync(r =>
                r.MarkId == id && r.Status == ApprovalStatus.Pending))
            return Conflict("A correction is already pending.");
        var request = new ResultCorrectionRequest
        {
            MarkId = id,
            PreviousScore = mark.Score,
            RequestedScore = score,
            Reason = reason.Trim(),
            RequestedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""
        };
        _context.ResultCorrectionRequests.Add(request);
        await _context.SaveChangesAsync();
        await _audit.RecordAsync("Requested published result correction", nameof(Mark), id);
        TempData["Success"] = "Correction request submitted.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [PermissionAuthorize("results.publish")]
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,ExaminationOfficer")]
    public async Task<IActionResult> ReviewCorrection(long id, bool approve)
    {
        var request = await _context.ResultCorrectionRequests
            .Include(r => r.Mark).ThenInclude(m => m.Exam)
            .SingleOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();
        if (request.Status != ApprovalStatus.Pending) return Conflict("Correction already reviewed.");
        request.Status = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        request.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        request.ReviewedUtc = DateTime.UtcNow;
        if (approve)
        {
            request.Mark.Score = request.RequestedScore;
            request.Mark.Grade = await _results.ResolveGradeAsync(
                request.RequestedScore,
                request.Mark.Exam.MaximumMarks,
                request.Mark.SubjectId);
            request.Mark.UpdatedUtc = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
        await _audit.RecordAsync(approve ? "Approved result correction" : "Rejected result correction",
            nameof(ResultCorrectionRequest), id);
        return RedirectToAction(nameof(Details), new { id = request.MarkId });
    }

}
