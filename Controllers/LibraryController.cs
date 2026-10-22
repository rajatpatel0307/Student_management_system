using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;

namespace Student_Management_System.Controllers;

[Authorize]
public sealed class LibraryController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AuditService _audit;
    public LibraryController(ApplicationDbContext db, UserManager<ApplicationUser> users, AuditService audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? search, bool availableOnly = false)
    {
        var query = _db.LibraryBooks.AsNoTracking()
            .Include(b => b.Copies)
            .Include(b => b.LibraryCategory)
            .Include(b => b.LibraryPublisher)
            .Where(b => !b.IsArchived).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(b => b.Title.Contains(term) || b.Author.Contains(term) ||
                                     (b.Isbn != null && b.Isbn.Contains(term)));
        }
        if (availableOnly) query = query.Where(b => b.Copies.Any(c => c.IsAvailable));
        ViewBag.Search = search;
        ViewBag.AvailableOnly = availableOnly;
        ViewBag.Available = await _db.LibraryBookCopies.CountAsync(c => c.IsAvailable);
        ViewBag.Issued = await _db.LibraryBookCopies.CountAsync(c => !c.IsAvailable);
        ViewBag.Categories = await _db.LibraryCategories.AsNoTracking()
            .OrderBy(c => c.Name).ToListAsync();
        return View(await query.OrderBy(b => b.Title).Take(250).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var book = await _db.LibraryBooks.AsNoTracking().Include(b => b.Copies)
            .ThenInclude(c => c.Loans.Where(l => l.ReturnedUtc == null))
            .SingleOrDefaultAsync(b => b.Id == id);
        return book == null ? NotFound() : View(book);
    }

    [Authorize(Roles = "SuperAdmin,Admin,Librarian")]
    [PermissionAuthorize("library.manage")]
    public IActionResult Create() => View(new LibraryBook());

    [PermissionAuthorize("library.manage")]
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,Librarian")]
    public async Task<IActionResult> Create([Bind("Title,Author,Isbn,Category,Publisher,LibraryCategoryId,LibraryPublisherId")] LibraryBook input, int copyCount = 1)
    {
        ModelState.Remove(nameof(LibraryBook.Copies));
        if (copyCount is < 1 or > 100) ModelState.AddModelError(nameof(copyCount), "Copy count must be between 1 and 100.");
        if (!string.IsNullOrWhiteSpace(input.Isbn) &&
            await _db.LibraryBooks.AnyAsync(b => b.Isbn == input.Isbn.Trim()))
            ModelState.AddModelError(nameof(input.Isbn), "This ISBN already exists.");
        if (!ModelState.IsValid) return View(input);

        input.Title = input.Title.Trim();
        input.Author = input.Author.Trim();
        input.Isbn = string.IsNullOrWhiteSpace(input.Isbn) ? null : input.Isbn.Trim();
        _db.LibraryBooks.Add(input);
        for (var i = 1; i <= copyCount; i++)
            input.Copies.Add(new LibraryBookCopy { Barcode = $"LIB-{Guid.NewGuid():N}"[..16].ToUpperInvariant() });
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Created library title", nameof(LibraryBook), input.Id);
        TempData["Success"] = "Library title and copies created.";
        return RedirectToAction(nameof(Details), new { id = input.Id });
    }

    [PermissionAuthorize("library.issue")]
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,Librarian")]
    public async Task<IActionResult> Issue(int copyId, string borrowerEmail, DateTime dueDate)
    {
        if (dueDate.Date <= DateTime.Today) return BadRequest("Due date must be in the future.");
        var borrower = await _users.FindByEmailAsync(borrowerEmail.Trim());
        if (borrower == null) return BadRequest("Borrower account was not found.");

        var strategy = _db.Database.CreateExecutionStrategy();
        long loanId;
        int bookId;
        try
        {
            (loanId, bookId) = await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);
                var copy = await _db.LibraryBookCopies
                    .SingleOrDefaultAsync(c => c.Id == copyId);
                if (copy == null) throw new KeyNotFoundException();
                if (!copy.IsAvailable ||
                    await _db.LibraryLoans.AnyAsync(l =>
                        l.LibraryBookCopyId == copyId && l.ReturnedUtc == null))
                    throw new InvalidOperationException("This copy is already issued.");
                copy.IsAvailable = false;
                var loan = new LibraryLoan
                {
                    LibraryBookCopyId = copyId,
                    BorrowerUserId = borrower.Id,
                    DueUtc = DateTime.SpecifyKind(
                        dueDate.Date.AddHours(23).AddMinutes(59), DateTimeKind.Utc)
                };
                _db.LibraryLoans.Add(loan);
                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    Action = "Issued library copy",
                    EntityName = nameof(LibraryLoan),
                    EntityId = copy.Barcode
                });
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return (loan.Id, copy.LibraryBookId);
            });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(exception.Message); }
        catch (DbUpdateException)
        {
            return Conflict("This copy was issued by another request. Refresh and try another copy.");
        }
        TempData["Success"] = "Book issued successfully.";
        return RedirectToAction(nameof(Details), new { id = bookId });
    }

    [PermissionAuthorize("library.return")]
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,Librarian")]
    public async Task<IActionResult> Return(long loanId)
    {
        var loan = await _db.LibraryLoans.Include(l => l.LibraryBookCopy)
            .SingleOrDefaultAsync(l => l.Id == loanId);
        if (loan == null) return NotFound();
        if (loan.ReturnedUtc.HasValue) return Conflict("This loan has already been returned.");
        var now = DateTime.UtcNow;
        loan.ReturnedUtc = now;
        loan.FineAmount = now <= loan.DueUtc ? 0 : Math.Ceiling((decimal)(now.Date - loan.DueUtc.Date).TotalDays) * 5m;
        loan.LibraryBookCopy.IsAvailable = true;
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Returned library copy", nameof(LibraryLoan), loan.Id);
        TempData["Success"] = loan.FineAmount == 0
            ? "Book returned successfully."
            : $"Book returned. Fine due: {loan.FineAmount:C}.";
        return RedirectToAction(nameof(Details), new { id = loan.LibraryBookCopy.LibraryBookId });
    }

    public async Task<IActionResult> MyLoans()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return View(await _db.LibraryLoans.AsNoTracking()
            .Include(l => l.LibraryBookCopy).ThenInclude(c => c.LibraryBook)
            .Where(l => l.BorrowerUserId == userId)
            .OrderByDescending(l => l.IssuedUtc).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reserve(int bookId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        if (!await _db.LibraryBooks.AnyAsync(b => b.Id == bookId)) return NotFound();
        if (await _db.LibraryReservations.AnyAsync(r =>
                r.LibraryBookId == bookId && r.UserId == userId &&
                r.FulfilledUtc == null && r.CancelledUtc == null))
            return Conflict("You already have an active reservation for this title.");
        _db.LibraryReservations.Add(new LibraryReservation
        {
            LibraryBookId = bookId,
            UserId = userId,
            ExpiresUtc = DateTime.UtcNow.AddDays(7)
        });
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Reserved library title", nameof(LibraryReservation), bookId);
        TempData["Success"] = "Reservation placed.";
        return RedirectToAction(nameof(Details), new { id = bookId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,Librarian")]
    public async Task<IActionResult> Renew(long loanId)
    {
        var loan = await _db.LibraryLoans.Include(l => l.LibraryBookCopy)
            .SingleOrDefaultAsync(l => l.Id == loanId);
        if (loan == null) return NotFound();
        if (loan.ReturnedUtc.HasValue) return Conflict("Returned loans cannot be renewed.");
        if (loan.RenewalCount >= 2) return Conflict("The renewal limit has been reached.");
        if (await _db.LibraryReservations.AnyAsync(r =>
                r.LibraryBookId == loan.LibraryBookCopy.LibraryBookId &&
                r.UserId != loan.BorrowerUserId && r.FulfilledUtc == null &&
                r.CancelledUtc == null && r.ExpiresUtc > DateTime.UtcNow))
            return Conflict("Another borrower is waiting for this title.");
        loan.DueUtc = loan.DueUtc.AddDays(14);
        loan.RenewalCount++;
        loan.LastRenewedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Renewed library loan", nameof(LibraryLoan), loanId);
        TempData["Success"] = "Loan renewed.";
        return RedirectToAction(nameof(Details), new { id = loan.LibraryBookCopy.LibraryBookId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RenewMyLoan(long loanId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var loan = await _db.LibraryLoans.Include(l => l.LibraryBookCopy)
            .SingleOrDefaultAsync(l => l.Id == loanId && l.BorrowerUserId == userId);
        if (loan == null) return NotFound();
        if (loan.ReturnedUtc.HasValue || loan.DueUtc < DateTime.UtcNow)
            return Conflict("Returned or overdue loans cannot be renewed online.");
        if (loan.RenewalCount >= 2) return Conflict("The renewal limit has been reached.");
        if (await _db.LibraryReservations.AnyAsync(r =>
                r.LibraryBookId == loan.LibraryBookCopy.LibraryBookId &&
                r.UserId != userId && r.FulfilledUtc == null &&
                r.CancelledUtc == null && r.ExpiresUtc > DateTime.UtcNow))
            return Conflict("Another borrower is waiting for this title.");
        loan.DueUtc = loan.DueUtc.AddDays(14);
        loan.RenewalCount++;
        loan.LastRenewedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Renewed own library loan", nameof(LibraryLoan), loanId);
        TempData["Success"] = "Loan renewed for 14 days.";
        return RedirectToAction(nameof(MyLoans));
    }

    [PermissionAuthorize("library.manage")]
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,Librarian")]
    public async Task<IActionResult> SetCopyCondition(int copyId, LibraryCopyCondition condition)
    {
        var copy = await _db.LibraryBookCopies.SingleOrDefaultAsync(c => c.Id == copyId);
        if (copy == null) return NotFound();
        copy.Condition = condition;
        copy.IsAvailable = condition == LibraryCopyCondition.Good &&
                           !await _db.LibraryLoans.AnyAsync(l =>
                               l.LibraryBookCopyId == copyId && l.ReturnedUtc == null);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync($"Set library copy condition to {condition}",
            nameof(LibraryBookCopy), copyId);
        return RedirectToAction(nameof(Details), new { id = copy.LibraryBookId });
    }

    [PermissionAuthorize("library.return")]
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,Librarian")]
    public async Task<IActionResult> SettleFine(long loanId, decimal amount, string method, string? reference)
    {
        var loan = await _db.LibraryLoans.SingleOrDefaultAsync(l => l.Id == loanId);
        if (loan == null) return NotFound();
        var settled = await _db.LibraryFineSettlements.Where(s => s.LibraryLoanId == loanId)
            .SumAsync(s => (decimal?)s.Amount) ?? 0;
        if (amount <= 0 || settled + amount > loan.FineAmount)
            return UnprocessableEntity("Settlement amount exceeds the outstanding fine.");
        _db.LibraryFineSettlements.Add(new LibraryFineSettlement
        {
            LibraryLoanId = loanId,
            Amount = amount,
            Method = method.Trim(),
            Reference = reference?.Trim(),
            CollectedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""
        });
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Settled library fine", nameof(LibraryLoan), loanId);
        TempData["Success"] = "Fine settlement recorded.";
        return RedirectToAction(nameof(Index));
    }

    [PermissionAuthorize("library.manage")]
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,Librarian")]
    public async Task<IActionResult> CreateCategory(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Category name is required.");
        var normalized = name.Trim();
        if (await _db.LibraryCategories.AnyAsync(c => c.Name == normalized))
            return Conflict("Category already exists.");
        var category = new LibraryCategory { Name = normalized };
        _db.LibraryCategories.Add(category);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Created library category",
            nameof(LibraryCategory), category.Id);
        return RedirectToAction(nameof(Index));
    }

    [PermissionAuthorize("library.manage")]
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,Librarian")]
    public async Task<IActionResult> ArchiveBook(int id)
    {
        var book = await _db.LibraryBooks.Include(b => b.Copies)
            .SingleOrDefaultAsync(b => b.Id == id);
        if (book == null) return NotFound();
        if (await _db.LibraryLoans.AnyAsync(l =>
                l.LibraryBookCopy.LibraryBookId == id && l.ReturnedUtc == null))
            return Conflict("A title with active loans cannot be archived.");
        book.IsArchived = true;
        foreach (var copy in book.Copies)
        {
            copy.IsAvailable = false;
            copy.Condition = LibraryCopyCondition.Withdrawn;
        }
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Archived library title",
            nameof(LibraryBook), id);
        TempData["Success"] = "Library title archived.";
        return RedirectToAction(nameof(Index));
    }

    [PermissionAuthorize("library.audit")]
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,Librarian")]
    public async Task<IActionResult> StartStockVerification(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Verification name is required.");
        if (await _db.LibraryStockVerifications.AnyAsync(v =>
                v.CompletedUtc == null))
            return Conflict("Complete the active stock verification first.");
        var verification = new LibraryStockVerification
        {
            StartedUtc = DateTime.UtcNow,
            StartedByUserId =
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
            ExpectedCopies = await _db.LibraryBookCopies.CountAsync(),
            Notes = name.Trim()
        };
        _db.LibraryStockVerifications.Add(verification);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Started library stock verification",
            nameof(LibraryStockVerification), verification.Id);
        TempData["Success"] = "Stock verification started.";
        return RedirectToAction(nameof(Index));
    }
}
