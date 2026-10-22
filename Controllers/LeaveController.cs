using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;

namespace Student_Management_System.Controllers;

[Authorize]
public sealed class LeaveController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    public LeaveController(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index(LeaveRequestStatus? status)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var canReview = User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("HOD");
        var query = _db.LeaveRequests.AsNoTracking().AsQueryable();
        if (!canReview) query = query.Where(l => l.RequesterUserId == userId);
        if (User.IsInRole("HOD"))
        {
            var departmentId = await _db.Teachers
                .Where(h => h.UserId == userId && h.TeacherType == TeacherType.HOD)
                .Select(h => h.DepartmentId).SingleOrDefaultAsync();
            if (!departmentId.HasValue) return Forbid();
            var teacherUserIds = _db.Teachers.Where(t => t.DepartmentId == departmentId)
                .Select(t => t.UserId);
            var studentUserIds = _db.Students.Where(s => s.DepartmentId == departmentId && s.UserId != null)
                .Select(s => s.UserId!);
            query = query.Where(l => l.RequesterUserId == userId ||
                                     teacherUserIds.Contains(l.RequesterUserId) ||
                                     studentUserIds.Contains(l.RequesterUserId));
        }
        if (status.HasValue) query = query.Where(l => l.Status == status);
        ViewBag.CanReview = canReview;
        ViewBag.Status = status;
        return View(await query.OrderByDescending(l => l.CreatedUtc).Take(250).ToListAsync());
    }

    public IActionResult Create() => View(new LeaveRequest
    {
        StartsOn = DateTime.Today,
        EndsOn = DateTime.Today
    });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("LeaveType,StartsOn,EndsOn,Reason")] LeaveRequest input)
    {
        ModelState.Remove(nameof(LeaveRequest.RequesterUserId));
        if (input.EndsOn.Date < input.StartsOn.Date)
            ModelState.AddModelError(nameof(input.EndsOn), "End date cannot be before the start date.");
        if (!ModelState.IsValid) return View(input);
        input.RequesterUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        input.LeaveType = input.LeaveType.Trim();
        input.Reason = input.Reason.Trim();
        input.Status = LeaveRequestStatus.Pending;
        input.CreatedUtc = DateTime.UtcNow;
        _db.Add(input);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Submitted leave request", nameof(LeaveRequest), input.Id);
        TempData["Success"] = "Leave request submitted.";
        return RedirectToAction(nameof(Index));
    }

    [PermissionAuthorize("leave.review")]
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,HOD")]
    public async Task<IActionResult> Review(long id, LeaveRequestStatus decision, string? comment)
    {
        if (decision is not (LeaveRequestStatus.Approved or LeaveRequestStatus.Rejected))
            return BadRequest("Select Approve or Reject.");
        var request = await _db.LeaveRequests.SingleOrDefaultAsync(l => l.Id == id);
        if (request == null) return NotFound();
        if (request.Status != LeaveRequestStatus.Pending) return Conflict("This request has already been reviewed.");
        if (request.RequesterUserId == User.FindFirstValue(ClaimTypes.NameIdentifier))
            return Conflict("You cannot approve your own leave request.");
        if (User.IsInRole("HOD") && !await HodCanReviewAsync(request.RequesterUserId))
            return Forbid();

        request.Status = decision;
        request.ReviewComment = comment?.Trim();
        request.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        request.ReviewedUtc = DateTime.UtcNow;
        _db.UserNotifications.Add(new UserNotification
        {
            UserId = request.RequesterUserId,
            Title = $"Leave request {decision}",
            Message = $"Your {request.LeaveType} leave request has been {decision.ToString().ToLowerInvariant()}.",
            ActionUrl = "/Leave",
            Category = "Leave"
        });
        await _db.SaveChangesAsync();
        await _audit.RecordAsync($"Reviewed leave request: {decision}", nameof(LeaveRequest), id);
        TempData["Success"] = $"Leave request {decision.ToString().ToLowerInvariant()}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(long id, string reason)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var request = await _db.LeaveRequests.SingleOrDefaultAsync(l =>
            l.Id == id && l.RequesterUserId == userId);
        if (request == null) return NotFound();
        if (request.Status is LeaveRequestStatus.Rejected or LeaveRequestStatus.Cancelled)
            return Conflict("This leave request cannot be cancelled.");
        if (request.StartsOn.Date <= DateTime.Today)
            return Conflict("Leave that has started requires an administrator correction.");
        request.Status = LeaveRequestStatus.Cancelled;
        request.ReviewComment = reason.Trim();
        request.ReviewedByUserId = userId;
        request.ReviewedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Cancelled leave request", nameof(LeaveRequest), id);
        TempData["Success"] = "Leave request cancelled.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> HodCanReviewAsync(string requesterUserId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var departmentId = await _db.Teachers
            .Where(t => t.UserId == userId && t.TeacherType == TeacherType.HOD)
            .Select(t => t.DepartmentId).SingleOrDefaultAsync();
        if (!departmentId.HasValue) return false;
        return await _db.Teachers.AnyAsync(t =>
                   t.UserId == requesterUserId && t.DepartmentId == departmentId) ||
               await _db.Students.AnyAsync(s =>
                   s.UserId == requesterUserId && s.DepartmentId == departmentId);
    }
}
