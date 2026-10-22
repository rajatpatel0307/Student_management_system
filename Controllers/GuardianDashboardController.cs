using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;

namespace Student_Management_System.Controllers;

[Authorize(Roles = "Guardian")]
public sealed class GuardianDashboardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IResultService _results;

    public GuardianDashboardController(ApplicationDbContext db, IResultService results)
    {
        _db = db;
        _results = results;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var guardian = await _db.GuardianProfiles.AsNoTracking()
            .Include(g => g.StudentLinks.Where(l => l.UnlinkedUtc == null))
                .ThenInclude(l => l.Student).ThenInclude(s => s.Class)
            .Include(g => g.StudentLinks.Where(l => l.UnlinkedUtc == null))
                .ThenInclude(l => l.Student).ThenInclude(s => s.Section)
            .SingleOrDefaultAsync(g => g.UserId == userId, cancellationToken);
        return guardian == null ? NotFound() : View(guardian);
    }

    public async Task<IActionResult> Student(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var link = await _db.GuardianStudentLinks.AsNoTracking()
            .Include(l => l.Student).ThenInclude(s => s.Class)
            .Include(l => l.Student).ThenInclude(s => s.Section)
            .Include(l => l.Student).ThenInclude(s => s.Department)
            .SingleOrDefaultAsync(l => l.StudentId == id &&
                l.UnlinkedUtc == null &&
                l.GuardianProfile.UserId == userId, cancellationToken);
        if (link == null) return Forbid();

        List<Attendance> attendance = link.CanViewAcademics
            ? await _db.Attendances.AsNoTracking().Include(a => a.Subject)
                .Where(a => a.StudentId == id)
                .OrderByDescending(a => a.Date).Take(100)
                .ToListAsync(cancellationToken)
            : [];
        var marksQuery = _db.Marks.AsNoTracking()
            .Include(m => m.Subject).Include(m => m.Exam)
            .Where(m => m.StudentId == id);
        marksQuery = link.CanViewAcademics
            ? await _results.ScopeAsync(marksQuery, User)
            : marksQuery.Where(_ => false);
        var marks = await marksQuery.OrderByDescending(m => m.Exam.ExamDate)
            .Take(100).ToListAsync(cancellationToken);
        List<Assignment> assignments = link.CanViewAcademics
            ? await _db.Assignments.AsNoTracking().Include(a => a.Subject)
                .VisibleAt(DateTime.UtcNow)
                .Where(a => a.Subject!.ClassId == link.Student.ClassId &&
                            a.DueUtc >= DateTime.UtcNow)
                .OrderByDescending(a => a.DueUtc).Take(20)
                .ToListAsync(cancellationToken)
            : [];
        List<FinancialTransaction> transactions = link.CanViewFinance
            ? await _db.FinancialTransactions.AsNoTracking().Include(t => t.Fee)
                .Where(t => t.StudentId == id).OrderByDescending(t => t.CreatedUtc)
                .Take(100).ToListAsync(cancellationToken)
            : [];
        List<StudentInvoice> invoices = link.CanViewFinance
            ? await _db.StudentInvoices.AsNoTracking()
                .Where(i => i.StudentId == id && i.Status != InvoiceStatus.Cancelled)
                .OrderByDescending(i => i.IssuedUtc).Take(50)
                .ToListAsync(cancellationToken)
            : [];
        var loans = await _db.LibraryLoans.AsNoTracking()
            .Include(l => l.LibraryBookCopy).ThenInclude(c => c.LibraryBook)
            .Where(l => l.BorrowerUserId == link.Student.UserId &&
                        l.ReturnedUtc == null)
            .OrderBy(l => l.DueUtc).Take(25)
            .ToListAsync(cancellationToken);
        var notices = await _db.Notices.AsNoTracking()
            .Where(n => n.TargetRole == "All" || n.TargetRole == "Student" ||
                        (n.TargetRole == "Department" &&
                         n.DepartmentId == link.Student.DepartmentId))
            .OrderByDescending(n => n.CreatedDate).Take(10)
            .ToListAsync(cancellationToken);

        return View(new GuardianStudentViewModel
        {
            Student = link.Student,
            Attendance = attendance,
            Marks = marks,
            Assignments = assignments,
            Transactions = transactions,
            Invoices = invoices,
            LibraryLoans = loans,
            Notices = notices,
            OutstandingBalance = invoices.Sum(i =>
                Math.Max(0, i.TotalAmount - i.AidAmount - i.PaidAmount)),
            OverdueLibraryLoans = loans.Count(l => l.DueUtc < DateTime.UtcNow),
            AttendancePercentage = attendance.Count == 0
                ? 0
                : Math.Round(attendance.Count(a => a.Status == "Present") *
                             100m / attendance.Count, 1)
        });
    }
}
