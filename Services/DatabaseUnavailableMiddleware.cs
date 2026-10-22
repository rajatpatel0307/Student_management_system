using System.Text.Encodings.Web;
using Microsoft.Data.SqlClient;

namespace Student_Management_System.Services;

public sealed class DatabaseUnavailableMiddleware(
    RequestDelegate next,
    ILogger<DatabaseUnavailableMiddleware> logger)
{
    private static readonly HashSet<int> AvailabilityErrorNumbers =
    [
        -2,     // SQL timeout
        2,      // server/instance not found
        20,     // instance does not support encryption
        53,     // network path not found
        64,     // network name unavailable
        233,    // no process on the other end of the pipe
        258,    // Windows wait timeout
        4060,   // database unavailable
        18456,  // login failed
        10053,
        10054,
        10060,
        10061,
        11001
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
            when (!context.Response.HasStarted &&
                  IsDatabaseAvailabilityFailure(exception))
        {
            TryGetAvailabilityError(exception, out var errorNumber);
            logger.LogError(
                "Database request failed with availability error {ErrorNumber}. CorrelationId: {CorrelationId}",
                errorNumber,
                context.TraceIdentifier);

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
            context.Response.Headers["Retry-After"] = "10";

            var correlationId = HtmlEncoder.Default.Encode(context.TraceIdentifier);
            await context.Response.WriteAsync(
                $$"""
                <!doctype html>
                <html lang="en">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1">
                    <title>Database temporarily unavailable - EduFlow</title>
                    <style>
                        :root { color-scheme: light dark; font-family: system-ui, sans-serif; }
                        body { margin: 0; min-height: 100vh; display: grid; place-items: center; background: #f4f7fb; color: #172033; }
                        main { width: min(42rem, calc(100% - 2rem)); background: white; border-radius: 1rem; padding: 2rem; box-shadow: 0 1rem 3rem #1720331f; }
                        h1 { margin-top: 0; color: #173b70; }
                        p { line-height: 1.6; }
                        button { border: 0; border-radius: .6rem; padding: .75rem 1rem; background: #1769aa; color: white; cursor: pointer; }
                        small { display: block; margin-top: 1.5rem; color: #58657a; }
                        @media (prefers-color-scheme: dark) { body { background: #111827; color: #e5e7eb; } main { background: #1f2937; } h1 { color: #bfdbfe; } small { color: #cbd5e1; } }
                    </style>
                </head>
                <body>
                    <main role="alert" aria-live="assertive">
                        <h1>Database temporarily unavailable</h1>
                        <p>EduFlow could not complete the database connection. Verify that SQL Server Express and the StudentDB database are online, then try again.</p>
                        <form method="get"><button type="submit">Try again</button></form>
                        <small>Reference: {{correlationId}}</small>
                    </main>
                </body>
                </html>
                """,
                context.RequestAborted);
        }
    }

    private static bool IsDatabaseAvailabilityFailure(Exception exception) =>
        TryGetAvailabilityError(exception, out _);

    internal static bool TryGetAvailabilityError(
        Exception exception,
        out int errorNumber)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is not SqlException sqlException)
                continue;

            foreach (SqlError error in sqlException.Errors)
            {
                if (!AvailabilityErrorNumbers.Contains(error.Number))
                    continue;

                errorNumber = error.Number;
                return true;
            }

            if (AvailabilityErrorNumbers.Contains(sqlException.Number))
            {
                errorNumber = sqlException.Number;
                return true;
            }
        }

        errorNumber = 0;
        return false;
    }
}
