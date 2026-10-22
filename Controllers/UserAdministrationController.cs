using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Student_Management_System.Models;
using Student_Management_System.Services;

namespace Student_Management_System.Controllers;

[Authorize(Roles = "Admin,SuperAdmin")]
public class UserAdministrationController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly AuditService _audit;
    public UserAdministrationController(UserManager<ApplicationUser> users, AuditService audit)
    { _users = users; _audit = audit; }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(string id, string newPassword, string? returnUrl)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();
        var roles = await _users.GetRolesAsync(user);
        if (!User.IsInRole("SuperAdmin") && roles.Any(r => r is "SuperAdmin" or "Admin")) return Forbid();
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded) return BadRequest(result.Errors);
        user.MustChangePassword = true;
        await _users.UpdateSecurityStampAsync(user);
        await _audit.RecordAsync("ResetPassword", "User", id);
        TempData["Success"] = "Password reset. Existing sessions were revoked and the user must change it at next sign-in.";
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToAction("Index", "Dashboard");
    }
}
