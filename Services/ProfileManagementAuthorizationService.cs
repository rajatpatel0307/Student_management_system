using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Security;
using System.Security.Claims;

namespace Student_Management_System.Services;

public enum ManagedProfileKind
{
    Student,
    Professor,
    Hod,
    OperationalStaff,
    Admin,
    SuperAdmin
}

public interface IProfileManagementAuthorizationService
{
    Task<bool> CanEditAsync(
        ClaimsPrincipal actor,
        string targetUserId,
        ManagedProfileKind targetKind,
        int? targetDepartmentId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Server-side profile hierarchy gate. Controllers must still use dedicated
/// allowlisted input models and concurrency tokens for each mutation.
/// </summary>
public sealed class ProfileManagementAuthorizationService
    : IProfileManagementAuthorizationService
{
    private static readonly HashSet<string> OperationalRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Accountant", "Librarian", "Admissions", "AdmissionsStaff",
        "ExaminationOfficer", "PlacementOfficer", "Employee", "Staff",
        "TransportManager", "HostelManager"
    };

    private readonly ApplicationDbContext _db;
    private readonly PermissionService _permissions;
    private readonly UserManager<ApplicationUser> _users;

    public ProfileManagementAuthorizationService(
        ApplicationDbContext db,
        PermissionService permissions,
        UserManager<ApplicationUser> users)
    {
        _db = db;
        _permissions = permissions;
        _users = users;
    }

    public async Task<bool> CanEditAsync(
        ClaimsPrincipal actor,
        string targetUserId,
        ManagedProfileKind targetKind,
        int? targetDepartmentId = null,
        CancellationToken cancellationToken = default)
    {
        var actorId = actor.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actorId) || actorId == targetUserId)
            return false;
        var actorUser = await _users.FindByIdAsync(actorId);
        var targetUser = await _users.FindByIdAsync(targetUserId);
        if (actorUser is not { IsActive: true } || targetUser == null)
            return false;

        var targetRoles = await _users.GetRolesAsync(targetUser);
        if (!TargetRoleMatches(targetRoles, targetKind)) return false;
        var permission = targetKind switch
        {
            ManagedProfileKind.Student => PermissionKeys.ProfileStudentEdit,
            ManagedProfileKind.Professor => PermissionKeys.ProfileProfessorEdit,
            ManagedProfileKind.Hod => PermissionKeys.ProfileHodEdit,
            ManagedProfileKind.OperationalStaff => PermissionKeys.ProfileOperationalStaffEdit,
            ManagedProfileKind.Admin => PermissionKeys.ProfileAdminEdit,
            ManagedProfileKind.SuperAdmin => PermissionKeys.ProfileSuperAdminEdit,
            _ => throw new ArgumentOutOfRangeException(nameof(targetKind))
        };
        if (!await _permissions.HasAsync(actor, permission, cancellationToken))
            return false; // An explicit denial is authoritative in PermissionService.

        if (targetKind is ManagedProfileKind.Student or ManagedProfileKind.Professor)
        {
            if (!actor.IsInRole("HOD") || !targetDepartmentId.HasValue) return false;
            var actorDepartment = await _db.Teachers.AsNoTracking()
                .Where(t => t.UserId == actorId && t.TeacherType == TeacherType.HOD &&
                            !t.IsArchived && t.User.IsActive)
                .Select(t => t.DepartmentId).SingleOrDefaultAsync(cancellationToken);
            return actorDepartment == targetDepartmentId &&
                   await SharesActiveCampusAsync(actorId, targetUserId, cancellationToken);
        }

        if (targetKind is ManagedProfileKind.Hod or ManagedProfileKind.OperationalStaff)
            return actor.IsInRole("Admin") &&
                   await SharesActiveCampusAsync(actorId, targetUserId, cancellationToken);

        if (targetKind == ManagedProfileKind.Admin)
            return actor.IsInRole("SuperAdmin");

        return actor.IsInRole("SuperAdmin") &&
               await HasProtectedSuperAdminConfirmationAsync(actor, actorUser);
    }

    private async Task<bool> SharesActiveCampusAsync(
        string actorId,
        string targetId,
        CancellationToken cancellationToken)
    {
        var actorCampuses = _db.UserCampusMemberships.AsNoTracking()
            .Where(m => m.UserId == actorId && m.IsActive).Select(m => m.CampusId);
        return await _db.UserCampusMemberships.AsNoTracking().AnyAsync(m =>
            m.UserId == targetId && m.IsActive && actorCampuses.Contains(m.CampusId),
            cancellationToken);
    }

    private static bool TargetRoleMatches(
        IEnumerable<string> roles,
        ManagedProfileKind kind) => kind switch
    {
        ManagedProfileKind.Student => roles.Contains("Student", StringComparer.OrdinalIgnoreCase),
        ManagedProfileKind.Professor => roles.Any(r =>
            r.Equals("Professor", StringComparison.OrdinalIgnoreCase) ||
            r.Equals("Teacher", StringComparison.OrdinalIgnoreCase)),
        ManagedProfileKind.Hod => roles.Contains("HOD", StringComparer.OrdinalIgnoreCase),
        ManagedProfileKind.OperationalStaff => roles.Any(OperationalRoles.Contains),
        ManagedProfileKind.Admin => roles.Contains("Admin", StringComparer.OrdinalIgnoreCase) &&
                                    !roles.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase),
        ManagedProfileKind.SuperAdmin => roles.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase),
        _ => false
    };

    private static Task<bool> HasProtectedSuperAdminConfirmationAsync(
        ClaimsPrincipal actor,
        ApplicationUser actorUser)
    {
        var authTimeValue = actor.FindFirstValue("auth_time");
        var recent = long.TryParse(authTimeValue, out var authTime) &&
            DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(authTime) <=
            TimeSpan.FromMinutes(5);
        var mfaSatisfied = !actorUser.TwoFactorEnabled ||
            actor.FindAll("amr").Any(c => c.Value.Equals("mfa", StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(recent && mfaSatisfied &&
            actor.HasClaim("profile_change_confirmed", "true"));
    }
}
