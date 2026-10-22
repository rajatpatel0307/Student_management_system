using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace Student_Management_System.Services;

public sealed class EmailSender : IEmailSender, IApplicationEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IOptions<SmtpOptions> options, ILogger<EmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage) =>
        SendAsync(email, subject, htmlMessage);

    public async Task SendAsync(
        string email,
        string subject,
        string htmlMessage,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Email delivery is unavailable.");
        if (!MailAddress.TryCreate(email, out var recipient))
            throw new ArgumentException("A valid recipient address is required.", nameof(email));

        using var smtp = new SmtpClient(_options.Host, _options.Port)
        {
            Credentials = new NetworkCredential(_options.User, _options.Password),
            EnableSsl = _options.UseStartTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Timeout = checked(_options.TimeoutSeconds * 1000)
        };
        using var mail = new MailMessage
        {
            From = new MailAddress(_options.User),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };
        mail.To.Add(recipient);

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(_options.TimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeout.Token);
        try
        {
            await smtp.SendMailAsync(mail).WaitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (
            timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("SMTP delivery timed out.");
            throw new TimeoutException("Email delivery timed out.");
        }
    }
}
