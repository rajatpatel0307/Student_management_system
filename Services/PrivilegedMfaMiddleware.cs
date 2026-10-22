using Microsoft.AspNetCore.Identity;

namespace Student_Management_System.Services;

public sealed class PrivilegedMfaMiddleware
{
    private static readonly string[] PrivilegedRoles =
    [
        "SuperAdmin", "Admin"
    ];
    private readonly RequestDelegate _next;

    public PrivilegedMfaMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        UserManager<ApplicationUser> users,
        IConfiguration configuration)
    {
        var path = context.Request.Path;
        var exempt = path.StartsWithSegments("/Account/Mfa") ||
                     path.StartsWithSegments("/Account/EnableMfa") ||
                     // Password rotation must finish before MFA enrollment.
                     // Otherwise the two security middlewares redirect each
                     // other's required pages indefinitely.
                     path.StartsWithSegments("/Account/ChangePassword") ||
                     path.StartsWithSegments("/Account/Logout") ||
                     path.StartsWithSegments("/Account/AccessDenied");
        if (!exempt &&
            configuration.GetValue("Security:RequirePrivilegedMfa", true) &&
            context.User.Identity?.IsAuthenticated == true &&
            PrivilegedRoles.Any(context.User.IsInRole))
        {
            var user = await users.GetUserAsync(context.User);
            if (user is { TwoFactorEnabled: false })
            {
                if (HttpMethods.IsGet(context.Request.Method) ||
                    HttpMethods.IsHead(context.Request.Method))
                {
                    context.Response.Redirect("/Account/Mfa");
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync(
                    "Multi-factor authentication enrollment is required before using this operation.",
                    context.RequestAborted);
                return;
            }
        }

        await _next(context);
    }
}
