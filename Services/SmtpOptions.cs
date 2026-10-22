using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Student_Management_System.Services;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";
    public bool Enabled { get; set; }
    [MaxLength(253)] public string Host { get; set; } = "";
    [Range(1, 65535)] public int Port { get; set; } = 587;
    // Email validity is conditional on Enabled and is therefore enforced by
    // SmtpOptionsValidator. DataAnnotations validation runs before the custom
    // validator and must not reject an unused Development placeholder.
    [MaxLength(256)] public string User { get; set; } = "";
    [MaxLength(512)] public string Password { get; set; } = "";
    public bool UseStartTls { get; set; } = true;
    [Range(5, 120)] public int TimeoutSeconds { get; set; } = 30;
}

public sealed class SmtpOptionsValidator : IValidateOptions<SmtpOptions>
{
    private readonly IWebHostEnvironment _environment;
    public SmtpOptionsValidator(IWebHostEnvironment environment) =>
        _environment = environment;

    public ValidateOptionsResult Validate(string? name, SmtpOptions options)
    {
        if (!options.Enabled)
            return _environment.IsProduction()
                ? ValidateOptionsResult.Fail("SMTP must be enabled in Production.")
                : ValidateOptionsResult.Success;
        if (string.IsNullOrWhiteSpace(options.Host) ||
            string.IsNullOrWhiteSpace(options.User) ||
            string.IsNullOrWhiteSpace(options.Password))
            return ValidateOptionsResult.Fail(
                "Enabled SMTP requires Host, User, and Password from protected configuration.");
        if (!new EmailAddressAttribute().IsValid(options.User))
            return ValidateOptionsResult.Fail(
                "Enabled SMTP requires a valid sender email address.");
        if (options.Port != 587 || !options.UseStartTls)
            return ValidateOptionsResult.Fail("SMTP must use port 587 with STARTTLS.");
        return ValidateOptionsResult.Success;
    }
}

public interface IApplicationEmailSender
{
    Task SendAsync(
        string email,
        string subject,
        string htmlMessage,
        CancellationToken cancellationToken = default);
}
