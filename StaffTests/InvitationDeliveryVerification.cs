using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Student_Management_System.Models;
using Student_Management_System.Services;

internal static class InvitationDeliveryVerification
{
    public static async Task RunAsync(string connection, ApplicationDbContext db, UserManager<ApplicationUser> users, Action<bool, string> check)
    {
        var protection = new EphemeralDataProtectionProvider();
        var sender = new FixtureEmail { Failures = 1 };
        var delivery = Delivery(db);
        ActivationDeliveryService Delivery(ApplicationDbContext context) => new(context, sender, protection,
            Options.Create(new ActivationDeliveryOptions { PublicOrigin = "https://campus.example.invalid" }), new FixtureEnvironment(),
            new HttpContextAccessor(), NullLogger<ActivationDeliveryService>.Instance);
        var accounts = new StudentAccountService(db, users, delivery, NullLogger<StudentAccountService>.Instance);
        async Task<ApplicationUser> Account(int number)
        {
            var user = new ApplicationUser { FullName = "Invitation fixture", UserName = $"invitation-{number}", Email = $"invitation-{number}@test.invalid", PhoneNumber = $"89999999{number:00}" };
            check((await users.CreateAsync(user)).Succeeded, "An inactive invitation fixture must be persisted without a password.");
            return user;
        }
        var user = await Account(80);
        var invitation = await accounts.CreateActivationAsync(user);
        var record = await db.AccountActivations.SingleAsync(a => a.UserId == user.Id);
        var message = await db.OutboxMessages.SingleAsync(m => m.Type == ActivationDeliveryService.MessagePrefix + record.TokenHash);
        check(!message.PayloadJson.Contains(invitation.RawToken) && !message.PayloadJson.Contains(user.Email!) && record.DeliveryPending && sender.Attempts == 0,
            "Token creation must atomically queue an encrypted invitation without calling SMTP.");
        check((await InvitationStatusQuery.ReadAsync(db, [user.Id]))[user.Id].State == "Queued", "Queued delivery must be visible through the status query.");
        try { await accounts.CreateActivationAsync(user); check(false, "Rapid invitation creation must be throttled."); }
        catch (InvalidOperationException) { check(true, "Duplicate submissions must not rotate the pending token."); }
        check(await db.AccountActivations.CountAsync(a => a.UserId == user.Id) == 1, "Duplicate submissions must preserve one invitation.");
        check(!await accounts.SendActivationAsync(user, "https://attacker.invalid/steal"), "Failed SMTP must report pending delivery.");
        message = await db.OutboxMessages.SingleAsync(m => m.Id == message.Id);
        check(message.AttemptCount == 1 && message.NextAttemptUtc > DateTime.UtcNow && !message.IsDeadLetter &&
            !message.LastError!.Contains("sensitive-provider-message"), "Failure must schedule a durable retry without persisting provider message content.");
        check((await InvitationStatusQuery.ReadAsync(db, [user.Id]))[user.Id].State == "Retry scheduled", "Failed delivery must show a retry state rather than a false sent state.");
        message.NextAttemptUtc = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        check(await Delivery(db).TrySendAsync(message.Id), "A fresh service instance must deliver the protected queued invitation after a retry.");
        check(sender.LastHtml!.Contains("https://campus.example.invalid/Account/Activate") && !sender.LastHtml.Contains("attacker.invalid"),
            "Email links must use the configured public origin rather than the submitted request URL.");
        var attempts = sender.Attempts;
        check(await delivery.TrySendAsync(message.Id) && sender.Attempts == attempts, "Completed jobs must not send duplicate email on repeated dispatch.");
        record = await db.AccountActivations.SingleAsync(a => a.UserId == user.Id);
        check(!record.DeliveryPending && record.SendCount == 1 && record.LastSentUtc.HasValue &&
            await db.AuditLogs.AnyAsync(a => a.UserId == user.Id && a.Action == "Delivered account activation invitation"), "Successful delivery must update status and retain a safe audit event.");
        check((await InvitationStatusQuery.ReadAsync(db, [user.Id]))[user.Id].State == "Delivered", "Successful delivery must appear in status UI data.");
        check((await accounts.ActivateAsync(user.Email!, invitation.RawToken, "Invitation!" + Guid.NewGuid().ToString("N") + "1a")).Succeeded,
            "A delivered token must activate its account transactionally.");
        check(!(await accounts.ActivateAsync(user.Email!, invitation.RawToken, "Another!Password123")).Succeeded, "Activation tokens must be single-use.");

        var expiredUser = await Account(81);
        var expired = await accounts.CreateActivationAsync(expiredUser);
        var expiredRecord = await db.AccountActivations.SingleAsync(a => a.UserId == expiredUser.Id);
        expiredRecord.ExpiresUtc = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        var expiredId = await db.OutboxMessages.Where(m => m.Type == ActivationDeliveryService.MessagePrefix + expiredRecord.TokenHash).Select(m => m.Id).SingleAsync();
        attempts = sender.Attempts;
        check(!await delivery.TrySendAsync(expiredId) && sender.Attempts == attempts && !(await accounts.ActivateAsync(expiredUser.Email!, expired.RawToken, "Expired!Password123")).Succeeded,
            "Expired invitations must neither send nor activate.");

        var leasedUser = await Account(82); await accounts.CreateActivationAsync(leasedUser);
        var leasedHash = await db.AccountActivations.Where(a => a.UserId == leasedUser.Id).Select(a => a.TokenHash).SingleAsync();
        var leased = await db.OutboxMessages.SingleAsync(m => m.Type == ActivationDeliveryService.MessagePrefix + leasedHash);
        leased.ClaimOwner = "another-worker"; leased.ClaimExpiresUtc = DateTime.UtcNow.AddMinutes(1); await db.SaveChangesAsync();
        check(!await delivery.TrySendAsync(leased.Id), "An active worker lease must prevent a second dispatch.");
        leased.ClaimExpiresUtc = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        check(await delivery.TrySendAsync(leased.Id), "A crashed worker's expired lease must be recoverable.");

        var queuedUser = await Account(83); var queued = await accounts.CreateActivationAsync(queuedUser);
        var services = new ServiceCollection(); services.AddLogging(); services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connection));
        services.AddIdentityCore<ApplicationUser>(options => options.Password.RequiredLength = 10)
            .AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        services.AddScoped<IStudentAccountService, StudentAccountService>();
        services.AddSingleton<IApplicationEmailSender>(sender); services.AddSingleton<IDataProtectionProvider>(protection);
        services.AddSingleton<IHostEnvironment>(new FixtureEnvironment()); services.AddHttpContextAccessor();
        services.Configure<ActivationDeliveryOptions>(o => o.PublicOrigin = "https://campus.example.invalid"); services.AddScoped<ActivationDeliveryService>();
        using var provider = services.BuildServiceProvider();
        var dispatcher = new OutboxDispatcher(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<OutboxDispatcher>.Instance);
        var dispatch = typeof(OutboxDispatcher).GetMethod("DispatchBatchAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        await (Task)dispatch.Invoke(dispatcher, [CancellationToken.None])!;
        check(!await db.AccountActivations.AsNoTracking().Where(a => a.UserId == queuedUser.Id).Select(a => a.DeliveryPending).SingleAsync(),
            "The real background dispatcher must deliver a token even when the request stopped after token creation.");
        check(sender.LastHtml!.Contains(queued.RawToken), "Worker retries must retain the original one-time token.");

        var recoveredUser = await Account(84);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            AccountActivationRequests.Enqueue(db, recoveredUser.Id);
            AccountActivationRequests.Enqueue(db, recoveredUser.Id); // Duplicate recovery intent must be harmless.
            await db.SaveChangesAsync(); await transaction.CommitAsync();
        }
        // Simulate losing the initiating request before it creates a token.
        check((await InvitationStatusQuery.ReadAsync(db, [recoveredUser.Id]))[recoveredUser.Id].State == "Queued", "A queued recovery request must be visible before token creation.");
        await (Task)dispatch.Invoke(dispatcher, [CancellationToken.None])!;
        check(await db.AccountActivations.AsNoTracking().CountAsync(a => a.UserId == recoveredUser.Id) == 1,
            "The dispatcher must recover token creation after an account-commit interruption without duplicating tokens.");
        var recoveryJobs = await db.OutboxMessages.AsNoTracking().Where(m => m.Type == AccountActivationRequests.MessageType && m.PayloadJson.Contains(recoveredUser.Id)).ToListAsync();
        check(recoveryJobs.Count == 2 && recoveryJobs.All(m => m.ProcessedUtc != null), "Duplicate account recovery jobs must be acknowledged after one protected invitation is queued.");
        await (Task)dispatch.Invoke(dispatcher, [CancellationToken.None])!;
        check(await db.AccountActivations.AsNoTracking().AnyAsync(a => a.UserId == recoveredUser.Id && !a.DeliveryPending && a.SendCount == 1),
            "Recovered invitations must pass through real background delivery.");
        string rolledBackId;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var abandoned = await Account(85); rolledBackId = abandoned.Id;
            AccountActivationRequests.Enqueue(db, abandoned.Id); await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        check(!await db.Users.AnyAsync(u => u.Id == rolledBackId) && !await db.OutboxMessages.AnyAsync(m => m.Type == AccountActivationRequests.MessageType && m.PayloadJson.Contains(rolledBackId)),
            "Rolling back account creation must also roll back its invitation recovery intent.");
    }

    private sealed class FixtureEmail : IApplicationEmailSender
    {
        public int Failures { get; set; }
        public int Attempts { get; private set; }
        public string? LastHtml { get; private set; }
        public Task SendAsync(string email, string subject, string htmlMessage, CancellationToken cancellationToken = default)
        {
            Attempts++; if (Failures-- > 0) throw new InvalidOperationException("sensitive-provider-message");
            LastHtml = htmlMessage; return Task.CompletedTask;
        }
    }
    private sealed class FixtureEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "EduFlow test";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
