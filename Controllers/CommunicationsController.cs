using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;

namespace Student_Management_System.Controllers;

[Authorize]
public sealed class CommunicationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICommunicationService _communications;

    public CommunicationsController(
        ApplicationDbContext db,
        ICommunicationService communications)
    {
        _db = db;
        _communications = communications;
    }

    public async Task<IActionResult> Index(
        [FromQuery] ListQuery request,
        CancellationToken cancellationToken)
    {
        request.Normalize();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var canSend = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");
        var query = _db.CommunicationMessages.AsNoTracking()
            .Include(m => m.Deliveries)
            .Where(m => canSend
                ? true
                : m.Deliveries.Any(d => d.UserId == userId &&
                                         d.Status == DeliveryStatus.Delivered));
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(m =>
                m.Title.Contains(request.Search) || m.Body.Contains(request.Search));
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<CommunicationStatus>(request.Status, true, out var status))
            query = query.Where(m => m.Status == status);
        return View(await query.OrderByDescending(m => m.CreatedUtc)
            .ToPagedResultAsync(request, cancellationToken));
    }

    [Authorize(Roles = "SuperAdmin,Admin")]
    [PermissionAuthorize("notifications.send")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await LoadOptionsAsync(cancellationToken);
        return View(new CommunicationInput());
    }

    [HttpPost, Authorize(Roles = "SuperAdmin,Admin")]
    [PermissionAuthorize("notifications.send")]
    public async Task<IActionResult> Create(
        CommunicationInput input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync(cancellationToken);
            return View(input);
        }
        try
        {
            var message = await _communications.CreateAsync(
                input,
                User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                cancellationToken);
            TempData["Success"] = message.Status == CommunicationStatus.Scheduled
                ? "Communication scheduled."
                : "Communication queued for delivery.";
            return RedirectToAction(nameof(Details), new { id = message.Id });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError("", exception.Message);
            await LoadOptionsAsync(cancellationToken);
            return View(input);
        }
    }

    public async Task<IActionResult> Details(long id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var canManage = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");
        var message = await _db.CommunicationMessages.AsNoTracking()
            .Include(m => m.Audiences)
            .Include(m => m.Deliveries).ThenInclude(d => d.User)
            .SingleOrDefaultAsync(m => m.Id == id &&
                (canManage || m.Deliveries.Any(d =>
                    d.UserId == userId && d.Status == DeliveryStatus.Delivered)),
                cancellationToken);
        if (message == null) return NotFound();
        if (!canManage)
        {
            var delivery = await _db.CommunicationDeliveries.SingleOrDefaultAsync(d =>
                d.CommunicationMessageId == id &&
                d.UserId == userId &&
                d.Channel == DeliveryChannel.InApp, cancellationToken);
            if (delivery != null && !delivery.ReadUtc.HasValue)
            {
                delivery.ReadUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
        ViewBag.CanManage = canManage;
        return View(message);
    }

    [HttpPost, Authorize(Roles = "SuperAdmin,Admin")]
    [PermissionAuthorize("notifications.send")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        try
        {
            await _communications.CancelAsync(
                id,
                User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                cancellationToken);
            TempData["Success"] = "Communication cancelled.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        ViewBag.Roles = new SelectList(await _db.Roles.AsNoTracking()
            .Where(r => r.Name != "SuperAdmin")
            .OrderBy(r => r.Name).Select(r => r.Name).ToListAsync(cancellationToken));
        ViewBag.Departments = new SelectList(await _db.Departments.AsNoTracking()
            .Where(d => !d.IsArchived).OrderBy(d => d.Name)
            .ToListAsync(cancellationToken), "Id", "Name");
        ViewBag.Classes = new SelectList(await _db.Classes.AsNoTracking()
            .Where(c => !c.IsArchived).OrderBy(c => c.Name)
            .ToListAsync(cancellationToken), "Id", "Name");
        ViewBag.Sections = new SelectList(await _db.Sections.AsNoTracking()
            .Where(s => !s.IsArchived).OrderBy(s => s.Name)
            .ToListAsync(cancellationToken), "Id", "Name");
    }
}
