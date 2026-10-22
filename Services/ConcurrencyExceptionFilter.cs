using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Student_Management_System.Services;

public sealed class ConcurrencyExceptionFilter : IAsyncExceptionFilter
{
    private readonly ILogger<ConcurrencyExceptionFilter> _logger;
    public ConcurrencyExceptionFilter(ILogger<ConcurrencyExceptionFilter> logger) => _logger = logger;

    public Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not DbUpdateConcurrencyException) return Task.CompletedTask;
        _logger.LogWarning(context.Exception,
            "An optimistic concurrency conflict occurred for {Path}",
            context.HttpContext.Request.Path);
        context.ExceptionHandled = true;
        context.Result = new ViewResult
        {
            ViewName = "~/Views/Shared/ConcurrencyConflict.cshtml",
            StatusCode = StatusCodes.Status409Conflict
        };
        return Task.CompletedTask;
    }
}
