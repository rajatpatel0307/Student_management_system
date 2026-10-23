using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;

namespace Student_Management_System.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StaffController(StaffAccessService access, ApplicationDbContext db,
    StaffRepository repository, StaffManagementService staffService, PermissionService permissions,
    IPrivateFileStorage files, AuditService audit, ICampusScopeService campuses) : Controller
{
    private async Task<bool> CanManage() => (User.IsInRole("Admin") || User.IsInRole("SuperAdmin")) && await permissions.HasAsync(User, "staff.manage");
    private async Task<bool> SchemaReady() => !(await db.Database.GetPendingMigrationsAsync()).Any(m => m.EndsWith("_StaffManagementWorkspaces") || m.EndsWith("_StaffOperationalRegisters"));

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpGet("/Staff"), HttpGet("/Staff/Index")]
    public async Task<IActionResult> Index(string? search, string? role, string? status, int page = 1)
    {
        if (!await SchemaReady()) return View("SetupRequired");
        if (!await CanManage()) return Forbid();
        var query = repository.Query.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(s => s.EmployeeProfile.User.FullName.Contains(search) || s.EmployeeProfile.User.Email!.Contains(search) || s.EmployeeProfile.EmployeeNumber.Contains(search));
        if (!string.IsNullOrWhiteSpace(role)) query = query.Where(s => s.PrimaryRole == role);
        if (status is "active" or "inactive") query = query.Where(s => s.EmployeeProfile.User.IsActive == (status == "active"));
        var total = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / 10d)));
        return View(new StaffDirectoryModel { Items = await query.OrderBy(s => s.EmployeeProfile.User.FullName).Skip((page - 1) * 10).Take(10).ToListAsync(), Search = search, Role = role, Status = status, Page = page, Total = total, TotalStaff = await repository.Query.CountAsync(), ActiveStaff = await repository.Query.CountAsync(s => s.EmployeeProfile.User.IsActive) });
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpGet("/Staff/Create"), HttpGet("/Staff/Edit/{id:int}")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (!await SchemaReady()) return View("SetupRequired");
        if (!await CanManage()) return Forbid();
        var model = new StaffEditorModel { Id = id };
        if (id.HasValue)
        {
            var s = await repository.FindAsync(id.Value);
            if (s == null) return NotFound();
            model.Input = new StaffInput { FullName = s.EmployeeProfile.User.FullName, Email = s.EmployeeProfile.User.Email ?? "", Phone = s.EmployeeProfile.User.PhoneNumber, Username = s.EmployeeProfile.User.UserName ?? "", EmployeeId = s.EmployeeProfile.EmployeeNumber, Role = s.PrimaryRole, DepartmentId = s.EmployeeProfile.DepartmentId, JoiningDate = s.EmployeeProfile.JoinedOn, IsActive = s.EmployeeProfile.User.IsActive, Gender = s.Gender, DateOfBirth = s.DateOfBirth, Address = s.Address, EmergencyContact = s.EmergencyContact, Notes = s.Notes, RowVersion = Convert.ToBase64String(s.RowVersion) };
        }
        model.Departments = await db.Departments.Where(d => !d.IsArchived).OrderBy(d => d.Name).ToListAsync();
        return View("Edit", model);
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPost("/Staff/Save"), ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Save(int? id, [Bind(Prefix = "Input")] StaffInput input, IFormFile? photo)
    {
        if (!await CanManage()) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                var savedId = await staffService.SaveAsync(id, input, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                if (photo is { Length: > 0 })
                {
                    try
                    {
                        var stored = await files.SaveProfileImageAsync(photo, HttpContext.RequestAborted);
                        var staff = (await repository.FindAsync(savedId))!;
                        var previous = staff.EmployeeProfile.User.ProfileImageStorageKey;
                        staff.EmployeeProfile.User.ProfileImageStorageKey = stored.StorageKey;
                        try { await audit.RecordAsync("Updated staff photo", nameof(StaffMember), savedId); }
                        catch { await files.DeleteAsync(stored.StorageKey, PrivateFileKind.ProfileImage); throw; }
                        await files.DeleteAsync(previous, PrivateFileKind.ProfileImage);
                    }
                    catch (PrivateFileValidationException ex)
                    {
                        TempData["Error"] = "Staff saved. Photo was not uploaded: " + ex.Message;
                        return RedirectToAction(nameof(Edit), new { id = savedId });
                    }
                }
                TempData["Success"] = id.HasValue ? "Staff profile updated." : "Staff account created. The staff member must change their password at first login.";
                return RedirectToAction(nameof(Index));
            }
            catch (StaffValidationException ex) { ModelState.AddModelError("", ex.Message); }
            catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "This record changed. Reload the staff profile before editing again."); }
            catch (DbUpdateException) { ModelState.AddModelError("", "An account or employee ID already exists. Check the details and try again."); }
            catch (KeyNotFoundException) { return NotFound(); }
        }
        return View("Edit", new StaffEditorModel { Id = id, Input = input, Departments = await db.Departments.AsNoTracking().Where(d => !d.IsArchived).OrderBy(d => d.Name).ToListAsync() });
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpGet("/Staff/Details/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        if (!await CanManage()) return Forbid();
        var staff = await repository.FindAsync(id);
        return staff == null ? NotFound() : View(staff);
    }

    [HttpGet("/Staff/Workspace/{workspace}"), HttpGet("/Staff/Workspace/{workspace}/{page}")]
    public async Task<IActionResult> Workspace(string workspace, string page = "dashboard")
    {
        var config = StaffWorkspaceCatalog.Find(workspace);
        if (config == null) return NotFound();
        if (!await access.AllowedAsync(User, workspace)) return Forbid();
        if (!await SchemaReady()) return View("SetupRequired");
        var definition = config.Pages.SingleOrDefault(p => p.Slug == page);
        if (definition == null && page != "dashboard" && !new[] { "messages", "activity", "settings", "help" }.Contains(page)) return NotFound();
        ViewData["StaffWorkspace"] = config;
        return View("Workspace", new StaffWorkspacePageModel(config, page, definition));
    }

    [Authorize(Roles = "Admin,SuperAdmin,Librarian,TransportManager,HostelManager,Registrar,Accountant")]
    [HttpGet("/Staff/Personal/{page}")]
    public IActionResult Personal(string page)
    {
        if (!new[] { "messages", "activity", "settings", "help" }.Contains(page)) return NotFound();
        var config = StaffWorkspaceCatalog.All.FirstOrDefault(w => User.IsInRole(w.Role));
        if (config != null) ViewData["StaffWorkspace"] = config;
        config ??= new StaffWorkspace("staff", "Admin", "Staff", "Personal staff tools", "", []);
        return View("Workspace", new StaffWorkspacePageModel(config, page, null));
    }

    [HttpGet("/LibrarianDashboard")]
    public IActionResult Librarian() => RedirectToAction(nameof(Workspace), new { workspace = "librarian" });
    [HttpGet("/TransportDashboard")]
    public IActionResult Transport() => RedirectToAction(nameof(Workspace), new { workspace = "transport" });
    [HttpGet("/HostelDashboard")]
    public IActionResult Hostel() => RedirectToAction(nameof(Workspace), new { workspace = "hostel" });
    [HttpGet("/RegistrarDashboard")]
    public IActionResult Registrar() => RedirectToAction(nameof(Workspace), new { workspace = "registrar" });
    [HttpGet("/AccountantDashboard")]
    public IActionResult Accountant() => RedirectToAction(nameof(Workspace), new { workspace = "accountant" });

    [HttpGet("/Staff/Certificates/{id:long}/Print")]
    public async Task<IActionResult> Certificate(long id)
    {
        if (!await access.AllowedAsync(User, "registrar")) return Forbid();
        var allowed = await campuses.AllowedCampusIdsAsync(User);
        var certificate = await db.RegistrarCertificates.AsNoTracking().Include(c => c.Campus).ThenInclude(c => c.Institution).Include(c => c.Student).SingleOrDefaultAsync(c => c.Id == id && c.Status == "Issued" && allowed.Contains(c.CampusId));
        if (certificate == null) return NotFound();
        await audit.RecordAsync("Viewed issued certificate", nameof(RegistrarCertificate), id);
        return View("Certificate", certificate);
    }
}
