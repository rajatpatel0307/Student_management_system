using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;

namespace Student_Management_System.Controllers;

[Authorize]
public class TimetableController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly ITimetableService _timetable;
    public TimetableController(
        ApplicationDbContext db,
        AuditService audit,
        ITimetableService timetable)
    { _db = db; _audit = audit; _timetable = timetable; }

    public async Task<IActionResult> Index()
    {
        if (!IsAcademicRole()) return Forbid();
        var query = _db.TimetableEntries.Include(x => x.Class).Include(x => x.Subject)
            .Include(x => x.Teacher).Include(x => x.RoomEntity).AsQueryable();
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.IsInRole("Professor")) query = query.Where(x => x.Teacher!.UserId == uid ||
            x.Subject!.TeacherAssignments.Any(a => a.Teacher.UserId == uid));
        if (User.IsInRole("HOD")) query = query.Where(x => x.Class!.Department!.Teachers.Any(t => t.UserId == uid && t.TeacherType == TeacherType.HOD));
        if (User.IsInRole("Student")) query = query.Where(x => x.Class!.Students.Any(s => s.UserId == uid));
        var entries = await query.OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartsAt).ToListAsync();
        ViewBag.Stats = new[]
        {
            new StatCardViewModel("Weekly periods", entries.Count.ToString("N0"), "Across your authorized schedule", "fa-solid fa-calendar-days"),
            new StatCardViewModel("Classes", entries.Select(e => e.ClassId).Distinct().Count().ToString("N0"), "Distinct class groups", "fa-solid fa-school", "info"),
            new StatCardViewModel("Subjects", entries.Select(e => e.SubjectId).Distinct().Count().ToString("N0"), "Distinct subjects", "fa-solid fa-book", "success"),
            new StatCardViewModel("Rooms", entries.Where(e => !string.IsNullOrWhiteSpace(e.Room)).Select(e => e.Room).Distinct().Count().ToString("N0"), "Scheduled locations", "fa-solid fa-location-dot", "warning")
        };
        return View(entries);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!IsAcademicRole()) return Forbid();
        var entry = await AllowedEntries().AsNoTracking().Include(x => x.Class).Include(x => x.Subject)
            .Include(x => x.Teacher).Include(x => x.RoomEntity)
            .SingleOrDefaultAsync(x => x.Id == id);
        if (entry != null)
        {
            ViewBag.Substitutions = await _db.TimetableSubstitutions.AsNoTracking()
                .Include(s => s.SubstituteTeacher).Include(s => s.Room)
                .Where(s => s.TimetableEntryId == id)
                .OrderByDescending(s => s.OccursOn).Take(50).ToListAsync();
            LoadOptions();
        }
        return entry == null ? NotFound() : View(entry);
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    public IActionResult Create()
    {
        LoadOptions();
        return View();
    }
    [HttpPost, Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Create([Bind("ClassId,SubjectId,TeacherId,DayOfWeek,StartsAt,EndsAt,Room,RoomId")] TimetableEntry model)
    {
        ModelState.Remove(nameof(model.Class)); ModelState.Remove(nameof(model.Subject));
        ModelState.Remove(nameof(model.Teacher)); ModelState.Remove(nameof(model.RoomEntity));
        var input = ToInput(model);
        foreach (var error in await _timetable.ValidateAsync(input))
            ModelState.AddModelError("", error);
        if (!ModelState.IsValid) { LoadOptions(); return View(model); }
        var entry = await _timetable.SaveAsync(
            input,
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        TempData["Success"] = "Timetable period created without conflicts.";
        return RedirectToAction(nameof(Details), new { id = entry.Id });
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Edit(int id)
    {
        var entry = await _db.TimetableEntries.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id);
        if (entry == null) return NotFound();
        LoadOptions();
        return View(entry);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Edit(int id, [Bind("Id,ClassId,SubjectId,TeacherId,DayOfWeek,StartsAt,EndsAt,Room,RoomId,RowVersion")] TimetableEntry input)
    {
        if (id != input.Id) return NotFound();
        var entry = await _db.TimetableEntries.SingleOrDefaultAsync(t => t.Id == id);
        if (entry == null) return NotFound();
        ModelState.Remove("Class"); ModelState.Remove("Subject"); ModelState.Remove("Teacher");
        ModelState.Remove(nameof(input.RoomEntity));
        var timetableInput = ToInput(input);
        foreach (var error in await _timetable.ValidateAsync(timetableInput))
            ModelState.AddModelError("", error);
        if (!ModelState.IsValid) { LoadOptions(); return View(input); }
        await _timetable.SaveAsync(
            timetableInput,
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        TempData["Success"] = "Timetable period updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Substitute(
        int id,
        DateTime occursOn,
        int? substituteTeacherId,
        int? roomId,
        string status,
        string? reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await _timetable.CreateSubstitutionAsync(
                id,
                occursOn,
                substituteTeacherId,
                roomId,
                status,
                reason,
                User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                cancellationToken);
            TempData["Success"] = "Schedule exception created and conflict checked.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    private void LoadOptions()
    {
        ViewBag.Classes = _db.Classes.AsNoTracking().OrderBy(c => c.Name).ToList();
        ViewBag.Subjects = _db.Subjects.AsNoTracking().Include(s => s.Class).OrderBy(s => s.Name).ToList();
        ViewBag.Teachers = _db.Teachers.AsNoTracking().OrderBy(t => t.FullName).ToList();
        ViewBag.Rooms = _db.Rooms.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.Name).ToList();
    }

    private IQueryable<TimetableEntry> AllowedEntries()
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = _db.TimetableEntries.AsQueryable();
        if (User.IsInRole("Professor")) return query.Where(x => x.Teacher!.UserId == uid ||
            x.Subject!.TeacherAssignments.Any(a => a.Teacher.UserId == uid));
        if (User.IsInRole("HOD")) return query.Where(x => x.Class!.Department!.Teachers.Any(
            t => t.UserId == uid && t.TeacherType == TeacherType.HOD));
        if (User.IsInRole("Student")) return query.Where(x => x.Class!.Students.Any(s => s.UserId == uid));
        return User.IsInRole("Admin") || User.IsInRole("SuperAdmin")
            ? query
            : query.Where(_ => false);
    }

    private bool IsAcademicRole() =>
        User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("HOD") ||
        User.IsInRole("Professor") || User.IsInRole("Student");

    private static TimetableEntryInput ToInput(TimetableEntry model) => new()
    {
        Id = model.Id == 0 ? null : model.Id,
        RowVersion = model.RowVersion,
        ClassId = model.ClassId,
        SubjectId = model.SubjectId,
        TeacherId = model.TeacherId,
        DayOfWeek = model.DayOfWeek,
        StartsAt = model.StartsAt,
        EndsAt = model.EndsAt,
        RoomId = model.RoomId
    };
}
