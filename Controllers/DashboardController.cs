using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Student_Management_System.Models;
using Student_Management_System.Services;

[Authorize(Roles = "Admin", Policy = "Dashboard.View")]
public class DashboardController : Controller
{
    private readonly IDashboardAnalyticsService _analytics;

    public DashboardController(IDashboardAnalyticsService analytics) => _analytics = analytics;

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Index([FromQuery] DashboardFilterInput filter, CancellationToken cancellationToken)
    {
        try { return View(await _analytics.GetAsync(User, "Admin", filter, cancellationToken)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
