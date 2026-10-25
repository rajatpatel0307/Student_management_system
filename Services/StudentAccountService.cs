using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using System.Security.Cryptography;
using System.Text;

namespace Student_Management_System.Services;

public sealed record ActivationInvitation(string RawToken, DateTime ExpiresUtc);

public interface IStudentAccountService
{
    Task<ActivationInvitation> CreateActivationAsync(ApplicationUser user, CancellationToken cancellationToken = default);
    Task<bool> SendActivationAsync(ApplicationUser user, string activationUrl, CancellationToken cancellationToken = default);
    Task<IdentityResult> ActivateAsync(string email, string token, string password, CancellationToken cancellationToken = default);
}

public sealed class StudentAccountService : IStudentAccountService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ActivationDeliveryService _delivery;

    public StudentAccountService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        ActivationDeliveryService delivery,
        ILogger<StudentAccountService> logger)
    {
        _db = db;
        _users = users;
        _delivery = delivery;
    }

    public async Task<ActivationInvitation> CreateActivationAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        if (_db.Database.CurrentTransaction != null) return await CreateCoreAsync(user, cancellationToken);
        return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
            var result = await CreateCoreAsync(user, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private async Task<ActivationInvitation> CreateCoreAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (!await _db.Users.AnyAsync(u => u.Id == user.Id && u.IsActive && u.PasswordHash == null && u.Email == user.Email, cancellationToken))
            throw new InvalidOperationException("This account is not awaiting activation.");
        var recent = await _db.AccountActivations
            .Where(a => a.UserId == user.Id && a.CreatedUtc > now.AddMinutes(-2))
            .AnyAsync(cancellationToken);
        if (recent) throw new InvalidOperationException("Please wait before requesting another activation message.");

        var openInvitations = await _db.AccountActivations
            .Where(a => a.UserId == user.Id && a.UsedUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var invitation in openInvitations) invitation.UsedUtc = now;

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var record = new AccountActivation
        {
            UserId = user.Id,
            TokenHash = Hash(rawToken),
            CreatedUtc = now,
            ExpiresUtc = now.AddHours(24),
            SendCount = 0,
            DeliveryPending = true
        };
        _db.AccountActivations.Add(record);
        _delivery.Enqueue(record, user, rawToken);
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            Action = "Created account activation invitation",
            EntityName = nameof(AccountActivation)
        });
        await _db.SaveChangesAsync(cancellationToken);
        return new ActivationInvitation(rawToken, record.ExpiresUtc);
    }

    public async Task<bool> SendActivationAsync(
        ApplicationUser user,
        string activationUrl,
        CancellationToken cancellationToken = default)
    {
        // Retain the existing method contract, but build mail URLs from protected
        // deployment configuration, never the caller's request Host header.
        var hash = await _db.AccountActivations.AsNoTracking().Where(a => a.UserId == user.Id && a.UsedUtc == null)
            .OrderByDescending(a => a.Id).Select(a => a.TokenHash).FirstOrDefaultAsync(cancellationToken);
        if (hash == null) return false;
        var id = await _db.OutboxMessages.AsNoTracking().Where(m => m.Type == ActivationDeliveryService.MessagePrefix + hash)
            .Select(m => (long?)m.Id).SingleOrDefaultAsync(cancellationToken);
        return id.HasValue && await _delivery.TrySendAsync(id.Value, cancellationToken);
    }

    public async Task<IdentityResult> ActivateAsync(
        string email,
        string token,
        string password,
        CancellationToken cancellationToken = default)
    {
        return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
            var result = await ActivateCoreAsync(email, token, password, cancellationToken);
            if (result.Succeeded) await transaction.CommitAsync(cancellationToken);
            else _db.ChangeTracker.Clear();
            return result;
        });
    }

    private async Task<IdentityResult> ActivateCoreAsync(string email, string token, string password, CancellationToken cancellationToken)
    {
        var user = await _users.FindByEmailAsync(email);
        if (user == null || !user.IsActive) return IdentityResult.Failed(new IdentityError { Description = "The activation link is invalid or expired." });
        var hash = Hash(token);
        var invitation = await _db.AccountActivations.SingleOrDefaultAsync(a =>
            a.UserId == user.Id && a.TokenHash == hash && a.UsedUtc == null, cancellationToken);
        if (invitation == null || invitation.ExpiresUtc <= DateTime.UtcNow)
            return IdentityResult.Failed(new IdentityError { Description = "The activation link is invalid or expired." });
        if (await _users.HasPasswordAsync(user))
            return IdentityResult.Failed(new IdentityError { Description = "This account is already active." });

        var passwordResult = await _users.AddPasswordAsync(user, password);
        if (!passwordResult.Succeeded) return passwordResult;
        user.EmailConfirmed = true;
        user.MustChangePassword = false;
        invitation.UsedUtc = DateTime.UtcNow;
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            Action = "Activated account",
            EntityName = nameof(ApplicationUser),
            EntityId = user.Id
        });
        var stamp = await _users.UpdateSecurityStampAsync(user);
        if (!stamp.Succeeded) throw new InvalidOperationException("Account activation could not invalidate earlier sessions.");
        await _db.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
