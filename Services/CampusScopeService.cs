using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using System.Security.Claims;

namespace Student_Management_System.Services;

public interface ICampusScopeService
{
    Task<int[]> AllowedCampusIdsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<int> RequireDefaultCampusIdAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<bool> CanAccessAsync(ClaimsPrincipal principal, int? campusId, CancellationToken cancellationToken = default);
}

public sealed class CampusScopeService : ICampusScopeService
{
    private readonly ApplicationDbContext _db;
    private string? _cachedUserId;
    private int[]? _cachedCampusIds;
    public CampusScopeService(ApplicationDbContext db) => _db = db;

    public async Task<int[]> AllowedCampusIdsAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (principal.IsInRole("SuperAdmin"))
            return await _db.Campuses.AsNoTracking().Where(c => c.IsActive && c.Institution.IsActive)
                .Select(c => c.Id).ToArrayAsync(cancellationToken);
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
        if (_cachedUserId == userId && _cachedCampusIds != null) return _cachedCampusIds;
        _cachedCampusIds = await _db.Set<UserCampusMembership>().AsNoTracking()
            .Where(m => m.UserId == userId && m.IsActive && m.Campus.IsActive && m.Campus.Institution.IsActive)
            .Select(m => m.CampusId).Distinct().ToArrayAsync(cancellationToken);
        _cachedUserId = userId;
        return _cachedCampusIds;
    }

    public async Task<int> RequireDefaultCampusIdAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var campusId = await _db.Set<UserCampusMembership>().AsNoTracking()
                .Where(m => m.UserId == userId && m.IsActive &&
                            m.Campus.IsActive && m.Campus.Institution.IsActive)
                .OrderByDescending(m => m.IsDefault).ThenBy(m => m.Id)
                .Select(m => (int?)m.CampusId).FirstOrDefaultAsync(cancellationToken);
            if (campusId.HasValue) return campusId.Value;
        }
        if (principal.IsInRole("SuperAdmin"))
        {
            var firstCampusId = await _db.Campuses.AsNoTracking().Where(c => c.IsActive && c.Institution.IsActive)
                .OrderBy(c => c.Id).Select(c => c.Id).FirstOrDefaultAsync(cancellationToken);
            if (firstCampusId > 0) return firstCampusId;
            throw new InvalidOperationException("Create an active campus before creating campus-owned records.");
        }
        throw new UnauthorizedAccessException("No active campus membership is assigned to this account.");
    }

    public async Task<bool> CanAccessAsync(
        ClaimsPrincipal principal,
        int? campusId,
        CancellationToken cancellationToken = default) =>
        campusId.HasValue && (await AllowedCampusIdsAsync(principal, cancellationToken)).Contains(campusId.Value);
}
