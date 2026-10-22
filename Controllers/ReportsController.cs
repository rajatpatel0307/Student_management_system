using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;
using System.Text.Json;

namespace Student_Management_System.Controllers;

[Authorize]
[PermissionAuthorize("reports.view")]
public sealed class ReportsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IReportingService _reports;

    public ReportsController(ApplicationDbContext db, IReportingService reports)
    {
        _db = db;
        _reports = reports;
    }

    public async Task<IActionResult> Index(
        [FromQuery] ReportCenterQuery query,
        CancellationToken cancellationToken)
    {
        query.Normalize();
        ReportDataset? dataset = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(query.ReportType))
                dataset = await _reports.BuildAsync(query, User, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        ViewBag.Query = query;
        ViewBag.Dataset = dataset;
        ViewBag.Departments = await _db.Departments.AsNoTracking()
            .Where(d => !d.IsArchived).OrderBy(d => d.Name).ToListAsync(cancellationToken);
        ViewBag.Classes = await _db.Classes.AsNoTracking()
            .Where(c => !c.IsArchived).OrderBy(c => c.Name).ToListAsync(cancellationToken);
        ViewBag.Sections = await _db.Sections.AsNoTracking()
            .Where(s => !s.IsArchived).OrderBy(s => s.Name).ToListAsync(cancellationToken);
        ViewBag.Subjects = await _db.Subjects.AsNoTracking()
            .Where(s => !s.IsArchived).OrderBy(s => s.Name).ToListAsync(cancellationToken);
        ViewBag.Years = await _db.AcademicYears.AsNoTracking()
            .Where(y => !y.IsArchived).OrderByDescending(y => y.StartsOn).ToListAsync(cancellationToken);
        ViewBag.SavedReports = await _db.SavedReports.AsNoTracking()
            .Where(r => r.OwnerUserId == User.FindFirstValue(ClaimTypes.NameIdentifier) || r.IsShared)
            .OrderBy(r => r.Name).ToListAsync(cancellationToken);
        return View();
    }

    [PermissionAuthorize("reports.export")]
    public async Task<IActionResult> Export(
        [FromQuery] ReportCenterQuery query,
        CancellationToken cancellationToken)
    {
        ReportDataset dataset;
        try
        {
            dataset = await _reports.BuildAsync(query, User, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        var safeName = string.Concat(dataset.Title
            .ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-'))
            .Trim('-');
        if (string.Equals(query.Format, "Csv", StringComparison.OrdinalIgnoreCase))
        {
            var rows = new List<string[]> { dataset.Headers.ToArray() };
            rows.AddRange(dataset.Rows.Select(row => row.ToArray()));
            return File(
                CsvExport.Create(rows),
                "text/csv",
                $"{safeName}-{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        if (string.Equals(query.Format, "Print", StringComparison.OrdinalIgnoreCase))
        {
            Response.Headers.CacheControl = "no-store";
            return View("Print", dataset);
        }

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Report");
        for (var column = 0; column < dataset.Headers.Count; column++)
            sheet.Cell(1, column + 1).Value = dataset.Headers[column];
        for (var row = 0; row < dataset.Rows.Count; row++)
            for (var column = 0; column < dataset.Rows[row].Count; column++)
                sheet.Cell(row + 2, column + 1).Value = dataset.Rows[row][column];
        sheet.RangeUsed()?.CreateTable();
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(8, 50);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{safeName}-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    [HttpPost]
    public async Task<IActionResult> Save(
        string name,
        ReportCenterQuery query,
        bool isShared,
        CancellationToken cancellationToken)
    {
        name = name?.Trim() ?? "";
        if (name.Length is < 2 or > 160)
        {
            TempData["Error"] = "Saved report name must contain 2 to 160 characters.";
            return RedirectToAction(nameof(Index), query);
        }
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (await _db.SavedReports.AnyAsync(r =>
                r.OwnerUserId == userId && r.Name == name, cancellationToken))
        {
            TempData["Error"] = "You already have a saved report with this name.";
            return RedirectToAction(nameof(Index), query);
        }
        _db.SavedReports.Add(new SavedReport
        {
            Name = name,
            ReportType = query.ReportType,
            FilterJson = JsonSerializer.Serialize(query),
            OwnerUserId = userId,
            IsShared = isShared && (User.IsInRole("SuperAdmin") || User.IsInRole("Admin"))
        });
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "Saved report filter",
            EntityName = nameof(SavedReport),
            EntityId = name
        });
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Report filter saved.";
        return RedirectToAction(nameof(Index), query);
    }

    public Task<IActionResult> StudentsExcel(CancellationToken cancellationToken) =>
        Export(new ReportCenterQuery
        {
            ReportType = "Enrollment",
            Format = "Excel",
            PageSize = 100
        }, cancellationToken);
}
