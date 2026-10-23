using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;

namespace Student_Management_System.Services;

public static class StaffAccessRules
{
    public static bool CanAccess(bool authenticated, bool active, bool admin, bool primaryRoleMatches, bool? explicitGrant) =>
        authenticated && active && (explicitGrant ?? (admin || primaryRoleMatches));

    public static bool CanTransition(string current, string next) => (current, next) switch
    {
        ("Pending", "Verified" or "Rejected") => true,
        ("Verified", "Approved" or "Rejected") => true,
        ("Approved", "Completed") => true,
        _ => false
    };
}

public sealed class StaffAccessService(ApplicationDbContext db, PermissionService permissions)
{
    public static string ManagePermission(string key) => key switch
    {
        "librarian" => "library.manage", "transport" => "transport.manage",
        "hostel" => "hostel.manage", "registrar" => "registrar.manage",
        "accountant" => "finance.expenses.manage", _ => "unknown"
    };

    public async Task<bool> CanManageAsync(ClaimsPrincipal principal, string key)
    {
        if (!await AllowedAsync(principal, key)) return false;
        var permission = ManagePermission(key);
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var decision = await db.UserPermissions.AsNoTracking().Where(p => p.UserId == id && p.Permission.Key == permission)
            .Select(p => (bool?)p.IsGranted).SingleOrDefaultAsync();
        if (decision.HasValue) return decision.Value;
        var workspace = StaffWorkspaceCatalog.Find(key)!;
        if (principal.IsInRole(workspace.Role) || principal.IsInRole("Admin") || principal.IsInRole("SuperAdmin"))
            return await permissions.HasAsync(principal, permission);
        // A cross-workspace grant is a deliberate administrator action. It does
        // not silently override a more specific explicit permission denial.
        return true;
    }

    public async Task<bool> AllowedAsync(ClaimsPrincipal principal, string key)
    {
        var workspace = StaffWorkspaceCatalog.Find(key);
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (workspace == null || id == null || principal.Identity?.IsAuthenticated != true) return false;
        var active = await db.Users.AsNoTracking().AnyAsync(u => u.Id == id && u.IsActive);
        if (key == "accountant" && await db.UserPermissions.AsNoTracking().AnyAsync(p => p.UserId == id && p.Permission.Key == "fees.view" && !p.IsGranted)) return false;
        var decision = await db.UserPermissions.AsNoTracking().Where(p => p.UserId == id && p.Permission.Key == $"workspace.{key}")
            .Select(p => (bool?)p.IsGranted).SingleOrDefaultAsync();
        return StaffAccessRules.CanAccess(true, active, principal.IsInRole("Admin") || principal.IsInRole("SuperAdmin"), principal.IsInRole(workspace.Role), decision);
    }
}
