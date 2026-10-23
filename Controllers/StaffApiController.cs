using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace Student_Management_System.Controllers;

[Authorize(Roles = "Admin,SuperAdmin")]
[ApiController]
[PermissionAuthorize("staff.manage")]
[Route("api/staff")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StaffApiController(ApplicationDbContext db, StaffRepository repository,
    StaffManagementService service, UserManager<ApplicationUser> users, AuditService audit,
    IPrivateFileStorage files) : Controller
{
    [HttpGet]
    public async Task<IActionResult> List(string? search, string? role, string? status, int page = 1, bool export = false)
    {
        var query = repository.Query.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(s =>
            s.EmployeeProfile.User.FullName.Contains(search) || s.EmployeeProfile.User.Email!.Contains(search) || s.EmployeeProfile.EmployeeNumber.Contains(search));
        if (!string.IsNullOrWhiteSpace(role)) query = query.Where(s => s.PrimaryRole == role);
        if (status is "active" or "inactive") query = query.Where(s => s.EmployeeProfile.User.IsActive == (status == "active"));
        var total = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / 10d)));
        var selection = query.OrderBy(s => s.EmployeeProfile.User.FullName);
        var records = await (export ? selection : selection.Skip((page - 1) * 10).Take(10)).ToListAsync();
        if (export)
        {
            await audit.RecordAsync("Exported staff records", nameof(StaffMember));
            List<string[]> rows = [["Employee ID", "Name", "Email", "Phone", "Role", "Department", "Joining Date", "Status", "Last Login UTC"]];
            rows.AddRange(records.Select(s => new[] { s.EmployeeProfile.EmployeeNumber, s.EmployeeProfile.User.FullName, s.EmployeeProfile.User.Email ?? "", s.EmployeeProfile.User.PhoneNumber ?? "", s.PrimaryRole, s.EmployeeProfile.Department?.Name ?? "", s.EmployeeProfile.JoinedOn.ToString("yyyy-MM-dd"), s.EmployeeProfile.User.IsActive ? "Active" : "Inactive", s.EmployeeProfile.User.LastLoginUtc?.ToString("O") ?? "" }));
            return File(CsvExport.Create(rows), "text/csv", "staff-records.csv");
        }
        return Json(new { total, page, pageSize = 10, items = records.Select(ToDto),
            summary = new { total = await repository.Query.CountAsync(), active = await repository.Query.CountAsync(s => s.EmployeeProfile.User.IsActive), roles = 5 } });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var staff = await repository.FindAsync(id);
        return staff == null ? NotFound() : Json(ToDto(staff));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Create([FromBody] StaffInput input) => Save(null, input);

    [HttpPut("{id:int}"), ValidateAntiForgeryToken]
    public Task<IActionResult> Update(int id, [FromBody] StaffInput input) => Save(id, input);

    private async Task<IActionResult> Save(int? id, StaffInput input)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try { return Json(new { id = await service.SaveAsync(id, input, User.FindFirstValue(ClaimTypes.NameIdentifier)!) }); }
        catch (StaffValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "This record changed. Refresh and try again." }); }
        catch (DbUpdateException) { return Conflict(new { message = "An account or employee ID already exists. Refresh and try again." }); }
    }

    [HttpPost("{id:int}/status"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Status(int id, [FromBody] StaffStatusInput input)
    {
        var staff = await repository.FindAsync(id);
        if (staff == null) return NotFound();
        var user = staff.EmployeeProfile.User;
        if ((await users.GetRolesAsync(user)).Any(r => r is "Admin" or "SuperAdmin")) return Forbid();
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            staff = await repository.FindAsync(id) ?? throw new KeyNotFoundException();
            user = staff.EmployeeProfile.User;
            await using var transaction = await db.Database.BeginTransactionAsync();
            user.IsActive = !input.Delete && input.Active == true;
            staff.EmployeeProfile.IsActive = user.IsActive;
            staff.IsDeleted = input.Delete;
            staff.UpdatedUtc = DateTime.UtcNow;
            var result = await users.UpdateAsync(user);
            if (!result.Succeeded) throw new StaffValidationException("Account could not be updated.");
            result = await users.UpdateSecurityStampAsync(user);
            if (!result.Succeeded) throw new StaffValidationException("Account sessions could not be revoked.");
            audit.Add(input.Delete ? "Soft deleted staff" : "Changed staff status", nameof(StaffMember), id);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        });
        return Ok();
    }

    [HttpPost("{id:int}/password"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Password(int id, [FromBody] StaffPasswordInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Password) || input.Password.Length is < 12 or > 128 || input.Password != input.ConfirmPassword)
            return BadRequest(new { message = "Provide matching passwords of at least 12 characters." });
        var staff = await repository.FindAsync(id);
        if (staff == null) return NotFound();
        var user = staff.EmployeeProfile.User;
        if ((await users.GetRolesAsync(user)).Any(r => r is "Admin" or "SuperAdmin")) return Forbid();
        user.MustChangePassword = true;
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, input.Password);
        if (!result.Succeeded) return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
        await audit.RecordAsync("Reset staff password", nameof(StaffMember), id);
        return Ok();
    }

    [HttpGet("{id:int}/activity")]
    public async Task<IActionResult> Activity(int id)
    {
        var staff = await repository.FindAsync(id);
        if (staff == null) return NotFound();
        var userId = staff.EmployeeProfile.UserId;
        return Json(new {
            logins = await db.LoginHistories.AsNoTracking().Where(l => l.UserId == userId).OrderByDescending(l => l.CreatedUtc).Take(50).Select(l => new { createdUtc = DateTime.SpecifyKind(l.CreatedUtc, DateTimeKind.Utc), l.Succeeded, l.IpAddress }).ToListAsync(),
            activity = await db.AuditLogs.AsNoTracking().Where(a => a.UserId == userId || (a.EntityName == nameof(StaffMember) && a.EntityId == id.ToString())).OrderByDescending(a => a.CreatedUtc).Take(50).Select(a => new { createdUtc = DateTime.SpecifyKind(a.CreatedUtc, DateTimeKind.Utc), a.Action, a.EntityName }).ToListAsync()
        });
    }

    [HttpGet("{id:int}/permissions")]
    public async Task<IActionResult> Permissions(int id)
    {
        var staff = await repository.FindAsync(id);
        if (staff == null) return NotFound();
        var decisions = await db.UserPermissions.AsNoTracking().Where(p => p.UserId == staff.EmployeeProfile.UserId).Select(p => new { p.Permission.Key, p.IsGranted }).ToListAsync();
        return Json(StaffWorkspaceCatalog.All.Select(w => new { w.Key, w.Title, own = w.Role == staff.PrimaryRole, granted = decisions.Any(d => d.Key == $"workspace.{w.Key}" && d.IsGranted) }));
    }

    [HttpPost("{id:int}/permissions"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Permissions(int id, [FromBody] string[] granted)
    {
        if (granted == null || granted.Length > 5 || granted.Any(k => StaffWorkspaceCatalog.Find(k) == null)) return BadRequest();
        var staff = await repository.FindAsync(id);
        if (staff == null) return NotFound();
        var target = staff.EmployeeProfile.User;
        if ((await users.GetRolesAsync(target)).Any(r => r is "Admin" or "SuperAdmin")) return Forbid();
        foreach (var workspace in StaffWorkspaceCatalog.All)
        {
            var key = $"workspace.{workspace.Key}";
            var permission = await db.Permissions.SingleAsync(p => p.Key == key);
            var decision = await db.UserPermissions.SingleOrDefaultAsync(p => p.UserId == target.Id && p.PermissionId == permission.Id);
            if (decision == null) { decision = new UserPermission { UserId = target.Id, PermissionId = permission.Id }; db.UserPermissions.Add(decision); }
            decision.IsGranted = granted.Contains(workspace.Key) || workspace.Role == staff.PrimaryRole;
        }
        audit.Add("Updated explicit staff workspace grants", nameof(StaffMember), id,
            detailsJson: System.Text.Json.JsonSerializer.Serialize(granted));
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{id:int}/photo"), ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Photo(int id, IFormFile photo, CancellationToken cancellationToken)
    {
        var staff = await repository.FindAsync(id);
        if (staff == null) return NotFound();
        if (photo == null) return BadRequest();
        try
        {
            var stored = await files.SaveProfileImageAsync(photo, cancellationToken);
            var previous = staff.EmployeeProfile.User.ProfileImageStorageKey;
            staff.EmployeeProfile.User.ProfileImageStorageKey = stored.StorageKey;
            try { await audit.RecordAsync("Updated staff photo", nameof(StaffMember), id); }
            catch { await files.DeleteAsync(stored.StorageKey, PrivateFileKind.ProfileImage, cancellationToken); throw; }
            await files.DeleteAsync(previous, PrivateFileKind.ProfileImage, cancellationToken);
            return Ok();
        }
        catch (PrivateFileValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:int}/photo")]
    public async Task<IActionResult> Photo(int id, CancellationToken cancellationToken)
    {
        var staff = await repository.FindAsync(id);
        if (staff == null) return NotFound();
        var file = await files.OpenReadAsync(staff.EmployeeProfile.User.ProfileImageStorageKey, PrivateFileKind.ProfileImage, cancellationToken);
        return file == null ? NotFound() : File(file.Stream, file.ContentType);
    }

    private static object ToDto(StaffMember s) => new {
        s.Id, fullName = s.EmployeeProfile.User.FullName, email = s.EmployeeProfile.User.Email,
        phone = s.EmployeeProfile.User.PhoneNumber, employeeId = s.EmployeeProfile.EmployeeNumber,
        username = s.EmployeeProfile.User.UserName, role = s.PrimaryRole, s.EmployeeProfile.DepartmentId,
        department = s.EmployeeProfile.Department?.Name, joiningDate = s.EmployeeProfile.JoinedOn,
        isActive = s.EmployeeProfile.User.IsActive, lastLogin = s.EmployeeProfile.User.LastLoginUtc.HasValue ? (DateTime?)DateTime.SpecifyKind(s.EmployeeProfile.User.LastLoginUtc.Value, DateTimeKind.Utc) : null,
        s.Gender, s.DateOfBirth, s.Address, s.EmergencyContact, s.Notes,
        hasPhoto = s.EmployeeProfile.User.ProfileImageStorageKey != null, rowVersion = Convert.ToBase64String(s.RowVersion)
    };
}

public sealed record StaffStatusInput([property: Required] bool? Active, bool Delete = false);
public sealed record StaffPasswordInput(string Password, string ConfirmPassword);
