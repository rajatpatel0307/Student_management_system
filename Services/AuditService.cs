using System.Security.Claims;
using Student_Management_System.Models;

namespace Student_Management_System.Services;

public class AuditService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _accessor;

    public AuditService(ApplicationDbContext db, IHttpContextAccessor accessor)
    {
        _db = db;
        _accessor = accessor;
    }

    public AuditLog Add(
        string action,
        string entityName,
        object? entityId = null,
        string outcome = "Succeeded",
        string? detailsJson = null)
    {
        var context = _accessor.HttpContext;
        var userAgent = context?.Request.Headers.UserAgent.ToString();
        if (userAgent?.Length > 500) userAgent = userAgent[..500];
        var entry = new AuditLog
        {
            UserId = context?.User.FindFirstValue(ClaimTypes.NameIdentifier),
            Action = action,
            EntityName = entityName,
            EntityId = entityId?.ToString(),
            IpAddress = context?.Connection.RemoteIpAddress?.ToString(),
            CorrelationId = context?.Items[CorrelationIdMiddleware.ItemKey]?.ToString(),
            HttpMethod = context?.Request.Method,
            RequestPath = context?.Request.Path.Value,
            UserAgent = userAgent,
            Outcome = outcome,
            DetailsJson = detailsJson
        };
        _db.AuditLogs.Add(entry);
        return entry;
    }

    public async Task RecordAsync(
        string action,
        string entityName,
        object? entityId = null,
        string outcome = "Succeeded",
        string? detailsJson = null)
    {
        Add(action, entityName, entityId, outcome, detailsJson);
        await _db.SaveChangesAsync();
    }
}
