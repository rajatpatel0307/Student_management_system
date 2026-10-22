using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;

namespace Student_Management_System.Controllers;

[Authorize]
public class FeeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    public FeeController(ApplicationDbContext db, AuditService audit)
    { _db = db; _audit = audit; }

    public async Task<IActionResult> Index(string? search, int? classId, string? status)
    {
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        IQueryable<Fee> feeQuery = _db.Fees.AsNoTracking()
            .Include(f => f.Class)
            .Where(f => !f.IsArchived);
        int? currentStudentId = null;
        if (User.IsInRole("Student"))
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var student = await _db.Students.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == uid);
            if (student == null) return NotFound();
            currentStudentId = student.Id;
            feeQuery = feeQuery.Where(f => f.ClassId == student.ClassId);
        }
        else if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && !User.IsInRole("Accountant"))
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(search))
            feeQuery = feeQuery.Where(f => f.Term.Contains(search) || f.Class!.Name.Contains(search));
        if (classId.HasValue && !User.IsInRole("Student"))
            feeQuery = feeQuery.Where(f => f.ClassId == classId.Value);
        if (string.Equals(status, "overdue", StringComparison.OrdinalIgnoreCase))
            feeQuery = feeQuery.Where(f => f.DueDate < DateTime.Today);
        else if (string.Equals(status, "upcoming", StringComparison.OrdinalIgnoreCase))
            feeQuery = feeQuery.Where(f => f.DueDate >= DateTime.Today);

        var fees = await feeQuery.OrderBy(f => f.DueDate).ThenBy(f => f.Class!.Name).ToListAsync();
        var feeIds = fees.Select(f => f.Id).ToList();
        var paymentQuery = _db.FinancialTransactions.AsNoTracking()
            .Include(p => p.Student).Include(p => p.Fee)
            .Where(p => feeIds.Contains(p.FeeId));
        if (currentStudentId.HasValue) paymentQuery = paymentQuery.Where(p => p.StudentId == currentStudentId);
        var payments = await paymentQuery.OrderByDescending(p => p.CreatedUtc).Take(100).ToListAsync();
        ViewBag.Payments = payments;
        ViewBag.Classes = await _db.Classes.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        ViewBag.Search = search;
        ViewBag.ClassId = classId;
        ViewBag.Status = status;
        ViewBag.Stats = new[]
        {
            new StatCardViewModel("Fee schedules", fees.Count.ToString("N0"), "Matching current filters", "fa-solid fa-receipt"),
            new StatCardViewModel("Scheduled value", fees.Sum(f => f.Amount).ToString("C"), "Configured charges", "fa-solid fa-indian-rupee-sign", "info"),
            new StatCardViewModel("Net payments", payments.Sum(p => p.Amount).ToString("C"), $"{payments.Count:N0} immutable ledger entries", "fa-solid fa-circle-check", "success"),
            new StatCardViewModel("Past due schedules", fees.Count(f => f.DueDate < DateTime.Today).ToString("N0"), "Requires review", "fa-solid fa-triangle-exclamation", "warning")
        };
        return View(fees);
    }

    [Authorize(Roles = "Admin,SuperAdmin,Accountant")]
    public IActionResult Create() { ViewBag.Classes = _db.Classes.ToList(); return View(); }
    [HttpPost, Authorize(Roles = "Admin,SuperAdmin,Accountant")]
    public async Task<IActionResult> Create([Bind("ClassId,Amount,Term,DueDate")] Fee fee)
    {
        if (!ModelState.IsValid) { ViewBag.Classes = _db.Classes.ToList(); return View(fee); }
        _db.Add(fee); await _db.SaveChangesAsync(); TempData["Success"] = "Fee schedule created."; return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var fee = await _db.Fees.AsNoTracking().Include(f => f.Class).SingleOrDefaultAsync(f => f.Id == id);
        if (fee == null) return NotFound();
        int? scopedStudentId = null;
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.IsInRole("Student"))
        {
            scopedStudentId = await _db.Students
                .Where(s => s.UserId == uid && s.ClassId == fee.ClassId)
                .Select(s => (int?)s.Id).SingleOrDefaultAsync();
            if (!scopedStudentId.HasValue) return Forbid();
        }
        else if (User.IsInRole("Guardian"))
        {
            var studentIds = await _db.GuardianStudentLinks.AsNoTracking()
                .Where(l => l.UnlinkedUtc == null &&
                            l.CanViewFinance &&
                            l.GuardianProfile.UserId == uid &&
                            l.Student.ClassId == fee.ClassId)
                .Select(l => l.StudentId)
                .ToListAsync();
            if (studentIds.Count != 1)
                return studentIds.Count == 0 ? Forbid() : BadRequest(
                    "Open this fee from a specific linked student's finance page.");
            scopedStudentId = studentIds[0];
        }
        else if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && !User.IsInRole("Accountant")) return Forbid();
        var payments = _db.FinancialTransactions.AsNoTracking().Include(p => p.Student)
            .Where(p => p.FeeId == id);
        if (scopedStudentId.HasValue)
            payments = payments.Where(p => p.StudentId == scopedStudentId.Value);
        ViewBag.Payments = await payments.OrderByDescending(p => p.CreatedUtc).ToListAsync();
        return View(fee);
    }

    [Authorize(Roles = "Admin,SuperAdmin,Accountant")]
    public async Task<IActionResult> Edit(int id)
    {
        var fee = await _db.Fees.AsNoTracking().SingleOrDefaultAsync(f => f.Id == id);
        if (fee == null) return NotFound();
        ViewBag.Classes = await _db.Classes.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        return View(fee);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,SuperAdmin,Accountant")]
    public async Task<IActionResult> Edit(int id, [Bind("Id,ClassId,Amount,Term,DueDate,RowVersion")] Fee input)
    {
        if (id != input.Id) return NotFound();
        var fee = await _db.Fees.SingleOrDefaultAsync(f => f.Id == id);
        if (fee == null) return NotFound();
        ConcurrencyToken.Apply(_db, fee, input.RowVersion);
        ModelState.Remove("Class");
        if (!await _db.Classes.AnyAsync(c => c.Id == input.ClassId))
            ModelState.AddModelError(nameof(input.ClassId), "Select a valid class.");
        if (!ModelState.IsValid)
        {
            ViewBag.Classes = await _db.Classes.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
            return View(input);
        }
        fee.ClassId = input.ClassId; fee.Term = input.Term.Trim(); fee.Amount = input.Amount; fee.DueDate = input.DueDate;
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Updated fee schedule", nameof(Fee), id);
        TempData["Success"] = "Fee schedule updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }
    [HttpPost, Authorize(Roles = "Admin,SuperAdmin,Accountant")]
    public IActionResult RecordPayment(Payment payment)
    {
        Response.StatusCode = StatusCodes.Status410Gone;
        return Problem(
            "Legacy payment recording has been retired. Use Finance / Collect so the immutable ledger remains authoritative.",
            statusCode: StatusCodes.Status410Gone);
    }
}
