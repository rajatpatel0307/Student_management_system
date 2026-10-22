using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using System.Text.Json;

namespace Student_Management_System.Services;

public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly string _workerId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Outbox dispatcher iteration failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var messages = await db.OutboxMessages
            .Where(m => m.ProcessedUtc == null && !m.IsDeadLetter &&
                        (m.NextAttemptUtc == null || m.NextAttemptUtc <= now) &&
                        (m.ClaimExpiresUtc == null || m.ClaimExpiresUtc <= now))
            .OrderBy(m => m.Id).Take(25).ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.ClaimOwner = _workerId;
            message.ClaimedUtc = now;
            message.ClaimExpiresUtc = now.AddMinutes(2);
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another worker won this batch; its row-version claim is authoritative.
            return;
        }

        foreach (var message in messages)
        {
            try
            {
                await HandleAsync(message, db, scope.ServiceProvider, cancellationToken);
                message.ProcessedUtc = DateTime.UtcNow;
                message.LastError = null;
                message.ClaimOwner = null;
                message.ClaimedUtc = null;
                message.ClaimExpiresUtc = null;
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Discard any partial handler state before recording the retry.
                db.ChangeTracker.Clear();
                var failed = await db.OutboxMessages.SingleAsync(
                    candidate => candidate.Id == message.Id, cancellationToken);
                failed.AttemptCount++;
                failed.LastError = exception.Message.Length > 2000
                    ? exception.Message[..2000]
                    : exception.Message;
                failed.IsDeadLetter = failed.AttemptCount >= 8;
                failed.NextAttemptUtc = failed.IsDeadLetter
                    ? null
                    : DateTime.UtcNow.AddSeconds(Math.Min(3600, Math.Pow(2, failed.AttemptCount) * 15));
                failed.ClaimOwner = null;
                failed.ClaimedUtc = null;
                failed.ClaimExpiresUtc = null;
                await db.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(exception,
                    "Outbox message {MessageId} ({MessageType}) failed on attempt {AttemptCount}.",
                    failed.Id, failed.Type, failed.AttemptCount);
                return;
            }
        }
    }

    private async Task HandleAsync(
        OutboxMessage message,
        ApplicationDbContext db,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(message.PayloadJson);
        var payload = document.RootElement;
        switch (message.Type)
        {
            case "CommunicationCreated":
                await services.GetRequiredService<ICommunicationService>()
                    .DispatchDueAsync(cancellationToken);
                return;
            case "ApprovalRequested":
                var role = RequiredString(payload, "ApproverRole");
                var roleId = await db.Roles.Where(r => r.Name == role)
                    .Select(r => r.Id).SingleOrDefaultAsync(cancellationToken);
                if (roleId == null) throw new InvalidOperationException($"Approval role '{role}' does not exist.");
                var recipients = await db.UserRoles.Where(r => r.RoleId == roleId)
                    .Select(r => r.UserId).ToListAsync(cancellationToken);
                foreach (var userId in recipients)
                    db.UserNotifications.Add(new UserNotification
                    {
                        UserId = userId,
                        Title = "Approval requires review",
                        Message = $"A {RequiredString(payload, "workflowType")} request is waiting for your role.",
                        Category = "Approval",
                        ActionUrl = "/Approvals"
                    });
                return;
            case "finance.payment-collected":
            case "finance.invoice-issued":
                var studentId = RequiredInt(payload, "StudentId");
                var studentUserId = await db.Students.Where(s => s.Id == studentId)
                    .Select(s => s.UserId).SingleOrDefaultAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(studentUserId))
                    db.UserNotifications.Add(new UserNotification
                    {
                        UserId = studentUserId,
                        Title = message.Type == "finance.invoice-issued" ? "Invoice issued" : "Payment received",
                        Message = "Your finance account has been updated. Open the finance portal for the verified details.",
                        Category = "Finance",
                        ActionUrl = "/StudentDashboard/Fees"
                    });
                return;
            case "ApprovalActed":
            case "finance.payment-reversed":
            case "finance.payment-refunded":
            case "PromotionApplied":
                // The originating transaction already applied its authoritative
                // domain change; this durable event is acknowledged for monitoring.
                _logger.LogInformation("Dispatched domain event {MessageType} ({MessageId}).", message.Type, message.Id);
                return;
            default:
                throw new InvalidOperationException($"No outbox handler is registered for '{message.Type}'.");
        }
    }

    private static string RequiredString(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidOperationException($"Outbox payload is missing '{name}'.");

    private static int RequiredInt(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && value.TryGetInt32(out var result)
            ? result
            : throw new InvalidOperationException($"Outbox payload is missing '{name}'.");
}
