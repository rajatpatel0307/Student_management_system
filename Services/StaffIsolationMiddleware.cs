using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using System.Security.Claims;

namespace Student_Management_System.Services;

// Enforce module boundaries on legacy MVC URLs as well as the React API.
// Explicit workspace grants open that module without granting an Identity role.
public sealed class StaffIsolationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, SignInManager<ApplicationUser> signIn)
    {
        var principal = context.User;
        if (principal.Identity?.IsAuthenticated != true) { await next(context); return; }
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
        if (user == null || !user.IsActive)
        {
            await signIn.SignOutAsync();
            context.Response.StatusCode = 401;
            return;
        }
        if (principal.IsInRole("Admin") || principal.IsInRole("SuperAdmin")) { await next(context); return; }
        var role = StaffWorkspaceCatalog.All.FirstOrDefault(w => principal.IsInRole(w.Role));
        if (role == null) { await next(context); return; }
        var path = context.Request.Path.Value ?? "/";
        var common = new[] { "/Staff", "/RoleDashboard", "/api/staff-workspace", "/api/staff-resources", "/api/staff-certificates", "/Account", "/Profile", "/Notifications", "/Home", "/Leave" };
        if (common.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase) || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase)))
        { await next(context); return; }
        var required = path.StartsWith("/Library", StringComparison.OrdinalIgnoreCase) ? "librarian"
            : path.StartsWith("/Finance", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/Fee", StringComparison.OrdinalIgnoreCase) ? "accountant"
            : path.StartsWith("/Admissions", StringComparison.OrdinalIgnoreCase) ? "registrar"
            : path.StartsWith("/Operations", StringComparison.OrdinalIgnoreCase) ? role.Key is "transport" or "hostel" ? role.Key : "operations"
            : path.StartsWith("/Reports", StringComparison.OrdinalIgnoreCase) && role.Key is "librarian" or "accountant" ? role.Key
            : null;
        var decision = required == null ? false : await db.UserPermissions.AsNoTracking()
            .Where(p => p.UserId == id && p.Permission.Key == "workspace." + required)
            .Select(p => (bool?)p.IsGranted).SingleOrDefaultAsync() ?? required == role.Key;
        if (!decision) { context.Response.StatusCode = 403; await context.Response.WriteAsync("Your staff role does not have permission to access this module."); return; }
        await next(context);
    }
}
