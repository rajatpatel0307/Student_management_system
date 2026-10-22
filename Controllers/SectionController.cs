using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;

namespace Student_Management_System.Controllers
{
    [Authorize]
    public class SectionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;
        private readonly IResultService _results;

        public SectionController(
            ApplicationDbContext context,
            AuditService audit,
            IResultService results)
        {
            _context = context;
            _audit = audit;
            _results = results;
        }

        // ================= INDEX =================
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Index([FromQuery] ListQuery request, int? classId, CancellationToken cancellationToken)
        {
            request.Normalize();
            IQueryable<Section> query = _context.Sections
                .Include(s => s.Class).ThenInclude(c => c.Students)
                .AsNoTracking()
                .Where(s => !s.IsArchived);
            ViewBag.Classes = new SelectList(_context.Classes.OrderBy(c => c.Name), "Id", "Name", classId);
            if (classId.HasValue) query = query.Where(s => s.ClassId == classId.Value);
            if (!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(s => s.Name.Contains(request.Search) || s.Class.Name.Contains(request.Search));
            ViewBag.Stats = new[]
            {
                new StatCardViewModel("Sections", (await query.CountAsync(cancellationToken)).ToString("N0"), "Across current filters", "fa-solid fa-layer-group"),
                new StatCardViewModel("Students", (await _context.Students.CountAsync(s => query.Select(x => x.Id).Contains(s.SectionId), cancellationToken)).ToString("N0"), "Students in these sections", "fa-solid fa-user-graduate", "info"),
                new StatCardViewModel("Classes represented", (await query.Select(s => s.ClassId).Distinct().CountAsync(cancellationToken)).ToString("N0"), "Distinct class groups", "fa-solid fa-school", "success")
            };
            return View(await query.OrderBy(s => s.Class.Name).ThenBy(s => s.Name).ToPagedResultAsync(request, cancellationToken));
        }

        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var allowed = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") ||
                (User.IsInRole("HOD") && await _context.Sections.AnyAsync(s => s.Id == id &&
                    s.Class.Department!.Teachers.Any(t => t.UserId == uid && t.TeacherType == TeacherType.HOD), cancellationToken)) ||
                (User.IsInRole("Professor") && await _context.Sections.AnyAsync(s => s.Id == id &&
                    s.Class.TeacherAssignments.Any(a => a.Teacher.UserId == uid), cancellationToken)) ||
                (User.IsInRole("Student") && await _context.Students.AnyAsync(s => s.UserId == uid && s.SectionId == id, cancellationToken));
            if (!allowed) return Forbid();
            var section = await _context.Sections.AsNoTracking().Include(s => s.Class).ThenInclude(c => c.Department)
                .Include(s => s.Class).ThenInclude(c => c.Semester).SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
            if (section == null) return NotFound();
            var studentQuery = _context.Students.AsNoTracking().Where(s => s.SectionId == id);
            if (User.IsInRole("Student")) studentQuery = studentQuery.Where(s => s.UserId == uid);
            var students = await studentQuery.OrderBy(s => s.FullName).ToListAsync(cancellationToken);
            var ids = students.Select(s => s.Id).ToList();
            var attendance = await _context.Attendances.AsNoTracking().Where(a => ids.Contains(a.StudentId)).ToListAsync(cancellationToken);
            var marksQuery = await _results.ScopeAsync(
                _context.Marks.AsNoTracking()
                    .Include(m => m.Exam)
                    .Where(m => ids.Contains(m.StudentId)),
                User,
                cancellationToken);
            var marks = await marksQuery.ToListAsync(cancellationToken);
            return View(new SectionDetailsViewModel
            {
                Section = section, Students = students,
                Subjects = await _context.Subjects.AsNoTracking().Include(s => s.Teacher).Where(s => s.ClassId == section.ClassId).OrderBy(s => s.Name).ToListAsync(cancellationToken),
                Timetable = await _context.TimetableEntries.AsNoTracking().Include(t => t.Subject).Include(t => t.Teacher).Where(t => t.ClassId == section.ClassId).OrderBy(t => t.DayOfWeek).ThenBy(t => t.StartsAt).ToListAsync(cancellationToken),
                AttendancePercentage = attendance.Count == 0 ? 0 : attendance.Count(a => a.Status == "Present") * 100.0 / attendance.Count,
                AverageScore = marks.Count == 0 ? 0 : (double)marks.Average(m => m.Score)
            });
        }

        // ================= CREATE GET =================
        [Authorize(Roles = "Admin,SuperAdmin")]
        public IActionResult Create()
        {
            ViewBag.Classes = new SelectList(_context.Classes, "Id", "Name");
            ViewBag.Sections = new SelectList(_context.Sections, "Id", "Name");
            return View(new Section());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Create([Bind("Name,ClassId")] Section section)
        {
            ModelState.Remove("Class");
            if (section.ClassId == 0)
            {
                ModelState.AddModelError("ClassId", "Class is required.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Classes = new SelectList(_context.Classes, "Id", "Name", section.ClassId);
                return View(section);
            }

            _context.Sections.Add(section);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Section created.";

            return RedirectToAction(nameof(Index), new { classId = section.ClassId });
        }

        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Edit(int id)
        {
            var section = await _context.Sections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
            if (section == null) return NotFound();
            ViewBag.Classes = new SelectList(_context.Classes.OrderBy(c => c.Name), "Id", "Name", section.ClassId);
            return View(section);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,ClassId,RowVersion")] Section input)
        {
            if (id != input.Id) return NotFound();
            var section = await _context.Sections.SingleOrDefaultAsync(s => s.Id == id);
            if (section == null) return NotFound();
            ConcurrencyToken.Apply(_context, section, input.RowVersion);
            ModelState.Remove("Class");
            if (!await _context.Classes.AnyAsync(c => c.Id == input.ClassId))
                ModelState.AddModelError(nameof(input.ClassId), "Select a valid class.");
            if (section.ClassId != input.ClassId && await _context.Students.AnyAsync(s => s.SectionId == id))
                ModelState.AddModelError(nameof(input.ClassId), "Move or reassign the students before changing this section's class.");
            if (await _context.Sections.AnyAsync(s => s.Id != id && s.ClassId == input.ClassId && s.Name == input.Name))
                ModelState.AddModelError(nameof(input.Name), "A section with this name already exists in the selected class.");
            if (!ModelState.IsValid)
            {
                ViewBag.Classes = new SelectList(_context.Classes.OrderBy(c => c.Name), "Id", "Name", input.ClassId);
                return View(input);
            }
            section.Name = input.Name.Trim();
            section.ClassId = input.ClassId;
            await _context.SaveChangesAsync();
            await _audit.RecordAsync("Updated section", nameof(Section), id);
            TempData["Success"] = "Section updated successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
