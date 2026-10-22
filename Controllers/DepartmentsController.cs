using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;

namespace Student_Management_System.Controllers;

[Authorize]
public class DepartmentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    public DepartmentsController(ApplicationDbContext db, AuditService audit)
    { _db = db; _audit = audit; }

    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Index([FromQuery] ListQuery request, CancellationToken cancellationToken)
    {
        request.Normalize();
        var query = _db.Departments.AsNoTracking().Include(d => d.HodTeacher)
            .Include(d => d.Teachers).Include(d => d.Students).Include(d => d.Classes)
            .Where(d => !d.IsArchived).AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(d => d.Name.Contains(request.Search) || d.Code.Contains(request.Search));
        if (request.Status == "assigned") query = query.Where(d => d.HodTeacherId != null);
        if (request.Status == "unassigned") query = query.Where(d => d.HodTeacherId == null);
        ViewBag.Stats = new[]
        {
            new StatCardViewModel("Departments", (await query.CountAsync(cancellationToken)).ToString("N0"), "Across current filters", "fa-solid fa-building-columns"),
            new StatCardViewModel("With HOD", (await query.CountAsync(d => d.HodTeacherId != null, cancellationToken)).ToString("N0"), "Leadership assigned", "fa-solid fa-user-check", "success", "status", "assigned"),
            new StatCardViewModel("Without HOD", (await query.CountAsync(d => d.HodTeacherId == null, cancellationToken)).ToString("N0"), "Needs leadership assignment", "fa-solid fa-user-clock", "warning", "status", "unassigned"),
            new StatCardViewModel("Students", (await query.SelectMany(d => d.Students).CountAsync(cancellationToken)).ToString("N0"), "Students in filtered departments", "fa-solid fa-user-graduate", "info")
        };
        query = (request.Sort?.ToLowerInvariant(), request.Direction) switch
        {
            ("code", "desc") => query.OrderByDescending(d => d.Code),
            ("code", _) => query.OrderBy(d => d.Code),
            ("students", "desc") => query.OrderByDescending(d => d.Students.Count),
            ("students", _) => query.OrderBy(d => d.Students.Count),
            ("name", "desc") => query.OrderByDescending(d => d.Name),
            _ => query.OrderBy(d => d.Name)
        };
        return View(await query.ToPagedResultAsync(request, cancellationToken));
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("HOD") || !await _db.Teachers.AnyAsync(
                t => t.UserId == uid && t.TeacherType == TeacherType.HOD && t.DepartmentId == id,
                cancellationToken))
                return Forbid();
        }
        var department = await _db.Departments.AsNoTracking().Include(d => d.HodTeacher).SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (department == null) return NotFound();
        var teachers = await _db.Teachers.AsNoTracking().Where(t => t.DepartmentId == id).OrderBy(t => t.FullName).ToListAsync(cancellationToken);
        var students = await _db.Students.AsNoTracking().Include(s => s.Class).Include(s => s.Section).Where(s => s.DepartmentId == id).OrderBy(s => s.FullName).ToListAsync(cancellationToken);
        var classes = await _db.Classes.AsNoTracking().Include(c => c.Semester).Where(c => c.DepartmentId == id).OrderBy(c => c.Name).ToListAsync(cancellationToken);
        var subjects = await _db.Subjects.AsNoTracking().Include(s => s.Teacher).Where(s => s.DepartmentId == id).OrderBy(s => s.Name).ToListAsync(cancellationToken);
        var studentIds = students.Select(s => s.Id).ToList();
        var attendance = await _db.Attendances.AsNoTracking().Where(a => studentIds.Contains(a.StudentId)).ToListAsync(cancellationToken);
        var marks = await _db.Marks.AsNoTracking().Where(m => studentIds.Contains(m.StudentId)).ToListAsync(cancellationToken);
        return View(new DepartmentDetailsViewModel
        {
            Department = department, Teachers = teachers, Students = students, Classes = classes, Subjects = subjects,
            Announcements = await _db.Notices.AsNoTracking().Where(n => n.DepartmentId == id).OrderByDescending(n => n.CreatedDate).Take(10).ToListAsync(cancellationToken),
            SectionCount = await _db.Sections.AsNoTracking().CountAsync(s => s.Class.DepartmentId == id, cancellationToken),
            Activities = await _db.AuditLogs.AsNoTracking().Where(a => a.EntityName == nameof(Department) && a.EntityId == id.ToString())
                .OrderByDescending(a => a.CreatedUtc).Take(20).ToListAsync(cancellationToken),
            AttendancePercentage = attendance.Count == 0 ? 0 : attendance.Count(a => a.Status == "Present") * 100.0 / attendance.Count,
            AverageScore = marks.Count == 0 ? 0 : (double)marks.Average(m => m.Score)
        });
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Export([FromQuery] ListQuery request, CancellationToken cancellationToken)
    {
        request.Normalize();
        var query = _db.Departments.AsNoTracking().Include(d => d.HodTeacher).Include(d => d.Teachers).Include(d => d.Students).Include(d => d.Classes).AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(d => d.Name.Contains(request.Search) || d.Code.Contains(request.Search));
        var data = await query.OrderBy(d => d.Name).ToListAsync(cancellationToken);
        var rows = new List<string[]> { new[] { "Code", "Department", "HOD", "Professors", "Students", "Classes" } };
        rows.AddRange(data.Select(d => new[] { d.Code, d.Name, d.HodTeacher?.FullName ?? "", d.Teachers.Count(t => t.TeacherType == TeacherType.Professor).ToString(), d.Students.Count.ToString(), d.Classes.Count.ToString() }));
        return File(CsvExport.Create(rows), "text/csv", $"departments-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Create()
    {
        ViewBag.Hods = await _db.Teachers.Where(t => t.TeacherType == TeacherType.HOD).ToListAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Create([Bind("Name,Code,HodTeacherId")] Department department)
    {
        if (department.HodTeacherId.HasValue &&
            !await _db.Teachers.AnyAsync(t => t.Id == department.HodTeacherId && t.TeacherType == TeacherType.HOD))
            ModelState.AddModelError(nameof(department.HodTeacherId), "Select a valid HOD.");
        if (!ModelState.IsValid)
        {
            ViewBag.Hods = await _db.Teachers.Where(t => t.TeacherType == TeacherType.HOD).ToListAsync();
            return View(department);
        }
        _db.Add(department); await _db.SaveChangesAsync();
        if (department.HodTeacherId.HasValue)
        {
            var hod = await _db.Teachers.FindAsync(department.HodTeacherId.Value);
            if (hod != null) { hod.DepartmentId = department.Id; await _db.SaveChangesAsync(); }
        }
        TempData["Success"] = "Department created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var department = await _db.Departments.AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (department == null) return NotFound();
        ViewBag.Hods = await _db.Teachers.AsNoTracking().Where(t => t.TeacherType == TeacherType.HOD)
            .OrderBy(t => t.FullName).ToListAsync(cancellationToken);
        return View(department);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Code,HodTeacherId,RowVersion")] Department input, CancellationToken cancellationToken)
    {
        if (id != input.Id) return NotFound();
        var department = await _db.Departments.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (department == null) return NotFound();
        ConcurrencyToken.Apply(_db, department, input.RowVersion);
        ModelState.Remove("HodTeacher"); ModelState.Remove("Teachers"); ModelState.Remove("Classes");
        ModelState.Remove("Students"); ModelState.Remove("Semesters");
        if (await _db.Departments.AnyAsync(d => d.Id != id && d.Code == input.Code, cancellationToken))
            ModelState.AddModelError(nameof(input.Code), "This department code is already in use.");
        if (input.HodTeacherId.HasValue && !await _db.Teachers.AnyAsync(
            t => t.Id == input.HodTeacherId && t.TeacherType == TeacherType.HOD, cancellationToken))
            ModelState.AddModelError(nameof(input.HodTeacherId), "Select a valid HOD.");
        if (!ModelState.IsValid)
        {
            ViewBag.Hods = await _db.Teachers.AsNoTracking().Where(t => t.TeacherType == TeacherType.HOD)
                .OrderBy(t => t.FullName).ToListAsync(cancellationToken);
            return View(input);
        }

        var oldHodId = department.HodTeacherId;
        department.Name = input.Name.Trim();
        department.Code = input.Code.Trim().ToUpperInvariant();
        department.HodTeacherId = input.HodTeacherId;
        if (oldHodId.HasValue && oldHodId != input.HodTeacherId)
        {
            var previousHod = await _db.Teachers.FindAsync(new object[] { oldHodId.Value }, cancellationToken);
            if (previousHod?.DepartmentId == id) previousHod.DepartmentId = null;
        }
        if (input.HodTeacherId.HasValue)
        {
            var hod = await _db.Teachers.FindAsync(new object[] { input.HodTeacherId.Value }, cancellationToken);
            if (hod != null) hod.DepartmentId = id;
        }
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync("Updated department profile and HOD", nameof(Department), id);
        TempData["Success"] = "Department updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
