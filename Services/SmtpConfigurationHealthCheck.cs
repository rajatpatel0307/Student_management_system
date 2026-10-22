using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Student_Management_System.Services;

public sealed class SmtpConfigurationHealthCheck : IHealthCheck
{
    private readonly SmtpOptions _options;
    public SmtpConfigurationHealthCheck(IOptions<SmtpOptions> options) =>
        _options = options.Value;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var available = _options.Enabled &&
                        !string.IsNullOrWhiteSpace(_options.Host) &&
                        !string.IsNullOrWhiteSpace(_options.User) &&
                        !string.IsNullOrWhiteSpace(_options.Password) &&
                        _options.Port == 587 &&
                        _options.UseStartTls;
        return Task.FromResult(available
            ? HealthCheckResult.Healthy("SMTP configuration is available.")
            : HealthCheckResult.Degraded("SMTP configuration is unavailable."));
    }
}
