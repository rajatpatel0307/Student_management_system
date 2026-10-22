using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Student_Management_System.Services;

// Defense in depth while legacy modules are moved to dedicated input models.
// Server-owned lifecycle fields must only change through named command actions.
public sealed class OverpostingGuardFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> ServerOwnedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "IsArchived", "ArchivedUtc", "ArchivedByUserId", "ArchiveReason",
        "RestoredUtc", "RestoredByUserId", "CreatedByUserId", "CreatedUtc",
        "UpdatedUtc", "EnrolledStudentId", "ProcessedUtc", "AttemptCount",
        "ClaimOwner", "ClaimedUtc", "ClaimExpiresUtc", "IsDeadLetter"
    };

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (HttpMethods.IsPost(context.HttpContext.Request.Method) &&
            context.HttpContext.Request.HasFormContentType)
        {
            var form = await context.HttpContext.Request.ReadFormAsync(
                context.HttpContext.RequestAborted);
            var forbidden = form.Keys.Select(key => key.Split('.').Last())
                .FirstOrDefault(ServerOwnedFields.Contains);
            if (forbidden != null)
            {
                context.Result = new BadRequestObjectResult(
                    $"The field '{forbidden}' is controlled by the server and cannot be submitted by this form.");
                return;
            }
        }
        await next();
    }
}
