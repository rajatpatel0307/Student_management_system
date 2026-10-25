using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Student_Management_System.Controllers;
using Student_Management_System.Models;
using Student_Management_System.Services;

internal static class StudentArchiveVerification
{
    public static async Task RunAsync(ApplicationDbContext db, UserManager<ApplicationUser> users, IUserStore<ApplicationUser> store,
        int classId, int sectionId, Action<bool, string> check)
    {
        var user = new ApplicationUser { FullName = "Archive fixture", UserName = "archive-fixture", Email = "archive@test.invalid", PublicUserId = "STU-ARCHIVE", PhoneNumber = "8999999997" };
        check((await users.CreateAsync(user, "Archive!" + Guid.NewGuid().ToString("N") + "1a")).Succeeded, "Archive fixture account must be created.");
        var student = new Student { UserId = user.Id, FullName = user.FullName, Email = user.Email!, ClassId = classId, SectionId = sectionId, DOB = DateTime.Today.AddYears(-20) };
        db.Students.Add(student); await db.SaveChangesAsync();
        var originalStamp = user.SecurityStamp;
        var context = new DefaultHttpContext();
        var audit = new AuditService(db, new HttpContextAccessor { HttpContext = context });
        StudentsController Controller(UserManager<ApplicationUser> manager) => new(db, manager, null!, audit,
            NullLogger<StudentsController>.Instance, null!, null!, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            TempData = new TempDataDictionary(context, new TestTempData())
        };
        // The store belongs to the shared DI scope; this adapter must not dispose it.
        var failing = new FailingStampUserManager(store);
        var rejected = Controller(failing);
        check(await rejected.DeleteConfirmed(student.Id, "Archive regression", CancellationToken.None) is ViewResult && !rejected.ModelState.IsValid,
            "An Identity failure must return visible archive validation feedback.");
        var unchanged = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        check(unchanged.IsActive && unchanged.SecurityStamp == originalStamp && !await db.Students.AsNoTracking().Where(s => s.Id == student.Id).Select(s => s.IsArchived).SingleAsync(),
            "Identity failure must roll back both student archive and account deactivation in SQL.");
        var success = Controller(users);
        check(await success.DeleteConfirmed(student.Id, "Archive regression", CancellationToken.None) is RedirectToActionResult, "A valid archive must complete.");
        unchanged = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        check(!unchanged.IsActive && unchanged.SecurityStamp != originalStamp && await db.Students.AsNoTracking().Where(s => s.Id == student.Id).Select(s => s.IsArchived).SingleAsync(),
            "Archive, account deactivation and session invalidation must commit together.");
        var audits = await db.AuditLogs.CountAsync(a => a.EntityName == nameof(Student) && a.EntityId == student.Id.ToString());
        await Controller(users).DeleteConfirmed(student.Id, "Repeated request", CancellationToken.None);
        check(audits == 1 && await db.AuditLogs.CountAsync(a => a.EntityName == nameof(Student) && a.EntityId == student.Id.ToString()) == 1,
            "Archive retries must not duplicate audit events or mutate history.");
    }
    private sealed class FailingStampUserManager(IUserStore<ApplicationUser> store) : UserManager<ApplicationUser>(store,
        Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(), [], [], new UpperInvariantLookupNormalizer(),
        new IdentityErrorDescriber(), null!, NullLogger<UserManager<ApplicationUser>>.Instance)
    {
        public override Task<IdentityResult> UpdateSecurityStampAsync(ApplicationUser user) => Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "TestFailure" }));
    }
    private sealed class TestTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
