using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;

namespace Student_Management_System.Controllers;

[Authorize]
public sealed class ApprovalsController : Controller
{
    private static readonly IReadOnlyDictionary<string, string[]> WorkflowRoles =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["AcademicException"] = ["HOD", "Admin"],
            ["FinanceAdjustment"] = ["Accountant", "Admin"],
            ["FinanceRefund"] = ["Accountant", "Admin"],
            ["Scholarship"] = ["HOD", "Accountant", "Admin"],
            ["ResultPublication"] = ["ExaminationOfficer", "Admin"],
            ["MarkCorrection"] = ["HOD", "ExaminationOfficer"],
            ["AttendanceCorrection"] = ["HOD"],
            ["StudentPromotion"] = ["HOD", "Admin"],
            ["Leave"] = ["HOD", "Admin"],
            ["DocumentApproval"] = ["Admin"],
            ["AdmissionException"] = ["AdmissionOfficer", "Admin"]
        };
    private static readonly IReadOnlyDictionary<string, string> WorkflowEntities =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AcademicException"] = nameof(Student),
            ["FinanceAdjustment"] = nameof(FinancialTransaction),
            ["FinanceRefund"] = nameof(FinancialTransaction),
            ["Scholarship"] = nameof(StudentAidAward),
            ["ResultPublication"] = nameof(Exam),
            ["MarkCorrection"] = nameof(Mark),
            ["AttendanceCorrection"] = nameof(Attendance),
            ["StudentPromotion"] = nameof(PromotionBatch),
            ["Leave"] = nameof(LeaveRequest),
            ["DocumentApproval"] = nameof(AdmissionDocument),
            ["AdmissionException"] = nameof(AdmissionApplication)
        };
    private static readonly IReadOnlyDictionary<string, string[]> WorkflowRequesters =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["AcademicException"] = ["SuperAdmin", "Admin", "HOD"],
            ["FinanceAdjustment"] = ["SuperAdmin", "Admin", "Accountant"],
            ["FinanceRefund"] = ["SuperAdmin", "Admin", "Accountant"],
            ["Scholarship"] = ["SuperAdmin", "Admin", "HOD", "Accountant"],
            ["ResultPublication"] = ["SuperAdmin", "Admin", "ExaminationOfficer"],
            ["MarkCorrection"] = ["SuperAdmin", "Admin", "HOD", "Professor", "ExaminationOfficer"],
            ["AttendanceCorrection"] = ["SuperAdmin", "Admin", "HOD", "Professor"],
            ["StudentPromotion"] = ["SuperAdmin", "Admin", "HOD"],
            ["Leave"] = ["SuperAdmin", "Admin", "HOD", "Professor"],
            ["DocumentApproval"] = ["SuperAdmin", "Admin", "AdmissionOfficer"],
            ["AdmissionException"] = ["SuperAdmin", "Admin", "AdmissionOfficer"]
        };
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly IApprovalWorkflowService _workflows;
    private readonly IApprovalTargetValidator _targets;
    public ApprovalsController(
        ApplicationDbContext db,
        AuditService audit,
        IApprovalWorkflowService workflows,
        IApprovalTargetValidator targets)
    {
        _db = db;
        _audit = audit;
        _workflows = workflows;
        _targets = targets;
    }

    public async Task<IActionResult> Index(ApprovalStatus? status)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var roles = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();
        var query = _db.ApprovalRequests.AsNoTracking().Include(r => r.Steps).AsQueryable();
        if (!User.IsInRole("SuperAdmin") && !User.IsInRole("Admin"))
            query = query.Where(r => r.RequestedByUserId == userId ||
                r.CurrentApproverUserId == userId ||
                r.Steps.Any(s => roles.Contains(s.ApproverRole) && s.Status == ApprovalStatus.Pending));
        if (status.HasValue) query = query.Where(r => r.Status == status);
        return View(await query.OrderByDescending(r => r.CreatedUtc).Take(250).ToListAsync());
    }

    [Authorize(Roles = "SuperAdmin,Admin,HOD,Professor,Accountant,AdmissionOfficer,ExaminationOfficer")]
    public IActionResult Create()
    {
        ViewBag.WorkflowTypes = WorkflowRoles.Keys;
        return View(new CreateApprovalRequestViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Admin,HOD,Professor,Accountant,AdmissionOfficer,ExaminationOfficer")]
    public async Task<IActionResult> Create(CreateApprovalRequestViewModel input)
    {
        ViewBag.WorkflowTypes = WorkflowRoles.Keys;
        WorkflowRoles.TryGetValue(input.WorkflowType, out var approverRoles);
        WorkflowEntities.TryGetValue(input.WorkflowType, out var entityType);
        if (approverRoles == null || entityType == null)
            ModelState.AddModelError(nameof(input.WorkflowType), "Select a supported workflow.");
        if (!ModelState.IsValid || approverRoles == null || entityType == null)
            return View(input);

        if (!WorkflowRequesters[input.WorkflowType].Any(User.IsInRole))
            return Forbid();

        input.EntityType = entityType;
        if (!await _targets.ExistsAsync(input.EntityType, input.EntityId, HttpContext.RequestAborted))
        {
            ModelState.AddModelError(nameof(input.EntityId),
                $"No authorized {input.EntityType} record exists with that ID.");
            return View(input);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        try
        {
            await _workflows.RequestAsync(input.WorkflowType,
                input.EntityType, input.EntityId, input.Reason,
                userId, approverRoles, DateTime.UtcNow.AddDays(5));
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError("", exception.Message);
            return View(input);
        }
        TempData["Success"] = "Approval workflow submitted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Act(long id, ApprovalStatus decision, string? comment)
    {
        if (decision is not (ApprovalStatus.Approved or ApprovalStatus.Rejected))
            return BadRequest();
        try
        {
            await _workflows.ActAsync(id, decision, comment, User);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(long id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var request = await _db.ApprovalRequests.SingleOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();
        if (request.RequestedByUserId != userId && !User.IsInRole("SuperAdmin"))
            return Forbid();
        if (request.Status != ApprovalStatus.Pending)
            return Conflict("Only pending workflows can be cancelled.");
        request.Status = ApprovalStatus.Cancelled;
        request.CompletedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Cancelled approval workflow", nameof(ApprovalRequest), id);
        TempData["Success"] = "Approval workflow cancelled.";
        return RedirectToAction(nameof(Index));
    }
}
