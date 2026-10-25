using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Security;
using Student_Management_System.Services;

internal static class PermissionDisplayVerification
{
    public static async Task RunAsync(ApplicationDbContext db, string actorId, Action<bool, string> check)
    {
        foreach (var role in ApplicationRoles.All)
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId), new Claim(ClaimTypes.Role, role)], "test"));
            var service = new PermissionService(db);
            var snapshot = await service.ReadForDisplayAsync(principal, PermissionPerformanceVerification.Keys);
            foreach (var key in PermissionPerformanceVerification.Keys)
                check(snapshot[key] == await service.HasAsync(principal, key), $"Bulk display lookup must preserve {role}'s decision for {key}.");
        }
        var permission = new Permission { Key = "phase4.display", Description = "Disposable display override fixture" };
        var decision = new UserPermission { UserId = actorId, Permission = permission, IsGranted = false };
        db.UserPermissions.Add(decision); await db.SaveChangesAsync();
        var super = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId), new Claim(ClaimTypes.Role, "SuperAdmin")], "test"));
        var reused = new PermissionService(db);
        check(!(await reused.ReadForDisplayAsync(super, [permission.Key]))[permission.Key], "A bulk SuperAdmin display lookup must respect explicit denial.");
        decision.IsGranted = true; await db.SaveChangesAsync();
        check((await reused.ReadForDisplayAsync(super, [permission.Key]))[permission.Key], "Display decisions must be read afresh on every invocation.");
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId), new Claim(ClaimTypes.Role, "SuperAdmin")]));
        check(!(await reused.ReadForDisplayAsync(anonymous, [permission.Key]))[permission.Key], "Anonymous role claims must never grant display permissions.");
        db.UserPermissions.Remove(decision); db.Permissions.Remove(permission); await db.SaveChangesAsync();
    }
}
