using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Security;
using System.Security.Claims;

namespace Student_Management_System.Services;

public interface IHodProfessorService
{
    Task<PagedResult<ProfessorListItemViewModel>> GetDepartmentProfessorsAsync(
        ClaimsPrincipal actor,
        ListQuery request,
        CancellationToken cancellationToken = default);
    Task<Teacher?> FindManageableProfessorAsync(
        ClaimsPrincipal actor,
        int teacherId,
        CancellationToken cancellationToken = default);
    Task<Teacher?> FindViewableProfessorAsync(
        ClaimsPrincipal actor,
        int teacherId,
        CancellationToken cancellationToken = default);
}

public sealed class HodProfessorService : IHodProfessorService
{
    private readonly ApplicationDbContext _db;
    private readonly PermissionService _permissions;
    private readonly ICampusScopeService _campusScope;

    public HodProfessorService(
        ApplicationDbContext db,
        PermissionService permissions,
        ICampusScopeService campusScope)
    {
        _db = db;
        _permissions = permissions;
        _campusScope = campusScope;
    }

    public async Task<PagedResult<ProfessorListItemViewModel>>
        GetDepartmentProfessorsAsync(
            ClaimsPrincipal actor,
            ListQuery request,
            CancellationToken cancellationToken = default)
    {
        if (!actor.IsInRole("HOD") ||
            !await _permissions.HasAsync(
                actor, PermissionKeys.ProfessorViewDepartment, cancellationToken))
            throw new UnauthorizedAccessException();
        request.Normalize();
        var actorId = actor.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
        var departmentId = await _db.Teachers.AsNoTracking()
            .Where(t => t.UserId == actorId && t.TeacherType == TeacherType.HOD &&
                        !t.IsArchived && t.User.IsActive)
            .Select(t => t.DepartmentId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!departmentId.HasValue) throw new UnauthorizedAccessException();
        var campusIds = await _campusScope.AllowedCampusIdsAsync(
            actor, cancellationToken);
        if (campusIds.Length == 0) throw new UnauthorizedAccessException();

        var query = _db.Teachers.AsNoTracking()
            .Where(t => t.DepartmentId == departmentId &&
                        t.TeacherType == TeacherType.Professor &&
                        !t.IsArchived &&
                        _db.UserCampusMemberships.Any(m =>
                            m.UserId == t.UserId && m.IsActive &&
                            campusIds.Contains(m.CampusId)));
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(t =>
                t.FullName.Contains(request.Search) ||
                t.Email.Contains(request.Search) ||
                t.Qualification.Contains(request.Search));
        if (request.Status == "active")
            query = query.Where(t => t.User.IsActive);
        if (request.Status == "inactive")
            query = query.Where(t => !t.User.IsActive);
        query = (request.Sort?.ToLowerInvariant(), request.Direction) switch
        {
            ("email", "desc") => query.OrderByDescending(t => t.Email),
            ("email", _) => query.OrderBy(t => t.Email),
            ("status", "desc") => query.OrderByDescending(t => t.User.IsActive)
                .ThenBy(t => t.FullName),
            ("status", _) => query.OrderBy(t => t.User.IsActive)
                .ThenBy(t => t.FullName),
            ("name", "desc") => query.OrderByDescending(t => t.FullName),
            _ => query.OrderBy(t => t.FullName)
        };

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new ProfessorListItemViewModel
            {
                TeacherId = t.Id,
                ProfessorNumber = _db.EmployeeProfiles
                    .Where(e => e.UserId == t.UserId)
                    .Select(e => e.EmployeeNumber)
                    .FirstOrDefault() ?? "Not assigned",
                FullName = t.FullName,
                Email = t.Email,
                Phone = t.Phone,
                DepartmentName = t.Department != null
                    ? t.Department.Name
                    : "Unassigned",
                Designation = string.IsNullOrWhiteSpace(t.Qualification)
                    ? "Professor"
                    : t.Qualification,
                IsActive = t.User.IsActive,
                HasProfileImage = t.User.ProfileImageStorageKey != null,
                AssignedSubjects = t.SubjectAssignments
                    .Select(a => a.Subject.Name).Distinct().OrderBy(n => n).ToList(),
                AssignedClasses = t.ClassAssignments
                    .Select(a => a.Class.Name).Distinct().OrderBy(n => n).ToList()
            })
            .ToListAsync(cancellationToken);
        return new PagedResult<ProfessorListItemViewModel>
        {
            Items = items,
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize,
            Search = request.Search,
            Sort = request.Sort,
            Direction = request.Direction
        };
    }

    public async Task<Teacher?> FindManageableProfessorAsync(
        ClaimsPrincipal actor,
        int teacherId,
        CancellationToken cancellationToken = default)
    {
        if (!await _permissions.HasAsync(
                actor, PermissionKeys.ProfileProfessorEdit, cancellationToken))
            return null;
        return await FindScopedProfessorAsync(
            actor, teacherId, PermissionKeys.ProfessorManageDepartment, cancellationToken);
    }

    public async Task<Teacher?> FindViewableProfessorAsync(
        ClaimsPrincipal actor,
        int teacherId,
        CancellationToken cancellationToken = default) =>
        await FindScopedProfessorAsync(
            actor, teacherId, PermissionKeys.ProfessorViewDepartment, cancellationToken);

    private async Task<Teacher?> FindScopedProfessorAsync(
        ClaimsPrincipal actor,
        int teacherId,
        string permission,
        CancellationToken cancellationToken)
    {
        if (!actor.IsInRole("HOD") ||
            !await _permissions.HasAsync(actor, permission, cancellationToken))
            return null;
        var actorId = actor.FindFirstValue(ClaimTypes.NameIdentifier);
        var actorProfile = await _db.Teachers.AsNoTracking()
            .Where(t => t.UserId == actorId && t.TeacherType == TeacherType.HOD &&
                        !t.IsArchived && t.User.IsActive)
            .Select(t => new { t.DepartmentId, t.UserId })
            .SingleOrDefaultAsync(cancellationToken);
        if (actorProfile?.DepartmentId == null) return null;
        var campusIds = await _campusScope.AllowedCampusIdsAsync(
            actor, cancellationToken);
        return await _db.Teachers.Include(t => t.User)
            .SingleOrDefaultAsync(t =>
                t.Id == teacherId &&
                t.UserId != actorProfile.UserId &&
                t.DepartmentId == actorProfile.DepartmentId &&
                t.TeacherType == TeacherType.Professor &&
                !t.IsArchived &&
                _db.UserCampusMemberships.Any(m =>
                    m.UserId == t.UserId && m.IsActive &&
                    campusIds.Contains(m.CampusId)),
                cancellationToken);
    }
}
