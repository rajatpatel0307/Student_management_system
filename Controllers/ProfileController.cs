using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using Student_Management_System.Security;
using Microsoft.AspNetCore.RateLimiting;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly IPrivateFileStorage _privateFiles;
    private readonly AuditService _audit;

    public ProfileController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        IPrivateFileStorage privateFiles,
        AuditService audit)
    {
        _userManager = userManager;
        _db = db;
        _privateFiles = privateFiles;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        ViewBag.Roles = await _userManager.GetRolesAsync(user);
        ViewBag.Teacher = await _db.Teachers.Include(t => t.Department)
            .Include(t => t.ClassAssignments).ThenInclude(a => a.Class)
            .Include(t => t.SubjectAssignments).ThenInclude(a => a.Subject)
            .SingleOrDefaultAsync(t => t.UserId == user.Id);
        ViewBag.Student = await _db.Students.Include(s => s.Department).Include(s => s.Class)
            .Include(s => s.Section).Include(s => s.Semester)
            .SingleOrDefaultAsync(s => s.UserId == user.Id);
        return View(user);
    }

    [HttpGet]
    public async Task<IActionResult> Image(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || string.IsNullOrWhiteSpace(user.ProfileImageStorageKey))
            return NotFound();
        var opened = await _privateFiles.OpenReadAsync(
            user.ProfileImageStorageKey, PrivateFileKind.ProfileImage, cancellationToken);
        if (opened == null) return NotFound();
        Response.Headers.CacheControl = "private, no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        return File(
            opened.Stream,
            opened.ContentType,
            fileDownloadName: null,
            enableRangeProcessing: false);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = ProfilePolicies.ImageEditSelf)]
    [EnableRateLimiting("profile-image")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UpdateImage(
        IFormFile image,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        StoredPrivateFile stored;
        try
        {
            stored = await _privateFiles.SaveProfileImageAsync(image, cancellationToken);
        }
        catch (PrivateFileValidationException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }

        var previousKey = user.ProfileImageStorageKey;
        user.ProfileImageStorageKey = stored.StorageKey;
        var campusIds = await _db.UserCampusMemberships.AsNoTracking()
            .Where(m => m.UserId == user.Id && m.IsActive)
            .Select(m => m.CampusId).ToArrayAsync(cancellationToken);
        _audit.Add("Updated own profile image", nameof(ApplicationUser), user.Id,
            detailsJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                TargetUserId = user.Id,
                ChangedFields = new[] { "ProfileImage" },
                CampusIds = campusIds
            }));
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            await _privateFiles.DeleteAsync(
                stored.StorageKey, PrivateFileKind.ProfileImage, cancellationToken);
            TempData["Error"] = "The profile image could not be updated.";
            return RedirectToAction(nameof(Index));
        }
        if (!string.IsNullOrWhiteSpace(previousKey))
            await _privateFiles.DeleteAsync(
                previousKey, PrivateFileKind.ProfileImage, cancellationToken);
        TempData["Success"] = "Profile image updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = ProfilePolicies.ImageEditSelf)]
    [EnableRateLimiting("profile-image")]
    public async Task<IActionResult> RemoveImage(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var previousKey = user.ProfileImageStorageKey;
        if (string.IsNullOrWhiteSpace(previousKey))
            return RedirectToAction(nameof(Index));
        user.ProfileImageStorageKey = null;
        var campusIds = await _db.UserCampusMemberships.AsNoTracking()
            .Where(m => m.UserId == user.Id && m.IsActive)
            .Select(m => m.CampusId).ToArrayAsync(cancellationToken);
        _audit.Add("Removed own profile image", nameof(ApplicationUser), user.Id,
            detailsJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                TargetUserId = user.Id,
                ChangedFields = new[] { "ProfileImage" },
                CampusIds = campusIds
            }));
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            TempData["Error"] = "The profile image could not be removed.";
            return RedirectToAction(nameof(Index));
        }
        await _privateFiles.DeleteAsync(
            previousKey, PrivateFileKind.ProfileImage, cancellationToken);
        TempData["Success"] = "Profile image removed.";
        return RedirectToAction(nameof(Index));
    }
}
