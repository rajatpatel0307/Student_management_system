using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Student_Management_System.Models;

namespace Student_Management_System.Services;

public sealed class ActivationDeliveryOptions
{
    // Deployment-owned origin, never an unvalidated Host header from an email request.
    public string PublicOrigin { get; set; } = "";
}

public sealed class ActivationDeliveryService(
    ApplicationDbContext db, IApplicationEmailSender email, IDataProtectionProvider protection,
    IOptions<ActivationDeliveryOptions> options, IHostEnvironment environment,
    IHttpContextAccessor accessor, ILogger<ActivationDeliveryService> logger)
{
    public const string MessagePrefix = "AccountActivationEmail:";
    private readonly IDataProtector _protector = protection.CreateProtector("EduFlow.AccountActivationEmail.v1");
    private readonly string _owner = Guid.NewGuid().ToString("N");
    private sealed record Payload(string UserId, string Email, string Token, DateTime ExpiresUtc, string? LocalOrigin);
    private sealed record Envelope(string Protected);

    public void Enqueue(AccountActivation invitation, ApplicationUser user, string token)
    {
        string? localOrigin = null;
        var request = accessor.HttpContext?.Request;
        if (environment.IsDevelopment() && request != null &&
            Uri.TryCreate($"{request.Scheme}://{request.Host}{request.PathBase}", UriKind.Absolute, out var local) && local.IsLoopback)
            localOrigin = local.ToString().TrimEnd('/');
        var payload = new Payload(user.Id, user.Email!, token, invitation.ExpiresUtc, localOrigin);
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = MessagePrefix + invitation.TokenHash,
            PayloadJson = JsonSerializer.Serialize(new Envelope(_protector.Protect(JsonSerializer.Serialize(payload))))
        });
    }

    public async Task<bool> TrySendAsync(long messageId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var message = await db.OutboxMessages.SingleOrDefaultAsync(m => m.Id == messageId, cancellationToken);
        if (message == null || !message.Type.StartsWith(MessagePrefix, StringComparison.Ordinal)) return false;
        if (message.ProcessedUtc.HasValue) return message.LastError == null;
        if (message.IsDeadLetter || message.NextAttemptUtc > now || message.ClaimExpiresUtc > now) return false;
        message.ClaimOwner = _owner;
        message.ClaimedUtc = now;
        message.ClaimExpiresUtc = now.AddMinutes(5); // Longer than the maximum configured SMTP timeout.
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { db.Entry(message).State = EntityState.Detached; return false; }

        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(message.PayloadJson) ?? throw new InvalidOperationException();
            var payload = JsonSerializer.Deserialize<Payload>(_protector.Unprotect(envelope.Protected)) ?? throw new InvalidOperationException();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload.Token)));
            if (message.Type != MessagePrefix + hash) throw new InvalidOperationException("Invalid protected invitation payload.");
            var invitation = await db.AccountActivations.SingleOrDefaultAsync(a => a.UserId == payload.UserId && a.TokenHash == hash, cancellationToken);
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == payload.UserId, cancellationToken);
            var valid = invitation != null && invitation.UsedUtc == null && invitation.ExpiresUtc > DateTime.UtcNow &&
                payload.ExpiresUtc > DateTime.UtcNow && user is { IsActive: true, PasswordHash: null } &&
                string.Equals(user.Email, payload.Email, StringComparison.OrdinalIgnoreCase);
            if (!valid)
            {
                message.LastError = "Invitation expired, superseded, activated or disabled.";
                if (invitation != null) invitation.DeliveryPending = false;
                AddAudit("Cancelled invalid activation delivery", payload.UserId, message.Id);
                Complete(message);
                await db.SaveChangesAsync(cancellationToken);
                return false;
            }
            var origin = options.Value.PublicOrigin;
            if (string.IsNullOrWhiteSpace(origin) && environment.IsDevelopment()) origin = payload.LocalOrigin ?? "";
            if (!ActivationDeliveryOptionsValidator.IsTrustedOrigin(origin, environment.IsDevelopment()))
                throw new InvalidOperationException("ActivationDelivery:PublicOrigin must be a trusted HTTPS origin.");
            var url = origin.TrimEnd('/') + "/Account/Activate" + QueryString.Create(new Dictionary<string, string?>
            { ["email"] = payload.Email, ["token"] = payload.Token });
            await email.SendAsync(payload.Email, "Activate your EduFlow account",
                "Create your password using this one-time link: <a href=\"" + System.Net.WebUtility.HtmlEncode(url) +
                "\">Activate account</a>. The invitation expires at " + payload.ExpiresUtc.ToString("u") + ".", cancellationToken);
            invitation!.DeliveryPending = false;
            invitation.LastSentUtc = DateTime.UtcNow;
            invitation.SendCount++;
            message.LastError = null;
            AddAudit("Delivered account activation invitation", payload.UserId, message.Id);
            Complete(message);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            // SMTP/provider exceptions can contain addresses or message content.
            // Persist and log only the failure type; never a token, URL or payload.
            db.ChangeTracker.Clear();
            var failed = await db.OutboxMessages.SingleAsync(m => m.Id == messageId, cancellationToken);
            if (failed.ClaimOwner != _owner) return false;
            failed.AttemptCount++;
            failed.LastError = $"Activation delivery failed ({exception.GetType().Name}). Verify delivery configuration.";
            failed.IsDeadLetter = failed.AttemptCount >= 8;
            failed.NextAttemptUtc = failed.IsDeadLetter ? null : DateTime.UtcNow.AddSeconds(Math.Min(3600, Math.Pow(2, failed.AttemptCount) * 15));
            Release(failed);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Activation message {MessageId} failed ({FailureType}); attempt {AttemptCount}.", messageId, exception.GetType().Name, failed.AttemptCount);
            return false;
        }
    }

    private static void Release(OutboxMessage message) { message.ClaimOwner = null; message.ClaimedUtc = null; message.ClaimExpiresUtc = null; }
    private static void Complete(OutboxMessage message) { message.ProcessedUtc = DateTime.UtcNow; Release(message); }
    private void AddAudit(string action, string userId, long messageId) => db.AuditLogs.Add(new AuditLog
    { UserId = userId, Action = action, EntityName = nameof(AccountActivation), EntityId = messageId.ToString() });
}
