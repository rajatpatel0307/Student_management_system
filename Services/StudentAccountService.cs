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
    private readonly IApplicationEmailSender _email;
    private readonly ILogger<StudentAccountService> _logger;

    public StudentAccountService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        IApplicationEmailSender email,
        ILogger<StudentAccountService> logger)
    {
        _db = db;
        _users = users;
        _email = email;
        _logger = logger;
    }

    public async Task<ActivationInvitation> CreateActivationAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var recent = await _db.AccountActivations
            .Where(a => a.UserId == user.Id && a.LastSentUtc > now.AddMinutes(-2))
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
            LastSentUtc = now,
            SendCount = 1,
            DeliveryPending = true
        };
        _db.AccountActivations.Add(record);
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
        try
        {
            await _email.SendAsync(
                user.Email!,
                "Activate your EduFlow account",
                $"Activate your account and create your password using this one-time link: " +
                $"<a href=\"{System.Net.WebUtility.HtmlEncode(activationUrl)}\">Activate account</a>. " +
                "The link expires in 24 hours.",
                cancellationToken);
            var latest = await _db.AccountActivations
                .Where(a => a.UserId == user.Id && a.UsedUtc == null)
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (latest != null)
            {
                latest.DeliveryPending = false;
                await _db.SaveChangesAsync(cancellationToken);
            }
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Account activation delivery failed for user {UserId}; invitation remains pending.",
                user.Id);
            return false;
        }
    }

    public async Task<IdentityResult> ActivateAsync(
        string email,
        string token,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByEmailAsync(email);
        if (user == null) return IdentityResult.Failed(new IdentityError { Description = "The activation link is invalid or expired." });
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
        await _users.UpdateSecurityStampAsync(user);
        await _db.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
