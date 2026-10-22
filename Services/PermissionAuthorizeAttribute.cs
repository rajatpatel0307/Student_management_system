using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Student_Management_System.Services;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class PermissionAuthorizeAttribute : TypeFilterAttribute
{
    public PermissionAuthorizeAttribute(string permission)
        : base(typeof(PermissionAuthorizationFilter))
    {
        Arguments = [permission];
    }
}

public sealed class PermissionAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly PermissionService _permissions;
    private readonly string _permission;
    public PermissionAuthorizationFilter(PermissionService permissions, string permission)
    {
        _permissions = permissions;
        _permission = permission;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!await _permissions.HasAsync(context.HttpContext.User, _permission))
            context.Result = new ForbidResult();
    }
}
