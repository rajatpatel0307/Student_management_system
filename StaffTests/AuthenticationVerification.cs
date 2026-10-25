using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Student_Management_System.Controllers;
using Student_Management_System.Models;

internal static class AuthenticationVerification
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        bool Valid(LoginViewModel input) => Validator.TryValidateObject(input, new ValidationContext(input), [], true);
        check(!Valid(new()), "Blank login credentials must be rejected.");
        check(Valid(new() { Email = "campus-user", Password = "existing-password" }), "Login must continue supporting usernames.");
        check(!Valid(new() { Email = new string('a', 257), Password = "password" }), "Oversized login identifiers must be rejected before database access.");
        check(!Valid(new() { Email = "user", Password = new string('a', 1025) }), "Oversized passwords must be rejected before hashing.");

        var markInput = new MarkEntryInputModel { StudentId = 1, SubjectId = 2, ExamId = 3 };
        check(!Validator.TryValidateObject(markInput, new ValidationContext(markInput), [], true), "A missing mark must not become a zero score.");
        markInput.Score = 0;
        check(Validator.TryValidateObject(markInput, new ValidationContext(markInput), [], true), "An explicitly entered zero mark is valid.");
        check(typeof(MarkEntryInputModel).GetProperty("IsApproved") == null && typeof(MarkEntryInputModel).GetProperty("GraceMarks") == null,
            "The legacy marks entry contract must exclude approval and grace fields.");
        var roster = new MarksEntryViewModel { SubjectId = 1, ExamId = 2, Entries = [new() { StudentId = 1 }, new() { StudentId = 1 }] };
        check(!Validator.TryValidateObject(roster, new ValidationContext(roster), [], true), "Duplicate student IDs must not silently overwrite one another in a bulk marks request.");

        var controller = new AccountController(null!, null!, null!, null!, null!, null!);
        var get = (ViewResult)controller.Login("/Students?page=2");
        check(((LoginViewModel)get.Model!).ReturnUrl == "/Students?page=2", "Login GET must preserve protected-navigation context.");
        var invalid = new LoginViewModel { Email = "entered-username", ReturnUrl = "/Students?page=2" };
        controller.ModelState.AddModelError(nameof(LoginViewModel.Password), "Required");
        var post = (ViewResult)await controller.Login(invalid);
        check(ReferenceEquals(post.Model, invalid), "Validation errors must preserve the login identifier and return URL without accessing Identity.");

        using var users = new UserManager<ApplicationUser>(new UnusedUserStore(),
            Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(), [], [],
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, NullLogger<UserManager<ApplicationUser>>.Instance);
        var signIn = new ChallengeSignInManager(users);
        controller = new AccountController(users, signIn, null!, null!, null!, null!);
        var failed = await controller.VerifyTwoFactor("123 456", "/Students");
        check(failed is ViewResult && !controller.ModelState.IsValid, "An invalid MFA challenge must remain on the form with validation feedback.");
        check(signIn.CapturedCode == "123456" && signIn.RememberClient == false && signIn.Persistent == false,
            "An MFA challenge must normalize input without silently remembering the browser or creating a persistent session.");
        check((string?)controller.ViewBag.ReturnUrl == "/Students", "MFA errors must preserve the original destination.");
        signIn.PendingUser = new ApplicationUser { IsActive = false };
        signIn.CapturedCode = null;
        var inactive = await controller.VerifyTwoFactor("123456", "/Students");
        check(inactive is RedirectToActionResult { ActionName: "Login" } && signIn.CapturedCode == null,
            "An account disabled during a challenge must never reach MFA sign-in.");
    }

    private sealed class ChallengeSignInManager(UserManager<ApplicationUser> users) : SignInManager<ApplicationUser>(
        users, new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, new UnusedClaimsFactory(),
        Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), NullLogger<SignInManager<ApplicationUser>>.Instance,
        new AuthenticationSchemeProvider(Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions())), new DefaultUserConfirmation<ApplicationUser>())
    {
        public ApplicationUser PendingUser { get; set; } = new() { IsActive = true };
        public string? CapturedCode { get; set; }
        public bool? RememberClient { get; private set; }
        public bool? Persistent { get; private set; }
        public override Task<ApplicationUser?> GetTwoFactorAuthenticationUserAsync() => Task.FromResult<ApplicationUser?>(PendingUser);
        public override Task<Microsoft.AspNetCore.Identity.SignInResult> TwoFactorSignInAsync(string provider, string code, bool isPersistent, bool rememberClient)
        {
            CapturedCode = code;
            Persistent = isPersistent;
            RememberClient = rememberClient;
            return Task.FromResult(Microsoft.AspNetCore.Identity.SignInResult.Failed);
        }
    }

    private sealed class UnusedClaimsFactory : IUserClaimsPrincipalFactory<ApplicationUser>
    {
        public Task<ClaimsPrincipal> CreateAsync(ApplicationUser user) => throw new NotSupportedException();
    }

    private sealed class UnusedUserStore : IUserStore<ApplicationUser>
    {
        public void Dispose() { }
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task SetUserNameAsync(ApplicationUser user, string? value, CancellationToken ct) => throw new NotSupportedException();
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? value, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationUser?> FindByIdAsync(string value, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationUser?> FindByNameAsync(string value, CancellationToken ct) => throw new NotSupportedException();
    }
}
