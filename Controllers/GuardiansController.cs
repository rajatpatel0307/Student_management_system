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

    public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var query = _db.GuardianProfiles.AsNoTracking()
            .Include(g => g.StudentLinks).ThenInclude(l => l.Student).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(g => g.FullName.Contains(search) || g.Email.Contains(search));
        ViewBag.Students = await _db.Students.AsNoTracking()
            .Where(s => !s.IsArchived).OrderBy(s => s.FullName).ToListAsync();
        var result = await query.OrderBy(g => g.FullName).ThenBy(g => g.Id)
            .ToPagedResultAsync(new ListQuery { Search = search, Page = page, PageSize = pageSize }, cancellationToken);
        ViewBag.InvitationStatuses = await InvitationStatusQuery.ReadAsync(_db, result.Items.Select(g => g.UserId).ToArray(), cancellationToken);
        return View(result);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string fullName, string email, string relationship,
        string? phone, int studentId, bool canViewFinance = false)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(fullName) || fullName.Length > 160 ||
            string.IsNullOrWhiteSpace(email) || email.Length > 256 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email.Trim()) ||
            string.IsNullOrWhiteSpace(relationship) || relationship.Length > 40 || phone?.Length > 30)
            return BadRequest("Provide a valid guardian name, email, relationship and phone within the permitted lengths.");
        email = email.Trim().ToLowerInvariant();
        if (!await _db.Students.AnyAsync(s => s.Id == studentId && !s.IsArchived))
            return BadRequest("Select a valid student.");
        if (await _users.FindByEmailAsync(email) != null)
            return Conflict("An account already uses this email.");
        var strategy = _db.Database.CreateExecutionStrategy();
        ApplicationUser? user = null;
        await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync();
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                PhoneNumber = phone?.Trim(),
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
                Relationship = relationship.Trim(),
                CanViewFinance = canViewFinance
            });
            _db.GuardianProfiles.Add(profile);
            AccountActivationRequests.Enqueue(_db, user.Id);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        });
        var delivered = false;
        try
        {
            var invitation = await _accounts.CreateActivationAsync(user!);
            var url = Url.Action("Activate", "Account",
                new { email = user!.Email, token = invitation.RawToken }, Request.Scheme);
            delivered = !string.IsNullOrWhiteSpace(url) && await _accounts.SendActivationAsync(user!, url);
        }
        catch (InvalidOperationException)
        {
            // The committed account survives an invitation/setup failure; do not
            // present a failed creation that encourages duplicate retries.
        }
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
        if (!ModelState.IsValid || relationship?.Length > 40 || communicationPreferences?.Length > 500)
            return BadRequest("Relationship must be at most 40 characters and communication preferences at most 500 characters.");
        if (!await _db.GuardianProfiles.AnyAsync(g => g.Id == guardianId) ||
            !await _db.Students.AnyAsync(s => s.Id == studentId && !s.IsArchived))
            return NotFound();
        var previous = await _db.GuardianStudentLinks.SingleOrDefaultAsync(l =>
            l.GuardianProfileId == guardianId && l.StudentId == studentId);
        if (previous is { UnlinkedUtc: null })
            return Conflict("This student is already linked.");
        if (isPrimary)
        {
            var currentPrimary = await _db.GuardianStudentLinks
                .Where(l => l.StudentId == studentId && l.IsPrimary &&
                            l.UnlinkedUtc == null).ToListAsync();
            currentPrimary.ForEach(l => l.IsPrimary = false);
        }
        var link = previous ?? new GuardianStudentLink { GuardianProfileId = guardianId, StudentId = studentId };
        link.Relationship = string.IsNullOrWhiteSpace(relationship) ? "Guardian" : relationship.Trim();
        link.IsPrimary = isPrimary;
        link.CanViewFinance = canViewFinance;
        link.CanViewAcademics = canViewAcademics;
        link.CanReceiveEmergencyContact = canReceiveEmergencyContact;
        link.CommunicationPreferences = communicationPreferences?.Trim();
        link.UnlinkedUtc = null;
        link.LinkedUtc = DateTime.UtcNow;
        if (previous == null) _db.GuardianStudentLinks.Add(link);
        _db.GuardianLinkHistories.Add(new GuardianLinkHistory
        {
            GuardianStudentLink = link,
            Action = previous == null ? "Linked" : "Relinked",
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
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
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
