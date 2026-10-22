using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Student_Management_System.Models;
using Student_Management_System.Services;

namespace Student_Management_System.Controllers;

[Authorize(Roles = "HOD", Policy = "Dashboard.View")]
public class HodDashboardController : Controller
{
    private readonly IDashboardAnalyticsService _analytics;
    public HodDashboardController(IDashboardAnalyticsService analytics) => _analytics = analytics;

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Index([FromQuery] DashboardFilterInput filter, CancellationToken cancellationToken)
    {
        try { return View(await _analytics.GetAsync(User, "HOD", filter, cancellationToken)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
