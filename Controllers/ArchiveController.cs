using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;

namespace Student_Management_System.Controllers;

[Authorize(Roles = "SuperAdmin,Admin")]
public sealed class ArchiveController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly UserManager<ApplicationUser> _users;
    public ArchiveController(
        ApplicationDbContext db,
        AuditService audit,
        UserManager<ApplicationUser> users)
    {
        _db = db;
        _audit = audit;
        _users = users;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var records = new List<ArchivedRecordViewModel>();
        records.AddRange(await _db.Students.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.IsArchived)
            .Select(x => new ArchivedRecordViewModel("student", x.Id, x.FullName, x.ArchivedUtc, x.ArchiveReason))
            .ToListAsync(cancellationToken));
        records.AddRange(await _db.Teachers.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.IsArchived)
            .Select(x => new ArchivedRecordViewModel("teacher", x.Id, x.FullName, x.ArchivedUtc, x.ArchiveReason))
            .ToListAsync(cancellationToken));
        records.AddRange(await _db.Departments.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.IsArchived)
            .Select(x => new ArchivedRecordViewModel("department", x.Id, x.Name, x.ArchivedUtc, x.ArchiveReason))
            .ToListAsync(cancellationToken));
        records.AddRange(await _db.Classes.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.IsArchived)
            .Select(x => new ArchivedRecordViewModel("class", x.Id, x.Name, x.ArchivedUtc, x.ArchiveReason))
            .ToListAsync(cancellationToken));
        records.AddRange(await _db.Sections.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.IsArchived)
            .Select(x => new ArchivedRecordViewModel("section", x.Id, x.Name, x.ArchivedUtc, x.ArchiveReason))
            .ToListAsync(cancellationToken));
        records.AddRange(await _db.Subjects.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.IsArchived)
            .Select(x => new ArchivedRecordViewModel("subject", x.Id, x.Name, x.ArchivedUtc, x.ArchiveReason))
            .ToListAsync(cancellationToken));
        records.AddRange(await _db.Semesters.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.IsArchived)
            .Select(x => new ArchivedRecordViewModel("semester", x.Id, x.Name, x.ArchivedUtc, x.ArchiveReason))
            .ToListAsync(cancellationToken));
        records.AddRange(await _db.AcademicYears.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.IsArchived)
            .Select(x => new ArchivedRecordViewModel("academicyear", x.Id, x.Name, x.ArchivedUtc, x.ArchiveReason))
            .ToListAsync(cancellationToken));
        records.AddRange(await _db.Fees.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.IsArchived)
            .Select(x => new ArchivedRecordViewModel("fee", x.Id, x.Term, x.ArchivedUtc, x.ArchiveReason))
            .ToListAsync(cancellationToken));
        records.AddRange(await _db.AdmissionApplications.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.IsArchived)
            .Select(x => new ArchivedRecordViewModel("admission", x.Id, x.ReferenceNumber + " · " + x.ApplicantName, x.ArchivedUtc, x.ArchiveReason))
            .ToListAsync(cancellationToken));
        return View(records.OrderByDescending(x => x.ArchivedUtc).ToList());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Set(string entity, long id, bool archived, string? reason)
    {
        if (archived && string.IsNullOrWhiteSpace(reason))
            return BadRequest("An archive reason is required.");
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        object? record = entity.ToLowerInvariant() switch
        {
            "student" => await _db.Students.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id),
            "teacher" => await _db.Teachers.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id),
            "department" => await _db.Departments.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id),
            "class" => await _db.Classes.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id),
            "section" => await _db.Sections.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id),
            "subject" => await _db.Subjects.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id),
            "semester" => await _db.Semesters.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id),
            "academicyear" => await _db.AcademicYears.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id),
            "fee" => await _db.Fees.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id),
            "admission" => await _db.AdmissionApplications.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id),
            _ => null
        };
        if (record == null) return NotFound();
        SetArchiveState(record, archived, reason?.Trim() ?? "", userId);
        var linkedUserId = record switch
        {
            Student student => student.UserId,
            Teacher teacher => teacher.UserId,
            _ => null
        };
        if (!string.IsNullOrWhiteSpace(linkedUserId))
        {
            var linkedUser = await _users.FindByIdAsync(linkedUserId);
            if (linkedUser != null)
            {
                linkedUser.IsActive = !archived;
                await _users.UpdateAsync(linkedUser);
                await _users.UpdateSecurityStampAsync(linkedUser);
            }
        }
        await _db.SaveChangesAsync();
        await _audit.RecordAsync(archived ? "Archived record" : "Restored record", record.GetType().Name, id);
        TempData["Success"] = archived ? "Record archived." : "Record restored.";
        return RedirectToAction(nameof(Index));
    }

    private static void SetArchiveState(object record, bool archived, string reason, string? userId)
    {
        var type = record.GetType();
        type.GetProperty("IsArchived")!.SetValue(record, archived);
        type.GetProperty("ArchivedUtc")?.SetValue(record, archived ? DateTime.UtcNow : null);
        type.GetProperty("ArchivedByUserId")?.SetValue(record, archived ? userId : null);
        type.GetProperty("ArchiveReason")?.SetValue(record, archived ? reason : null);
        type.GetProperty("RestoredUtc")?.SetValue(record, archived ? null : DateTime.UtcNow);
        type.GetProperty("RestoredByUserId")?.SetValue(record, archived ? null : userId);
    }

}
