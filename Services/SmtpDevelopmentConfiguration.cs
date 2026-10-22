namespace Student_Management_System.Services;

public static class SmtpDevelopmentConfiguration
{
    /// <summary>
    /// A checked-in example placeholder or an old placeholder in User Secrets
    /// means SMTP has not actually been configured. Development can run with
    /// email delivery disabled; Production must continue to fail validation.
    /// </summary>
    public static void DisableIncompletePlaceholderConfiguration(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (!environment.IsDevelopment())
            return;

        var configuredUser = configuration["Smtp:User"];
        if (!IsPlaceholder(configuredUser))
            return;

        configuration["Smtp:Enabled"] = "false";
        configuration["Smtp:User"] = "";
        configuration["Smtp:Password"] = "";
    }

    private static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var candidate = value.Trim();
        return candidate.Length >= 3 &&
               candidate[0] == '<' &&
               candidate[^1] == '>';
    }
}
