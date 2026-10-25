using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Student_Management_System.Models;
using System.Text.Encodings.Web;
using Student_Management_System.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace Student_Management_System.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _db;
        private readonly IStudentAccountService _studentAccounts;
        private readonly AuditService _audit;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ApplicationDbContext db,
            IStudentAccountService studentAccounts,
            AuditService audit)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _db = db;
            _studentAccounts = studentAccounts;
            _audit = audit;
        }

        // ================= LOGIN =================
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> Login(LoginViewModel input)
        {
            if (!ModelState.IsValid) return View(input);

            var email = input.Email.Trim();
            var password = input.Password;
            var returnUrl = input.ReturnUrl;

            var user = await _userManager.FindByEmailAsync(email)
                ?? await _userManager.FindByNameAsync(email);
            if (user == null)
            {
                await RecordLoginAsync(email, false, null);
                ViewBag.Error = "Invalid email or password";
                return View(input);
            }
            if (!user.IsActive)
            {
                await RecordLoginAsync(email, false, user.Id);
                ViewBag.Error = "Invalid email or password";
                return View(input);
            }
            var roles = await _userManager.GetRolesAsync(user);
            // Preserve Identity's MFA challenge for every role. MFA must never be
            // disabled as a side effect of a successful password check.
            var result = await _signInManager.PasswordSignInAsync(
                user, password, isPersistent: false, lockoutOnFailure: true);
            if (result.RequiresTwoFactor)
                return RedirectToAction(nameof(VerifyTwoFactor), new { returnUrl });
            if (!result.Succeeded)
            {
                await RecordLoginAsync(email, false, user.Id);
                ViewBag.Error = result.IsLockedOut
                    ? "Account temporarily locked. Try again later."
                    : "Invalid email or password";
                return View(input);
            }
            user.LastLoginUtc = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            await RecordLoginAsync(email, true, user.Id);
            if (user.MustChangePassword)
                return RedirectToAction(nameof(ChangePassword));

            // 🔥 SAFE RETURN URL
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            // 🔥 ROLE BASED REDIRECT
            if (roles.Contains("SuperAdmin"))
                return RedirectToAction("Index", "SuperAdmin");
            if (roles.Contains("Admin"))
                return RedirectToAction("Index", "Dashboard");

            var staffWorkspace = StaffWorkspaceCatalog.All.FirstOrDefault(w => roles.Contains(w.Role));
            if (staffWorkspace != null)
                return RedirectToAction("Workspace", "Staff", new { workspace = staffWorkspace.Key });

            if (roles.Contains("HOD"))
                return RedirectToAction("Index", "HodDashboard");
            if (roles.Contains("Professor"))
                return RedirectToAction("Index", "ProfessorDashboard");

            if (roles.Contains("Student"))
                return RedirectToAction("Index", "StudentDashboard");
            if (roles.Contains("Guardian"))
                return RedirectToAction("Index", "GuardianDashboard");
            if (roles.Contains("Accountant"))
                return RedirectToAction("Finance", "RoleDashboard");
            if (roles.Contains("Librarian"))
                return RedirectToAction("Library", "RoleDashboard");
            if (roles.Contains("AdmissionOfficer"))
                return RedirectToAction("Admissions", "RoleDashboard");
            if (roles.Contains("ExaminationOfficer"))
                return RedirectToAction("Examinations", "RoleDashboard");
            if (roles.Contains("TransportManager") || roles.Contains("HostelManager") ||
                roles.Contains("PlacementOfficer") || roles.Contains("Employee"))
                return RedirectToAction("Operations", "RoleDashboard");

            return RedirectToAction("Workspace", "Home");
        }

        // ================= LOGOUT =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        [Authorize]
        public IActionResult ChangePassword() => View();

        [Authorize]
        public async Task<IActionResult> Mfa()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            var key = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrWhiteSpace(key))
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
                key = await _userManager.GetAuthenticatorKeyAsync(user);
            }
            var email = user.Email ?? user.UserName ?? "EduFlow account";
            var uri = $"otpauth://totp/{Uri.EscapeDataString($"EduFlow:{email}")}" +
                      $"?secret={key}&issuer={Uri.EscapeDataString("EduFlow")}&digits=6";
            // This action also builds the response for invalid EnableMfa posts.
            // Name the view explicitly because the route action remains EnableMfa.
            return View(nameof(Mfa), new MfaSetupViewModel(user.TwoFactorEnabled, key ?? "", uri, []));
        }

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableMfa(string? code)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            code = code?.Replace(" ", "").Replace("-", "");
            if (string.IsNullOrWhiteSpace(code) ||
                !await _userManager.VerifyTwoFactorTokenAsync(
                    user, TokenOptions.DefaultAuthenticatorProvider, code))
            {
                ModelState.AddModelError("", "The authenticator code is invalid.");
                return await Mfa();
            }
            await _userManager.SetTwoFactorEnabledAsync(user, true);
            await _userManager.UpdateSecurityStampAsync(user);
            var recoveryCodes = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray() ?? [];
            await _audit.RecordAsync("Enabled authenticator MFA", nameof(ApplicationUser), user.Id);
            await _signInManager.RefreshSignInAsync(user);
            return View("RecoveryCodes", recoveryCodes);
        }

        [AllowAnonymous]
        public async Task<IActionResult> VerifyTwoFactor(string? returnUrl)
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null) return RedirectToAction(nameof(Login));
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> VerifyTwoFactor(string? code, string? returnUrl)
        {
            var pendingUser = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (pendingUser == null || !pendingUser.IsActive) return RedirectToAction(nameof(Login));
            var result = await _signInManager.TwoFactorSignInAsync(
                TokenOptions.DefaultAuthenticatorProvider,
                (code ?? "").Replace(" ", "").Replace("-", ""),
                isPersistent: false, rememberClient: false);
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Invalid or expired verification code.");
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }
            // The new application cookie is set on the response; User still
            // describes the challenge request. Use its verified pending account.
            pendingUser.LastLoginUtc = DateTime.UtcNow;
            await _userManager.UpdateAsync(pendingUser);
            await RecordLoginAsync(pendingUser.Email ?? "", true, pendingUser.Id);
            await _audit.RecordAsync(
                "Completed authenticator MFA challenge", nameof(ApplicationUser), pendingUser.Id);
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return RedirectToAction("Dashboard", "Home");
        }

        [AllowAnonymous]
        public async Task<IActionResult> RecoveryCode(string? returnUrl)
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null) return RedirectToAction(nameof(Login));
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> RecoveryCode(string? code, string? returnUrl)
        {
            var pendingUser = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (pendingUser == null || !pendingUser.IsActive) return RedirectToAction(nameof(Login));
            var result = await _signInManager.TwoFactorRecoveryCodeSignInAsync(
                (code ?? "").Replace(" ", ""));
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Invalid or already-used recovery code.");
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }
            pendingUser.LastLoginUtc = DateTime.UtcNow;
            await _userManager.UpdateAsync(pendingUser);
            await RecordLoginAsync(pendingUser.Email ?? "", true, pendingUser.Id);
            await _audit.RecordAsync("Completed recovery-code MFA challenge", nameof(ApplicationUser), pendingUser.Id);
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return RedirectToAction("Dashboard", "Home");
        }

        [HttpPost, Authorize]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (result.Succeeded)
            {
                user.MustChangePassword = false;
                await _userManager.UpdateAsync(user);
                await _signInManager.RefreshSignInAsync(user);
                TempData["Success"] = "Password changed.";
                return RedirectToAction("Dashboard", "Home");
            }
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return View();
        }

        [AllowAnonymous]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("account-recovery")]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(nameof(email), "Enter your account email.");
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);
            // Always show the same response to prevent account enumeration.
            if (user != null && await _userManager.IsEmailConfirmedAsync(user))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var callbackUrl = Url.Action(
                    nameof(ResetPassword),
                    "Account",
                    new { email, token },
                    Request.Scheme);

                if (!string.IsNullOrWhiteSpace(callbackUrl))
                {
                    try
                    {
                        await _emailSender.SendEmailAsync(
                            email,
                            "Reset your EduFlow password",
                            $"Use this secure link to reset your password: <a href=\"{HtmlEncoder.Default.Encode(callbackUrl)}\">Reset password</a>. If you did not request this, you can ignore this email.");
                    }
                    catch (InvalidOperationException)
                    {
                        // Keep the response identical for existing and unknown accounts.
                        // Delivery failures are handled operationally and must not enable
                        // account enumeration.
                    }
                }
            }

            ViewData["ConfirmationTitle"] = "Check your email";
            ViewData["ConfirmationMessage"] = "If an account matches that email, a password-reset link has been sent.";
            return View("Confirmation");
        }

        [AllowAnonymous]
        public IActionResult ResetPassword(string? email, string? token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
                return RedirectToAction(nameof(Login));
            ViewBag.Email = email;
            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("account-recovery")]
        public async Task<IActionResult> ResetPassword(string email, string token, string newPassword, string confirmPassword)
        {
            ViewBag.Email = email;
            ViewBag.Token = token;
            if (newPassword != confirmPassword)
                ModelState.AddModelError(nameof(confirmPassword), "Passwords do not match.");
            if (!ModelState.IsValid) return View();

            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                        ModelState.AddModelError("", error.Description);
                    return View();
                }
                user.MustChangePassword = false;
                await _userManager.UpdateSecurityStampAsync(user);
                await _audit.RecordAsync("Completed password reset", nameof(ApplicationUser), user.Id);
            }

            ViewData["ConfirmationTitle"] = "Password updated";
            ViewData["ConfirmationMessage"] = "You can now sign in with your new password.";
            return View("Confirmation");
        }

        [AllowAnonymous]
        public IActionResult Activate(string? email, string? token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
                return RedirectToAction(nameof(Login));
            ViewBag.Email = email;
            ViewBag.Token = token;
            return View();
        }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        [EnableRateLimiting("account-recovery")]
        public async Task<IActionResult> Activate(
            string email,
            string token,
            string newPassword,
            string confirmPassword,
            CancellationToken cancellationToken)
        {
            ViewBag.Email = email;
            ViewBag.Token = token;
            if (newPassword != confirmPassword)
                ModelState.AddModelError(nameof(confirmPassword), "Passwords do not match.");
            if (!ModelState.IsValid) return View();
            var result = await _studentAccounts.ActivateAsync(
                email, token, newPassword, cancellationToken);
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "The activation link is invalid or expired.");
                return View();
            }
            ViewData["ConfirmationTitle"] = "Account activated";
            ViewData["ConfirmationMessage"] = "Your password is set. You can now sign in.";
            return View("Confirmation");
        }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        [EnableRateLimiting("account-recovery")]
        public async Task<IActionResult> ResendActivation(
            string email,
            CancellationToken cancellationToken)
        {
            var user = string.IsNullOrWhiteSpace(email)
                ? null
                : await _userManager.FindByEmailAsync(email);
            if (user != null && !user.EmailConfirmed && !await _userManager.HasPasswordAsync(user))
            {
                try
                {
                    var invitation = await _studentAccounts.CreateActivationAsync(user, cancellationToken);
                    var url = Url.Action(nameof(Activate), "Account",
                        new { email = user.Email, token = invitation.RawToken }, Request.Scheme);
                    if (!string.IsNullOrWhiteSpace(url))
                        await _studentAccounts.SendActivationAsync(user, url, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    // Generic response prevents account and delivery-state enumeration.
                }
            }
            ViewData["ConfirmationTitle"] = "Check your email";
            ViewData["ConfirmationMessage"] =
                "If the account is awaiting activation, an invitation has been queued for delivery. Delivery may be delayed.";
            return View("Confirmation");
        }

        // ================= ACCESS DENIED =================
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private async Task RecordLoginAsync(string email, bool succeeded, string? userId)
        {
            _db.LoginHistories.Add(new LoginHistory
            {
                Email = email,
                UserId = userId,
                Succeeded = succeeded,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await _db.SaveChangesAsync();
        }
    }
}
