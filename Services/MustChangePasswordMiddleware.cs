using Microsoft.AspNetCore.Identity;

namespace Student_Management_System.Services;

public sealed class MustChangePasswordMiddleware
{
    private static readonly PathString ChangePasswordPath = new("/Account/ChangePassword");
    private static readonly PathString LogoutPath = new("/Account/Logout");
    private static readonly PathString AccessDeniedPath = new("/Account/AccessDenied");
    private readonly RequestDelegate _next;

    public MustChangePasswordMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        UserManager<ApplicationUser> users)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            !context.Request.Path.StartsWithSegments(ChangePasswordPath) &&
            !context.Request.Path.StartsWithSegments(LogoutPath) &&
            !context.Request.Path.StartsWithSegments(AccessDeniedPath))
        {
            var user = await users.GetUserAsync(context.User);
            if (user?.MustChangePassword == true)
            {
                if (HttpMethods.IsGet(context.Request.Method) ||
                    HttpMethods.IsHead(context.Request.Method))
                {
                    context.Response.Redirect(ChangePasswordPath);
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync(
                    "You must change your password before using this operation.",
                    context.RequestAborted);
                return;
            }
        }

        await _next(context);
    }
}
