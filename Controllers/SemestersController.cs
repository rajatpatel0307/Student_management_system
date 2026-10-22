using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;

namespace Student_Management_System.Controllers;

[Authorize(Roles = "Admin,SuperAdmin")]
public class SemestersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    public SemestersController(ApplicationDbContext db, AuditService audit)
    { _db = db; _audit = audit; }
    public async Task<IActionResult> Index([FromQuery] ListQuery request, CancellationToken cancellationToken)
    {
        request.Normalize();
        var query = _db.Semesters.AsNoTracking()
            .Include(s => s.Department)
            .Include(s => s.AcademicYear)
            .Where(s => !s.IsArchived)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(s => s.Name.Contains(request.Search) || s.Department.Name.Contains(request.Search));
        if (request.DepartmentId.HasValue) query = query.Where(s => s.DepartmentId == request.DepartmentId);
        if (request.AcademicYearId.HasValue) query = query.Where(s => s.AcademicYearId == request.AcademicYearId);
        ViewBag.Departments = await _db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(cancellationToken);
        ViewBag.Years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartsOn).ToListAsync(cancellationToken);
        ViewBag.Stats = new[]
        {
            new StatCardViewModel("Semesters", (await query.CountAsync(cancellationToken)).ToString("N0"), "Across current filters", "fa-solid fa-calendar-days"),
            new StatCardViewModel("Departments", (await query.Select(s => s.DepartmentId).Distinct().CountAsync(cancellationToken)).ToString("N0"), "Represented departments", "fa-solid fa-building-columns", "info"),
            new StatCardViewModel("Academic years", (await query.Select(s => s.AcademicYearId).Distinct().CountAsync(cancellationToken)).ToString("N0"), "Represented periods", "fa-regular fa-calendar", "success")
        };
        return View(await query.OrderByDescending(s => s.AcademicYear.StartsOn).ThenBy(s => s.Number).ToPagedResultAsync(request, cancellationToken));
    }
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var semester = await _db.Semesters.AsNoTracking().Include(s => s.Department).Include(s => s.AcademicYear).SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (semester == null) return NotFound();
        return View(new SemesterDetailsViewModel
        {
            Semester = semester,
            Classes = await _db.Classes.AsNoTracking().Include(c => c.Teacher).Where(c => c.SemesterId == id).OrderBy(c => c.Name).ToListAsync(cancellationToken),
            Students = await _db.Students.AsNoTracking().Include(s => s.Class).Include(s => s.Section).Where(s => s.SemesterId == id).OrderBy(s => s.FullName).ToListAsync(cancellationToken),
            Subjects = await _db.Subjects.AsNoTracking().Include(s => s.Teacher).Where(s => s.SemesterId == id).OrderBy(s => s.Name).ToListAsync(cancellationToken)
        });
    }
    public async Task<IActionResult> Create()
    {
        ViewBag.Departments = await _db.Departments.ToListAsync(); ViewBag.Years = await _db.AcademicYears.ToListAsync(); return View();
    }
    [HttpPost]
    public async Task<IActionResult> Create([Bind("Name,Number,DepartmentId,AcademicYearId")] Semester model)
    {
        ModelState.Remove("Department"); ModelState.Remove("AcademicYear");
        if (!ModelState.IsValid) { ViewBag.Departments = await _db.Departments.ToListAsync(); ViewBag.Years = await _db.AcademicYears.ToListAsync(); return View(model); }
        _db.Add(model); await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var semester = await _db.Semesters.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
        if (semester == null) return NotFound();
        await LoadOptionsAsync();
        return View(semester);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Number,DepartmentId,AcademicYearId,RowVersion")] Semester input)
    {
        if (id != input.Id) return NotFound();
        var semester = await _db.Semesters.SingleOrDefaultAsync(s => s.Id == id);
        if (semester == null) return NotFound();
        ConcurrencyToken.Apply(_db, semester, input.RowVersion);
        ModelState.Remove("Department"); ModelState.Remove("AcademicYear");
        if (!await _db.Departments.AnyAsync(d => d.Id == input.DepartmentId))
            ModelState.AddModelError(nameof(input.DepartmentId), "Select a valid department.");
        if (!await _db.AcademicYears.AnyAsync(y => y.Id == input.AcademicYearId))
            ModelState.AddModelError(nameof(input.AcademicYearId), "Select a valid academic year.");
        if (await _db.Semesters.AnyAsync(s => s.Id != id && s.DepartmentId == input.DepartmentId &&
            s.AcademicYearId == input.AcademicYearId && s.Number == input.Number))
            ModelState.AddModelError(nameof(input.Number), "This semester number already exists for the department and academic year.");
        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync();
            return View(input);
        }
        semester.Name = input.Name.Trim();
        semester.Number = input.Number;
        semester.DepartmentId = input.DepartmentId;
        semester.AcademicYearId = input.AcademicYearId;
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Updated semester", nameof(Semester), id);
        TempData["Success"] = "Semester updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task LoadOptionsAsync()
    {
        ViewBag.Departments = await _db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
        ViewBag.Years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartsOn).ToListAsync();
    }
}
