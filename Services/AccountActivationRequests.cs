using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;

namespace Student_Management_System.Services;

public static class AccountActivationRequests
{
    public const string MessageType = "AccountActivationRequested";
    public static string PayloadFor(string userId) => JsonSerializer.Serialize(new { UserId = userId });

    // Persist this recovery intent in the account/profile transaction. It contains
    // an internal account ID, never a password, recipient address or raw token.
    public static void Enqueue(ApplicationDbContext db, string userId)
    {
        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Activation requests require the account transaction.");
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = MessageType, PayloadJson = PayloadFor(userId)
        });
    }

    public static async Task EnsureInvitationAsync(IServiceProvider services, string userId, CancellationToken cancellationToken)
    {
        // Invitation creation clears/retries its unit of work. Keep the dispatcher's
        // leased request tracked in its own context so it can acknowledge the job.
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is not { IsActive: true, PasswordHash: null }) return;
        var queued = await db.AccountActivations.AnyAsync(invitation => invitation.UserId == userId &&
            db.OutboxMessages.Any(message => message.Type == ActivationDeliveryService.MessagePrefix + invitation.TokenHash), cancellationToken);
        if (queued) return; // Preserve existing token, expiry and delivery/dead-letter state.
        await scope.ServiceProvider.GetRequiredService<IStudentAccountService>().CreateActivationAsync(user, cancellationToken);
    }
}
