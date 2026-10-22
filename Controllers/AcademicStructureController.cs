using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;

namespace Student_Management_System.Controllers;

[Authorize(Roles = "SuperAdmin,Admin")]
[PermissionAuthorize("ManageAcademics")]
public sealed class AcademicStructureController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAcademicStructureService _structure;

    public AcademicStructureController(
        ApplicationDbContext db,
        IAcademicStructureService structure)
    {
        _db = db;
        _structure = structure;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewBag.Institutions = await _db.Institutions.AsNoTracking()
            .OrderBy(i => i.Name).ToListAsync(cancellationToken);
        ViewBag.Campuses = await _db.Campuses.AsNoTracking()
            .Include(c => c.Institution).OrderBy(c => c.Name).ToListAsync(cancellationToken);
        ViewBag.ProgramTypes = await _db.ProgramTypes.AsNoTracking()
            .OrderBy(p => p.Name).ToListAsync(cancellationToken);
        ViewBag.SubjectTypes = await _db.AcademicSubjectTypes.AsNoTracking()
            .OrderBy(t => t.Name).ToListAsync(cancellationToken);
        ViewBag.Sessions = await _db.AcademicSessions.AsNoTracking()
            .OrderByDescending(s => s.StartsOn).ToListAsync(cancellationToken);
        ViewBag.Curricula = await _db.Curricula.AsNoTracking()
            .Include(c => c.AcademicProgram)
            .Include(c => c.Versions)
            .OrderBy(c => c.Name).ToListAsync(cancellationToken);
        ViewBag.Events = await _db.AcademicCalendarEvents.AsNoTracking()
            .Include(e => e.AcademicSession)
            .OrderByDescending(e => e.StartsUtc).Take(100).ToListAsync(cancellationToken);
        ViewBag.Rooms = await _db.Rooms.AsNoTracking()
            .Include(r => r.Campus).OrderBy(r => r.Name).ToListAsync(cancellationToken);
        ViewBag.Years = await _db.AcademicYears.AsNoTracking()
            .Where(y => !y.IsArchived).OrderByDescending(y => y.StartsOn).ToListAsync(cancellationToken);
        ViewBag.Programs = await _db.AcademicPrograms.AsNoTracking()
            .Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken);
        ViewBag.Subjects = await _db.Subjects.AsNoTracking()
            .Include(s => s.Class)
            .Where(s => !s.IsArchived).OrderBy(s => s.Name).ToListAsync(cancellationToken);
        ViewBag.Departments = await _db.Departments.AsNoTracking()
            .Where(d => !d.IsArchived).OrderBy(d => d.Name).ToListAsync(cancellationToken);
        if (User.IsInRole("SuperAdmin"))
        {
            ViewBag.CampusUsers = await _db.Users.AsNoTracking().Where(u => u.IsActive)
                .OrderBy(u => u.FullName).Select(u => new { u.Id, u.FullName, u.Email })
                .ToListAsync(cancellationToken);
            ViewBag.CampusMemberships = await _db.UserCampusMemberships.AsNoTracking()
                .Include(m => m.User).Include(m => m.Campus)
                .Where(m => m.IsActive).OrderBy(m => m.User.FullName).ThenBy(m => m.Campus.Name)
                .ToListAsync(cancellationToken);
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SaveInstitution(
        InstitutionInput input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return RedirectInvalid();
        try
        {
            await _structure.SaveInstitutionAsync(input, ActorId(), cancellationToken);
            TempData["Success"] = "Institution saved.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        return RedirectToAction(nameof(Index), null, null, "institutions");
    }

    [HttpPost]
    public async Task<IActionResult> SaveCampus(
        CampusInput input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return RedirectInvalid();
        try
        {
            await _structure.SaveCampusAsync(input, ActorId(), cancellationToken);
            TempData["Success"] = "Campus saved.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        return RedirectToAction(nameof(Index), null, null, "campuses");
    }

    [HttpPost]
    public async Task<IActionResult> SaveSession(
        AcademicSessionInput input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return RedirectInvalid();
        try
        {
            await _structure.SaveSessionAsync(input, ActorId(), cancellationToken);
            TempData["Success"] = "Academic session saved.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        return RedirectToAction(nameof(Index), null, null, "sessions");
    }

    [HttpPost]
    public async Task<IActionResult> CreateCurriculum(
        CurriculumInput input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return RedirectInvalid();
        try
        {
            await _structure.CreateCurriculumAsync(input, ActorId(), cancellationToken);
            TempData["Success"] = "Curriculum and initial version created.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        return RedirectToAction(nameof(Index), null, null, "curricula");
    }

    [HttpPost]
    public async Task<IActionResult> CreateEvent(
        AcademicEventInput input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return RedirectInvalid();
        try
        {
            await _structure.CreateEventAsync(input, ActorId(), cancellationToken);
            TempData["Success"] = "Academic calendar event created.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        return RedirectToAction(nameof(Index), null, null, "calendar");
    }

    [HttpPost]
    public async Task<IActionResult> CreateLookup(
        string lookupType,
        string name,
        bool option,
        CancellationToken cancellationToken)
    {
        name = name?.Trim() ?? "";
        if (name.Length is < 2 or > 80)
        {
            TempData["Error"] = "Lookup name must contain 2 to 80 characters.";
            return RedirectToAction(nameof(Index));
        }
        switch (lookupType)
        {
            case "ProgramType":
                if (!await _db.ProgramTypes.AnyAsync(p => p.Name == name, cancellationToken))
                    _db.ProgramTypes.Add(new ProgramType { Name = name, UsesCredits = option });
                break;
            case "SubjectType":
                if (!await _db.AcademicSubjectTypes.AnyAsync(t => t.Name == name, cancellationToken))
                    _db.AcademicSubjectTypes.Add(new AcademicSubjectType { Name = name, IsPractical = option });
                break;
            default:
                return BadRequest();
        }
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = ActorId(),
            Action = $"Created {lookupType}",
            EntityName = lookupType,
            EntityId = name
        });
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = $"{lookupType} created.";
        return RedirectToAction(nameof(Index), null, null, "lookups");
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> AssignCampus(
        string userId,
        int campusId,
        bool isDefault,
        CancellationToken cancellationToken)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId && u.IsActive, cancellationToken) ||
            !await _db.Campuses.AnyAsync(c => c.Id == campusId && c.IsActive, cancellationToken))
            return BadRequest("Select an active account and campus.");
        var membership = await _db.UserCampusMemberships
            .SingleOrDefaultAsync(m => m.UserId == userId && m.CampusId == campusId, cancellationToken);
        if (isDefault)
        {
            var previousDefaults = await _db.UserCampusMemberships
                .Where(m => m.UserId == userId && m.IsDefault && m.IsActive)
                .ToListAsync(cancellationToken);
            foreach (var previous in previousDefaults) previous.IsDefault = false;
        }
        if (membership == null)
            _db.UserCampusMemberships.Add(new UserCampusMembership
            {
                UserId = userId,
                CampusId = campusId,
                IsDefault = isDefault,
                GrantedByUserId = ActorId()
            });
        else
        {
            membership.IsActive = true;
            membership.IsDefault = isDefault;
            membership.GrantedByUserId = ActorId();
            membership.GrantedUtc = DateTime.UtcNow;
        }
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = ActorId(), Action = "Assigned campus membership",
            EntityName = nameof(ApplicationUser), EntityId = userId
        });
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Campus membership assigned.";
        return RedirectToAction(nameof(Index), null, null, "campus-access");
    }

    [HttpPost]
    public async Task<IActionResult> CreateRoom(
        int? campusId,
        string code,
        string name,
        int capacity,
        CancellationToken cancellationToken)
    {
        code = code?.Trim().ToUpperInvariant() ?? "";
        name = name?.Trim() ?? "";
        if (code.Length == 0 || name.Length == 0 || capacity <= 0)
        {
            TempData["Error"] = "Room code, name, and positive capacity are required.";
            return RedirectToAction(nameof(Index), null, null, "rooms");
        }
        if (await _db.Rooms.AnyAsync(r => r.CampusId == campusId && r.Code == code, cancellationToken))
        {
            TempData["Error"] = "Room code already exists at this campus.";
            return RedirectToAction(nameof(Index), null, null, "rooms");
        }
        _db.Rooms.Add(new Room { CampusId = campusId, Code = code, Name = name, Capacity = capacity });
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = ActorId(),
            Action = "Created room",
            EntityName = nameof(Room),
            EntityId = code
        });
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Room created.";
        return RedirectToAction(nameof(Index), null, null, "rooms");
    }

    private string ActorId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private IActionResult RedirectInvalid()
    {
        TempData["Error"] = string.Join(" ", ModelState.Values
            .SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
        return RedirectToAction(nameof(Index));
    }
}
