using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using System.Security.Claims;

namespace Student_Management_System.Services;

public sealed class PermissionService
{
    private readonly ApplicationDbContext _db;
    private string? _cachedUserId;
    private IReadOnlyDictionary<string, bool>? _cachedPermissions;
    public PermissionService(ApplicationDbContext db) => _db = db;

    public async Task<bool> HasAsync(
        ClaimsPrincipal principal,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        if (principal.Identity?.IsAuthenticated != true) return false;
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return false;

        // An explicit per-user denial is authoritative for every role. This
        // includes SuperAdmin; emergency override must be a separate audited
        // workflow rather than an implicit role bypass.
        var explicitDecision = await _db.UserPermissions.AsNoTracking()
            .Where(p => p.UserId == userId && p.Permission.Key == permissionKey)
            .Select(p => (bool?)p.IsGranted)
            .SingleOrDefaultAsync(cancellationToken);
        if (explicitDecision.HasValue) return explicitDecision.Value;
        if (principal.IsInRole("SuperAdmin")) return true;

        var permissions = await GetPermissionsAsync(principal, userId, cancellationToken);
        return permissions.TryGetValue(permissionKey, out var granted) && granted;
    }

    private async Task<IReadOnlyDictionary<string, bool>> GetPermissionsAsync(
        ClaimsPrincipal principal,
        string userId,
        CancellationToken cancellationToken)
    {
        if (_cachedUserId == userId && _cachedPermissions != null)
            return _cachedPermissions;

        var roleNames = principal.FindAll(ClaimTypes.Role)
            .Select(c => c.Value).Distinct(StringComparer.Ordinal).ToArray();
        var grantedByRole = await _db.RolePermissions.AsNoTracking()
            .Where(p => roleNames.Contains(p.RoleName))
            .Select(p => p.Permission.Key)
            .Distinct()
            .ToListAsync(cancellationToken);
        var effective = grantedByRole.ToDictionary(
            key => key, _ => true, StringComparer.Ordinal);

        // A user override is authoritative, including an explicit denial.
        var overrides = await _db.UserPermissions.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.Permission.Key, p.IsGranted })
            .ToListAsync(cancellationToken);
        foreach (var item in overrides)
            effective[item.Key] = item.IsGranted;

        _cachedUserId = userId;
        _cachedPermissions = effective;
        return effective;
    }
}
