using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;

namespace Student_Management_System.Controllers;

[Authorize(Roles = "SuperAdmin,Admin")]
public sealed class GuardiansController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IStudentAccountService _accounts;
    private readonly AuditService _audit;

    public GuardiansController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        IStudentAccountService accounts,
        AuditService audit)
    {
        _db = db;
        _users = users;
        _accounts = accounts;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var query = _db.GuardianProfiles.AsNoTracking()
            .Include(g => g.StudentLinks).ThenInclude(l => l.Student).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(g => g.FullName.Contains(search) || g.Email.Contains(search));
        ViewBag.Students = await _db.Students.AsNoTracking()
            .Where(s => !s.IsArchived).OrderBy(s => s.FullName).ToListAsync();
        return View(await query.OrderBy(g => g.FullName).Take(250).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string fullName, string email, string relationship,
        string? phone, int studentId, bool canViewFinance = true)
    {
        email = email.Trim().ToLowerInvariant();
        if (!await _db.Students.AnyAsync(s => s.Id == studentId && !s.IsArchived))
            return BadRequest("Select a valid student.");
        if (await _users.FindByEmailAsync(email) != null)
            return Conflict("An account already uses this email.");
        var strategy = _db.Database.CreateExecutionStrategy();
        ApplicationUser? user = null;
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName.Trim(),
                EmailConfirmed = false
            };
            var created = await _users.CreateAsync(user);
            if (!created.Succeeded)
                throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));
            var role = await _users.AddToRoleAsync(user, "Guardian");
            if (!role.Succeeded)
                throw new InvalidOperationException(string.Join("; ", role.Errors.Select(e => e.Description)));
            var profile = new GuardianProfile
            {
                UserId = user.Id,
                FullName = fullName.Trim(),
                Email = email,
                Relationship = relationship.Trim(),
                Phone = phone?.Trim()
            };
            profile.StudentLinks.Add(new GuardianStudentLink
            {
                StudentId = studentId,
                IsPrimary = true,
                CanViewFinance = canViewFinance
            });
            _db.GuardianProfiles.Add(profile);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        });
        var invitation = await _accounts.CreateActivationAsync(user!);
        var url = Url.Action("Activate", "Account",
            new { email = user!.Email, token = invitation.RawToken }, Request.Scheme);
        var delivered = !string.IsNullOrWhiteSpace(url) &&
                        await _accounts.SendActivationAsync(user!, url);
        await _audit.RecordAsync("Created guardian and student link", nameof(GuardianProfile), user!.Id);
        TempData[delivered ? "Success" : "Warning"] = delivered
            ? "Guardian created and activation sent."
            : "Guardian created; activation delivery is pending.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Link(
        int guardianId, int studentId, string relationship,
        bool isPrimary, bool canViewFinance, bool canViewAcademics,
        bool canReceiveEmergencyContact, string? communicationPreferences)
    {
        if (!await _db.GuardianProfiles.AnyAsync(g => g.Id == guardianId) ||
            !await _db.Students.AnyAsync(s => s.Id == studentId && !s.IsArchived))
            return NotFound();
        if (await _db.GuardianStudentLinks.AnyAsync(l =>
                l.GuardianProfileId == guardianId && l.StudentId == studentId &&
                l.UnlinkedUtc == null))
            return Conflict("This student is already linked.");
        if (isPrimary)
        {
            var currentPrimary = await _db.GuardianStudentLinks
                .Where(l => l.StudentId == studentId && l.IsPrimary &&
                            l.UnlinkedUtc == null).ToListAsync();
            currentPrimary.ForEach(l => l.IsPrimary = false);
        }
        var link = new GuardianStudentLink
        {
            GuardianProfileId = guardianId,
            StudentId = studentId,
            Relationship = string.IsNullOrWhiteSpace(relationship)
                ? "Guardian" : relationship.Trim(),
            IsPrimary = isPrimary,
            CanViewFinance = canViewFinance,
            CanViewAcademics = canViewAcademics,
            CanReceiveEmergencyContact = canReceiveEmergencyContact,
            CommunicationPreferences = communicationPreferences?.Trim()
        };
        _db.GuardianStudentLinks.Add(link);
        await _db.SaveChangesAsync();
        _db.GuardianLinkHistories.Add(new GuardianLinkHistory
        {
            GuardianStudentLinkId = link.Id,
            Action = "Linked",
            ChangedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
            Comment = $"Relationship: {link.Relationship}"
        });
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Linked guardian to student", nameof(GuardianStudentLink), guardianId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlink(int id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest("A reason is required to remove a Guardian link.");
        var link = await _db.GuardianStudentLinks
            .SingleOrDefaultAsync(l => l.Id == id && l.UnlinkedUtc == null);
        if (link == null) return NotFound();
        link.UnlinkedUtc = DateTime.UtcNow;
        link.IsPrimary = false;
        _db.GuardianLinkHistories.Add(new GuardianLinkHistory
        {
            GuardianStudentLink = link,
            Action = "Unlinked",
            ChangedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
            Comment = reason.Trim()
        });
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Unlinked guardian from student",
            nameof(GuardianStudentLink), link.Id);
        TempData["Success"] = "Guardian link removed and retained in history.";
        return RedirectToAction(nameof(Index));
    }
}
