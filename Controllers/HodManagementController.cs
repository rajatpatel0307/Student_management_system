using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Student_Management_System.Models;
using Student_Management_System.Services;
using Student_Management_System.Security;
using System.Security.Claims;

namespace Student_Management_System.Controllers;

[Authorize(Roles = "Admin,SuperAdmin,HOD")]
public class HodManagementController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IHodProfessorService _professors;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AuditService _audit;
    private readonly IPrivateFileStorage _privateFiles;
    private readonly PermissionService _permissions;
    private readonly ICampusScopeService _campusScope;
    public HodManagementController(
        ApplicationDbContext db,
        IHodProfessorService professors,
        UserManager<ApplicationUser> users,
        AuditService audit,
        IPrivateFileStorage privateFiles,
        PermissionService permissions,
        ICampusScopeService campusScope)
    {
        _db = db;
        _professors = professors;
        _users = users;
        _audit = audit;
        _privateFiles = privateFiles;
        _permissions = permissions;
        _campusScope = campusScope;
    }

    [Authorize(Policy = Student_Management_System.Security.ProfilePolicies.ProfessorViewDepartment)]
    [Authorize(Roles = "HOD")]
    public async Task<IActionResult> Professors(
        [FromQuery] ListQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            return View(await _professors.GetDepartmentProfessorsAsync(
                User, request, cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet]
    [Authorize(Roles = "HOD")]
    [Authorize(Policy = ProfilePolicies.ProfessorManageDepartment)]
    [Authorize(Policy = ProfilePolicies.ProfessorEdit)]
    public async Task<IActionResult> EditProfessor(
        int id,
        CancellationToken cancellationToken)
    {
        var professor = await _professors.FindManageableProfessorAsync(
            User, id, cancellationToken);
        if (professor == null) return NotFound();
        return View(new ProfessorEditInputModel
        {
            TeacherId = professor.Id,
            FullName = professor.FullName,
            Email = professor.Email,
            Phone = professor.Phone,
            Qualification = professor.Qualification,
            IsActive = professor.User.IsActive,
            RowVersion = professor.RowVersion
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "HOD")]
    [Authorize(Policy = ProfilePolicies.ProfessorManageDepartment)]
    [Authorize(Policy = ProfilePolicies.ProfessorEdit)]
    public async Task<IActionResult> EditProfessor(
        int id,
        ProfessorEditInputModel input,
        CancellationToken cancellationToken)
    {
        if (id != input.TeacherId) return NotFound();
        var professor = await _professors.FindManageableProfessorAsync(
            User, id, cancellationToken);
        if (professor == null) return NotFound();
        var email = input.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AsNoTracking().AnyAsync(
                u => u.Id != professor.UserId && u.NormalizedEmail ==
                     email.ToUpperInvariant(), cancellationToken))
            ModelState.AddModelError(nameof(input.Email), "That email is already in use.");
        if (!ModelState.IsValid) return View(input);

        ConcurrencyToken.Apply(_db, professor, input.RowVersion);
        var changedFields = new List<string>();
        if (professor.FullName != input.FullName.Trim()) changedFields.Add("FullName");
        if (!string.Equals(professor.Email, email, StringComparison.OrdinalIgnoreCase))
            changedFields.Add("Email");
        if (professor.Phone != (input.Phone?.Trim() ?? "")) changedFields.Add("Phone");
        if (professor.Qualification != input.Qualification.Trim())
            changedFields.Add("Qualification");
        if (professor.User.IsActive != input.IsActive) changedFields.Add("Status");
        professor.FullName = input.FullName.Trim();
        professor.Email = email;
        professor.Phone = input.Phone?.Trim() ?? "";
        professor.Qualification = input.Qualification.Trim();
        professor.User.IsActive = input.IsActive;
        professor.User.FullName = professor.FullName;
        professor.User.Email = email;
        professor.User.UserName = email;
        professor.User.NormalizedEmail = _users.NormalizeEmail(email);
        professor.User.NormalizedUserName = _users.NormalizeName(email);
        var targetCampusIds = await _db.UserCampusMemberships.AsNoTracking()
            .Where(m => m.UserId == professor.UserId && m.IsActive)
            .Select(m => m.CampusId).ToArrayAsync(cancellationToken);
        _audit.Add("Updated professor authoritative profile", nameof(Teacher), id,
            detailsJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                TargetUserId = professor.UserId,
                ChangedFields = changedFields,
                professor.DepartmentId,
                CampusIds = targetCampusIds
            }));
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError("", "This professor was changed by another user. Reload the page and try again.");
            var current = await _professors.FindManageableProfessorAsync(User, id, cancellationToken);
            if (current == null) return NotFound();
            input.RowVersion = current.RowVersion;
            return View(input);
        }
        await _users.UpdateSecurityStampAsync(professor.User);
        TempData["Success"] = "Professor profile updated.";
        return RedirectToAction(nameof(Professors));
    }

    [HttpGet]
    [Authorize(Roles = "HOD")]
    [Authorize(Policy = ProfilePolicies.ProfessorViewDepartment)]
    public async Task<IActionResult> ProfessorImage(
        int id,
        CancellationToken cancellationToken)
    {
        var professor = await _professors.FindViewableProfessorAsync(
            User, id, cancellationToken);
        if (professor == null ||
            string.IsNullOrWhiteSpace(professor.User.ProfileImageStorageKey))
            return NotFound();
        var opened = await _privateFiles.OpenReadAsync(
            professor.User.ProfileImageStorageKey,
            PrivateFileKind.ProfileImage,
            cancellationToken);
        if (opened == null) return NotFound();
        Response.Headers.CacheControl = "private, no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        return File(opened.Stream, opened.ContentType, fileDownloadName: null,
            enableRangeProcessing: false);
    }

    [HttpGet]
    [Authorize(Roles = "HOD")]
    [Authorize(Policy = ProfilePolicies.StudentEdit)]
    public async Task<IActionResult> EditStudent(int id, CancellationToken cancellationToken)
    {
        var student = await FindManageableStudentAsync(id, cancellationToken);
        if (student == null) return NotFound();
        return View(new StudentAuthoritativeEditInputModel
        {
            StudentId = student.Id,
            FullName = student.FullName,
            Email = student.Email,
            Phone = student.Phone,
            AdmissionNumber = student.AdmissionNumber,
            RollNumber = student.RollNumber,
            IsActive = student.User?.IsActive ?? false,
            RowVersion = student.RowVersion
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "HOD")]
    [Authorize(Policy = ProfilePolicies.StudentEdit)]
    public async Task<IActionResult> EditStudent(
        int id,
        StudentAuthoritativeEditInputModel input,
        CancellationToken cancellationToken)
    {
        if (id != input.StudentId) return NotFound();
        var student = await FindManageableStudentAsync(id, cancellationToken);
        if (student?.User == null) return NotFound();
        if (!ModelState.IsValid) return View(input);
        var email = input.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AsNoTracking().AnyAsync(u =>
                u.Id != student.UserId && u.NormalizedEmail == email.ToUpperInvariant(),
                cancellationToken))
            ModelState.AddModelError(nameof(input.Email), "That email is already in use.");
        if (!string.IsNullOrWhiteSpace(input.AdmissionNumber) &&
            await _db.Students.AsNoTracking().AnyAsync(s => s.Id != id &&
                s.AdmissionNumber == input.AdmissionNumber.Trim(), cancellationToken))
            ModelState.AddModelError(nameof(input.AdmissionNumber), "That admission number is already in use.");
        if (!string.IsNullOrWhiteSpace(input.RollNumber) &&
            await _db.Students.AsNoTracking().AnyAsync(s => s.Id != id &&
                s.RollNumber == input.RollNumber.Trim(), cancellationToken))
            ModelState.AddModelError(nameof(input.RollNumber), "That roll number is already in use.");
        if (!ModelState.IsValid) return View(input);

        ConcurrencyToken.Apply(_db, student, input.RowVersion);
        var changedFields = new List<string>();
        if (student.FullName != input.FullName.Trim()) changedFields.Add("FullName");
        if (!string.Equals(student.Email, email, StringComparison.OrdinalIgnoreCase)) changedFields.Add("Email");
        if (student.Phone != input.Phone?.Trim()) changedFields.Add("Phone");
        if (student.AdmissionNumber != input.AdmissionNumber?.Trim()) changedFields.Add("AdmissionNumber");
        if (student.RollNumber != input.RollNumber?.Trim()) changedFields.Add("RollNumber");
        if (student.User.IsActive != input.IsActive) changedFields.Add("Status");
        student.FullName = input.FullName.Trim();
        student.Email = email;
        student.Phone = input.Phone?.Trim();
        student.AdmissionNumber = input.AdmissionNumber?.Trim();
        student.RollNumber = input.RollNumber?.Trim();
        student.User.IsActive = input.IsActive;
        student.User.FullName = student.FullName;
        student.User.Email = email;
        student.User.UserName = email;
        student.User.PhoneNumber = student.Phone;
        student.User.NormalizedEmail = _users.NormalizeEmail(email);
        student.User.NormalizedUserName = _users.NormalizeName(email);
        var campusIds = await _db.UserCampusMemberships.AsNoTracking()
            .Where(m => m.UserId == student.UserId && m.IsActive)
            .Select(m => m.CampusId).ToArrayAsync(cancellationToken);
        _audit.Add("Updated student authoritative profile", nameof(Student), id,
            detailsJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                TargetUserId = student.UserId,
                ChangedFields = changedFields,
                student.DepartmentId,
                CampusIds = campusIds
            }));
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError("", "This student was changed by another user. Reload the page and try again.");
            var current = await FindManageableStudentAsync(id, cancellationToken);
            if (current == null) return NotFound();
            input.RowVersion = current.RowVersion;
            return View(input);
        }
        await _users.UpdateSecurityStampAsync(student.User);
        TempData["Success"] = "Student profile updated.";
        return RedirectToAction("Index", "Students");
    }

    private async Task<Student?> FindManageableStudentAsync(
        int studentId,
        CancellationToken cancellationToken)
    {
        if (!await _permissions.HasAsync(User, PermissionKeys.ProfileStudentEdit, cancellationToken))
            return null;
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var departmentId = await _db.Teachers.AsNoTracking()
            .Where(t => t.UserId == actorId && t.TeacherType == TeacherType.HOD &&
                        !t.IsArchived && t.User.IsActive)
            .Select(t => t.DepartmentId).SingleOrDefaultAsync(cancellationToken);
        if (!departmentId.HasValue) return null;
        var campusIds = await _campusScope.AllowedCampusIdsAsync(User, cancellationToken);
        var student = await _db.Students.Include(s => s.User)
            .SingleOrDefaultAsync(s => s.Id == studentId && s.UserId != actorId &&
                !s.IsArchived && s.DepartmentId == departmentId &&
                _db.UserCampusMemberships.Any(m => m.UserId == s.UserId &&
                    m.IsActive && campusIds.Contains(m.CampusId)), cancellationToken);
        return student?.User != null && await _users.IsInRoleAsync(student.User, "Student")
            ? student
            : null;
    }

    public async Task<IActionResult> Index(int? departmentId)
    {
        var resolvedDepartmentId = await ResolveDepartmentIdAsync(departmentId);
        if (!resolvedDepartmentId.HasValue) return Forbid();
        ViewBag.DepartmentId = resolvedDepartmentId.Value;
        ViewBag.Department = await _db.Departments.FindAsync(resolvedDepartmentId.Value);
        if (!User.IsInRole("HOD"))
            ViewBag.Departments = await _db.Departments.OrderBy(d => d.Name).ToListAsync();
        var isHod = User.IsInRole("HOD");
        var actorCampusIds = isHod
            ? await _campusScope.AllowedCampusIdsAsync(User)
            : Array.Empty<int>();
        ViewBag.Professors = await _db.Teachers.Where(t =>
            t.DepartmentId == resolvedDepartmentId && t.TeacherType == TeacherType.Professor &&
            (!isHod || _db.UserCampusMemberships.Any(m =>
                m.UserId == t.UserId && m.IsActive && actorCampusIds.Contains(m.CampusId))))
            .OrderBy(t => t.FullName).ToListAsync();
        ViewBag.Classes = await _db.Classes.Where(c => c.DepartmentId == resolvedDepartmentId).OrderBy(c => c.Name).ToListAsync();
        ViewBag.Subjects = await _db.Subjects.Where(s => s.DepartmentId == resolvedDepartmentId).Include(s => s.Class).OrderBy(s => s.Name).ToListAsync();
        ViewBag.Students = await _db.Students.Where(s => s.DepartmentId == resolvedDepartmentId &&
            (!isHod || s.UserId != null && _db.UserCampusMemberships.Any(m =>
                m.UserId == s.UserId && m.IsActive && actorCampusIds.Contains(m.CampusId))))
            .OrderBy(s => s.FullName).ToListAsync();
        ViewBag.Sections = await _db.Sections.Where(s => s.Class.DepartmentId == resolvedDepartmentId).Include(s => s.Class).OrderBy(s => s.Name).ToListAsync();
        ViewBag.ProfessorAssignments = await _db.TeacherSubjectAssignments
            .Include(a => a.Teacher).Include(a => a.Subject).ThenInclude(s => s.Class)
            .Where(a => a.Teacher.DepartmentId == resolvedDepartmentId &&
                (!isHod || _db.UserCampusMemberships.Any(m => m.UserId == a.Teacher.UserId &&
                    m.IsActive && actorCampusIds.Contains(m.CampusId))))
            .OrderByDescending(a => a.AssignedUtc).ToListAsync();
        ViewBag.StudentAssignments = await _db.StudentSubjectEnrollments
            .Include(a => a.Student).ThenInclude(s => s.Class)
            .Include(a => a.Subject)
            .Where(a => a.Student.DepartmentId == resolvedDepartmentId &&
                (!isHod || a.Student.UserId != null && _db.UserCampusMemberships.Any(m =>
                    m.UserId == a.Student.UserId && m.IsActive && actorCampusIds.Contains(m.CampusId))))
            .OrderByDescending(a => a.EnrolledUtc).ToListAsync();
        return View();
    }

    [HttpPost]
    [Authorize(Policy = ProfilePolicies.ProfessorManageDepartment)]
    public async Task<IActionResult> AssignProfessor(int teacherId, int classId, int subjectId, int? departmentId)
    {
        var resolvedDepartmentId = await ResolveDepartmentIdAsync(departmentId);
        if (!resolvedDepartmentId.HasValue) return Forbid();
        var valid = await _db.Teachers.AnyAsync(t => t.Id == teacherId && t.DepartmentId == resolvedDepartmentId && t.TeacherType == TeacherType.Professor)
            && await _db.Classes.AnyAsync(c => c.Id == classId && c.DepartmentId == resolvedDepartmentId)
            && await _db.Subjects.AnyAsync(s => s.Id == subjectId && s.DepartmentId == resolvedDepartmentId && s.ClassId == classId);
        if (!valid) return NotFound();
        if (User.IsInRole("HOD"))
        {
            var targetUserId = await _db.Teachers.AsNoTracking()
                .Where(t => t.Id == teacherId).Select(t => t.UserId).SingleOrDefaultAsync();
            if (targetUserId == null || !await SharesActorCampusAsync(targetUserId))
                return NotFound();
        }
        if (!await _db.TeacherClassAssignments.AnyAsync(a => a.TeacherId == teacherId && a.ClassId == classId))
            _db.Add(new TeacherClassAssignment { TeacherId = teacherId, ClassId = classId });
        if (!await _db.TeacherSubjectAssignments.AnyAsync(a => a.TeacherId == teacherId && a.SubjectId == subjectId))
            _db.Add(new TeacherSubjectAssignment { TeacherId = teacherId, SubjectId = subjectId });
        await _db.SaveChangesAsync(); TempData["Success"] = "Professor assignment saved."; return RedirectToAction(nameof(Index), new { departmentId = resolvedDepartmentId });
    }

    [HttpPost]
    public async Task<IActionResult> AssignStudent(int studentId, int classId, int sectionId, int subjectId, int? departmentId)
    {
        var resolvedDepartmentId = await ResolveDepartmentIdAsync(departmentId);
        if (!resolvedDepartmentId.HasValue) return Forbid();
        var valid = await _db.Students.AnyAsync(s => s.Id == studentId && s.DepartmentId == resolvedDepartmentId)
            && await _db.Classes.AnyAsync(c => c.Id == classId && c.DepartmentId == resolvedDepartmentId)
            && await _db.Sections.AnyAsync(s => s.Id == sectionId && s.ClassId == classId)
            && await _db.Subjects.AnyAsync(s => s.Id == subjectId && s.DepartmentId == resolvedDepartmentId && s.ClassId == classId);
        if (!valid) return NotFound();
        var student = await _db.Students.FindAsync(studentId);
        if (student == null) return NotFound();
        if (User.IsInRole("HOD") &&
            (student.UserId == null || !await SharesActorCampusAsync(student.UserId)))
            return NotFound();
        student.ClassId = classId; student.SectionId = sectionId;
        if (!await _db.StudentSubjectEnrollments.AnyAsync(e => e.StudentId == studentId && e.SubjectId == subjectId))
            _db.Add(new StudentSubjectEnrollment { StudentId = studentId, SubjectId = subjectId });
        await _db.SaveChangesAsync(); TempData["Success"] = "Student assignment saved."; return RedirectToAction(nameof(Index), new { departmentId = resolvedDepartmentId });
    }

    private async Task<int?> ResolveDepartmentIdAsync(int? requestedDepartmentId)
    {
        if (!User.IsInRole("HOD"))
        {
            if (requestedDepartmentId.HasValue && await _db.Departments.AnyAsync(d => d.Id == requestedDepartmentId.Value))
                return requestedDepartmentId;
            return await _db.Departments.OrderBy(d => d.Name).Select(d => (int?)d.Id).FirstOrDefaultAsync();
        }
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return await _db.Teachers.Where(t => t.UserId == uid && t.TeacherType == TeacherType.HOD).Select(t => t.DepartmentId).SingleOrDefaultAsync();
    }

    private async Task<bool> SharesActorCampusAsync(string targetUserId)
    {
        var campusIds = await _campusScope.AllowedCampusIdsAsync(User);
        return await _db.UserCampusMemberships.AsNoTracking().AnyAsync(m =>
            m.UserId == targetUserId && m.IsActive && campusIds.Contains(m.CampusId));
    }
}
