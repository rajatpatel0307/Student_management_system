using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;

namespace Student_Management_System.Controllers
{
    [Authorize]
    public class NoticeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;

        public NoticeController(ApplicationDbContext context, AuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        // ================= CREATE =================
        [Authorize(Roles = "Admin,SuperAdmin,HOD")]
        public IActionResult Create()
        {
            if (!User.IsInRole("HOD"))
                ViewBag.Departments = _context.Departments.OrderBy(d => d.Name).ToList();
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin,HOD")]
        public async Task<IActionResult> Create([Bind("Title,Message,TargetRole,DepartmentId")] Notice notice)
        {
            if (User.IsInRole("HOD"))
            {
                var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                notice.DepartmentId = await _context.Teachers.Where(t => t.UserId == uid && t.TeacherType == TeacherType.HOD)
                    .Select(t => t.DepartmentId).SingleOrDefaultAsync();
                notice.TargetRole = "Department";
            }
            else if (notice.TargetRole != "Department")
            {
                notice.DepartmentId = null;
            }
            else if (!notice.DepartmentId.HasValue ||
                     !await _context.Departments.AnyAsync(d => d.Id == notice.DepartmentId.Value))
            {
                ModelState.AddModelError(nameof(notice.DepartmentId), "Select a valid department.");
            }
            if (!ModelState.IsValid)
            {
                if (!User.IsInRole("HOD"))
                    ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
                return View(notice);
            }
            notice.CreatedDate = DateTime.Now;

            _context.Add(notice);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Announcement published.";

            return RedirectToAction("Index");
        }

        // ================= INDEX =================
        public async Task<IActionResult> Index([FromQuery] ListQuery request, CancellationToken cancellationToken)
        {
            request.Normalize();
            var role = CurrentRole();
            if (role == null) return Forbid();

            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var departmentId = await _context.Teachers.Where(t => t.UserId == uid).Select(t => t.DepartmentId).SingleOrDefaultAsync()
                ?? await _context.Students.Where(s => s.UserId == uid).Select(s => s.DepartmentId).SingleOrDefaultAsync();
            var notices = _context.Notices
                .AsNoTracking()
                .Where(n => n.TargetRole == role || n.TargetRole == "All" ||
                    (n.TargetRole == "Department" && n.DepartmentId == departmentId));
            if (!string.IsNullOrWhiteSpace(request.Search)) notices = notices.Where(n => n.Title.Contains(request.Search) || n.Message.Contains(request.Search));
            if (!string.IsNullOrWhiteSpace(request.Status)) notices = notices.Where(n => n.TargetRole == request.Status);
            ViewBag.Stats = new[]
            {
                new StatCardViewModel("Announcements", (await notices.CountAsync(cancellationToken)).ToString("N0"), "Visible to your role", "fa-solid fa-bullhorn"),
                new StatCardViewModel("Recent", (await notices.CountAsync(n => n.CreatedDate >= DateTime.Today.AddDays(-30), cancellationToken)).ToString("N0"), "Published in 30 days", "fa-solid fa-clock", "success"),
                new StatCardViewModel("Organization-wide", (await notices.CountAsync(n => n.TargetRole == "All", cancellationToken)).ToString("N0"), "Targeting all roles", "fa-solid fa-users", "info", "status", "All")
            };
            return View(await notices.OrderByDescending(n => n.CreatedDate).ToPagedResultAsync(request, cancellationToken));
        }

        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            var role = CurrentRole();
            if (role == null) return Forbid();
            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var departmentId = await _context.Teachers.Where(t => t.UserId == uid).Select(t => t.DepartmentId).SingleOrDefaultAsync(cancellationToken)
                ?? await _context.Students.Where(s => s.UserId == uid).Select(s => s.DepartmentId).SingleOrDefaultAsync(cancellationToken);
            var notice = await _context.Notices.AsNoTracking().Include(n => n.Department).SingleOrDefaultAsync(n => n.Id == id &&
                (n.TargetRole == role || n.TargetRole == "All" || (n.TargetRole == "Department" && n.DepartmentId == departmentId)), cancellationToken);
            return notice == null ? NotFound() : View(notice);
        }

        [Authorize(Roles = "Admin,SuperAdmin,HOD")]
        public async Task<IActionResult> Edit(int id)
        {
            var notice = await EditableNotices().AsNoTracking().SingleOrDefaultAsync(n => n.Id == id);
            if (notice == null) return NotFound();
            if (!User.IsInRole("HOD"))
                ViewBag.Departments = await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
            return View(notice);
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,SuperAdmin,HOD")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Message,TargetRole,DepartmentId")] Notice input)
        {
            if (id != input.Id) return NotFound();
            var notice = await EditableNotices().SingleOrDefaultAsync(n => n.Id == id);
            if (notice == null) return NotFound();
            ModelState.Remove("Department");
            if (User.IsInRole("HOD"))
            {
                input.TargetRole = "Department";
                input.DepartmentId = notice.DepartmentId;
            }
            else if (input.TargetRole != "Department")
            {
                input.DepartmentId = null;
            }
            else if (!input.DepartmentId.HasValue ||
                !await _context.Departments.AnyAsync(d => d.Id == input.DepartmentId))
            {
                ModelState.AddModelError(nameof(input.DepartmentId), "Select a valid department.");
            }
            if (!ModelState.IsValid)
            {
                if (!User.IsInRole("HOD"))
                    ViewBag.Departments = await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
                return View(input);
            }
            notice.Title = input.Title.Trim();
            notice.Message = input.Message.Trim();
            notice.TargetRole = input.TargetRole;
            notice.DepartmentId = input.DepartmentId;
            await _context.SaveChangesAsync();
            await _audit.RecordAsync("Updated announcement", nameof(Notice), id);
            TempData["Success"] = "Announcement updated successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }

        private IQueryable<Notice> EditableNotices()
        {
            var query = _context.Notices.AsQueryable();
            if (!User.IsInRole("HOD")) return query;
            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return query.Where(n => n.TargetRole == "Department" &&
                n.Department!.Teachers.Any(t => t.UserId == uid && t.TeacherType == TeacherType.HOD));
        }

        private string? CurrentRole()
        {
            var roles = new[]
            {
                "SuperAdmin", "Admin", "HOD", "Professor", "Student", "Guardian",
                "Accountant", "Librarian", "AdmissionOfficer", "ExaminationOfficer",
                "TransportManager", "HostelManager", "PlacementOfficer", "Employee"
            };
            return roles.FirstOrDefault(User.IsInRole);
        }
    }
}
