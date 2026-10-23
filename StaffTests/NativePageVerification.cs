using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Student_Management_System.Controllers;
using Student_Management_System.Models;
using Student_Management_System.Services;

namespace StaffUiTests;

// The test host uses only the disposable integration database. Its authentication
// handler is registered here, never in the real application or its startup path.
public static class NativePageVerification
{
    public static async Task RunAsync(string connection, string adminId, string librarianId, Action<bool, string> check)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = Directory.GetCurrentDirectory(), EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connection));
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        builder.Services.AddScoped<StaffAccessService>(); builder.Services.AddScoped<PermissionService>();
        builder.Services.AddScoped<StaffRepository>(); builder.Services.AddScoped<StaffManagementService>();
        builder.Services.AddScoped<AuditService>(); builder.Services.AddScoped<ICampusScopeService, CampusScopeService>();
        builder.Services.AddSingleton<IPrivateFileStorage, NoTestUploads>();
        var identity = new PageTestIdentity { UserId = adminId, Role = "Admin" };
        builder.Services.AddSingleton(identity);
        builder.Services.AddAuthentication("StaffPageTest").AddScheme<AuthenticationSchemeOptions, PageTestAuthentication>("StaffPageTest", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(StaffController).Assembly);
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }) { BaseAddress = new Uri(app.Urls.Single()) };
            var directory = await client.GetAsync("/Staff"); var html = await directory.Content.ReadAsStringAsync();
            check(directory.IsSuccessStatusCode && html.Contains("Staff Management") && html.Contains("/Staff/Create"), "The authenticated staff directory must render its real Razor view and add-staff link.");
            check(!html.Contains("/staff-assets/staff.js") && !html.Contains("id=\"root\""), "Staff pages must not depend on the removed frontend application shell.");
            var form = await client.GetStringAsync("/Staff/Create");
            check(form.Contains("Input.FullName") && form.Contains("Input.ConfirmPassword") && form.Contains("/Staff/Save"), "The native add-staff form must render its server-bound fields and submit route.");
            check(form.Contains("/css/site.css") && form.Contains("bootstrap"), "Staff pages must use the existing site styles and Bootstrap layout.");
            foreach (var workspace in StaffWorkspaceCatalog.All)
            {
                var response = await client.GetAsync($"/Staff/Workspace/{workspace.Key}");
                var dashboard = await response.Content.ReadAsStringAsync();
                check(response.IsSuccessStatusCode && dashboard.Contains($"data-workspace=\"{workspace.Key}\"") && dashboard.Contains("/js/staff-native.js"), "Each staff dashboard must render through the shared Razor layout.");
                var rolePage = await client.GetAsync($"/Staff/Workspace/{workspace.Key}/{workspace.Pages[0].Slug}");
                check(rolePage.IsSuccessStatusCode, "Every role's first operational page must be reachable by a normal server route.");
            }
            identity.UserId = librarianId; identity.Role = "Librarian";
            check((int)(await client.GetAsync("/Staff")).StatusCode == 403, "Native staff administration must remain administrator-only.");
            check((int)(await client.GetAsync("/Staff/Workspace/accountant")).StatusCode == 403, "A librarian must not render the accountant workspace through direct navigation.");
            check((await client.GetAsync("/Staff/Workspace/librarian/books")).IsSuccessStatusCode, "A librarian must be able to open their native book management page.");
        }
        finally { await app.StopAsync(); }
    }
}

public sealed class PageTestIdentity
{
    public string UserId { get; set; } = "";
    public string Role { get; set; } = "";
}
public sealed class PageTestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, PageTestIdentity identity)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, identity.UserId), new Claim(ClaimTypes.Name, "Integration test user"), new Claim(ClaimTypes.Role, identity.Role)], Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
public sealed class NoTestUploads : IPrivateFileStorage
{
    public Task<StoredPrivateFile> SaveStudyMaterialAsync(IFormFile file, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<StoredPrivateFile> SaveStudentPhotoAsync(IFormFile file, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<StoredPrivateFile> SaveProfileImageAsync(IFormFile file, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<StoredPrivateFile> SaveAdmissionDocumentAsync(IFormFile file, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<StoredPrivateFile> SaveAssignmentAttachmentAsync(IFormFile file, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<OpenedPrivateFile?> OpenReadAsync(string? storageKey, PrivateFileKind kind, CancellationToken cancellationToken = default) => Task.FromResult<OpenedPrivateFile?>(null);
    public Task DeleteAsync(string? storageKey, PrivateFileKind kind, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task MigrateLegacyFilesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
