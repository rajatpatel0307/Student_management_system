using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;

namespace Student_Management_System.Services;

public sealed class StaffValidationException(string message) : Exception(message);

public sealed class StaffRepository(ApplicationDbContext db)
{
    public IQueryable<StaffMember> Query => db.StaffMembers
        .Include(s => s.EmployeeProfile).ThenInclude(e => e.User)
        .Include(s => s.EmployeeProfile).ThenInclude(e => e.Department)
        .Where(s => !s.IsDeleted);
    public Task<StaffMember?> FindAsync(int id) => Query.SingleOrDefaultAsync(s => s.Id == id);
}

public sealed class StaffManagementService(
    ApplicationDbContext db, StaffRepository repository,
    UserManager<ApplicationUser> users, AuditService audit)
{
    public async Task<int> SaveAsync(int? id, StaffInput input, string actor)
    {
        var workspace = StaffWorkspaceCatalog.All.SingleOrDefault(w => w.Role == input.Role)
            ?? throw new StaffValidationException("Select one of the five staff roles.");
        if (input.JoiningDate == default || input.JoiningDate.Date > DateTime.Today)
            throw new StaffValidationException("Select a joining date on or before today.");
        if (input.DateOfBirth.HasValue && (input.DateOfBirth.Value.Date >= input.JoiningDate.Date || input.DateOfBirth.Value.Year < 1900))
            throw new StaffValidationException("Date of birth must be valid and precede joining date.");
        if (input.DepartmentId.HasValue && !await db.Departments.AnyAsync(d => d.Id == input.DepartmentId && !d.IsArchived))
            throw new StaffValidationException("Select an active department.");
        if (string.IsNullOrWhiteSpace(input.FullName))
            throw new StaffValidationException("Full name is required.");
        var staff = id.HasValue ? await repository.FindAsync(id.Value) : null;
        if (id.HasValue && staff == null) throw new KeyNotFoundException();
        var user = staff?.EmployeeProfile.User;
        var phone = input.Phone?.Trim();
        var phoneChanged = staff == null || !string.Equals(user?.PhoneNumber, phone, StringComparison.Ordinal);
        var normalizedMobile = phoneChanged ? StaffIdentityFields.NormalizeMobile(phone) : user?.NormalizedMobileNumber;
        if (phoneChanged && await db.Users.AnyAsync(u => u.NormalizedMobileNumber == normalizedMobile && u.Id != (user == null ? "" : user.Id)))
            throw new StaffValidationException("This mobile number is already registered. Please use a different mobile number.");
        if (user != null && (await users.GetRolesAsync(user)).Any(r => !StaffWorkspaceCatalog.All.Any(w => w.Role == r)))
            throw new StaffValidationException("This account has protected roles and cannot be managed here.");
        var emailOwner = await users.FindByEmailAsync(input.Email.Trim());
        if (emailOwner != null && emailOwner.Id != user?.Id)
            throw new StaffValidationException("Email is already in use.");
        if (staff == null && (string.IsNullOrWhiteSpace(input.Password) || input.Password != input.ConfirmPassword))
            throw new StaffValidationException("A matching initial password is required.");
        if (staff != null)
        {
            byte[] version;
            try { version = Convert.FromBase64String(input.RowVersion ?? ""); }
            catch (FormatException) { throw new StaffValidationException("Refresh this staff record before editing."); }
            if (!version.SequenceEqual(staff.RowVersion)) throw new DbUpdateConcurrencyException();
        }
        var resultId = 0;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // A transient rollback must not leave Identity entities from the
            // previous attempt tracked as if they were committed.
            db.ChangeTracker.Clear();
            staff = id.HasValue ? await repository.FindAsync(id.Value) : null;
            user = staff?.EmployeeProfile.User;
            if (id.HasValue && staff == null) throw new KeyNotFoundException();
            if (staff != null && !Convert.FromBase64String(input.RowVersion ?? "").SequenceEqual(staff.RowVersion))
                throw new DbUpdateConcurrencyException();
            await using var transaction = await db.Database.BeginTransactionAsync();
            if (user == null)
            {
                var identifiers = await GenerateIdentifiersAsync();
                user = new ApplicationUser { FullName = input.FullName.Trim(), Email = input.Email.Trim(), UserName = identifiers.Username, PublicUserId = identifiers.EmployeeId, PhoneNumber = normalizedMobile, MustChangePassword = true };
                Ensure(await users.CreateAsync(user, input.Password!));
                var campusId = await db.UserCampusMemberships.Where(m => m.UserId == actor && m.IsActive)
                    .OrderByDescending(m => m.IsDefault).Select(m => (int?)m.CampusId).FirstOrDefaultAsync();
                var actorUser = await users.FindByIdAsync(actor);
                if (!campusId.HasValue && actorUser != null && await users.IsInRoleAsync(actorUser, "SuperAdmin"))
                    campusId = await db.Campuses.Where(c => c.IsActive && c.Institution.IsActive).OrderBy(c => c.Id).Select(c => (int?)c.Id).FirstOrDefaultAsync();
                if (campusId.HasValue)
                    db.UserCampusMemberships.Add(new UserCampusMembership { UserId = user.Id, CampusId = campusId.Value, IsDefault = true, GrantedByUserId = actor });
                var employee = new EmployeeProfile { UserId = user.Id, EmployeeNumber = identifiers.EmployeeId };
                db.EmployeeProfiles.Add(employee);
                staff = new StaffMember { EmployeeProfile = employee, CreatedByUserId = actor };
                db.StaffMembers.Add(staff);
            }
            user.FullName = input.FullName.Trim();
            if (!string.Equals(user.Email, input.Email.Trim(), StringComparison.OrdinalIgnoreCase)) user.EmailConfirmed = false;
            user.Email = input.Email.Trim();
            user.PhoneNumber = phoneChanged ? normalizedMobile : phone;
            if (phoneChanged)
            {
                user.PhoneNumberConfirmed = false;
            }
            user.IsActive = input.IsActive;
            Ensure(await users.UpdateAsync(user));
            var currentRoles = await users.GetRolesAsync(user);
            if (currentRoles.Count > 0) Ensure(await users.RemoveFromRolesAsync(user, currentRoles));
            Ensure(await users.AddToRoleAsync(user, workspace.Role));
            // Role changes revoke old grants, preventing accidental privilege carryover.
            if (id.HasValue && staff!.PrimaryRole != workspace.Role)
                db.UserPermissions.RemoveRange(await db.UserPermissions.Where(p => p.UserId == user.Id).ToListAsync());
            Ensure(await users.UpdateSecurityStampAsync(user));
            var employment = staff!.EmployeeProfile;
            employment.JobTitle = workspace.Title;
            employment.JoinedOn = input.JoiningDate.Date;
            employment.DepartmentId = input.DepartmentId;
            employment.IsActive = input.IsActive;
            staff.PrimaryRole = workspace.Role;
            staff.Gender = input.Gender;
            staff.DateOfBirth = input.DateOfBirth?.Date;
            staff.Address = input.Address;
            staff.EmergencyContact = input.EmergencyContact;
            staff.Notes = input.Notes;
            staff.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
            audit.Add(id.HasValue ? "Updated staff and role" : "Created staff", nameof(StaffMember), staff.Id);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            resultId = staff.Id;
        });
        return resultId;
    }

    private async Task<(string EmployeeId, string Username)> GenerateIdentifiersAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var employeeId = "EMP-" + token.ToUpperInvariant();
            var username = "staff." + token;
            // Include inactive/deleted staff and all Identity accounts. Database
            // unique indexes also protect simultaneous account creation.
            if (!await db.EmployeeProfiles.AnyAsync(e => e.EmployeeNumber == employeeId)
                && !await db.Users.AnyAsync(u => u.PublicUserId == employeeId)
                && await users.FindByNameAsync(username) == null)
                return (employeeId, username);
        }
        throw new StaffValidationException("Could not allocate staff identifiers. Please try again.");
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new StaffValidationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
