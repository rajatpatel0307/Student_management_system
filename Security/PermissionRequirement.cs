using Microsoft.AspNetCore.Authorization;
using Student_Management_System.Services;

namespace Student_Management_System.Security;

public sealed record PermissionRequirement(string PermissionKey)
    : IAuthorizationRequirement;

public sealed class GranularPermissionHandler
    : AuthorizationHandler<PermissionRequirement>
{
    private readonly PermissionService _permissions;
    public GranularPermissionHandler(PermissionService permissions) =>
        _permissions = permissions;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (await _permissions.HasAsync(context.User, requirement.PermissionKey))
            context.Succeed(requirement);
    }
}
