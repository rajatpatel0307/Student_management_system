using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using System.Security.Claims;
using Student_Management_System.Services;

namespace Student_Management_System.Controllers;

[Authorize]
public class AssignmentController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly IPrivateFileStorage _privateFiles;
    public AssignmentController(
        ApplicationDbContext db,
        AuditService audit,
        IPrivateFileStorage privateFiles)
    { _db = db; _audit = audit; _privateFiles = privateFiles; }

    public async Task<IActionResult> Index([FromQuery] ListQuery request, CancellationToken cancellationToken)
    {
        if (!IsAcademicRole()) return Forbid();
        request.Normalize();
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = _db.Assignments
            .Include(a => a.Subject).ThenInclude(s => s!.Class)
            .Include(a => a.Submissions)
            .AsQueryable();
        if (User.IsInRole("Professor")) query = query.Where(a => a.Subject!.TeacherAssignments.Any(x => x.Teacher.UserId == uid));
        if (User.IsInRole("HOD")) query = query.Where(a => a.Subject!.Department!.Teachers.Any(t => t.UserId == uid && t.TeacherType == TeacherType.HOD));
        if (User.IsInRole("Student"))
            query = query.VisibleToStudent(uid ?? "", DateTime.UtcNow);
        if (!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(a => a.Title.Contains(request.Search) || a.Subject!.Name.Contains(request.Search));
        if (request.ClassId.HasValue) query = query.Where(a => a.Subject!.ClassId == request.ClassId);
        if (request.Status == "active") query = query.Where(a => a.DueUtc >= DateTime.UtcNow);
        if (request.Status == "overdue") query = query.Where(a => a.DueUtc < DateTime.UtcNow);
        ViewBag.Stats = new[]
        {
            new StatCardViewModel("Assignments", (await query.CountAsync(cancellationToken)).ToString("N0"), "Across your permitted records", "fa-solid fa-list-check"),
            new StatCardViewModel("Active", (await query.CountAsync(a => a.DueUtc >= DateTime.UtcNow, cancellationToken)).ToString("N0"), "Due date is open", "fa-solid fa-clock", "success", "status", "active"),
            new StatCardViewModel("Overdue", (await query.CountAsync(a => a.DueUtc < DateTime.UtcNow, cancellationToken)).ToString("N0"), "Due date has passed", "fa-solid fa-triangle-exclamation", "danger", "status", "overdue"),
            new StatCardViewModel("Submissions", (await query.SelectMany(a => a.Submissions).CountAsync(cancellationToken)).ToString("N0"), "Student responses", "fa-solid fa-file-circle-check", "info")
        };
        ViewBag.Classes = await query.Select(a => a.Subject!.Class).Distinct().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        return View(await query.OrderByDescending(a => a.DueUtc).ToPagedResultAsync(request, cancellationToken));
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        if (!IsAcademicRole()) return Forbid();
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = _db.Assignments.AsNoTracking()
            .Include(a => a.Subject).ThenInclude(s => s!.Class)
            .Include(a => a.Rubric).AsQueryable();
        if (User.IsInRole("Professor")) query = query.Where(a => a.Subject!.TeacherAssignments.Any(x => x.Teacher.UserId == uid));
        if (User.IsInRole("HOD")) query = query.Where(a => a.Subject!.Department!.Teachers.Any(t => t.UserId == uid && t.TeacherType == TeacherType.HOD));
        if (User.IsInRole("Student"))
            query = query.VisibleToStudent(uid ?? "", DateTime.UtcNow);
        var assignment = await query.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (assignment == null) return NotFound();
        var submissionQuery = _db.AssignmentSubmissions.AsNoTracking()
            .Include(s => s.Student)
            .Include(s => s.Versions)
            .Where(s => s.AssignmentId == id);
        if (User.IsInRole("Student"))
            submissionQuery = submissionQuery.Where(s => s.Student!.UserId == uid);
        var submissions = await submissionQuery
            .OrderByDescending(s => s.SubmittedUtc)
            .ToListAsync(cancellationToken);
        return View(new AssignmentDetailsViewModel
        {
            Assignment = assignment, Submissions = submissions,
            EligibleStudents = await _db.Students.CountAsync(s => s.ClassId == assignment.Subject!.ClassId || s.SubjectEnrollments.Any(e => e.SubjectId == assignment.SubjectId), cancellationToken)
        });
    }

    [Authorize(Roles = "HOD,Professor")]
    public IActionResult Create()
    {
        ViewBag.Subjects = AllowedSubjects().OrderBy(s => s.Name).ToList();
        return View(new AssignmentInputModel());
    }
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> Create(AssignmentInputModel input)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var allowed = User.IsInRole("HOD")
            ? await _db.Subjects.AnyAsync(s => s.Id == input.SubjectId && s.Department!.Teachers.Any(t => t.UserId == uid && t.TeacherType == TeacherType.HOD))
            : await _db.TeacherSubjectAssignments.AnyAsync(a => a.SubjectId == input.SubjectId && a.Teacher.UserId == uid);
        if (!allowed) return Forbid();
        if (!ModelState.IsValid)
        {
            ViewBag.Subjects = await AllowedSubjects().OrderBy(s => s.Name).ToListAsync();
            return View(input);
        }
        var strategy = _db.Database.CreateExecutionStrategy();
        var model = await strategy.ExecuteAsync(async () =>
        {
            // Each retry must recreate the complete unit after a rollback,
            // including identities and audit entries from any earlier attempt.
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync();
            var assignment = new Assignment
            {
                SubjectId = input.SubjectId,
                Title = input.Title.Trim(),
                Instructions = input.Instructions.Trim(),
                DueUtc = input.DueUtc,
                PublishUtc = input.PublishUtc,
                SubmissionType = input.SubmissionType,
                MaximumScore = input.MaximumScore,
                MaximumSubmissions = input.MaximumSubmissions,
                AllowLateSubmission = input.AllowLateSubmission,
                LatePenaltyPercent = input.LatePenaltyPercent,
                Status = input.Status,
                CreatedUtc = DateTime.UtcNow
            };
            _db.Add(assignment);
            await _db.SaveChangesAsync();
            _audit.Add("Created assignment", nameof(Assignment), assignment.Id,
                detailsJson: System.Text.Json.JsonSerializer.Serialize(new { assignment.Status, assignment.SubjectId }));
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return assignment;
        });
        TempData["Success"] = "Assignment created successfully.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> Edit(int id)
    {
        var allowedSubjectIds = AllowedSubjects().Select(s => s.Id);
        var assignment = await _db.Assignments.AsNoTracking()
            .Where(a => a.Id == id && allowedSubjectIds.Contains(a.SubjectId))
            .SingleOrDefaultAsync();
        if (assignment == null) return NotFound();
        ViewBag.Subjects = await AllowedSubjects().OrderBy(s => s.Name).ToListAsync();
        return View(AssignmentInputModel.FromEntity(assignment));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> Edit(int id, AssignmentInputModel input)
    {
        if (id != input.Id) return NotFound();
        var allowedSubjectIds = AllowedSubjects().Select(s => s.Id);
        var assignment = await _db.Assignments
            .Where(a => a.Id == id && allowedSubjectIds.Contains(a.SubjectId))
            .SingleOrDefaultAsync();
        if (assignment == null) return NotFound();
        if (!await AllowedSubjects().AnyAsync(s => s.Id == input.SubjectId))
            ModelState.AddModelError(nameof(input.SubjectId), "Select a subject you are authorized to manage.");
        if (!ModelState.IsValid)
        {
            ViewBag.Subjects = await AllowedSubjects().OrderBy(s => s.Name).ToListAsync();
            return View(input);
        }
        assignment.SubjectId = input.SubjectId;
        assignment.Title = input.Title.Trim();
        assignment.Instructions = input.Instructions.Trim();
        assignment.DueUtc = input.DueUtc;
        assignment.MaximumScore = input.MaximumScore;
        assignment.PublishUtc = input.PublishUtc;
        assignment.Status = input.Status;
        assignment.SubmissionType = input.SubmissionType;
        assignment.MaximumSubmissions = input.MaximumSubmissions;
        assignment.AllowLateSubmission = input.AllowLateSubmission;
        assignment.LatePenaltyPercent = input.LatePenaltyPercent;
        _audit.Add("Updated assignment", nameof(Assignment), id,
            detailsJson: System.Text.Json.JsonSerializer.Serialize(new { assignment.Status, assignment.SubjectId }));
        await _db.SaveChangesAsync();
        TempData["Success"] = "Assignment updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Student")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Submit(
        int assignmentId,
        string answer,
        IFormFile? attachment,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(answer) || answer.Length > 4000)
            return UnprocessableEntity("Provide an answer of no more than 4,000 characters.");
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var student = await _db.Students.SingleOrDefaultAsync(s => s.UserId == uid);
        var assignment = await _db.Assignments.Include(a => a.Subject).SingleOrDefaultAsync(a => a.Id == assignmentId);
        if (student == null || assignment == null) return Forbid();
        var enrolled = assignment.Subject?.ClassId == student.ClassId ||
            await _db.StudentSubjectEnrollments.AnyAsync(e =>
                e.StudentId == student.Id && e.SubjectId == assignment.SubjectId,
                cancellationToken);
        if (!enrolled) return Forbid();
        if (!AssignmentVisibility.IsVisibleToStudent(assignment, DateTime.UtcNow))
            return NotFound();
        if (assignment.DueUtc < DateTime.UtcNow && !assignment.AllowLateSubmission)
            return BadRequest("The due date has passed and late submissions are disabled.");
        StoredPrivateFile? stored = null;
        if (attachment is { Length: > 0 })
        {
            try
            {
                stored = await _privateFiles.SaveAssignmentAttachmentAsync(
                    attachment, cancellationToken);
            }
            catch (PrivateFileValidationException exception)
            {
                return UnprocessableEntity(exception.Message);
            }
        }
        var submission = await _db.AssignmentSubmissions.SingleOrDefaultAsync(
            s => s.AssignmentId == assignmentId && s.StudentId == student.Id,
            cancellationToken);
        if (submission == null)
        {
            submission = new AssignmentSubmission
            {
                AssignmentId = assignmentId,
                StudentId = student.Id,
                Answer = answer.Trim(),
                SubmittedUtc = DateTime.UtcNow
            };
            _db.Add(submission);
        }
        else
        {
            submission.Answer = answer.Trim();
            submission.SubmittedUtc = DateTime.UtcNow;
        }
        if (submission.Id == 0)
            await _db.SaveChangesAsync(cancellationToken);
        var versionNumber = await _db.AssignmentSubmissionVersions
            .Where(v => v.AssignmentSubmissionId == submission.Id)
            .Select(v => (int?)v.VersionNumber).MaxAsync(cancellationToken) ?? 0;
        if (versionNumber >= assignment.MaximumSubmissions)
        {
            if (stored != null)
                await _privateFiles.DeleteAsync(stored.StorageKey,
                    PrivateFileKind.AssignmentAttachment, cancellationToken);
            return Conflict("The maximum number of submissions has been reached.");
        }
        submission.Versions.Add(new AssignmentSubmissionVersion
        {
            VersionNumber = versionNumber + 1,
            Answer = answer.Trim(),
            AttachmentStorageKey = stored?.StorageKey,
            SubmittedUtc = submission.SubmittedUtc
        });
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (stored != null)
                await _privateFiles.DeleteAsync(
                    stored.StorageKey,
                    PrivateFileKind.AssignmentAttachment,
                    CancellationToken.None);
            throw;
        }
        await _audit.RecordAsync("Submitted assignment", nameof(Assignment), assignmentId);
        TempData["Success"] = "Assignment submitted successfully.";
        return RedirectToAction(nameof(Details), new { id = assignmentId });
    }

    public async Task<IActionResult> DownloadAttachment(
        long id,
        CancellationToken cancellationToken)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var version = await _db.AssignmentSubmissionVersions.AsNoTracking()
            .Include(v => v.AssignmentSubmission).ThenInclude(s => s.Student)
            .Include(v => v.AssignmentSubmission).ThenInclude(s => s.Assignment)
                .ThenInclude(a => a!.Subject).ThenInclude(s => s!.Department)
            .SingleOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (version == null || string.IsNullOrWhiteSpace(version.AttachmentStorageKey))
            return NotFound();
        var allowed = User.IsInRole("SuperAdmin") || User.IsInRole("Admin") ||
            (User.IsInRole("Student") && version.AssignmentSubmission.Student!.UserId == uid) ||
            (User.IsInRole("Professor") &&
             await _db.TeacherSubjectAssignments.AnyAsync(a =>
                 a.SubjectId == version.AssignmentSubmission.Assignment!.SubjectId &&
                 a.Teacher.UserId == uid, cancellationToken)) ||
            (User.IsInRole("HOD") &&
             await _db.Subjects.AnyAsync(s =>
                 s.Id == version.AssignmentSubmission.Assignment!.SubjectId &&
                 s.Department!.Teachers.Any(t =>
                     t.UserId == uid && t.TeacherType == TeacherType.HOD),
                 cancellationToken));
        if (!allowed) return NotFound();

        var opened = await _privateFiles.OpenReadAsync(
            version.AttachmentStorageKey,
            PrivateFileKind.AssignmentAttachment,
            cancellationToken);
        if (opened == null) return NotFound();
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        return File(opened.Stream, opened.ContentType,
            $"assignment-{version.AssignmentSubmission.AssignmentId}-v{version.VersionNumber}{opened.Extension}",
            enableRangeProcessing: false);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> AddRubric(
        int assignmentId,
        string name,
        decimal maximumScore,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || maximumScore <= 0)
            return BadRequest("Criterion name and a positive maximum score are required.");
        var assignment = await _db.Assignments.Include(a => a.Rubric)
            .SingleOrDefaultAsync(a => a.Id == assignmentId);
        if (assignment == null) return NotFound();
        if (!await AllowedSubjects().AnyAsync(s => s.Id == assignment.SubjectId))
            return Forbid();
        if (assignment.Rubric.Any(r =>
                r.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
            return Conflict("A criterion with this name already exists.");
        if (assignment.Rubric.Sum(r => r.MaximumScore) + maximumScore >
            assignment.MaximumScore)
            return UnprocessableEntity(
                "Rubric criteria cannot exceed the assignment maximum score.");
        assignment.Rubric.Add(new AssignmentRubricCriterion
        {
            Name = name.Trim(),
            MaximumScore = maximumScore,
            Description = description?.Trim()
        });
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Added assignment rubric criterion", nameof(Assignment), assignmentId);
        TempData["Success"] = "Rubric criterion added.";
        return RedirectToAction(nameof(Details), new { id = assignmentId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> Grade(
        int submissionId, decimal score, string feedback)
    {
        var submission = await _db.AssignmentSubmissions
            .Include(s => s.Assignment).ThenInclude(a => a!.Subject)
            .SingleOrDefaultAsync(s => s.Id == submissionId);
        if (submission == null) return NotFound();
        var assignment = submission.Assignment;
        if (assignment == null) return Conflict("The assignment linked to this submission no longer exists.");
        if (!await AllowedSubjects().AnyAsync(s => s.Id == assignment.SubjectId))
            return Forbid();
        if (score < 0 || score > assignment.MaximumScore)
            return UnprocessableEntity($"Score must be between 0 and {assignment.MaximumScore:0.##}.");
        submission.Score = score;
        submission.Feedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
        submission.EvaluationStatus = "Evaluated";
        submission.GradedUtc = DateTime.UtcNow;
        submission.GradedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Graded assignment submission", nameof(AssignmentSubmission), submissionId);
        TempData["Success"] = "Submission graded.";
        return RedirectToAction(nameof(Details), new { id = submission.AssignmentId });
    }

    private IQueryable<Subject> AllowedSubjects()
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return User.IsInRole("HOD")
            ? _db.Subjects.Include(s => s.Class).Where(s => s.Department!.Teachers.Any(t => t.UserId == uid && t.TeacherType == TeacherType.HOD))
            : _db.Subjects.Include(s => s.Class).Where(s => s.TeacherAssignments.Any(a => a.Teacher.UserId == uid));
    }

    private bool IsAcademicRole() =>
        User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("HOD") ||
        User.IsInRole("Professor") || User.IsInRole("Student");
}
