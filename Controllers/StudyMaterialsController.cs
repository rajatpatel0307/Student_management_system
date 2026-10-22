using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using System.Security.Claims;
using Student_Management_System.Services;

namespace Student_Management_System.Controllers;

[Authorize]
public class StudyMaterialsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IPrivateFileStorage _privateFiles;
    private readonly AuditService _audit;
    public StudyMaterialsController(
        ApplicationDbContext db,
        IPrivateFileStorage privateFiles,
        AuditService audit)
    {
        _db = db;
        _privateFiles = privateFiles;
        _audit = audit;
    }

    public async Task<IActionResult> Index([FromQuery] ListQuery request, CancellationToken cancellationToken)
    {
        if (!IsAcademicRole()) return Forbid();
        request.Normalize();
        IQueryable<StudyMaterial> query = AllowedMaterials()
            .Include(m => m.Subject)
            .Include(m => m.UploadedByTeacher);
        if (!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(m => m.Title.Contains(request.Search) || m.Subject.Name.Contains(request.Search));
        ViewBag.Stats = new[]
        {
            new StatCardViewModel("Study materials", (await query.CountAsync(cancellationToken)).ToString("N0"), "Files you are permitted to access", "fa-solid fa-folder-open"),
            new StatCardViewModel("Recent uploads", (await query.CountAsync(m => m.CreatedUtc >= DateTime.UtcNow.AddDays(-30), cancellationToken)).ToString("N0"), "Uploaded in the last 30 days", "fa-solid fa-cloud-arrow-up", "success"),
            new StatCardViewModel("Subjects covered", (await query.Select(m => m.SubjectId).Distinct().CountAsync(cancellationToken)).ToString("N0"), "Distinct curriculum subjects", "fa-solid fa-book", "info")
        };
        return View(await query.OrderByDescending(m => m.CreatedUtc).ToPagedResultAsync(request, cancellationToken));
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        if (!IsAcademicRole()) return Forbid();
        var material = await AllowedMaterials().AsNoTracking()
            .Include(m => m.Subject).ThenInclude(s => s.Class)
            .Include(m => m.UploadedByTeacher)
            .SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
        return material == null ? NotFound() : View(material);
    }

    [HttpGet]
    public async Task<IActionResult> Download(
        int id,
        CancellationToken cancellationToken)
    {
        if (!IsAcademicRole()) return Forbid();
        var material = await AllowedMaterials()
            .AsNoTracking()
            .SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (material == null) return NotFound();

        var openedFile = await _privateFiles.OpenReadAsync(
            material.FilePath,
            PrivateFileKind.StudyMaterial,
            cancellationToken);
        if (openedFile == null) return NotFound();

        SetPrivateNoStoreHeaders();
        var fileName = BuildSafeDownloadName(material.Title, openedFile.Extension);
        return File(
            openedFile.Stream,
            openedFile.ContentType,
            fileName,
            enableRangeProcessing: false);
    }

    [Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await LoadCreateSubjectsAsync(cancellationToken);
        return View();
    }

    [HttpPost, Authorize(Roles = "HOD,Professor")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Create(
        int subjectId,
        string? title,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var teacher = await _db.Teachers.SingleOrDefaultAsync(
            t => t.UserId == uid,
            cancellationToken);
        if (teacher == null) return Forbid();
        var allowed = teacher.TeacherType == TeacherType.HOD
            ? await _db.Subjects.AnyAsync(
                s => s.Id == subjectId &&
                     s.DepartmentId == teacher.DepartmentId,
                cancellationToken)
            : await _db.TeacherSubjectAssignments.AnyAsync(
                a => a.SubjectId == subjectId &&
                     a.TeacherId == teacher.Id,
                cancellationToken);
        if (!allowed) return Forbid();

        var normalizedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle))
            ModelState.AddModelError(nameof(title), "Material title is required.");
        else if (normalizedTitle.Length > 160)
            ModelState.AddModelError(nameof(title), "Material title cannot exceed 160 characters.");
        if (file == null)
            ModelState.AddModelError(nameof(file), "Select a PDF, DOCX, or PPTX file.");
        if (!ModelState.IsValid)
        {
            await LoadCreateSubjectsAsync(cancellationToken);
            return View();
        }

        StoredPrivateFile? storedFile = null;
        var databaseSaved = false;
        try
        {
            storedFile = await _privateFiles.SaveStudyMaterialAsync(
                file!,
                cancellationToken);
            var material = new StudyMaterial
            {
                SubjectId = subjectId,
                UploadedByTeacherId = teacher.Id,
                Title = normalizedTitle!,
                FilePath = storedFile.StorageKey,
                ContentType = storedFile.ContentType
            };
            _db.Add(material);
            await _db.SaveChangesAsync(cancellationToken);
            databaseSaved = true;
            await _audit.RecordAsync(
                "Uploaded study material",
                nameof(StudyMaterial),
                material.Id);
            TempData["Success"] = "Study material uploaded successfully.";
            return RedirectToAction(nameof(Details), new { id = material.Id });
        }
        catch (PrivateFileValidationException exception)
        {
            ModelState.AddModelError(nameof(file), exception.Message);
            await LoadCreateSubjectsAsync(cancellationToken);
            return View();
        }
        catch
        {
            if (!databaseSaved && storedFile != null)
                await _privateFiles.DeleteAsync(
                    storedFile.StorageKey,
                    PrivateFileKind.StudyMaterial,
                    CancellationToken.None);
            throw;
        }
    }

    [Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> Edit(int id)
    {
        var material = await EditableMaterials().AsNoTracking().SingleOrDefaultAsync(m => m.Id == id);
        if (material == null) return NotFound();
        ViewBag.Subjects = await AllowedSubjects().AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        return View(material);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> Edit(int id, [Bind("Id,SubjectId,Title")] StudyMaterial input)
    {
        if (id != input.Id) return NotFound();
        var material = await EditableMaterials().SingleOrDefaultAsync(m => m.Id == id);
        if (material == null) return NotFound();
        ModelState.Remove("Subject"); ModelState.Remove("UploadedByTeacher");
        ModelState.Remove("FilePath"); ModelState.Remove("ContentType");
        if (!await AllowedSubjects().AnyAsync(s => s.Id == input.SubjectId))
            ModelState.AddModelError(nameof(input.SubjectId), "Select a subject you are authorized to manage.");
        if (!ModelState.IsValid)
        {
            ViewBag.Subjects = await AllowedSubjects().AsNoTracking().OrderBy(s => s.Name).ToListAsync();
            input.FilePath = material.FilePath;
            input.ContentType = material.ContentType;
            return View(input);
        }
        material.Title = input.Title.Trim();
        material.SubjectId = input.SubjectId;
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Updated study material metadata", nameof(StudyMaterial), id);
        TempData["Success"] = "Study material updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private IQueryable<StudyMaterial> AllowedMaterials()
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = _db.StudyMaterials.AsQueryable();
        if (User.IsInRole("Student"))
            return query.Where(m => m.Subject.StudentEnrollments.Any(e => e.Student.UserId == uid) ||
                                    m.Subject.Class.Students.Any(s => s.UserId == uid));
        if (User.IsInRole("Professor"))
            return query.Where(m => m.Subject.TeacherAssignments.Any(a => a.Teacher.UserId == uid));
        if (User.IsInRole("HOD"))
            return query.Where(m => m.Subject.Department!.Teachers.Any(t => t.UserId == uid && t.TeacherType == TeacherType.HOD));
        return User.IsInRole("Admin") || User.IsInRole("SuperAdmin")
            ? query
            : query.Where(_ => false);
    }

    private IQueryable<StudyMaterial> EditableMaterials()
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.IsInRole("HOD"))
            return _db.StudyMaterials.Where(m => m.Subject.Department!.Teachers
                .Any(t => t.UserId == uid && t.TeacherType == TeacherType.HOD));
        return _db.StudyMaterials.Where(m => m.UploadedByTeacher.UserId == uid);
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

    private async Task LoadCreateSubjectsAsync(
        CancellationToken cancellationToken)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ViewBag.Subjects = User.IsInRole("HOD")
            ? await _db.Subjects
                .Where(s => s.Department!.Teachers.Any(
                    t => t.UserId == uid &&
                         t.TeacherType == TeacherType.HOD))
                .OrderBy(s => s.Name)
                .ToListAsync(cancellationToken)
            : await _db.Subjects
                .Where(s => s.TeacherAssignments.Any(
                    a => a.Teacher.UserId == uid))
                .OrderBy(s => s.Name)
                .ToListAsync(cancellationToken);
    }

    private void SetPrivateNoStoreHeaders()
    {
        Response.Headers.CacheControl = "private, no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        Response.Headers.XContentTypeOptions = "nosniff";
    }

    private static string BuildSafeDownloadName(
        string title,
        string extension)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars()
            .Concat(new[] { '\r', '\n', '"', ';' })
            .ToHashSet();
        var safeName = new string(title
                .Where(character => !invalidCharacters.Contains(character))
                .ToArray())
            .Trim()
            .Trim('.');
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "study-material";
        if (safeName.Length > 100)
            safeName = safeName[..100].Trim();
        return $"{safeName}{extension}";
    }
}
