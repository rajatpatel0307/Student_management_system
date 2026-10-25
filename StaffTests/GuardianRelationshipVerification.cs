using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Controllers;
using Student_Management_System.Models;
using Student_Management_System.Services;

internal static class GuardianRelationshipVerification
{
    public static async Task RunAsync(ApplicationDbContext db, int studentId, string actorId, Action<bool, string> check, UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles)
    {
        var profile = new GuardianProfile { UserId = actorId, FullName = "Guardian relationship fixture", Email = "guardian-link@test.invalid", Relationship = "Parent" };
        db.GuardianProfiles.Add(profile); await db.SaveChangesAsync();
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId), new Claim(ClaimTypes.Role, "Admin")], "test")) };
        var controller = new GuardiansController(db, null!, null!, new AuditService(db, new HttpContextAccessor { HttpContext = context }))
        { ControllerContext = new ControllerContext { HttpContext = context }, TempData = new TempDataDictionary(context, new TempData()) };
        check(await controller.Link(profile.Id, studentId, "Parent", true, false, true, true, "Email") is RedirectToActionResult, "Guardian linking must succeed for an active student.");
        var link = await db.GuardianStudentLinks.SingleAsync(l => l.GuardianProfileId == profile.Id && l.StudentId == studentId);
        var id = link.Id;
        check(!link.CanViewFinance && link.CanViewAcademics && link.Relationship == "Parent", "Guardian relationship flags must persist exactly as requested.");
        check(await controller.Link(profile.Id, studentId, "Parent", false, true, true, true, null) is ConflictObjectResult, "Duplicate active guardian links must be rejected.");
        await controller.Unlink(link.Id, "Access withdrawn");
        check(link.UnlinkedUtc.HasValue && !link.IsPrimary, "Unlinking must revoke access while retaining the relationship row.");
        check(await controller.Link(profile.Id, studentId, "Emergency contact", false, false, false, true, null) is RedirectToActionResult,
            "A previously revoked guardian relationship must be relinkable without violating the historical unique index.");
        check(link.Id == id && link.UnlinkedUtc == null && !link.CanViewAcademics && !link.CanViewFinance,
            "Relinking must preserve the original row ID and use newly authorized access flags.");
        check(await db.GuardianLinkHistories.CountAsync(h => h.GuardianStudentLinkId == id) == 3,
            "Link, unlink and relink must each retain a history event.");
        check(await controller.Link(profile.Id, studentId, new string('r', 41), false, false, true, true, null) is BadRequestObjectResult,
            "Overlong relationship data must fail before database writes.");
        var finance = typeof(GuardiansController).GetMethod(nameof(GuardiansController.Create))!.GetParameters().Single(p => p.Name == "canViewFinance");
        check(Equals(finance.DefaultValue, false), "An unchecked finance-access checkbox must never default to a grant.");
        if (!await roles.RoleExistsAsync("Guardian")) await roles.CreateAsync(new IdentityRole("Guardian"));
        var create = new GuardiansController(db, users, new FailingInvitations(), new AuditService(db, new HttpContextAccessor { HttpContext = context }))
        { ControllerContext = new ControllerContext { HttpContext = context }, TempData = new TempDataDictionary(context, new TempData()) };
        check(await create.Create("Created guardian", "guardian-create@test.invalid", "Parent", "8999999992", studentId) is RedirectToActionResult,
            "Guardian creation must survive invitation setup failure after committing the account.");
        var created = await db.GuardianProfiles.AsNoTracking().Include(g => g.User).Include(g => g.StudentLinks).SingleAsync(g => g.Email == "guardian-create@test.invalid");
        check(created.User.PublicUserId?.StartsWith("USR-") == true && created.User.PhoneNumber == "8999999992" && created.StudentLinks.Single().Relationship == "Parent" && !created.StudentLinks.Single().CanViewFinance,
            "New guardians must receive generated public identifiers, synchronized phones, the requested relationship and no implicit finance grant.");
        check(create.TempData.ContainsKey("Warning"), "An invitation failure must be visible without falsely reporting failed account creation.");
    }
    private sealed class TempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
    private sealed class FailingInvitations : IStudentAccountService
    {
        public Task<ActivationInvitation> CreateActivationAsync(ApplicationUser user, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Test invitation setup failure");
        public Task<bool> SendActivationAsync(ApplicationUser user, string url, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IdentityResult> ActivateAsync(string email, string token, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
