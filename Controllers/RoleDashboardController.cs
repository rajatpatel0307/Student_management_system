using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Student_Management_System.Models;
using Student_Management_System.Services;

namespace Student_Management_System.Controllers;

[Authorize(Policy = "Dashboard.View")]
public sealed class RoleDashboardController : Controller
{
    private readonly IDashboardAnalyticsService _analytics;
    private readonly AuditService _audit;

    public RoleDashboardController(IDashboardAnalyticsService analytics, AuditService audit)
    { _analytics = analytics; _audit = audit; }

    [Authorize(Roles = "Accountant")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<IActionResult> Finance([FromQuery] DashboardFilterInput filter, CancellationToken cancellationToken) =>
        RenderAsync("Accountant", filter, cancellationToken);

    [Authorize(Roles = "AdmissionOfficer")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<IActionResult> Admissions([FromQuery] DashboardFilterInput filter, CancellationToken cancellationToken) =>
        RenderAsync("AdmissionOfficer", filter, cancellationToken);

    [Authorize(Roles = "Librarian")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<IActionResult> Library([FromQuery] DashboardFilterInput filter, CancellationToken cancellationToken) =>
        RenderAsync("Librarian", filter, cancellationToken);

    [Authorize(Roles = "ExaminationOfficer")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<IActionResult> Examinations([FromQuery] DashboardFilterInput filter, CancellationToken cancellationToken) =>
        RenderAsync("ExaminationOfficer", filter, cancellationToken);

    [Authorize(Roles = "TransportManager,HostelManager,PlacementOfficer,Employee")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<IActionResult> Operations([FromQuery] DashboardFilterInput filter, CancellationToken cancellationToken) =>
        RenderAsync("Operations", filter, cancellationToken);

    [Authorize(Policy = "Dashboard.Export")]
    [HttpGet]
    public async Task<IActionResult> Export([FromQuery] DashboardFilterInput filter, CancellationToken cancellationToken)
    {
        var dashboardRole = EffectiveDashboardRole();
        if (dashboardRole == null) return Forbid();
        RoleDashboardViewModel model;
        try { model = await _analytics.GetAsync(User, dashboardRole, filter, cancellationToken); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        List<string[]> rows = [["Section", "Metric", "Category", "Value", "Period"]];
        rows.AddRange(model.Kpis.Select(kpi => new[] { "KPI", kpi.Title, "", kpi.Value, model.Range.Label }));
        foreach (var chart in model.Charts)
            for (var category = 0; category < chart.Labels.Count; category++)
                foreach (var series in chart.Datasets)
                    rows.Add(new[] { chart.Title, series.Label, chart.Labels[category],
                        category < series.Values.Count ? series.Values[category]?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "" : "",
                        model.Range.Label });
        await _audit.RecordAsync("Exported scoped dashboard analytics", "Dashboard", dashboardRole,
            detailsJson: System.Text.Json.JsonSerializer.Serialize(new { model.Role, model.Range.Label, RowCount = rows.Count - 1 }));
        return File(CsvExport.Create(rows), "text/csv", $"{dashboardRole.ToLowerInvariant()}-dashboard-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private async Task<IActionResult> RenderAsync(
        string dashboardRole,
        DashboardFilterInput filter,
        CancellationToken cancellationToken)
    {
        try { return View("Index", await _analytics.GetAsync(User, dashboardRole, filter, cancellationToken)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    private string? EffectiveDashboardRole()
    {
        if (User.IsInRole("SuperAdmin")) return "SuperAdmin";
        if (User.IsInRole("Admin")) return "Admin";
        if (User.IsInRole("HOD")) return "HOD";
        if (User.IsInRole("Professor")) return "Professor";
        if (User.IsInRole("Student")) return "Student";
        if (User.IsInRole("Accountant")) return "Accountant";
        if (User.IsInRole("AdmissionOfficer")) return "AdmissionOfficer";
        if (User.IsInRole("Librarian")) return "Librarian";
        if (User.IsInRole("ExaminationOfficer")) return "ExaminationOfficer";
        if (User.IsInRole("TransportManager") || User.IsInRole("HostelManager") ||
            User.IsInRole("PlacementOfficer") || User.IsInRole("Employee")) return "Operations";
        return null;
    }
}
