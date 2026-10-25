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

    // Presentation-only bulk lookup. Controllers continue to use fresh HasAsync
    // checks independently of the navigation rendered for a request.
    public async Task<IReadOnlyDictionary<string, bool>> ReadForDisplayAsync(
        ClaimsPrincipal principal, IEnumerable<string> permissionKeys, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        var keys = permissionKeys.Distinct(StringComparer.Ordinal).ToArray();
        foreach (var key in keys) result[key] = false;
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (keys.Length == 0 || principal.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(userId)) return result;
        if (principal.IsInRole("SuperAdmin"))
            foreach (var key in keys) result[key] = true;
        else
        {
            var roles = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct().ToArray();
            var grants = await _db.RolePermissions.AsNoTracking().Where(permission => roles.Contains(permission.RoleName) && keys.Contains(permission.Permission.Key))
                .Select(permission => permission.Permission.Key).Distinct().ToListAsync(cancellationToken);
            foreach (var key in grants) result[key] = true;
        }
        var overrides = await _db.UserPermissions.AsNoTracking().Where(permission => permission.UserId == userId && keys.Contains(permission.Permission.Key))
            .Select(permission => new { permission.Permission.Key, permission.IsGranted }).ToListAsync(cancellationToken);
        foreach (var decision in overrides) result[decision.Key] = decision.IsGranted;
        return result;
    }

    public Task<bool> IsExplicitlyDeniedAsync(ClaimsPrincipal principal, string permissionKey, CancellationToken cancellationToken = default)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return _db.UserPermissions.AsNoTracking().AnyAsync(permission => permission.UserId == userId &&
            permission.Permission.Key == permissionKey && !permission.IsGranted, cancellationToken);
    }

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
