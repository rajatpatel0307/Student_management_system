using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using System.Security.Claims;
using Student_Management_System.Services;

[Authorize]
public class AttendanceController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;
    private readonly IAttendanceService _attendance;

    public AttendanceController(
        ApplicationDbContext context,
        AuditService audit,
        IAttendanceService attendance)
    {
        _context = context;
        _audit = audit;
        _attendance = attendance;
    }

    // 🔹 SELECT CLASS
    [Authorize(Roles = "HOD,Professor")]
    public IActionResult SelectClass()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ViewBag.Classes = User.IsInRole("HOD")
            ? _context.Classes.Where(c => c.Department!.Teachers.Any(t => t.UserId == userId && t.TeacherType == TeacherType.HOD)).ToList()
            : _context.Classes.Where(c => c.TeacherAssignments.Any(a => a.Teacher.UserId == userId)).ToList();
        return View();
    }

    // 🔹 MARK ATTENDANCE
    [Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> Mark(int classId)
    {
        if (!await CanAccessClass(classId)) return Forbid();

        var students = await _context.Students
            .Include(s => s.Section)
            .Where(s => s.ClassId == classId)
            .ToListAsync();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ViewBag.Class = await _context.Classes.FindAsync(classId);
        ViewBag.Subjects = User.IsInRole("HOD")
            ? await _context.Subjects.Where(s => s.ClassId == classId).OrderBy(s => s.Name).ToListAsync()
            : await _context.Subjects.Where(s => s.ClassId == classId && s.TeacherAssignments.Any(a => a.Teacher.UserId == userId)).OrderBy(s => s.Name).ToListAsync();

        return View(students);
    }

    // 🔹 SAVE ATTENDANCE
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> Mark(List<Attendance> model, int? subjectId, string? lecture)
    {
        if (model.Count == 0) return BadRequest();
        var studentIds = model.Select(x => x.StudentId).Distinct().ToList();
        var classIds = await _context.Students.Where(s => studentIds.Contains(s.Id))
            .Select(s => s.ClassId).Distinct().ToListAsync();
        if (classIds.Count != 1 || !await CanAccessClass(classIds[0])) return Forbid();
        if (!subjectId.HasValue)
            ModelState.AddModelError(nameof(subjectId), "Select a subject.");
        else
        {
            var userIdForSubject = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var subjectAllowed = User.IsInRole("HOD")
                ? await _context.Subjects.AnyAsync(s => s.Id == subjectId && s.ClassId == classIds[0] &&
                    s.Department!.Teachers.Any(t => t.UserId == userIdForSubject && t.TeacherType == TeacherType.HOD))
                : await _context.TeacherSubjectAssignments.AnyAsync(a =>
                    a.SubjectId == subjectId && a.Subject.ClassId == classIds[0] && a.Teacher.UserId == userIdForSubject);
            if (!subjectAllowed) return Forbid();
        }
        var date = DateTime.Today;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var teacherId = await _context.Teachers.Where(t => t.UserId == userId).Select(t => (int?)t.Id).SingleOrDefaultAsync();
        if (!teacherId.HasValue) return Forbid();
        if (!subjectId.HasValue) return BadRequest("Select a subject.");
        try
        {
            await _attendance.SubmitAsync(
                classIds[0], subjectId.Value, teacherId.Value, date,
                lecture ?? "", model, userId ?? "");
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }

        TempData["Success"] = "Attendance submitted successfully.";
        return RedirectToAction("SelectClass");
    }

    // 🔹 HISTORY
    [Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> History(int classId)
    {
        if (!await CanAccessClass(classId)) return Forbid();
        var data = await _context.Attendances
            .Include(a => a.Student)
            .Where(a => a.Student.ClassId == classId)
            .OrderByDescending(a => a.Date)
            .ToListAsync();

        return View(data);
    }

    [Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> StudentStats(int classId)
    {
        if (!await CanAccessClass(classId)) return Forbid();
        var students = await _context.Students.AsNoTracking()
            .Where(s => s.ClassId == classId)
            .Select(s => new { s.Id, s.FullName }).ToListAsync();
        var percentages = await _attendance.GetPercentagesAsync(
            _context.Attendances.AsNoTracking().Where(a => a.Student.ClassId == classId));
        var result = students.Select(s => new
        {
            Name = s.FullName,
            Percentage = percentages.GetValueOrDefault(s.Id)
        }).Cast<dynamic>().ToList();

        return View(result);
    }
    [Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> MonthlyReport(int classId, int month, int? year)
    {
        if (!await CanAccessClass(classId)) return Forbid();
        if (month is < 1 or > 12) return BadRequest();
        var reportYear = year ?? DateTime.Today.Year;
        if (reportYear is < 2000 or > 2200) return BadRequest();
        var data = await _context.Attendances
            .Include(a => a.Student)
            .Where(a => a.Student.ClassId == classId &&
                        a.Date.Month == month && a.Date.Year == reportYear)
            .ToListAsync();

        return View(data);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Professor,HOD")]
    public async Task<IActionResult> RequestCorrection(
        int id, string requestedStatus, string reason)
    {
        if (requestedStatus is not ("Present" or "Absent" or "Late" or "Excused") ||
            string.IsNullOrWhiteSpace(reason)) return BadRequest();
        var attendance = await _context.Attendances.Include(a => a.Student)
            .SingleOrDefaultAsync(a => a.Id == id);
        if (attendance == null) return NotFound();
        if (!await CanAccessClass(attendance.Student.ClassId)) return Forbid();
        if (await _context.AttendanceCorrections.AnyAsync(c =>
                c.AttendanceId == id && c.Status == ApprovalStatus.Pending))
            return Conflict("A correction request is already pending.");
        _context.AttendanceCorrections.Add(new AttendanceCorrection
        {
            AttendanceId = id,
            PreviousStatus = attendance.Status,
            RequestedStatus = requestedStatus,
            Reason = reason.Trim(),
            RequestedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""
        });
        await _context.SaveChangesAsync();
        await _audit.RecordAsync("Requested attendance correction", nameof(Attendance), id);
        TempData["Success"] = "Correction request submitted to the HOD.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "HOD")]
    public async Task<IActionResult> ReviewCorrection(
        long id, bool approve, string? comment)
    {
        var correction = await _context.AttendanceCorrections
            .Include(c => c.Attendance).ThenInclude(a => a.Student)
            .Include(c => c.Attendance).ThenInclude(a => a.AttendanceSession)
            .SingleOrDefaultAsync(c => c.Id == id);
        if (correction == null) return NotFound();
        if (correction.Status != ApprovalStatus.Pending)
            return Conflict("This request has already been reviewed.");
        if (!await CanAccessClass(correction.Attendance.Student.ClassId)) return Forbid();
        correction.Status = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        correction.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        correction.ReviewComment = comment?.Trim();
        correction.ReviewedUtc = DateTime.UtcNow;
        if (approve)
        {
            correction.Attendance.Status = correction.RequestedStatus;
            correction.Attendance.UpdatedUtc = DateTime.UtcNow;
            if (correction.Attendance.AttendanceSession != null)
                correction.Attendance.AttendanceSession.Status = AttendanceSessionStatus.Corrected;
        }
        await _context.SaveChangesAsync();
        await _audit.RecordAsync(approve ? "Approved attendance correction" : "Rejected attendance correction",
            nameof(AttendanceCorrection), id);
        TempData["Success"] = approve ? "Correction approved." : "Correction rejected.";
        return RedirectToAction(nameof(Details), new { id = correction.AttendanceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> Update(int id, string status)
    {
        if (status is not ("Present" or "Absent" or "Late" or "Excused")) return BadRequest();
        var attendance = await _context.Attendances.Include(a => a.Student).SingleOrDefaultAsync(a => a.Id == id);
        if (attendance == null) return NotFound();
        if (!await CanAccessClass(attendance.Student.ClassId)) return Forbid();
        if (!User.IsInRole("HOD") && attendance.Date < DateTime.Today.AddDays(-1)) return Forbid();
        attendance.Status = status; attendance.UpdatedUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _audit.RecordAsync($"Updated attendance status to {status}", nameof(Attendance), id);
        TempData["Success"] = "Attendance updated successfully.";
        return RedirectToAction(nameof(History), new { classId = attendance.Student.ClassId });
    }

    public async Task<IActionResult> Details(int id)
    {
        var attendance = await _context.Attendances.AsNoTracking()
            .Include(a => a.Student).ThenInclude(s => s.Class)
            .Include(a => a.Student).ThenInclude(s => s.Section)
            .Include(a => a.Subject).Include(a => a.Teacher)
            .SingleOrDefaultAsync(a => a.Id == id);
        if (attendance == null) return NotFound();
        if (!await CanReadAttendanceAsync(attendance)) return Forbid();
        ViewBag.Corrections = await _context.AttendanceCorrections.AsNoTracking()
            .Where(c => c.AttendanceId == id)
            .OrderByDescending(c => c.RequestedUtc)
            .ToListAsync();
        return View(attendance);
    }

    [Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> Edit(int id)
    {
        var attendance = await _context.Attendances.AsNoTracking().Include(a => a.Student)
            .Include(a => a.Subject).SingleOrDefaultAsync(a => a.Id == id);
        if (attendance == null) return NotFound();
        if (!await CanAccessClass(attendance.Student.ClassId)) return Forbid();
        if (!User.IsInRole("HOD") && attendance.Date < DateTime.Today.AddDays(-1)) return Forbid();
        return View(attendance);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "HOD,Professor")]
    public async Task<IActionResult> Edit(int id, string status, string? lecture)
    {
        var attendance = await _context.Attendances.Include(a => a.Student)
            .SingleOrDefaultAsync(a => a.Id == id);
        if (attendance == null) return NotFound();
        if (!await CanAccessClass(attendance.Student.ClassId)) return Forbid();
        if (!User.IsInRole("HOD") && attendance.Date < DateTime.Today.AddDays(-1)) return Forbid();
        if (status is not ("Present" or "Absent" or "Late" or "Excused"))
        {
            ModelState.AddModelError(nameof(status), "Select a valid attendance status.");
            return View(attendance);
        }
        attendance.Status = status;
        attendance.Lecture = string.IsNullOrWhiteSpace(lecture) ? null : lecture.Trim();
        attendance.UpdatedUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _audit.RecordAsync("Updated attendance record", nameof(Attendance), id);
        TempData["Success"] = "Attendance updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<bool> CanAccessClass(int classId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.IsInRole("HOD"))
            return await _context.Classes.AnyAsync(c => c.Id == classId &&
                c.Department!.Teachers.Any(t => t.UserId == userId && t.TeacherType == TeacherType.HOD));
        return await _context.TeacherClassAssignments.AnyAsync(a => a.ClassId == classId && a.Teacher.UserId == userId);
    }

    private async Task<bool> CanReadAttendanceAsync(Attendance attendance)
    {
        if (User.IsInRole("Admin") || User.IsInRole("SuperAdmin")) return true;
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.IsInRole("Student")) return attendance.Student.UserId == uid;
        return await CanAccessClass(attendance.Student.ClassId);
    }

    [Authorize(Roles = "Admin,SuperAdmin,HOD,Professor")]
    public async Task<IActionResult> Index([FromQuery] ListQuery request, CancellationToken cancellationToken)
    {
        request.Normalize();
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = _context.Attendances.AsNoTracking()
            .Include(a => a.Student).ThenInclude(s => s.Class)
            .Include(a => a.Student).ThenInclude(s => s.Section)
            .Include(a => a.Student).ThenInclude(s => s.Department)
            .Include(a => a.Subject).Include(a => a.Teacher).AsQueryable();
        if (User.IsInRole("HOD"))
            query = query.Where(a => a.Student.Department!.Teachers.Any(t => t.UserId == uid && t.TeacherType == TeacherType.HOD));
        else if (User.IsInRole("Professor"))
            query = query.Where(a => a.Teacher!.UserId == uid || a.Subject!.TeacherAssignments.Any(x => x.Teacher.UserId == uid));
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(a => a.Student.FullName.Contains(request.Search) || (a.Subject != null && a.Subject.Name.Contains(request.Search)));
        if (request.DepartmentId.HasValue) query = query.Where(a => a.Student.DepartmentId == request.DepartmentId);
        if (request.ClassId.HasValue) query = query.Where(a => a.Student.ClassId == request.ClassId);
        if (request.SectionId.HasValue) query = query.Where(a => a.Student.SectionId == request.SectionId);
        if (request.SubjectId.HasValue) query = query.Where(a => a.SubjectId == request.SubjectId);
        if (request.TeacherId.HasValue) query = query.Where(a => a.TeacherId == request.TeacherId);
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(a => a.Status == request.Status);
        if (request.From.HasValue) query = query.Where(a => a.Date >= request.From.Value.Date);
        if (request.To.HasValue) query = query.Where(a => a.Date <= request.To.Value.Date);
        var today = DateTime.Today;
        var total = await query.CountAsync(cancellationToken);
        var present = await query.CountAsync(a => a.Status == "Present", cancellationToken);
        ViewBag.Stats = new[]
        {
            new StatCardViewModel("Attendance records", total.ToString("N0"), "Across current filters", "fa-solid fa-clipboard-user"),
            new StatCardViewModel("Present", present.ToString("N0"), total == 0 ? "No records" : $"{present * 100.0 / total:0.0}% of filtered records", "fa-solid fa-user-check", "success", "status", "Present"),
            new StatCardViewModel("Absent", (await query.CountAsync(a => a.Status == "Absent", cancellationToken)).ToString("N0"), "Recorded absences", "fa-solid fa-user-xmark", "danger", "status", "Absent"),
            new StatCardViewModel("Today", (await query.CountAsync(a => a.Date == today, cancellationToken)).ToString("N0"), "Records for today", "fa-regular fa-calendar", "info")
        };
        ViewBag.Departments = await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(cancellationToken);
        ViewBag.Classes = await _context.Classes.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        ViewBag.Subjects = await _context.Subjects.AsNoTracking().OrderBy(s => s.Name).ToListAsync(cancellationToken);
        return View(await query.OrderByDescending(a => a.Date).ThenBy(a => a.Student.FullName).ToPagedResultAsync(request, cancellationToken));
    }

    [Authorize(Roles = "Admin,SuperAdmin,HOD,Professor")]
    public async Task<IActionResult> Sessions(CancellationToken cancellationToken)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = _context.AttendanceSessions.AsNoTracking()
            .Include(s => s.Class).ThenInclude(c => c.Department)
            .Include(s => s.Subject).Include(s => s.Teacher)
            .Include(s => s.Records).AsQueryable();
        if (User.IsInRole("HOD"))
            query = query.Where(s => s.Class.Department!.Teachers.Any(t =>
                t.UserId == uid && t.TeacherType == TeacherType.HOD));
        else if (User.IsInRole("Professor"))
            query = query.Where(s => s.Teacher.UserId == uid);
        return View(await query.OrderByDescending(s => s.SessionDate)
            .ThenByDescending(s => s.SubmittedUtc).Take(250).ToListAsync(cancellationToken));
    }

    [PermissionAuthorize("attendance.approve")]
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "HOD")]
    public async Task<IActionResult> ApproveSession(
        long id,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        var session = await _context.AttendanceSessions
            .Include(s => s.Class).ThenInclude(c => c.Department)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session == null) return NotFound();
        if (!await CanAccessClass(session.ClassId)) return Forbid();
        if (session.Status != AttendanceSessionStatus.Submitted)
            return Conflict("Only submitted attendance sessions can be approved.");
        ConcurrencyToken.Apply(_context, session, rowVersion);
        session.Status = AttendanceSessionStatus.Approved;
        session.ApprovedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        session.ApprovedUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync("Approved attendance session", nameof(AttendanceSession), id);
        TempData["Success"] = "Attendance session approved.";
        return RedirectToAction(nameof(Sessions));
    }

    [PermissionAuthorize("reports.export")]
    [Authorize(Roles = "SuperAdmin,Admin,HOD,Professor")]
    public async Task<IActionResult> Export(
        [FromQuery] ListQuery request,
        CancellationToken cancellationToken)
    {
        request.Normalize();
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = _context.Attendances.AsNoTracking()
            .Include(a => a.Student).ThenInclude(s => s.Class)
            .Include(a => a.Student).ThenInclude(s => s.Section)
            .Include(a => a.Student).ThenInclude(s => s.Department)
            .Include(a => a.Subject).Include(a => a.Teacher).AsQueryable();
        if (User.IsInRole("HOD"))
            query = query.Where(a => a.Student.Department!.Teachers.Any(t =>
                t.UserId == uid && t.TeacherType == TeacherType.HOD));
        else if (User.IsInRole("Professor"))
            query = query.Where(a => a.Teacher!.UserId == uid ||
                a.Subject!.TeacherAssignments.Any(x => x.Teacher.UserId == uid));
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(a => a.Student.FullName.Contains(request.Search) ||
                                     (a.Subject != null && a.Subject.Name.Contains(request.Search)));
        if (request.DepartmentId.HasValue) query = query.Where(a => a.Student.DepartmentId == request.DepartmentId);
        if (request.ClassId.HasValue) query = query.Where(a => a.Student.ClassId == request.ClassId);
        if (request.SectionId.HasValue) query = query.Where(a => a.Student.SectionId == request.SectionId);
        if (request.SubjectId.HasValue) query = query.Where(a => a.SubjectId == request.SubjectId);
        if (request.TeacherId.HasValue) query = query.Where(a => a.TeacherId == request.TeacherId);
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(a => a.Status == request.Status);
        if (request.From.HasValue) query = query.Where(a => a.Date >= request.From.Value.Date);
        if (request.To.HasValue) query = query.Where(a => a.Date <= request.To.Value.Date);

        var data = await query.OrderByDescending(a => a.Date)
            .ThenBy(a => a.Student.FullName).ToListAsync(cancellationToken);
        var rows = new List<string[]>
        {
            new[] { "Date", "Student", "Department", "Class", "Section",
                "Subject", "Period", "Status", "Professor" }
        };
        rows.AddRange(data.Select(a => new[]
        {
            a.Date.ToString("yyyy-MM-dd"),
            a.Student.FullName,
            a.Student.Department?.Name ?? "",
            a.Student.Class?.Name ?? "",
            a.Student.Section?.Name ?? "",
            a.Subject?.Name ?? "",
            a.Lecture ?? "",
            a.Status,
            a.Teacher?.FullName ?? ""
        }));
        return File(CsvExport.Create(rows), "text/csv",
            $"attendance-filtered-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

}
