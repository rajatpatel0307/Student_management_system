using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;

namespace Student_Management_System.Controllers;

[Authorize]
public sealed class NotificationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    public NotificationsController(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index(bool unreadOnly = false)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = _db.UserNotifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);
        ViewBag.UnreadOnly = unreadOnly;
        ViewBag.UnreadCount = await _db.UserNotifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        return View(await query.OrderByDescending(n => n.CreatedUtc).Take(250).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Read(long id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var notification = await _db.UserNotifications
            .SingleOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notification == null) return NotFound();
        notification.IsRead = true;
        notification.ReadUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return string.IsNullOrWhiteSpace(notification.ActionUrl)
            ? RedirectToAction(nameof(Index))
            : LocalRedirect(notification.ActionUrl);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadAll()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var unread = await _db.UserNotifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadUtc = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = "All notifications marked as read.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "SuperAdmin,Admin")]
    [PermissionAuthorize("notifications.send")]
    public IActionResult Send() => View();

    [PermissionAuthorize("notifications.send")]
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Send(string role, string title, string message, string? actionUrl)
    {
        if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            return BadRequest("Role, title, and message are required.");
        if (!string.IsNullOrWhiteSpace(actionUrl) && !Url.IsLocalUrl(actionUrl))
            return BadRequest("Notification links must be local application paths.");
        var userIds = await (from user in _db.Users
                             join userRole in _db.UserRoles on user.Id equals userRole.UserId
                             join identityRole in _db.Roles on userRole.RoleId equals identityRole.Id
                             where identityRole.Name == role
                             select user.Id).ToListAsync();
        _db.UserNotifications.AddRange(userIds.Select(userId => new UserNotification
        {
            UserId = userId,
            Title = title.Trim(),
            Message = message.Trim(),
            ActionUrl = actionUrl?.Trim(),
            Category = "Administration"
        }));
        await _db.SaveChangesAsync();
        await _audit.RecordAsync($"Sent notification to role {role}", nameof(UserNotification), role);
        TempData["Success"] = $"Notification queued for {userIds.Count} users.";
        return RedirectToAction(nameof(Index));
    }
}
