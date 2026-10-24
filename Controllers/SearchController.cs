using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using System.Security.Claims;
using Student_Management_System.Services;

namespace Student_Management_System.Controllers;

[Authorize]
public sealed class SearchController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PermissionService _permissions;
    private readonly ICampusScopeService _campusScope;
    public SearchController(ApplicationDbContext db, PermissionService permissions, ICampusScopeService campusScope)
    {
        _db = db;
        _permissions = permissions;
        _campusScope = campusScope;
    }

    public async Task<IActionResult> Index(string? q, int page = 1, CancellationToken cancellationToken = default)
    {
        var results = new List<SearchResultViewModel>();
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        {
            ViewBag.Query = q;
            return View(results);
        }
        var term = q.Trim();
        page = Math.Max(1, page);
        const int pageSize = 20;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ViewBag.Query = term;

        var canSearchStudents = !User.IsInRole("Admin") || User.IsInRole("SuperAdmin") ||
            await _permissions.HasAsync(User, "student.view", cancellationToken);
        if (canSearchStudents && (User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("HOD") ||
            User.IsInRole("Professor") || User.IsInRole("Guardian") || User.IsInRole("Student")))
        {
            var students = _db.Students.AsNoTracking().Include(s => s.Class)
                .Where(s => !s.IsArchived &&
                    (s.FullName.Contains(term) || s.Email.Contains(term)));
            if (User.IsInRole("Student")) students = students.Where(s => s.UserId == userId);
            if (User.IsInRole("Guardian"))
                students = students.Where(s => _db.GuardianStudentLinks.Any(l =>
                    l.StudentId == s.Id && l.UnlinkedUtc == null && l.GuardianProfile.UserId == userId));
            if (User.IsInRole("Professor"))
                students = students.Where(s => s.Class!.TeacherAssignments.Any(a => a.Teacher.UserId == userId));
            if (User.IsInRole("HOD"))
            {
                var departmentId = await _db.Teachers.AsNoTracking()
                    .Where(t => t.UserId == userId && t.TeacherType == TeacherType.HOD && !t.IsArchived && t.User.IsActive)
                    .Select(t => t.DepartmentId).SingleOrDefaultAsync(cancellationToken);
                students = departmentId.HasValue
                    ? StudentRecordScope.ForHod(_db, students, userId, departmentId.Value)
                    : students.Where(_ => false);
            }
            foreach (var student in await students.OrderBy(s => s.FullName)
                         .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken))
            {
                var detailsController = User.IsInRole("Guardian") ? "GuardianDashboard" : "Students";
                var detailsAction = User.IsInRole("Guardian") ? "Student" : "Details";
                results.Add(new("Students", student.FullName, student.Class?.Name ?? student.Email,
                    detailsController, detailsAction, student.Id));
            }
        }

        if (await _permissions.HasAsync(User, "admission.view", cancellationToken))
        {
            var allowedCampuses = await _campusScope.AllowedCampusIdsAsync(User, cancellationToken);
            foreach (var applicant in await _db.AdmissionApplications.AsNoTracking()
                         .Where(a => !a.IsArchived && allowedCampuses.Contains(a.CampusId ?? 0) &&
                            (a.ApplicantName.Contains(term) ||
                             a.ReferenceNumber.Contains(term) ||
                             a.Email.Contains(term)))
                         .OrderBy(a => a.ApplicantName)
                         .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken))
                results.Add(new("Admissions", applicant.ApplicantName, $"Application · {applicant.Stage}",
                    "Admissions", "Details", applicant.Id));
        }

        foreach (var book in await _db.LibraryBooks.AsNoTracking()
                     .Where(b => b.Title.Contains(term) || b.Author.Contains(term) ||
                                 (b.Isbn != null && b.Isbn.Contains(term)))
                     .OrderBy(b => b.Title).Skip((page - 1) * pageSize).Take(pageSize)
                     .ToListAsync(cancellationToken))
            results.Add(new("Library", book.Title, book.Author, "Library", "Details", book.Id));

        if (User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("HOD") ||
            User.IsInRole("Professor") || User.IsInRole("Student"))
        {
            var subjects = _db.Subjects.AsNoTracking()
                .Where(s => !s.IsArchived && s.Name.Contains(term));
            if (User.IsInRole("HOD"))
                subjects = subjects.Where(s => s.Department!.Teachers.Any(t =>
                    t.UserId == userId && t.TeacherType == TeacherType.HOD));
            else if (User.IsInRole("Professor"))
                subjects = subjects.Where(s => s.TeacherAssignments.Any(a => a.Teacher.UserId == userId));
            else if (User.IsInRole("Student"))
                subjects = subjects.Where(s => s.StudentEnrollments.Any(e => e.Student.UserId == userId));
            foreach (var subject in await subjects.OrderBy(s => s.Name)
                         .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken))
                results.Add(new("Subjects", subject.Name, "Academic subject", "Subject", "Details", subject.Id));
        }

        if (await _permissions.HasAsync(User, "fees.view", cancellationToken))
        {
            var finance = _db.FinancialTransactions.AsNoTracking()
                .Where(t => t.ReceiptNumber.Contains(term) ||
                            (t.TransactionReference != null && t.TransactionReference.Contains(term)));
            if (User.IsInRole("Student"))
                finance = finance.Where(t => t.Student.UserId == userId);
            else if (User.IsInRole("Guardian"))
                finance = finance.Where(t => t.Student != null &&
                    _db.GuardianStudentLinks.Any(l => l.StudentId == t.StudentId &&
                        l.UnlinkedUtc == null && l.CanViewFinance && l.GuardianProfile.UserId == userId));
            else if (!User.IsInRole("SuperAdmin") && !User.IsInRole("Admin") &&
                     !User.IsInRole("Accountant"))
                finance = finance.Where(_ => false);
            foreach (var item in await finance.OrderByDescending(t => t.CreatedUtc)
                         .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken))
                results.Add(new("Finance", item.ReceiptNumber, $"{item.Type} · {item.Amount:C}",
                    "Finance", "Details", item.Id));
        }

        ViewBag.Page = page;
        ViewBag.HasMore = results.Count >= pageSize;
        return View(results);
    }
}
