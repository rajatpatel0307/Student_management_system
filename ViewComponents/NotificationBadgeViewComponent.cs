using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using System.Security.Claims;

namespace Student_Management_System.ViewComponents;

public sealed class NotificationBadgeViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;
    public NotificationBadgeViewComponent(ApplicationDbContext db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var unread = string.IsNullOrWhiteSpace(userId)
            ? 0
            : await _db.UserNotifications.AsNoTracking()
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        return View(unread);
    }
}
