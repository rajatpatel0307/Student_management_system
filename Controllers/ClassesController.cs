using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;

namespace Student_Management_System.Controllers
{
    [Authorize]
    public class ClassesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;

        public ClassesController(ApplicationDbContext context, AuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        private void LoadEditOptions(Class? selected = null)
        {
            ViewBag.Teachers = new SelectList(_context.Teachers.OrderBy(t => t.FullName), "Id", "FullName", selected?.TeacherId);
            ViewBag.Departments = new SelectList(_context.Departments.OrderBy(d => d.Name), "Id", "Name", selected?.DepartmentId);
            ViewBag.Semesters = new SelectList(_context.Semesters.OrderBy(s => s.Number), "Id", "Name", selected?.SemesterId);
        }

        // ================= INDEX =================
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Index([FromQuery] ListQuery request, int? teacherId, CancellationToken cancellationToken)
        {
            request.Normalize();
            var classes = _context.Classes
                .Include(c => c.Teacher)
                .Include(c => c.Department)
                .Include(c => c.Semester)
                .Include(c => c.Students)
                .Include(c => c.Sections)
                .AsNoTracking()
                .Where(c => !c.IsArchived)
                .AsQueryable();

            if (teacherId != null)
            {
                classes = classes.Where(c => c.TeacherId == teacherId);
            }

            if (!string.IsNullOrWhiteSpace(request.Search)) classes = classes.Where(c => c.Name.Contains(request.Search));
            if (request.DepartmentId.HasValue) classes = classes.Where(c => c.DepartmentId == request.DepartmentId);
            if (request.SemesterId.HasValue) classes = classes.Where(c => c.SemesterId == request.SemesterId);
            if (request.Status == "unassigned") classes = classes.Where(c => !c.TeacherAssignments.Any());
            ViewBag.Stats = new[]
            {
                new StatCardViewModel("Classes", (await classes.CountAsync(cancellationToken)).ToString("N0"), "Across current filters", "fa-solid fa-school"),
                new StatCardViewModel("Students", (await classes.SelectMany(c => c.Students).CountAsync(cancellationToken)).ToString("N0"), "Students in these classes", "fa-solid fa-user-graduate", "info"),
                new StatCardViewModel("Sections", (await classes.SelectMany(c => c.Sections).CountAsync(cancellationToken)).ToString("N0"), "Teaching groups", "fa-solid fa-layer-group", "success"),
                new StatCardViewModel("Without assignments", (await classes.CountAsync(c => !c.TeacherAssignments.Any(), cancellationToken)).ToString("N0"), "No additional Professor", "fa-solid fa-user-clock", "warning", "status", "unassigned")
            };
            ViewBag.Teachers = await _context.Teachers.AsNoTracking().OrderBy(t => t.FullName).ToListAsync(cancellationToken);
            ViewBag.Departments = await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(cancellationToken);
            ViewBag.Semesters = await _context.Semesters.AsNoTracking().OrderBy(s => s.Number).ToListAsync(cancellationToken);
            classes = (request.Sort?.ToLowerInvariant(), request.Direction) switch
            {
                ("students", "desc") => classes.OrderByDescending(c => c.Students.Count),
                ("students", _) => classes.OrderBy(c => c.Students.Count),
                ("department", "desc") => classes.OrderByDescending(c => c.Department!.Name).ThenBy(c => c.Name),
                ("department", _) => classes.OrderBy(c => c.Department!.Name).ThenBy(c => c.Name),
                ("name", "desc") => classes.OrderByDescending(c => c.Name),
                _ => classes.OrderBy(c => c.Name)
            };
            return View(await classes.ToPagedResultAsync(request, cancellationToken));
        }

        // ================= CREATE GET =================
        [Authorize(Roles = "Admin,SuperAdmin")]
        public IActionResult Create()
        {
            ViewBag.Teachers = new SelectList(_context.Teachers, "Id", "FullName");
            ViewBag.Departments = new SelectList(_context.Departments, "Id", "Name");
            ViewBag.Semesters = new SelectList(_context.Semesters, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Create([Bind("Name,TeacherId,DepartmentId,SemesterId")] Class cls)
        {
            ModelState.Remove("Students");
            ModelState.Remove("Sections");

            if (!ModelState.IsValid)
            {
                // 🔥 VERY IMPORTANT (reload dropdown)
                ViewBag.Teachers = new SelectList(_context.Teachers, "Id", "FullName", cls.TeacherId);
                ViewBag.Departments = new SelectList(_context.Departments, "Id", "Name", cls.DepartmentId);
                ViewBag.Semesters = new SelectList(_context.Semesters, "Id", "Name", cls.SemesterId);

                return View(cls);
            }

            _context.Classes.Add(cls);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var allowed = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") ||
                (User.IsInRole("HOD") && await _context.Classes.AnyAsync(c => c.Id == id && c.Department!.Teachers.Any(t => t.UserId == uid && t.TeacherType == TeacherType.HOD))) ||
                (User.IsInRole("Professor") && await _context.TeacherClassAssignments.AnyAsync(a => a.ClassId == id && a.Teacher.UserId == uid));
            if (!allowed) return Forbid();
            var item = await _context.Classes.AsNoTracking().Include(c => c.Teacher).Include(c => c.Department)
                .Include(c => c.Semester).ThenInclude(s => s!.AcademicYear).FirstOrDefaultAsync(c => c.Id == id);
            if (item == null) return NotFound();
            var students = await _context.Students.AsNoTracking().Include(s => s.Section).Where(s => s.ClassId == id).OrderBy(s => s.FullName).ToListAsync();
            var studentIds = students.Select(s => s.Id).ToList();
            var attendance = await _context.Attendances.AsNoTracking().Where(a => studentIds.Contains(a.StudentId)).ToListAsync();
            var marks = await _context.Marks.AsNoTracking().Where(m => studentIds.Contains(m.StudentId)).ToListAsync();
            return View(new ClassDetailsViewModel
            {
                Class = item,
                Students = students,
                Sections = await _context.Sections.AsNoTracking().Where(s => s.ClassId == id).OrderBy(s => s.Name).ToListAsync(),
                Subjects = await _context.Subjects.AsNoTracking().Include(s => s.Teacher).Where(s => s.ClassId == id).OrderBy(s => s.Name).ToListAsync(),
                Teachers = await _context.TeacherClassAssignments.AsNoTracking().Where(a => a.ClassId == id).Select(a => a.Teacher).OrderBy(t => t.FullName).ToListAsync(),
                Timetable = await _context.TimetableEntries.AsNoTracking().Include(t => t.Subject).Include(t => t.Teacher).Where(t => t.ClassId == id).OrderBy(t => t.DayOfWeek).ThenBy(t => t.StartsAt).ToListAsync(),
                Activities = await _context.AuditLogs.AsNoTracking().Where(a => a.EntityName == nameof(Class) && a.EntityId == id.ToString())
                    .OrderByDescending(a => a.CreatedUtc).Take(20).ToListAsync(),
                AttendancePercentage = attendance.Count == 0 ? 0 : attendance.Count(a => a.Status == "Present") * 100.0 / attendance.Count,
                AverageScore = marks.Count == 0 ? 0 : (double)marks.Average(m => m.Score)
            });
        }

        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Export(CancellationToken cancellationToken)
        {
            var data = await _context.Classes.AsNoTracking().Include(c => c.Department).Include(c => c.Semester).Include(c => c.Teacher).Include(c => c.Students).Include(c => c.Sections).OrderBy(c => c.Name).ToListAsync(cancellationToken);
            var rows = new List<string[]> { new[] { "Class", "Department", "Semester", "Class teacher", "Students", "Sections" } };
            rows.AddRange(data.Select(c => new[] { c.Name, c.Department?.Name ?? "", c.Semester?.Name ?? "", c.Teacher?.FullName ?? "", c.Students.Count.ToString(), c.Sections.Count.ToString() }));
            return File(CsvExport.Create(rows), "text/csv", $"classes-{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Edit(int id)
        {
            var item = await _context.Classes.FindAsync(id);
            if (item == null) return NotFound();
            LoadEditOptions(item); return View(item);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,TeacherId,DepartmentId,SemesterId,RowVersion")] Class input)
        {
            if (id != input.Id) return NotFound();
            var item = await _context.Classes.FindAsync(id);
            if (item == null) return NotFound();
            ConcurrencyToken.Apply(_context, item, input.RowVersion);
            ModelState.Remove("Teacher"); ModelState.Remove("Department"); ModelState.Remove("Semester");
            ModelState.Remove("Students"); ModelState.Remove("Sections"); ModelState.Remove("TeacherAssignments");
            if (!await _context.Teachers.AnyAsync(t => t.Id == input.TeacherId &&
                (!input.DepartmentId.HasValue || t.DepartmentId == null || t.DepartmentId == input.DepartmentId)))
                ModelState.AddModelError(nameof(input.TeacherId), "Select a valid Teacher for this department.");
            if (input.DepartmentId.HasValue && !await _context.Departments.AnyAsync(d => d.Id == input.DepartmentId))
                ModelState.AddModelError(nameof(input.DepartmentId), "Select a valid department.");
            if (input.SemesterId.HasValue && !await _context.Semesters.AnyAsync(s => s.Id == input.SemesterId &&
                (!input.DepartmentId.HasValue || s.DepartmentId == input.DepartmentId)))
                ModelState.AddModelError(nameof(input.SemesterId), "Select a semester from this class department.");
            if (await _context.Classes.AnyAsync(c => c.Id != id && c.Name == input.Name &&
                c.DepartmentId == input.DepartmentId && c.SemesterId == input.SemesterId))
                ModelState.AddModelError(nameof(input.Name), "A class with this name already exists in the selected department and semester.");
            if (!ModelState.IsValid) { LoadEditOptions(input); return View(input); }
            item.Name = input.Name; item.TeacherId = input.TeacherId;
            item.DepartmentId = input.DepartmentId; item.SemesterId = input.SemesterId;
            if (!await _context.TeacherClassAssignments.AnyAsync(a => a.ClassId == id && a.TeacherId == input.TeacherId))
                _context.TeacherClassAssignments.Add(new TeacherClassAssignment { ClassId = id, TeacherId = input.TeacherId });
            await _context.SaveChangesAsync();
            await _audit.RecordAsync("Updated class profile and primary Teacher", nameof(Class), id);
            TempData["Success"] = "Class updated successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Classes.Include(c => c.Students).Include(c => c.Sections).FirstOrDefaultAsync(c => c.Id == id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> DeleteConfirmed(int id, string? reason)
        {
            var item = await _context.Classes.Include(c => c.Students).Include(c => c.Sections).FirstOrDefaultAsync(c => c.Id == id);
            if (item == null) return NotFound();
            if (string.IsNullOrWhiteSpace(reason))
            {
                ModelState.AddModelError(nameof(reason), "An archive reason is required.");
                return View("Delete", item);
            }
            item.IsArchived = true;
            item.ArchivedUtc = DateTime.UtcNow;
            item.ArchivedByUserId = User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            item.ArchiveReason = reason.Trim();
            await _context.SaveChangesAsync();
            await _audit.RecordAsync("Archived class", nameof(Class), id);
            TempData["Success"] = "Class archived; historical relationships were preserved.";
            return RedirectToAction(nameof(Index));
        }
    }
}
