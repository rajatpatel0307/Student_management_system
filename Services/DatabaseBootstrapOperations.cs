using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Data;
using Student_Management_System.Models;

namespace Student_Management_System.Services;

/// <summary>
/// Keeps asynchronous database-bootstrap iteration out of top-level Program.cs.
/// Besides making the operations reusable and testable, this prevents Visual
/// Studio from evaluating uninitialized loop-state fields in Program's Locals
/// window after an unrelated startup exception.
/// </summary>
public static class DatabaseBootstrapOperations
{
    public static async Task EnsureInitialSuperAdminAsync(
        ApplicationDbContext db,
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(roleManager);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredEmail = configuration["BootstrapAdmin:Email"];
        var configuredPassword = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(configuredEmail) ||
            string.IsNullOrWhiteSpace(configuredPassword))
            return;

        var accountCreated = false;
        var account = await userManager.FindByEmailAsync(configuredEmail);
        if (account is null)
        {
            account = new ApplicationUser
            {
                UserName = configuredEmail,
                Email = configuredEmail,
                EmailConfirmed = true,
                FullName = "Super Administrator"
            };
            var createResult = await userManager.CreateAsync(
                account,
                configuredPassword);
            if (!createResult.Succeeded)
                throw IdentityOperationFailure(
                    "create the initial SuperAdmin account",
                    createResult);
            accountCreated = true;
        }

        if (!account.IsActive)
            throw new InvalidOperationException(
                "The configured SuperAdmin account is inactive. Use the protected account-recovery workflow instead of bootstrap reactivation.");

        if (!await userManager.IsInRoleAsync(account, "SuperAdmin"))
        {
            var roleResult = await userManager.AddToRoleAsync(account, "SuperAdmin");
            if (!roleResult.Succeeded)
                throw IdentityOperationFailure(
                    "assign the SuperAdmin role",
                    roleResult);
        }

        var activeCampusIds = await db.Campuses
            .AsNoTracking()
            .Where(campus => campus.IsActive)
            .OrderBy(campus => campus.Id)
            .Select(campus => campus.Id)
            .ToListAsync(cancellationToken);
        var existingCampusIds = await db.UserCampusMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.UserId == account.Id && membership.IsActive)
            .Select(membership => membership.CampusId)
            .ToListAsync(cancellationToken);
        var existingCampusIdsSet = existingCampusIds.ToHashSet();
        var hasDefaultCampus = await db.UserCampusMemberships
            .AsNoTracking()
            .AnyAsync(
                membership =>
                    membership.UserId == account.Id &&
                    membership.IsActive &&
                    membership.IsDefault,
                cancellationToken);

        foreach (var campusId in activeCampusIds)
        {
            if (existingCampusIdsSet.Contains(campusId))
                continue;

            db.UserCampusMemberships.Add(new UserCampusMembership
            {
                UserId = account.Id,
                CampusId = campusId,
                IsDefault = !hasDefaultCampus,
                IsActive = true,
                GrantedUtc = DateTime.UtcNow,
                GrantedByUserId = account.Id
            });
            hasDefaultCampus = true;
        }

        db.AuditLogs.Add(new AuditLog
        {
            UserId = account.Id,
            Action = accountCreated
                ? "BootstrapSuperAdminCreated"
                : "BootstrapSuperAdminVerified",
            EntityName = nameof(ApplicationUser),
            EntityId = account.Id,
            CorrelationId = Guid.NewGuid().ToString("D"),
            Outcome = "Succeeded",
            DetailsJson = "{\"source\":\"ProtectedBootstrap\",\"campusScope\":\"AllActive\"}",
            CreatedUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task EnsurePermissionsAsync(
        ApplicationDbContext db,
        IReadOnlyDictionary<string, string> permissionCatalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(permissionCatalog);

        var existingKeys = new HashSet<string>(
            await db.Permissions
                .AsNoTracking()
                .Select(permission => permission.Key)
                .ToListAsync(cancellationToken));

        foreach (var entry in permissionCatalog)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) ||
                existingKeys.Contains(entry.Key))
                continue;

            db.Permissions.Add(new Permission
            {
                Key = entry.Key,
                Description = entry.Value
            });
            existingKeys.Add(entry.Key);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task EnsureRolePermissionsAsync(
        ApplicationDbContext db,
        IReadOnlyDictionary<string, string[]> rolePermissionCatalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(rolePermissionCatalog);

        var permissionsByKey = await db.Permissions
            .AsNoTracking()
            .ToDictionaryAsync(
                permission => permission.Key,
                cancellationToken);
        var persistedMappings = await db.RolePermissions
            .AsNoTracking()
            .Select(mapping => new
            {
                mapping.RoleName,
                mapping.PermissionId
            })
            .ToListAsync(cancellationToken);
        var existingMappings = persistedMappings
            .Select(mapping => (mapping.RoleName, mapping.PermissionId))
            .ToHashSet();

        foreach (var roleEntry in rolePermissionCatalog)
        {
            if (string.IsNullOrWhiteSpace(roleEntry.Key) ||
                roleEntry.Value is null)
                continue;

            foreach (var configuredPermissionKey in roleEntry.Value)
            {
                if (!permissionsByKey.TryGetValue(
                        configuredPermissionKey,
                        out var configuredPermission) ||
                    configuredPermission is null)
                    continue;

                var mappingKey = (roleEntry.Key, configuredPermission.Id);
                if (!existingMappings.Add(mappingKey))
                    continue;

                db.RolePermissions.Add(new RolePermission
                {
                    RoleName = roleEntry.Key,
                    PermissionId = configuredPermission.Id
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task UpgradeLegacyTeachersAsync(
        ApplicationDbContext db,
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(roleManager);
        ArgumentNullException.ThrowIfNull(userManager);

        if (!await roleManager.RoleExistsAsync("Teacher"))
            return;

        var legacyUsers = await userManager.GetUsersInRoleAsync("Teacher");
        foreach (var account in legacyUsers.Where(account => account is not null))
        {
            var profile = await db.Teachers
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    teacher => teacher.UserId == account.Id,
                    cancellationToken);
            var removeResult = await userManager.RemoveFromRoleAsync(
                account,
                "Teacher");
            if (!removeResult.Succeeded)
                throw IdentityOperationFailure("remove the legacy Teacher role", removeResult);

            var targetRole = profile?.TeacherType == TeacherType.HOD
                ? "HOD"
                : "Professor";
            var addResult = await userManager.AddToRoleAsync(account, targetRole);
            if (!addResult.Succeeded)
                throw IdentityOperationFailure($"assign the {targetRole} role", addResult);
        }
    }

    private static InvalidOperationException IdentityOperationFailure(
        string operation,
        IdentityResult result) =>
        new(
            $"Could not {operation}: " +
            string.Join("; ", result.Errors.Select(error => error.Description)));
}
