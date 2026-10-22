using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Student_Management_System.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace Student_Management_System.Services;

public sealed class SmsGatewayOptions
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string SenderId { get; set; } = "EduFlow";
}

public interface ISmsGateway
{
    Task SendAsync(string phone, string message, CancellationToken cancellationToken);
}

public sealed class HttpSmsGateway : ISmsGateway
{
    private readonly HttpClient _client;
    private readonly SmsGatewayOptions _options;

    public HttpSmsGateway(HttpClient client, IOptions<SmsGatewayOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task SendAsync(
        string phone,
        string message,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.Endpoint))
            throw new InvalidOperationException("The SMS gateway is not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
        {
            Content = JsonContent.Create(new
            {
                to = phone,
                message,
                sender = _options.SenderId
            })
        };
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_options.ApiKey}");
        using var response = await _client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public interface ICommunicationService
{
    Task<CommunicationMessage> CreateAsync(
        CommunicationInput input,
        string actorUserId,
        CancellationToken cancellationToken = default);
    Task CancelAsync(long id, string actorUserId, CancellationToken cancellationToken = default);
    Task DispatchDueAsync(CancellationToken cancellationToken = default);
}

public sealed class CommunicationService : ICommunicationService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IEmailSender _email;
    private readonly ISmsGateway _sms;
    private readonly ILogger<CommunicationService> _logger;
    private readonly string _claimOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public CommunicationService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        IEmailSender email,
        ISmsGateway sms,
        ILogger<CommunicationService> logger)
    {
        _db = db;
        _users = users;
        _email = email;
        _sms = sms;
        _logger = logger;
    }

    public async Task<CommunicationMessage> CreateAsync(
        CommunicationInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!input.InApp && !input.Email && !input.Sms)
            throw new InvalidOperationException("Select at least one delivery channel.");
        if (input.ExpiresLocal.HasValue &&
            input.ScheduledLocal.HasValue &&
            input.ExpiresLocal <= input.ScheduledLocal)
            throw new InvalidOperationException("Expiration must be after scheduled delivery.");

        var recipients = await ResolveRecipientsAsync(
            input.AudienceType,
            input.AudienceValue,
            cancellationToken);
        if (recipients.Count == 0)
            throw new InvalidOperationException("The selected audience contains no active users.");

        var scheduledUtc = input.ScheduledLocal?.ToUniversalTime() ?? DateTime.UtcNow;
        var message = new CommunicationMessage
        {
            Title = input.Title.Trim(),
            Body = input.Body.Trim(),
            Priority = input.Priority.Trim(),
            Status = scheduledUtc > DateTime.UtcNow
                ? CommunicationStatus.Scheduled
                : CommunicationStatus.Processing,
            ScheduledUtc = scheduledUtc,
            ExpiresUtc = input.ExpiresLocal?.ToUniversalTime(),
            CreatedByUserId = actorUserId,
            Audiences =
            [
                new CommunicationAudience
                {
                    AudienceType = input.AudienceType.Trim(),
                    AudienceValue = input.AudienceValue.Trim()
                }
            ]
        };
        var channels = new List<DeliveryChannel>();
        if (input.InApp) channels.Add(DeliveryChannel.InApp);
        if (input.Email) channels.Add(DeliveryChannel.Email);
        if (input.Sms) channels.Add(DeliveryChannel.Sms);
        message.Deliveries = recipients
            .SelectMany(userId => channels.Select(channel => new CommunicationDelivery
            {
                UserId = userId,
                Channel = channel
            }))
            .ToList();
        _db.CommunicationMessages.Add(message);
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = actorUserId,
            Action = $"Created communication for {recipients.Count} recipients",
            EntityName = nameof(CommunicationMessage),
            EntityId = message.Title
        });
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "CommunicationCreated",
            PayloadJson = JsonSerializer.Serialize(new
            {
                message.Title,
                RecipientCount = recipients.Count,
                Channels = channels
            })
        });
        await _db.SaveChangesAsync(cancellationToken);
        return message;
    }

    public async Task CancelAsync(
        long id,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var message = await _db.CommunicationMessages
            .Include(m => m.Deliveries)
            .SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Communication not found.");
        if (message.Status is CommunicationStatus.Completed or CommunicationStatus.Cancelled)
            throw new InvalidOperationException("This communication cannot be cancelled.");
        message.Status = CommunicationStatus.Cancelled;
        foreach (var delivery in message.Deliveries.Where(d => d.Status == DeliveryStatus.Pending))
            delivery.Status = DeliveryStatus.Skipped;
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = actorUserId,
            Action = "Cancelled communication",
            EntityName = nameof(CommunicationMessage),
            EntityId = id.ToString()
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var leaseExpires = now.AddMinutes(5);
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            ;WITH claimable AS
            (
                SELECT TOP (100) d.Id
                FROM CommunicationDeliveries d WITH (UPDLOCK, READPAST, ROWLOCK)
                INNER JOIN CommunicationMessages m ON m.Id = d.CommunicationMessageId
                WHERE d.Status = {(int)DeliveryStatus.Pending}
                  AND (d.NextAttemptUtc IS NULL OR d.NextAttemptUtc <= {now})
                  AND (d.ClaimExpiresUtc IS NULL OR d.ClaimExpiresUtc <= {now})
                  AND m.Status <> {(int)CommunicationStatus.Cancelled}
                  AND m.ScheduledUtc <= {now}
                  AND (m.ExpiresUtc IS NULL OR m.ExpiresUtc > {now})
                ORDER BY d.Id
            )
            UPDATE d
               SET d.Status = {(int)DeliveryStatus.Processing},
                   d.ClaimOwner = {_claimOwner},
                   d.ClaimedUtc = {now},
                   d.ClaimExpiresUtc = {leaseExpires},
                   d.AttemptCount = d.AttemptCount + 1
            FROM CommunicationDeliveries d
            INNER JOIN claimable c ON c.Id = d.Id;
            """, cancellationToken);

        var deliveries = await _db.CommunicationDeliveries
            .Include(d => d.User)
            .Include(d => d.CommunicationMessage)
            .Where(d => d.Status == DeliveryStatus.Processing &&
                        d.ClaimOwner == _claimOwner)
            .OrderBy(d => d.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var delivery in deliveries)
        {
            try
            {
                switch (delivery.Channel)
                {
                    case DeliveryChannel.InApp:
                        _db.UserNotifications.Add(new UserNotification
                        {
                            UserId = delivery.UserId,
                            Title = delivery.CommunicationMessage.Title,
                            Message = delivery.CommunicationMessage.Body.Length > 1000
                                ? delivery.CommunicationMessage.Body[..1000]
                                : delivery.CommunicationMessage.Body,
                            Category = "Communication",
                            ActionUrl = $"/Communications/Details/{delivery.CommunicationMessageId}"
                        });
                        break;
                    case DeliveryChannel.Email:
                        if (string.IsNullOrWhiteSpace(delivery.User.Email))
                            throw new InvalidOperationException("Recipient has no email address.");
                        await _email.SendEmailAsync(
                            delivery.User.Email,
                            delivery.CommunicationMessage.Title,
                            System.Net.WebUtility.HtmlEncode(delivery.CommunicationMessage.Body)
                                .Replace("\n", "<br />"));
                        break;
                    case DeliveryChannel.Sms:
                        if (string.IsNullOrWhiteSpace(delivery.User.PhoneNumber))
                            throw new InvalidOperationException("Recipient has no phone number.");
                        await _sms.SendAsync(
                            delivery.User.PhoneNumber,
                            $"{delivery.CommunicationMessage.Title}: {delivery.CommunicationMessage.Body}",
                            cancellationToken);
                        break;
                }
                delivery.Status = DeliveryStatus.Delivered;
                delivery.DeliveredUtc = DateTime.UtcNow;
                delivery.LastError = null;
                delivery.NextAttemptUtc = null;
            }
            catch (Exception exception)
            {
                delivery.Status = delivery.AttemptCount >= 5
                    ? DeliveryStatus.Failed
                    : DeliveryStatus.Pending;
                if (delivery.Status == DeliveryStatus.Pending)
                {
                    var seconds = Math.Min(
                        3600,
                        Math.Pow(2, delivery.AttemptCount) * 15 +
                        Random.Shared.Next(0, 20));
                    delivery.NextAttemptUtc = DateTime.UtcNow.AddSeconds(seconds);
                }
                delivery.LastError = exception.Message.Length > 1000
                    ? exception.Message[..1000]
                    : exception.Message;
                _logger.LogWarning(
                    exception,
                    "Communication delivery {DeliveryId} failed on attempt {Attempt}",
                    delivery.Id,
                    delivery.AttemptCount);
            }
            delivery.ClaimOwner = null;
            delivery.ClaimedUtc = null;
            delivery.ClaimExpiresUtc = null;
        }
        await _db.SaveChangesAsync(cancellationToken);

        var messageIds = deliveries.Select(d => d.CommunicationMessageId).Distinct().ToArray();
        var messages = await _db.CommunicationMessages
            .Include(m => m.Deliveries)
            .Where(m => messageIds.Contains(m.Id))
            .ToListAsync(cancellationToken);
        foreach (var message in messages)
        {
            if (message.Deliveries.All(d =>
                    d.Status is DeliveryStatus.Delivered or DeliveryStatus.Failed or DeliveryStatus.Skipped))
                message.Status = CommunicationStatus.Completed;
            else
                message.Status = CommunicationStatus.Processing;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<string>> ResolveRecipientsAsync(
        string audienceType,
        string audienceValue,
        CancellationToken cancellationToken)
    {
        audienceType = audienceType.Trim();
        audienceValue = audienceValue.Trim();
        switch (audienceType)
        {
            case "Role":
                if (!await _db.Roles.AnyAsync(r => r.Name == audienceValue, cancellationToken))
                    throw new InvalidOperationException("Select a valid role.");
                return (await _users.GetUsersInRoleAsync(audienceValue))
                    .Where(u => u.IsActive)
                    .Select(u => u.Id)
                    .Distinct()
                    .ToList();
            case "Department":
                if (!int.TryParse(audienceValue, out var departmentId))
                    throw new InvalidOperationException("Select a valid department.");
                return await _db.Students.Where(s => s.DepartmentId == departmentId &&
                                                    !s.IsArchived && s.UserId != null)
                    .Select(s => s.UserId!)
                    .Concat(_db.Teachers.Where(t => t.DepartmentId == departmentId &&
                                                    !t.IsArchived && t.UserId != null)
                        .Select(t => t.UserId!))
                    .Distinct().ToListAsync(cancellationToken);
            case "Class":
                if (!int.TryParse(audienceValue, out var classId))
                    throw new InvalidOperationException("Select a valid class.");
                return await _db.Students.Where(s => s.ClassId == classId &&
                                                    !s.IsArchived && s.UserId != null)
                    .Select(s => s.UserId!).Distinct().ToListAsync(cancellationToken);
            case "Section":
                if (!int.TryParse(audienceValue, out var sectionId))
                    throw new InvalidOperationException("Select a valid section.");
                return await _db.Students.Where(s => s.SectionId == sectionId &&
                                                    !s.IsArchived && s.UserId != null)
                    .Select(s => s.UserId!).Distinct().ToListAsync(cancellationToken);
            case "User":
                return await _db.Users.Where(u => u.Id == audienceValue && u.IsActive)
                    .Select(u => u.Id).ToListAsync(cancellationToken);
            default:
                throw new InvalidOperationException("Unsupported audience type.");
        }
    }
}

public sealed class CommunicationDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CommunicationDispatcher> _logger;

    public CommunicationDispatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<CommunicationDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ICommunicationService>()
                    .DispatchDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Communication dispatcher iteration failed.");
            }
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
