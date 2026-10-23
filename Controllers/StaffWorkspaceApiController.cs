using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;

namespace Student_Management_System.Controllers;

[Authorize]
[ApiController]
[Route("api/staff-workspace")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StaffWorkspaceApiController(ApplicationDbContext db, ICampusScopeService campusScope,
    UserManager<ApplicationUser> users, IAntiforgery antiforgery, AuditService audit, StaffAccessService access) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool Admin => User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

    [HttpGet("session")]
    public async Task<IActionResult> Session()
    {
        var user = await users.GetUserAsync(User);
        if (user == null || !user.IsActive) return Unauthorized();
        var allowed = new List<StaffWorkspace>();
        foreach (var workspace in StaffWorkspaceCatalog.All)
            if (await Allowed(workspace.Key)) allowed.Add(workspace);
        var primary = StaffWorkspaceCatalog.All.FirstOrDefault(w => User.IsInRole(w.Role))?.Key;
        return Json(new { user.FullName, user.Email, isAdmin = Admin, primary,
            csrfToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken,
            workspaces = allowed,
            departments = Admin ? await db.Departments.AsNoTracking().Where(d => !d.IsArchived).OrderBy(d => d.Name).Select(d => new { d.Id, d.Name }).ToListAsync() : null });
    }

    private Task<bool> Allowed(string key) => access.AllowedAsync(User, key);

    [HttpGet("{workspace}/dashboard")]
    public async Task<IActionResult> Dashboard(string workspace)
    {
        if (!await Allowed(workspace)) return Forbid();
        var now = DateTime.UtcNow;
        var campuses = await campusScope.AllowedCampusIdsAsync(User);
        var zoneId = await db.Campuses.Where(c => campuses.Contains(c.Id)).OrderBy(c => c.Id).Select(c => c.Institution.TimeZoneId).FirstOrDefaultAsync() ?? "Asia/Kolkata";
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId); }
        catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
        catch (InvalidTimeZoneException) { zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
        var localToday = TimeZoneInfo.ConvertTimeFromUtc(now, zone).Date;
        var localMonth = new DateTime(localToday.Year, localToday.Month, 1);
        var today = TimeZoneInfo.ConvertTimeToUtc(localToday, zone);
        var month = TimeZoneInfo.ConvertTimeToUtc(localMonth, zone);
        var students = db.Students.Where(s => s.UserId != null && db.UserCampusMemberships.Any(m => m.UserId == s.UserId && m.IsActive && campuses.Contains(m.CampusId)));
        var admissions = db.AdmissionApplications.Where(a => a.CampusId.HasValue && campuses.Contains(a.CampusId.Value) && !a.IsArchived);
        var loans = db.LibraryLoans.Where(l => db.UserCampusMemberships.Any(m => m.UserId == l.BorrowerUserId && m.IsActive && campuses.Contains(m.CampusId)));
        var metrics = new List<object>();
        void Add(string label, object? value, string? note = null) => metrics.Add(new { label, value, note });
        var items = db.StaffWorkItems.AsNoTracking().Where(w => w.Workspace == workspace && w.CampusId.HasValue && campuses.Contains(w.CampusId.Value));
        switch (workspace)
        {
            case "librarian":
                Add("Total Books", await db.LibraryBookCopies.CountAsync(c => !c.LibraryBook.IsArchived));
                Add("Available Books", await db.LibraryBookCopies.CountAsync(c => c.IsAvailable && !c.LibraryBook.IsArchived));
                Add("Issued Books", await loans.CountAsync(l => l.ReturnedUtc == null));
                Add("Overdue Books", await loans.CountAsync(l => l.ReturnedUtc == null && l.DueUtc < now));
                Add("Total Members", await loans.Select(l => l.BorrowerUserId).Distinct().CountAsync(), "Members with circulation history in your campuses");
                Add("Books Returned Today", await loans.CountAsync(l => l.ReturnedUtc >= today));
                Add("Pending Returns", await loans.CountAsync(l => l.ReturnedUtc == null && l.DueUtc >= now));
                Add("Recent Library Activity", await loans.CountAsync(l => l.IssuedUtc >= today || l.ReturnedUtc >= today), "Today's circulation events");
                break;
            case "transport":
                Add("Total Vehicles", await db.TransportVehicles.CountAsync());
                Add("Active Vehicles", await db.TransportVehicles.CountAsync(v => v.IsActive));
                Add("Total Drivers", await db.TransportDrivers.CountAsync(d => d.IsActive));
                Add("Total Routes", await db.TransportRoutes.CountAsync(r => r.IsActive));
                Add("Students Using Transport", await db.TransportEnrollments.CountAsync(e => e.EndsOn == null && students.Any(s => s.Id == e.StudentId)));
                Add("Today's Trips", await db.TransportTrips.CountAsync(t => t.ScheduledOn == localToday && t.Status != "Cancelled"));
                Add("Maintenance Due", await db.TransportVehicles.CountAsync(v => v.MaintenanceStatus != "Ready"));
                Add("Transport Complaints", await items.CountAsync(w => w.Category == "complaints" && w.Status == "Pending"));
                break;
            case "hostel":
                var occupied = await db.HostelAllocations.Where(a => a.EndsOn == null).Select(a => a.HostelRoomId).Distinct().CountAsync();
                var rooms = await db.HostelRooms.CountAsync(r => r.IsActive);
                var residents = await db.HostelAllocations.CountAsync(a => a.EndsOn == null);
                Add("Total Hostels", await db.HostelBuildings.CountAsync(b => b.IsActive));
                Add("Total Rooms", rooms); Add("Occupied Rooms", occupied); Add("Available Rooms", Math.Max(0, rooms - occupied));
                Add("Total Residents", residents);
                Add("Vacant Beds", Math.Max(0, (await db.HostelRooms.Where(r => r.IsActive).SumAsync(r => (int?)r.Capacity) ?? 0) - residents));
                Add("Pending Complaints", await items.CountAsync(w => w.Category == "complaints" && w.Status == "Pending"));
                Add("Today's Attendance", await db.StaffAttendanceRecords.CountAsync(a => a.Workspace == "hostel" && a.AttendanceDate == localToday && campuses.Contains(a.CampusId) && (a.Status == "Present" || a.Status == "Late")), "Residents marked present or late");
                break;
            case "registrar":
                Add("Total Students", await students.CountAsync(s => !s.IsArchived));
                Add("New Admissions", await admissions.CountAsync(a => a.CreatedUtc >= month));
                Add("Pending Applications", await admissions.CountAsync(a => a.Stage == AdmissionStage.Submitted || a.Stage == AdmissionStage.UnderReview));
                Add("Active Courses", await db.AcademicPrograms.CountAsync(p => p.IsActive && p.CampusId.HasValue && campuses.Contains(p.CampusId.Value)));
                Add("Certificates Issued", await db.RegistrarCertificates.CountAsync(c => campuses.Contains(c.CampusId) && c.Status == "Issued"));
                Add("Documents Pending", await db.AdmissionDocuments.CountAsync(d => d.Status == DocumentVerificationStatus.Pending && admissions.Any(a => a.Id == d.AdmissionApplicationId)));
                Add("Transfer Requests", await db.RegistrarCertificates.CountAsync(c => campuses.Contains(c.CampusId) && c.CertificateType == "Transfer" && c.Status != "Issued" && c.Status != "Rejected"));
                Add("Today's Requests", await items.CountAsync(w => w.CreatedUtc >= today));
                break;
            case "accountant":
                var transactions = db.FinancialTransactions.Where(t => t.Status == FinancialTransactionStatus.Completed && students.Any(s => s.Id == t.StudentId));
                var invoices = db.StudentInvoices.Where(i => students.Any(s => s.Id == i.StudentId) && i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled);
                var expenses = db.StaffExpenses.Where(e => e.Status == "Approved" && e.CampusId.HasValue && campuses.Contains(e.CampusId.Value));
                var revenue = await transactions.SumAsync(t => (decimal?)(t.Type == FinancialTransactionType.Payment || t.Type == FinancialTransactionType.Adjustment ? t.Amount : -Math.Abs(t.Amount))) ?? 0;
                var monthly = await transactions.Where(t => t.CreatedUtc >= month).SumAsync(t => (decimal?)(t.Type == FinancialTransactionType.Payment || t.Type == FinancialTransactionType.Adjustment ? t.Amount : -Math.Abs(t.Amount))) ?? 0;
                var totalExpenses = await expenses.SumAsync(e => (decimal?)e.Amount) ?? 0;
                Add("Total Revenue", revenue, "Signed net ledger amount (INR)");
                Add("Today's Collection", await transactions.Where(t => t.CreatedUtc >= today).SumAsync(t => (decimal?)(t.Type == FinancialTransactionType.Payment || t.Type == FinancialTransactionType.Adjustment ? t.Amount : -Math.Abs(t.Amount))) ?? 0, "INR");
                Add("Pending Fees", await invoices.CountAsync(i => i.TotalAmount > i.AidAmount + i.PaidAmount));
                Add("Outstanding Amount", await invoices.Where(i => i.TotalAmount > i.AidAmount + i.PaidAmount).SumAsync(i => (decimal?)(i.TotalAmount - i.AidAmount - i.PaidAmount)) ?? 0, "INR");
                Add("Expenses", totalExpenses, "Approved expenses (INR)");
                Add("Monthly Income", monthly, "Signed net ledger amount (INR)");
                Add("Monthly Expenses", await expenses.Where(e => e.ExpenseDate >= localMonth).SumAsync(e => (decimal?)e.Amount) ?? 0, "Approved expenses (INR)");
                Add("Net Balance", revenue - totalExpenses, "Net fee collections less approved expenses (INR)");
                break;
        }
        var recent = await items.OrderByDescending(w => w.UpdatedUtc).Take(7)
            .Select(w => new { w.Id, w.Title, w.Category, w.Status, updatedUtc = DateTime.SpecifyKind(w.UpdatedUtc, DateTimeKind.Utc) }).ToListAsync();
        return Json(new { metrics, recent, generatedUtc = now, timeZone = zone.Id });
    }

    [HttpGet("{workspace}/records/{category}")]
    public async Task<IActionResult> Records(string workspace, string category, string? search, string? status, int page = 1)
    {
        if (!await Allowed(workspace)) return Forbid();
        if (StaffWorkspaceCatalog.Find(workspace)!.Pages.All(p => p.Slug != category)) return NotFound();
        var campuses = await campusScope.AllowedCampusIdsAsync(User);
        var query = db.StaffWorkItems.AsNoTracking().Where(w => w.Workspace == workspace && w.Category == category && w.CampusId.HasValue && campuses.Contains(w.CampusId.Value));
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(w => w.Title.Contains(search) || (w.Reference != null && w.Reference.Contains(search)));
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(w => w.Status == status);
        var total = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / 10d)));
        return Json(new { total, page, items = await query.OrderByDescending(w => w.UpdatedUtc).Skip((page - 1) * 10).Take(10)
            .Select(w => new { w.Id, w.Title, w.Description, w.Reference, w.StudentId, studentName = w.Student == null ? null : w.Student.FullName,
                w.DueOn, w.Status, w.ReviewComment, updatedUtc = DateTime.SpecifyKind(w.UpdatedUtc, DateTimeKind.Utc), rowVersion = Convert.ToBase64String(w.RowVersion) }).ToListAsync() });
    }

    [HttpPost("{workspace}/records/{category}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string workspace, string category, [FromBody] WorkItemInput input)
    {
        if (!await access.CanManageAsync(User, workspace)) return Forbid();
        if (StaffWorkspaceCatalog.Find(workspace)!.Pages.All(p => p.Slug != category)) return NotFound();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (string.IsNullOrWhiteSpace(input.Title)) return BadRequest(new { message = "Title is required." });
        var campuses = await campusScope.AllowedCampusIdsAsync(User);
        if (campuses.Length == 0) return BadRequest(new { message = "Ask an administrator to assign an active campus before creating requests." });
        if (input.StudentId.HasValue && !await db.Students.AnyAsync(s => s.Id == input.StudentId && !s.IsArchived && s.UserId != null && db.UserCampusMemberships.Any(m => m.UserId == s.UserId && m.IsActive && campuses.Contains(m.CampusId)))) return BadRequest(new { message = "Student ID must identify an active student in your campus access." });
        var item = new StaffWorkItem { Workspace = workspace, Category = category, Title = input.Title.Trim(), Description = input.Description,
            Reference = input.Reference, StudentId = input.StudentId, DueOn = input.DueOn?.Date, CreatedByUserId = UserId, CampusId = await campusScope.RequireDefaultCampusIdAsync(User) };
        db.StaffWorkItems.Add(item);
        audit.Add("Created operational request", nameof(StaffWorkItem), detailsJson: System.Text.Json.JsonSerializer.Serialize(new { workspace, category }));
        await db.SaveChangesAsync();
        return Json(new { item.Id });
    }

    [HttpPost("{workspace}/records/{id:long}/review"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(string workspace, long id, [FromBody] ReviewWorkInput input)
    {
        if (!await access.CanManageAsync(User, workspace)) return Forbid();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var campuses = await campusScope.AllowedCampusIdsAsync(User);
        var item = await db.StaffWorkItems.SingleOrDefaultAsync(w => w.Id == id && w.Workspace == workspace && w.CampusId.HasValue && campuses.Contains(w.CampusId.Value));
        if (item == null) return NotFound();
        if (input.Status is not ("Verified" or "Approved" or "Rejected" or "Completed")) return BadRequest();
        var valid = StaffAccessRules.CanTransition(item.Status, input.Status);
        if (!valid) return Conflict(new { message = "Follow the verification → approval → completion workflow." });
        if (string.IsNullOrWhiteSpace(input.Comment)) return BadRequest(new { message = "A review comment is required." });
        try
        {
            if (!Convert.FromBase64String(input.RowVersion).SequenceEqual(item.RowVersion)) return Conflict(new { message = "This request changed. Refresh before reviewing." });
        }
        catch (FormatException) { return BadRequest(); }
        item.Status = input.Status; item.ReviewComment = input.Comment; item.ReviewedByUserId = UserId; item.UpdatedUtc = DateTime.UtcNow;
        audit.Add($"{input.Status} operational request", nameof(StaffWorkItem), item.Id,
            detailsJson: System.Text.Json.JsonSerializer.Serialize(new { input.Comment, workspace }));
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "This request changed. Refresh before reviewing." }); }
        return Ok();
    }

    [HttpGet("activity")]
    public async Task<IActionResult> Activity() => Json(await db.AuditLogs.AsNoTracking().Where(a => a.UserId == UserId)
        .OrderByDescending(a => a.CreatedUtc).Take(100).Select(a => new { a.Action, a.EntityName, createdUtc = DateTime.SpecifyKind(a.CreatedUtc, DateTimeKind.Utc) }).ToListAsync());

    [HttpGet("messages")]
    public async Task<IActionResult> Messages(string box = "inbox", int page = 1)
    {
        var query = db.StaffMessages.AsNoTracking().Where(m => box == "sent" ? m.SenderUserId == UserId : m.RecipientUserId == UserId);
        var total = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / 10d)));
        return Json(new { total, page, items = await query.OrderByDescending(m => m.CreatedUtc).Skip((page - 1) * 10).Take(10)
            .Select(m => new { m.Id, m.Subject, m.Body, createdUtc = DateTime.SpecifyKind(m.CreatedUtc, DateTimeKind.Utc), m.ReadUtc, sender = m.SenderUser.FullName, recipient = m.RecipientUser.FullName }).ToListAsync() });
    }

    [HttpPost("messages"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage([FromBody] SendStaffMessageInput input)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var target = await users.FindByEmailAsync(input.RecipientEmail.Trim());
        if (target == null || !target.IsActive || target.Id == UserId) return BadRequest(new { message = "Select another active institutional staff account." });
        var targetRoles = await users.GetRolesAsync(target);
        if (!targetRoles.Any(r => r is "Admin" or "SuperAdmin" || StaffWorkspaceCatalog.All.Any(w => w.Role == r)))
            return BadRequest(new { message = "Recipient must be a staff member or administrator." });
        var senderRoles = User.FindAll(ClaimTypes.Role).Select(r => r.Value);
        if (!senderRoles.Any(r => r is "Admin" or "SuperAdmin" || StaffWorkspaceCatalog.All.Any(w => w.Role == r))) return Forbid();
        if (string.IsNullOrWhiteSpace(input.Subject) || string.IsNullOrWhiteSpace(input.Body)) return BadRequest();
        db.StaffMessages.Add(new StaffMessage { SenderUserId = UserId, RecipientUserId = target.Id, Subject = input.Subject.Trim(), Body = input.Body.Trim() });
        db.UserNotifications.Add(new UserNotification { UserId = target.Id, Title = "New staff message", Message = input.Subject.Trim(), ActionUrl = "/Staff/Personal/messages" });
        audit.Add("Sent internal staff message", nameof(StaffMessage));
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("messages/{id:long}/read"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadMessage(long id)
    {
        var message = await db.StaffMessages.SingleOrDefaultAsync(m => m.Id == id && m.RecipientUserId == UserId);
        if (message == null) return NotFound();
        message.ReadUtc ??= DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok();
    }
}
