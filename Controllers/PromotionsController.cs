using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;

namespace Student_Management_System.Controllers;

[Authorize(Roles = "SuperAdmin,Admin,HOD")]
public sealed class PromotionsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IPromotionService _promotions;

    public PromotionsController(ApplicationDbContext db, IPromotionService promotions)
    {
        _db = db;
        _promotions = promotions;
    }

    public async Task<IActionResult> Index(
        [FromQuery] ListQuery request,
        CancellationToken cancellationToken)
    {
        request.Normalize();
        var query = _db.PromotionBatches.AsNoTracking()
            .Include(b => b.SourceAcademicYear)
            .Include(b => b.DestinationAcademicYear)
            .Include(b => b.SourceClass)
            .Include(b => b.SourceSection)
            .Include(b => b.Entries)
            .AsQueryable();
        if (User.IsInRole("HOD"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var departmentId = await _db.Teachers.AsNoTracking()
                .Where(t => t.UserId == userId && t.TeacherType == TeacherType.HOD)
                .Select(t => t.DepartmentId)
                .SingleOrDefaultAsync(cancellationToken);
            if (!departmentId.HasValue) return Forbid();
            query = query.Where(b => b.DepartmentId == departmentId);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(b =>
                b.BatchNumber.Contains(request.Search) ||
                b.SourceClass.Name.Contains(request.Search));
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<PromotionBatchStatus>(request.Status, true, out var status))
            query = query.Where(b => b.Status == status);
        query = query.OrderByDescending(b => b.CreatedUtc);
        return View(await query.ToPagedResultAsync(request, cancellationToken));
    }

    [Authorize(Roles = "SuperAdmin,Admin")]
    [PermissionAuthorize("student.lifecycle.manage")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await LoadOptionsAsync(cancellationToken);
        return View(new CreatePromotionBatchInput());
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [PermissionAuthorize("student.lifecycle.manage")]
    public async Task<IActionResult> Create(
        CreatePromotionBatchInput input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync(cancellationToken);
            return View(input);
        }
        try
        {
            var batch = await _promotions.CreateDraftAsync(
                input,
                User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                cancellationToken);
            TempData["Success"] = "Promotion batch created. Review every student before submission.";
            return RedirectToAction(nameof(Details), new { id = batch.Id });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError("", exception.Message);
            await LoadOptionsAsync(cancellationToken);
            return View(input);
        }
    }

    public async Task<IActionResult> Details(long id, CancellationToken cancellationToken)
    {
        var batch = await ScopedBatches()
            .Include(b => b.SourceAcademicYear)
            .Include(b => b.DestinationAcademicYear)
            .Include(b => b.SourceClass)
            .Include(b => b.SourceSection)
            .Include(b => b.DestinationClass)
            .Include(b => b.DestinationSection)
            .Include(b => b.Entries).ThenInclude(e => e.Student)
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        return batch == null
            ? NotFound()
            : View(new PromotionDetailsViewModel
            {
                Batch = batch,
                Entries = batch.Entries.OrderBy(e => e.Student.FullName).ToList()
            });
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [PermissionAuthorize("student.lifecycle.manage")]
    public async Task<IActionResult> SaveDecisions(
        long id,
        List<PromotionDecisionInput> decisions,
        CancellationToken cancellationToken)
    {
        try
        {
            await _promotions.UpdateDecisionsAsync(
                id,
                decisions,
                User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                cancellationToken);
            TempData["Success"] = "Promotion decisions saved.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [PermissionAuthorize("student.lifecycle.manage")]
    public async Task<IActionResult> Submit(long id, CancellationToken cancellationToken)
    {
        try
        {
            await _promotions.SubmitAsync(
                id,
                User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                cancellationToken);
            TempData["Success"] = "Promotion batch submitted for ordered approval.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Roles = "SuperAdmin,Admin,HOD")]
    [PermissionAuthorize("student.lifecycle.approve")]
    public async Task<IActionResult> Review(
        PromotionReviewInput input,
        CancellationToken cancellationToken)
    {
        try
        {
            await _promotions.ReviewAsync(
                input,
                User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                cancellationToken);
            TempData["Success"] = input.Approve
                ? "Promotion batch approved."
                : "Promotion batch rejected.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        return RedirectToAction(nameof(Details), new { id = input.BatchId });
    }

    [HttpPost, Authorize(Roles = "SuperAdmin,Admin")]
    [PermissionAuthorize("student.lifecycle.manage")]
    public async Task<IActionResult> Apply(long id, CancellationToken cancellationToken)
    {
        try
        {
            await _promotions.ApplyAsync(
                id,
                User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                cancellationToken);
            TempData["Success"] = "Promotion changes were applied transactionally.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    private IQueryable<PromotionBatch> ScopedBatches()
    {
        var query = _db.PromotionBatches.AsQueryable();
        if (!User.IsInRole("HOD")) return query;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return query.Where(b => b.Department != null &&
            b.Department.HodTeacher != null &&
            b.Department.HodTeacher.UserId == userId);
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        var years = await _db.AcademicYears.AsNoTracking()
            .Where(y => !y.IsArchived).OrderByDescending(y => y.StartsOn)
            .ToListAsync(cancellationToken);
        var classes = _db.Classes.AsNoTracking().Where(c => !c.IsArchived);
        var departments = _db.Departments.AsNoTracking().Where(d => !d.IsArchived);
        if (User.IsInRole("HOD"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var departmentId = await _db.Teachers.AsNoTracking()
                .Where(t => t.UserId == userId && t.TeacherType == TeacherType.HOD)
                .Select(t => t.DepartmentId).SingleOrDefaultAsync(cancellationToken);
            classes = classes.Where(c => c.DepartmentId == departmentId);
            departments = departments.Where(d => d.Id == departmentId);
        }
        ViewBag.Years = new SelectList(years, "Id", "Name");
        ViewBag.Classes = new SelectList(
            await classes.OrderBy(c => c.Name).ToListAsync(cancellationToken),
            "Id", "Name");
        ViewBag.Sections = new SelectList(
            await _db.Sections.AsNoTracking().Where(s => !s.IsArchived)
                .OrderBy(s => s.Name).ToListAsync(cancellationToken),
            "Id", "Name");
        ViewBag.Departments = new SelectList(
            await departments.OrderBy(d => d.Name).ToListAsync(cancellationToken),
            "Id", "Name");
    }
}
