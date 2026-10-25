using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;

namespace Student_Management_System.Services;

public static class InvitationStatusQuery
{
    public static async Task<IReadOnlyDictionary<string, InvitationDeliveryStatus>> ReadAsync(
        ApplicationDbContext db, string[] userIds, CancellationToken cancellationToken = default)
    {
        var records = await db.Users.AsNoTracking().Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.IsActive, HasPassword = user.PasswordHash != null,
                Invitation = db.AccountActivations.Where(a => a.UserId == user.Id).OrderByDescending(a => a.Id).FirstOrDefault() })
            .ToListAsync(cancellationToken);
        var types = records.Where(r => r.Invitation != null).Select(r => ActivationDeliveryService.MessagePrefix + r.Invitation!.TokenHash).ToArray();
        var jobs = await db.OutboxMessages.AsNoTracking().Where(m => types.Contains(m.Type))
            .Select(m => new { m.Type, m.AttemptCount, m.NextAttemptUtc, m.IsDeadLetter })
            .ToDictionaryAsync(m => m.Type, cancellationToken);
        var requestPayloads = records.Where(r => r.Invitation == null && !r.HasPassword && r.IsActive)
            .Select(r => AccountActivationRequests.PayloadFor(r.Id)).ToArray();
        var requests = requestPayloads.Length == 0 ? [] : await db.OutboxMessages.AsNoTracking()
            .Where(m => m.Type == AccountActivationRequests.MessageType && requestPayloads.Contains(m.PayloadJson))
            .OrderByDescending(m => m.Id).Select(m => new { m.PayloadJson, m.AttemptCount, m.NextAttemptUtc, m.IsDeadLetter }).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        return records.ToDictionary(record => record.Id, record =>
        {
            var invitation = record.Invitation;
            var job = invitation == null ? null : jobs.GetValueOrDefault(ActivationDeliveryService.MessagePrefix + invitation.TokenHash);
            var request = invitation == null ? requests.FirstOrDefault(r => r.PayloadJson == AccountActivationRequests.PayloadFor(record.Id)) : null;
            var state = record.HasPassword ? "Activated" : !record.IsActive ? "Disabled" : invitation == null
                ? request == null ? "Not requested" : request.IsDeadLetter ? "Needs attention" : request.AttemptCount > 0 ? "Retry scheduled" : "Queued"
                : invitation.ExpiresUtc <= now ? "Expired" : invitation.UsedUtc.HasValue ? "Superseded"
                : invitation.LastSentUtc.HasValue && !invitation.DeliveryPending ? "Delivered"
                : job == null || job.IsDeadLetter ? "Needs attention" : job.AttemptCount > 0 ? "Retry scheduled" : "Queued";
            return new InvitationDeliveryStatus(state, invitation?.ExpiresUtc, invitation?.LastSentUtc,
                job?.AttemptCount ?? request?.AttemptCount ?? 0, job?.NextAttemptUtc ?? request?.NextAttemptUtc,
                !record.HasPassword && record.IsActive && (invitation == null || invitation.CreatedUtc <= now.AddMinutes(-2)));
        });
    }
}
