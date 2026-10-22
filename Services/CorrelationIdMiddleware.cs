namespace Student_Management_System.Services;

public sealed class CorrelationIdMiddleware
{
    public const string ItemKey = "EduFlow.CorrelationId";
    private const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = Guid.TryParse(supplied, out var parsed)
            ? parsed.ToString("D")
            : Guid.NewGuid().ToString("D");
        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        using (context.RequestServices.GetRequiredService<ILogger<CorrelationIdMiddleware>>()
                   .BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
            await _next(context);
    }
}
