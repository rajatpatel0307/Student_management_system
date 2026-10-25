using Microsoft.Extensions.Options;

namespace Student_Management_System.Services;

public sealed class ActivationDeliveryOptionsValidator(IHostEnvironment environment) : IValidateOptions<ActivationDeliveryOptions>
{
    public ValidateOptionsResult Validate(string? name, ActivationDeliveryOptions options)
    {
        if (environment.IsDevelopment() && string.IsNullOrWhiteSpace(options.PublicOrigin))
            return ValidateOptionsResult.Success; // Local requests may supply a loopback origin.
        return IsTrustedOrigin(options.PublicOrigin, environment.IsDevelopment())
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("ActivationDelivery:PublicOrigin must be a trusted HTTPS application URL without credentials, query or fragment.");
    }

    public static bool IsTrustedOrigin(string? origin, bool development) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
        (uri.Scheme == Uri.UriSchemeHttps || (development && uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp));
}
