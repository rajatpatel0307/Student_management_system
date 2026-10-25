using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Student_Management_System.Services;

internal static class ProductionConfigurationVerification
{
    public static void Run(Action<bool, string> check)
    {
        var production = new ActivationDeliveryOptionsValidator(new FixtureEnvironment { EnvironmentName = "Production" });
        foreach (var origin in new[] { "", "http://campus.invalid", "https://user:password@campus.invalid", "https://campus.invalid/?token=secret", "https://campus.invalid/#fragment", "javascript:alert(1)", "//campus.invalid" })
            check(production.Validate(null, new ActivationDeliveryOptions { PublicOrigin = origin }).Failed, "Unsafe activation origin must fail production startup validation.");
        check(production.Validate(null, new ActivationDeliveryOptions { PublicOrigin = "https://campus.invalid/school" }).Succeeded, "A trusted HTTPS application base path must be supported.");
        var development = new ActivationDeliveryOptionsValidator(new FixtureEnvironment { EnvironmentName = "Development" });
        check(development.Validate(null, new ActivationDeliveryOptions()).Succeeded && development.Validate(null, new ActivationDeliveryOptions { PublicOrigin = "http://127.0.0.1:5050" }).Succeeded,
            "Local development must preserve loopback origin support.");
        check(development.Validate(null, new ActivationDeliveryOptions { PublicOrigin = "http://external.invalid" }).Failed, "Development must not allow unencrypted remote activation origins.");
    }
    private sealed class FixtureEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Configuration tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
