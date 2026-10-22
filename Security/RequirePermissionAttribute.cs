using Microsoft.AspNetCore.Mvc;
using Student_Management_System.Services;

namespace Student_Management_System.Security;

public sealed class RequirePermissionAttribute : TypeFilterAttribute
{
    public RequirePermissionAttribute(string permission) : base(typeof(PermissionAuthorizationFilter))
        => Arguments = new object[] { permission };
}
