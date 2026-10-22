using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Student_Management_System.Services;

public sealed class DashboardSetupRequiredException : InvalidOperationException
{
    public DashboardSetupRequiredException() : base("No active campus membership is assigned to this account.") { }
}

// A valid login without campus assignment needs onboarding guidance. Do not
// substitute an unscoped dashboard or turn unrelated access denials into success.
public sealed class DashboardSetupExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not DashboardSetupRequiredException) return;
        context.ExceptionHandled = true;
        context.Result = new ViewResult
        {
            ViewName = "~/Views/Shared/DashboardSetupRequired.cshtml",
            StatusCode = StatusCodes.Status200OK
        };
    }
}
