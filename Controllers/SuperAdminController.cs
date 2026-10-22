using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using Student_Management_System.Security;
using System.Security.Claims;

namespace Student_Management_System.Controllers;

[Authorize(Roles = "SuperAdmin")]
public class SuperAdminController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly IStudentAccountService _accounts;
    private readonly IDashboardAnalyticsService _analytics;

    public SuperAdminController(
        UserManager<ApplicationUser> users,
        ApplicationDbContext db,
        AuditService audit,
        IStudentAccountService accounts,
        IDashboardAnalyticsService analytics)
    { _users = users; _db = db; _audit = audit; _accounts = accounts; _analytics = analytics; }

    [Authorize(Policy = DashboardPolicies.View)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Index([FromQuery] DashboardFilterInput filter, CancellationToken cancellationToken)
    {
        try { return View(await _analytics.GetAsync(User, "SuperAdmin", filter, cancellationToken)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    public async Task<IActionResult> Admins([FromQuery] ListQuery request, CancellationToken cancellationToken)
    {
        request.Normalize();
        var roleId = await _db.Roles.Where(r => r.Name == "Admin").Select(r => r.Id).SingleAsync(cancellationToken);
        var adminIds = _db.UserRoles.Where(r => r.RoleId == roleId).Select(r => r.UserId);
        var query = _users.Users.AsNoTracking().Where(u => adminIds.Contains(u.Id));
        if (!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(u => u.FullName.Contains(request.Search) || (u.Email != null && u.Email.Contains(request.Search)));
        if (request.Status == "active") query = query.Where(u => !u.LockoutEnd.HasValue || u.LockoutEnd <= DateTimeOffset.UtcNow);
        if (request.Status == "inactive") query = query.Where(u => u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow);
        ViewBag.Stats = new[]
        {
            new StatCardViewModel("Administrators", (await query.CountAsync(cancellationToken)).ToString("N0"), "Across current filters", "fa-solid fa-user-shield"),
            new StatCardViewModel("Active", (await query.CountAsync(u => !u.LockoutEnd.HasValue || u.LockoutEnd <= DateTimeOffset.UtcNow, cancellationToken)).ToString("N0"), "Can sign in", "fa-solid fa-user-check", "success", "status", "active"),
            new StatCardViewModel("Inactive", (await query.CountAsync(u => u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow, cancellationToken)).ToString("N0"), "Access disabled", "fa-solid fa-user-lock", "warning", "status", "inactive"),
            new StatCardViewModel("Permission grants", (await _db.UserPermissions.CountAsync(p => adminIds.Contains(p.UserId), cancellationToken)).ToString("N0"), "Explicit Admin permissions", "fa-solid fa-key", "info")
        };
        return View(await query.OrderBy(u => u.FullName).ToPagedResultAsync(request, cancellationToken));
    }

    public async Task<IActionResult> AdminDetails(string id, CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null || !await _users.IsInRoleAsync(user, "Admin")) return NotFound();
        return View(new AdminDetailsViewModel
        {
            Admin = user,
            Permissions = await _db.UserPermissions.AsNoTracking().Where(p => p.UserId == id).Select(p => p.Permission).OrderBy(p => p.Key).ToListAsync(cancellationToken),
            Activities = await _db.AuditLogs.AsNoTracking().Where(a => a.UserId == id).OrderByDescending(a => a.CreatedUtc).Take(50).ToListAsync(cancellationToken),
            LoginHistory = await _db.LoginHistories.AsNoTracking().Where(l => l.UserId == id || l.Email == user.Email).OrderByDescending(l => l.CreatedUtc).Take(50).ToListAsync(cancellationToken)
        });
    }

    [Authorize(Policy = ProfilePolicies.AdminEdit)]
    public async Task<IActionResult> EditAdmin(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null || id == User.FindFirstValue(ClaimTypes.NameIdentifier) ||
            !await _users.IsInRoleAsync(user, "Admin")) return NotFound();
        return View(new AdminEditInputModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? "",
            PhoneNumber = user.PhoneNumber,
            ConcurrencyStamp = user.ConcurrencyStamp ?? ""
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = ProfilePolicies.AdminEdit)]
    public async Task<IActionResult> EditAdmin(string id, AdminEditInputModel input)
    {
        if (id != input.Id) return NotFound();
        var user = await _users.FindByIdAsync(id);
        if (user == null || id == User.FindFirstValue(ClaimTypes.NameIdentifier) ||
            !await _users.IsInRoleAsync(user, "Admin")) return NotFound();
        var duplicate = !string.IsNullOrWhiteSpace(input.Email) ? await _users.FindByEmailAsync(input.Email) : null;
        if (duplicate != null && duplicate.Id != id)
            ModelState.AddModelError(nameof(input.Email), "This email address is already in use.");
        if (!ModelState.IsValid) return View(input);
        _db.Entry(user).Property(u => u.ConcurrencyStamp).OriginalValue = input.ConcurrencyStamp;
        user.FullName = input.FullName.Trim();
        user.Email = input.Email.Trim().ToLowerInvariant();
        user.UserName = user.Email;
        user.NormalizedEmail = _users.NormalizeEmail(user.Email);
        user.NormalizedUserName = _users.NormalizeName(user.Email);
        user.PhoneNumber = input.PhoneNumber?.Trim();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        user.SecurityStamp = Guid.NewGuid().ToString();
        var targetCampusIds = await _db.UserCampusMemberships.AsNoTracking()
            .Where(m => m.UserId == id && m.IsActive)
            .Select(m => m.CampusId).ToArrayAsync();
        _audit.Add("Updated Administrator authoritative profile", nameof(ApplicationUser), id,
            detailsJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                TargetUserId = id,
                ChangedFields = new[] { "FullName", "Email", "PhoneNumber" },
                CampusIds = targetCampusIds,
                CrossCampus = true
            }));
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError("", "This administrator was changed by another user. Reload and try again.");
            var currentValues = await _db.Entry(user).GetDatabaseValuesAsync();
            input.ConcurrencyStamp = currentValues?.GetValue<string>(nameof(ApplicationUser.ConcurrencyStamp)) ?? "";
            return View(input);
        }
        TempData["Success"] = "Administrator updated successfully.";
        return RedirectToAction(nameof(AdminDetails), new { id });
    }

    public async Task<IActionResult> Permissions(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null || !await _users.IsInRoleAsync(user, "Admin")) return NotFound();
        ViewBag.Admin = user;
        ViewBag.Assigned = await _db.UserPermissions.Where(p => p.UserId == id).Select(p => p.PermissionId).ToListAsync();
        ViewBag.PermissionEffects = await _db.UserPermissions.Where(p => p.UserId == id)
            .ToDictionaryAsync(p => p.PermissionId, p => p.IsGranted ? "allow" : "deny");
        return View(await _db.Permissions.OrderBy(p => p.Key).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> TogglePermission(string userId, int permissionId)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user == null || !await _users.IsInRoleAsync(user, "Admin")) return NotFound();
        var assignment = await _db.UserPermissions.SingleOrDefaultAsync(p => p.UserId == userId && p.PermissionId == permissionId);
        if (assignment == null) _db.Add(new UserPermission { UserId = userId, PermissionId = permissionId });
        else _db.Remove(assignment);
        await _db.SaveChangesAsync();
        await _users.UpdateSecurityStampAsync(user);
        await _audit.RecordAsync("TogglePermission", "AdminUser", userId);
        return RedirectToAction(nameof(Permissions), new { id = userId });
    }

    [HttpPost]
    public async Task<IActionResult> SetPermission(string userId, int permissionId, string effect)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user == null || !await _users.IsInRoleAsync(user, "Admin")) return NotFound();
        if (!await _db.Permissions.AnyAsync(p => p.Id == permissionId)) return BadRequest();
        var assignment = await _db.UserPermissions.SingleOrDefaultAsync(p =>
            p.UserId == userId && p.PermissionId == permissionId);
        switch (effect.ToLowerInvariant())
        {
            case "inherit":
                if (assignment != null) _db.UserPermissions.Remove(assignment);
                break;
            case "allow":
            case "deny":
                if (assignment == null)
                    _db.UserPermissions.Add(new UserPermission
                    {
                        UserId = userId,
                        PermissionId = permissionId,
                        IsGranted = effect.Equals("allow", StringComparison.OrdinalIgnoreCase)
                    });
                else assignment.IsGranted = effect.Equals("allow", StringComparison.OrdinalIgnoreCase);
                break;
            default:
                return BadRequest("Effect must be inherit, allow, or deny.");
        }
        await _db.SaveChangesAsync();
        await _users.UpdateSecurityStampAsync(user);
        await _audit.RecordAsync($"Set permission {permissionId} to {effect}", "AdminUser", userId);
        TempData["Success"] = "Permission updated and active sessions were revoked.";
        return RedirectToAction(nameof(Permissions), new { id = userId });
    }

    public IActionResult CreateAdmin() => View();

    [HttpPost]
    public async Task<IActionResult> CreateAdmin(string fullName, string email)
    {
        if (await _users.FindByEmailAsync(email) != null)
            ModelState.AddModelError("", "Email already exists.");
        if (!ModelState.IsValid) return View();
        var user = new ApplicationUser
        {
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            UserName = email.Trim().ToLowerInvariant(),
            EmailConfirmed = false
        };
        var result = await _users.CreateAsync(user);
        if (result.Succeeded)
        {
            var roleResult = await _users.AddToRoleAsync(user, "Admin");
            if (!roleResult.Succeeded)
            {
                var compensation = await _users.DeleteAsync(user);
                if (!compensation.Succeeded)
                    throw new InvalidOperationException(
                        "Admin role assignment and account compensation both failed.");
                foreach (var error in roleResult.Errors)
                    ModelState.AddModelError("", error.Description);
                return View();
            }
            await _users.UpdateSecurityStampAsync(user);
            await _audit.RecordAsync("Create", "AdminUser", user.Id);
            var invitation = await _accounts.CreateActivationAsync(user);
            var url = Url.Action("Activate", "Account",
                new { email = user.Email, token = invitation.RawToken },
                Request.Scheme);
            var delivered = !string.IsNullOrWhiteSpace(url) &&
                await _accounts.SendActivationAsync(user, url);
            TempData[delivered ? "Success" : "Warning"] = delivered
                ? "Administrator created and the activation invitation was sent."
                : "Administrator created, but activation email delivery is pending.";
            return RedirectToAction(nameof(Admins));
        }
        foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SetAdminActive(string id, bool active)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null || !await _users.IsInRoleAsync(user, "Admin")) return NotFound();
        user.LockoutEnd = active ? null : DateTimeOffset.MaxValue;
        user.IsActive = active;
        await _users.UpdateAsync(user);
        await _users.UpdateSecurityStampAsync(user);
        await _audit.RecordAsync(active ? "Enable" : "Disable", "AdminUser", user.Id);
        return RedirectToAction(nameof(Admins));
    }

    public async Task<IActionResult> Audit([FromQuery] ListQuery request, CancellationToken cancellationToken)
    {
        request.Normalize();
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(a => a.Action.Contains(request.Search) || a.EntityName.Contains(request.Search) || (a.EntityId != null && a.EntityId.Contains(request.Search)));
        ViewBag.Stats = new[]
        {
            new StatCardViewModel("Activity records", (await query.CountAsync(cancellationToken)).ToString("N0"), "Across current filters", "fa-solid fa-clock-rotate-left"),
            new StatCardViewModel("Today", (await query.CountAsync(a => a.CreatedUtc >= DateTime.UtcNow.Date, cancellationToken)).ToString("N0"), "Recorded since midnight UTC", "fa-solid fa-calendar-day", "info"),
            new StatCardViewModel("Entities affected", (await query.Select(a => a.EntityName).Distinct().CountAsync(cancellationToken)).ToString("N0"), "Distinct entity types", "fa-solid fa-cubes", "success")
        };
        return View(await query.OrderByDescending(x => x.CreatedUtc).ToPagedResultAsync(request, cancellationToken));
    }

    public async Task<IActionResult> LoginHistory([FromQuery] ListQuery request, CancellationToken cancellationToken)
    {
        request.Normalize();
        var query = _db.LoginHistories.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(l => l.Email.Contains(request.Search) || (l.IpAddress != null && l.IpAddress.Contains(request.Search)));
        if (request.Status == "success") query = query.Where(l => l.Succeeded);
        if (request.Status == "failed") query = query.Where(l => !l.Succeeded);
        ViewBag.Stats = new[]
        {
            new StatCardViewModel("Login attempts", (await query.CountAsync(cancellationToken)).ToString("N0"), "Across current filters", "fa-solid fa-right-to-bracket"),
            new StatCardViewModel("Successful", (await query.CountAsync(l => l.Succeeded, cancellationToken)).ToString("N0"), "Authenticated sessions", "fa-solid fa-user-check", "success", "status", "success"),
            new StatCardViewModel("Failed", (await query.CountAsync(l => !l.Succeeded, cancellationToken)).ToString("N0"), "Rejected attempts", "fa-solid fa-user-xmark", "danger", "status", "failed")
        };
        return View(await query.OrderByDescending(x => x.CreatedUtc).ToPagedResultAsync(request, cancellationToken));
    }

    public async Task<IActionResult> Settings() =>
        View(await _db.SystemSettings.OrderBy(s => s.Key).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> SaveSetting(string key, string value)
    {
        var setting = await _db.SystemSettings.SingleOrDefaultAsync(s => s.Key == key);
        if (setting == null) _db.Add(new SystemSetting { Key = key, Value = value });
        else setting.Value = value;
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Update", "SystemSetting", key);
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(string id, string newPassword)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded) return BadRequest(result.Errors);
        user.MustChangePassword = true;
        await _users.UpdateSecurityStampAsync(user);
        await _audit.RecordAsync("ResetPassword", "User", user.Id);
        TempData["Success"] = "Password reset.";
        return RedirectToAction(nameof(Index));
    }
}
