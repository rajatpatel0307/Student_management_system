using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Student_Management_System.Services;

[Authorize]
public class SubjectController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;
    private readonly IResultService _results;

    public SubjectController(
        ApplicationDbContext context,
        AuditService audit,
        IResultService results)
    {
        _context = context;
        _audit = audit;
        _results = results;
    }

    private void LoadDropdowns(SubjectViewModel vm)
    {
        vm.Classes = _context.Classes.Select(c => new SelectListItem
        {
            Value = c.Id.ToString(),
            Text = c.Name
        });

        vm.Teachers = _context.Teachers.Select(t => new SelectListItem
        {
            Value = t.Id.ToString(),
            Text = t.FullName
        });
        vm.Departments = _context.Departments.Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Name });
        vm.Semesters = _context.Semesters.Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name });
    }

    // 📋 LIST
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Index([FromQuery] ListQuery request, CancellationToken cancellationToken)
    {
        request.Normalize();
        var subjects = _context.Subjects
            .Include(s => s.Class)
            .Include(s => s.Teacher)
            .Include(s => s.Department)
            .Include(s => s.Semester)
            .Include(s => s.StudentEnrollments)
            .Include(s => s.TeacherAssignments)
            .AsNoTracking()
            .Where(s => !s.IsArchived)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search)) subjects = subjects.Where(s => s.Name.Contains(request.Search));
        if (request.DepartmentId.HasValue) subjects = subjects.Where(s => s.DepartmentId == request.DepartmentId);
        if (request.ClassId.HasValue) subjects = subjects.Where(s => s.ClassId == request.ClassId);
        if (request.SemesterId.HasValue) subjects = subjects.Where(s => s.SemesterId == request.SemesterId);
        if (request.Status == "unassigned") subjects = subjects.Where(s => !s.TeacherAssignments.Any() && s.TeacherId == null);
        ViewBag.Stats = new[]
        {
            new StatCardViewModel("Subjects", (await subjects.CountAsync(cancellationToken)).ToString("N0"), "Across current filters", "fa-solid fa-book"),
            new StatCardViewModel("Assigned", (await subjects.CountAsync(s => s.TeacherId != null || s.TeacherAssignments.Any(), cancellationToken)).ToString("N0"), "Professor responsibility set", "fa-solid fa-user-check", "success"),
            new StatCardViewModel("Unassigned", (await subjects.CountAsync(s => s.TeacherId == null && !s.TeacherAssignments.Any(), cancellationToken)).ToString("N0"), "Needs a Professor", "fa-solid fa-user-clock", "warning", "status", "unassigned"),
            new StatCardViewModel("Enrollments", (await subjects.SelectMany(s => s.StudentEnrollments).CountAsync(cancellationToken)).ToString("N0"), "Student-subject links", "fa-solid fa-link", "info")
        };
        ViewBag.Departments = await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(cancellationToken);
        ViewBag.Classes = await _context.Classes.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        ViewBag.Semesters = await _context.Semesters.AsNoTracking().OrderBy(s => s.Number).ToListAsync(cancellationToken);
        subjects = (request.Sort?.ToLowerInvariant(), request.Direction) switch
        {
            ("class", "desc") => subjects.OrderByDescending(s => s.Class.Name).ThenBy(s => s.Name),
            ("class", _) => subjects.OrderBy(s => s.Class.Name).ThenBy(s => s.Name),
            ("department", "desc") => subjects.OrderByDescending(s => s.Department!.Name).ThenBy(s => s.Name),
            ("department", _) => subjects.OrderBy(s => s.Department!.Name).ThenBy(s => s.Name),
            ("name", "desc") => subjects.OrderByDescending(s => s.Name),
            _ => subjects.OrderBy(s => s.Name)
        };
        return View(await subjects.ToPagedResultAsync(request, cancellationToken));
    }

    // ➕ CREATE (GET)
    [Authorize(Roles = "Admin,SuperAdmin")]
    public IActionResult Create()
    {
        var model = new SubjectViewModel
        {
            Classes = new SelectList(_context.Classes, "Id", "Name"),
            Teachers = new SelectList(_context.Teachers, "Id", "FullName")
            ,Departments = new SelectList(_context.Departments, "Id", "Name")
            ,Semesters = new SelectList(_context.Semesters, "Id", "Name")
        };

        return View(model);
    }

    // ➕ CREATE (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Create(SubjectViewModel model)
    {
        // 🔥 REMOVE VALIDATION FOR DROPDOWNS
        ModelState.Remove("Classes");
        ModelState.Remove("Teachers");
        ModelState.Remove("Departments");
        ModelState.Remove("Semesters");

        if (!ModelState.IsValid)
        {
            LoadDropdowns(model);
            return View(model);
        }

        var subject = new Subject
        {
            Name = model.Name,
            ClassId = model.ClassId.GetValueOrDefault(),
            TeacherId = model.TeacherId
            ,DepartmentId = model.DepartmentId
            ,SemesterId = model.SemesterId
        };

        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Subject created.";

        return RedirectToAction("Index");
    }

    // ✏️ EDIT (GET)
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Edit(int id)
    {
        var subject = await _context.Subjects.FindAsync(id);
        if (subject == null) return NotFound();

        var vm = new SubjectViewModel
        {
            Id = subject.Id,
            Name = subject.Name,
            ClassId = subject.ClassId,
            TeacherId = subject.TeacherId
            ,DepartmentId = subject.DepartmentId
            ,SemesterId = subject.SemesterId
            ,RowVersion = subject.RowVersion
        };

        LoadDropdowns(vm);
        return View(vm);
    }

    // ✏️ EDIT (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Edit(int id, SubjectViewModel vm)
    {
        if (id != vm.Id) return NotFound();

        var subject = await _context.Subjects.FindAsync(id);
        if (subject == null) return NotFound();
        ConcurrencyToken.Apply(_context, subject, vm.RowVersion);

        ModelState.Remove("Classes");
        ModelState.Remove("Teachers");
        ModelState.Remove("Departments");
        ModelState.Remove("Semesters");
        if (vm.ClassId == null)
        {
            ModelState.AddModelError(nameof(vm.ClassId), "Class is required.");
        }
        var selectedClass = vm.ClassId.HasValue
            ? await _context.Classes.AsNoTracking().SingleOrDefaultAsync(c => c.Id == vm.ClassId)
            : null;
        if (vm.ClassId.HasValue && selectedClass == null)
            ModelState.AddModelError(nameof(vm.ClassId), "Select a valid class.");
        if (vm.DepartmentId.HasValue && selectedClass?.DepartmentId != vm.DepartmentId)
            ModelState.AddModelError(nameof(vm.DepartmentId), "The department must match the selected class.");
        if (vm.SemesterId.HasValue && !await _context.Semesters.AnyAsync(s => s.Id == vm.SemesterId &&
            (!vm.DepartmentId.HasValue || s.DepartmentId == vm.DepartmentId)))
            ModelState.AddModelError(nameof(vm.SemesterId), "Select a semester from this department.");
        if (vm.TeacherId.HasValue && !await _context.Teachers.AnyAsync(t => t.Id == vm.TeacherId &&
            (!vm.DepartmentId.HasValue || t.DepartmentId == null || t.DepartmentId == vm.DepartmentId)))
            ModelState.AddModelError(nameof(vm.TeacherId), "Select a Professor from this department.");
        if (vm.ClassId.HasValue && await _context.Subjects.AnyAsync(s =>
            s.Id != id && s.ClassId == vm.ClassId && s.Name == vm.Name))
            ModelState.AddModelError(nameof(vm.Name), "This subject already exists in the selected class.");

        if (ModelState.IsValid)
        {
            subject.Name = vm.Name;
            subject.ClassId = vm.ClassId.GetValueOrDefault();
            subject.TeacherId = vm.TeacherId;
            subject.DepartmentId = vm.DepartmentId;
            subject.SemesterId = vm.SemesterId;

            if (vm.TeacherId.HasValue &&
                !await _context.TeacherSubjectAssignments.AnyAsync(a => a.SubjectId == id && a.TeacherId == vm.TeacherId))
                _context.TeacherSubjectAssignments.Add(new TeacherSubjectAssignment
                    { SubjectId = id, TeacherId = vm.TeacherId.Value });

            await _context.SaveChangesAsync();
            await _audit.RecordAsync("Updated subject profile and primary Professor", nameof(Subject), id);
            TempData["Success"] = "Subject updated successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }

        LoadDropdowns(vm);
        return View(vm);
    }

    // ❌ DELETE
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var subject = await _context.Subjects.FindAsync(id);
        if (subject == null) return NotFound();

        return View(subject);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> DeleteConfirmed(int id, string? reason)
    {
        var subject = await _context.Subjects.FindAsync(id);
        if (subject == null) return NotFound();
        if (string.IsNullOrWhiteSpace(reason))
        {
            ModelState.AddModelError(nameof(reason), "An archive reason is required.");
            return View("Delete", subject);
        }
        subject.IsArchived = true;
        subject.ArchivedUtc = DateTime.UtcNow;
        subject.ArchivedByUserId = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        subject.ArchiveReason = reason.Trim();
        await _context.SaveChangesAsync();
        await _audit.RecordAsync("Archived subject", nameof(Subject), id);
        TempData["Success"] = "Subject archived; historical records were preserved.";
        return RedirectToAction(nameof(Index));
    }

    // 🔍 DETAILS
    public async Task<IActionResult> Details(int id)
    {
        var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var allowed = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") ||
            (User.IsInRole("HOD") && await _context.Subjects.AnyAsync(s => s.Id == id && s.Department!.Teachers.Any(t => t.UserId == uid && t.TeacherType == TeacherType.HOD))) ||
            (User.IsInRole("Professor") && await _context.TeacherSubjectAssignments.AnyAsync(a => a.SubjectId == id && a.Teacher.UserId == uid)) ||
            (User.IsInRole("Student") && await _context.Subjects.AnyAsync(s => s.Id == id && (s.StudentEnrollments.Any(e => e.Student.UserId == uid) || s.Class.Students.Any(st => st.UserId == uid))));
        if (!allowed) return Forbid();
        var subject = await _context.Subjects.AsNoTracking()
            .Include(s => s.Class)
            .Include(s => s.Teacher)
            .Include(s => s.Department)
            .Include(s => s.Semester)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (subject == null) return NotFound();

        var teachers = await _context.TeacherSubjectAssignments.AsNoTracking().Where(a => a.SubjectId == id).Select(a => a.Teacher).OrderBy(t => t.FullName).ToListAsync();
        var studentQuery = _context.Students.AsNoTracking().Where(s => s.ClassId == subject.ClassId || s.SubjectEnrollments.Any(e => e.SubjectId == id));
        if (User.IsInRole("Student")) studentQuery = studentQuery.Where(s => s.UserId == uid);
        var students = await studentQuery.OrderBy(s => s.FullName).ToListAsync();
        var permittedStudentIds = students.Select(s => s.Id).ToList();
        var attendance = await _context.Attendances.AsNoTracking().Include(a => a.Student).Where(a => a.SubjectId == id && permittedStudentIds.Contains(a.StudentId)).OrderByDescending(a => a.Date).ToListAsync();
        var visibleMarks = await _results.ScopeAsync(
            _context.Marks.AsNoTracking()
                .Include(m => m.Student)
                .Include(m => m.Exam)
                .Where(m => m.SubjectId == id && permittedStudentIds.Contains(m.StudentId)),
            User);
        var marks = await visibleMarks
            .OrderByDescending(m => m.Exam.ExamDate)
            .ToListAsync();
        return View(new SubjectDetailsViewModel
        {
            Subject = subject, Teachers = teachers, Students = students, Attendance = attendance, Marks = marks,
            Assignments = await _context.Assignments.AsNoTracking().Where(a => a.SubjectId == id).OrderByDescending(a => a.DueUtc).ToListAsync(),
            Materials = await _context.StudyMaterials.AsNoTracking().Where(m => m.SubjectId == id).OrderByDescending(m => m.CreatedUtc).ToListAsync(),
            Exams = marks.Select(m => m.Exam).DistinctBy(e => e.Id)
                .OrderByDescending(e => e.ExamDate).ToList(),
            AttendancePercentage = attendance.Count == 0 ? 0 : attendance.Count(a => a.Status == "Present") * 100.0 / attendance.Count,
            AverageScore = marks.Count == 0 ? 0 : (double)marks.Average(m => m.Score)
        });
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var data = await _context.Subjects.AsNoTracking().Include(s => s.Department).Include(s => s.Class).Include(s => s.Semester).Include(s => s.Teacher).Include(s => s.StudentEnrollments).OrderBy(s => s.Name).ToListAsync(cancellationToken);
        var rows = new List<string[]> { new[] { "Subject", "Department", "Class", "Semester", "Professor", "Students" } };
        rows.AddRange(data.Select(s => new[] { s.Name, s.Department?.Name ?? "", s.Class.Name, s.Semester?.Name ?? "", s.Teacher?.FullName ?? "", s.StudentEnrollments.Count.ToString() }));
        return File(CsvExport.Create(rows), "text/csv", $"subjects-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
