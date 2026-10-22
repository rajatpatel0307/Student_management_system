using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;

namespace Student_Management_System.Controllers;

[Authorize(Roles = "Admin,SuperAdmin")]
public class AcademicYearController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    public AcademicYearController(ApplicationDbContext db, AuditService audit)
    { _db = db; _audit = audit; }
    public async Task<IActionResult> Index([FromQuery] ListQuery request, CancellationToken cancellationToken)
    {
        request.Normalize();
        var query = _db.AcademicYears.AsNoTracking()
            .Where(y => !y.IsArchived)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(y => y.Name.Contains(request.Search));
        if (request.Status == "current") query = query.Where(y => y.IsCurrent);
        if (request.Status == "past") query = query.Where(y => y.EndsOn < DateTime.Today);
        ViewBag.Stats = new[]
        {
            new StatCardViewModel("Academic years", (await query.CountAsync(cancellationToken)).ToString("N0"), "Recorded periods", "fa-regular fa-calendar"),
            new StatCardViewModel("Current", (await query.CountAsync(y => y.IsCurrent, cancellationToken)).ToString("N0"), "Active academic period", "fa-solid fa-circle-check", "success", "status", "current"),
            new StatCardViewModel("Historical", (await query.CountAsync(y => y.EndsOn < DateTime.Today, cancellationToken)).ToString("N0"), "Completed periods", "fa-solid fa-clock-rotate-left", "info", "status", "past")
        };
        return View(await query.OrderByDescending(x => x.StartsOn).ToPagedResultAsync(request, cancellationToken));
    }
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var year = await _db.AcademicYears.AsNoTracking().SingleOrDefaultAsync(y => y.Id == id, cancellationToken);
        if (year == null) return NotFound();
        return View(new AcademicYearDetailsViewModel
        {
            AcademicYear = year,
            Semesters = await _db.Semesters.AsNoTracking().Include(s => s.Department).Where(s => s.AcademicYearId == id).OrderBy(s => s.Department.Name).ThenBy(s => s.Number).ToListAsync(cancellationToken),
            Enrollments = await _db.StudentClassEnrollments.AsNoTracking().Include(e => e.Student).Include(e => e.Class).Include(e => e.Section).Where(e => e.AcademicYearId == id).ToListAsync(cancellationToken),
            Exams = await _db.Exams.AsNoTracking().Where(e => e.ExamDate >= year.StartsOn && e.ExamDate <= year.EndsOn).OrderBy(e => e.ExamDate).ToListAsync(cancellationToken)
        });
    }
    public IActionResult Create() => View();
    [HttpPost]
    public async Task<IActionResult> Create([Bind("Name,StartsOn,EndsOn,IsCurrent")] AcademicYear model)
    {
        if (model.EndsOn <= model.StartsOn) ModelState.AddModelError(nameof(model.EndsOn), "End must follow start.");
        if (!ModelState.IsValid) return View(model);
        if (model.IsCurrent)
            await _db.AcademicYears.ExecuteUpdateAsync(s => s.SetProperty(x => x.IsCurrent, false));
        _db.Add(model); await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var year = await _db.AcademicYears.AsNoTracking().SingleOrDefaultAsync(y => y.Id == id);
        return year == null ? NotFound() : View(year);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,StartsOn,EndsOn,IsCurrent,RowVersion")] AcademicYear input)
    {
        if (id != input.Id) return NotFound();
        var year = await _db.AcademicYears.SingleOrDefaultAsync(y => y.Id == id);
        if (year == null) return NotFound();
        ConcurrencyToken.Apply(_db, year, input.RowVersion);
        if (input.EndsOn <= input.StartsOn)
            ModelState.AddModelError(nameof(input.EndsOn), "End date must be after the start date.");
        if (await _db.AcademicYears.AnyAsync(y => y.Id != id && y.Name == input.Name))
            ModelState.AddModelError(nameof(input.Name), "This academic year name already exists.");
        if (!ModelState.IsValid) return View(input);
        if (input.IsCurrent)
            await _db.AcademicYears.Where(y => y.Id != id)
                .ExecuteUpdateAsync(s => s.SetProperty(y => y.IsCurrent, false));
        year.Name = input.Name.Trim();
        year.StartsOn = input.StartsOn;
        year.EndsOn = input.EndsOn;
        year.IsCurrent = input.IsCurrent;
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Updated academic year", nameof(AcademicYear), id);
        TempData["Success"] = "Academic year updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
