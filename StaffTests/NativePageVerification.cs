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
using Student_Management_System.Security;

namespace StaffUiTests;

// The test host uses only the disposable integration database. Its authentication
// handler is registered here, never in the real application or its startup path.
public static class NativePageVerification
{
    public static async Task RunAsync(string connection, string adminId, string librarianId, Action<bool, string> check, int studentId)
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
        builder.Services.AddSingleton<IStudentAccountService, NoPageAccountChanges>();
        builder.Services.AddScoped<IResultService, ResultService>();
        builder.Services.AddScoped<IAttendanceService, AttendanceService>();
        builder.Services.AddScoped<IFinanceLedgerService, FinanceLedgerService>();
        builder.Services.AddScoped<IInvoiceService, InvoiceService>();
        builder.Services.AddScoped<IReportingService, ReportingService>();
        var identity = new PageTestIdentity { UserId = adminId, Role = "Admin" };
        builder.Services.AddSingleton(identity);
        builder.Services.AddAuthentication("StaffPageTest").AddScheme<AuthenticationSchemeOptions, PageTestAuthentication>("StaffPageTest", _ => { });
        builder.Services.AddEduFlowAuthorization();
        builder.Services.AddControllersWithViews(options => options.Filters.Add<AdminPermissionParityFilter>()).AddApplicationPart(typeof(StaffController).Assembly);
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }) { BaseAddress = new Uri(app.Urls.Single()) };
            await PermissionMatrixVerification.RunAsync(app.Services, client, identity, adminId, check);
            var directory = await client.GetAsync("/Staff"); var html = await directory.Content.ReadAsStringAsync();
            check(directory.IsSuccessStatusCode && html.Contains("Staff Management") && html.Contains("/Staff/Create"), "The authenticated staff directory must render its real Razor view and add-staff link.");
            var sidebar = html.IndexOf("<nav class=\"sidebar-nav\">", StringComparison.Ordinal);
            var manageStaff = sidebar < 0 ? -1 : html.IndexOf("title=\"Manage staff\"", sidebar, StringComparison.Ordinal);
            check(sidebar >= 0 && manageStaff > sidebar && html.IndexOf("<span>Manage staff</span>", manageStaff, StringComparison.Ordinal) > manageStaff, "The administrator sidebar must expose the Manage staff menu item.");
            identity.Role = "SuperAdmin";
            var admissions = await client.GetStringAsync("/Admissions/Index?page=26&pageSize=10&search=Applicant");
            check(admissions.Contains("Record pages") && admissions.Contains("251 applications") && admissions.Contains("name=\"pageSize\""),
                "The admissions register must render its real pagination, count and page-size controls.");
            var guardians = await client.GetStringAsync("/Guardians");
            check(guardians.Contains("Link an existing guardian") && guardians.Contains("/Guardians/Unlink/") && guardians.Contains("guardianSearch"),
                "The guardian directory must render real relationship actions, search and pagination.");
            var reportCenter = await client.GetStringAsync("/Reports?reportType=Finance");
            check(reportCenter.Contains("SCOPE-OWN") && reportCenter.Contains("SCOPE-FOREIGN"), "The real SuperAdmin finance report must render persisted rows from both campuses.");
            var invoices = await client.GetStringAsync("/Finance/Invoices");
            check(invoices.Contains("SCOPE-INVOICE") && invoices.Contains("Record pages") && invoices.Contains("issueInvoiceHeading"), "The nonempty invoice view must render persisted invoices, shared pagination and permitted issuance controls.");
            using (var admissionScope = app.Services.CreateScope())
            {
                var fixtureDb = admissionScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var application = await fixtureDb.AdmissionApplications.OrderBy(a => a.Id).FirstAsync();
                application.EnrolledStudentId = studentId; application.Stage = AdmissionStage.Enrolled;
                await fixtureDb.SaveChangesAsync();
                var detail = await client.GetStringAsync($"/Admissions/Details/{application.Id}");
                check(detail.Contains("Account invitation") && detail.Contains("role=\"status\""), "Enrolled admission details must render the shared real invitation status panel.");
            }
            var sessions = await client.GetStringAsync("/Attendance/Sessions");
            check(sessions.Contains("Scope") && sessions.Contains("Record pages"), "The actual attendance-session view must render its persisted roster and shared pager.");
            var profile = await client.GetStringAsync($"/Students/Details/{studentId}?attendancePage=2&marksPage=2");
            check(profile.Contains("Attendance history pages") && profile.Contains("Marks history pages") && profile.Contains("#attendance") && profile.Contains("marksPage=2"),
                "The actual student profile must render independent accessible history pagers that preserve the other page selection.");
            identity.UserId = "scope-hod"; identity.Role = "HOD";
            var marks = await client.GetStringAsync("/Mark/Create");
            check(marks.Contains("scope-local") && !marks.Contains("scope-foreign") && marks.Contains("ExpectedUpdatedUtc"),
                "The rendered HOD marks roster must enforce campus scope and include original revisions.");
            await CheckInlineScripts(marks, "marks", check);
            identity.UserId = adminId; identity.Role = "Professor";
            using (var fixtureScope = app.Services.CreateScope())
            {
                var fixtureDb = fixtureScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var classId = await fixtureDb.Students.Where(student => student.Id == studentId).Select(student => student.ClassId).SingleAsync();
                var attendance = await client.GetStringAsync($"/Attendance/Mark?classId={classId}");
                check(attendance.Contains("Test Resident") && !attendance.Contains("Archive fixture") && attendance.Contains("name=\"lecture\""),
                    "Attendance must render the active roster and exclude archived students that its submission service rejects.");
                await CheckInlineScripts(attendance, "attendance", check);
            }
            identity.UserId = adminId; identity.Role = "SuperAdmin";
            var superAdminDirectory = await client.GetStringAsync("/Staff");
            check(superAdminDirectory.Contains("title=\"Manage staff\""), "The Super Admin sidebar must also expose the Manage staff menu item.");
            identity.Role = "Admin";
            check(!html.Contains("/staff-assets/staff.js") && !html.Contains("id=\"root\""), "Staff pages must not depend on the removed frontend application shell.");
            var form = await client.GetStringAsync("/Staff/Create");
            check(form.Contains("Input.FullName") && form.Contains("Input.ConfirmPassword") && form.Contains("/Staff/Save"), "The native add-staff form must render its server-bound fields and submit route.");
            check(form.Contains("Generated automatically when saved") && !form.Contains("name=\"Input.EmployeeId\"") && !form.Contains("name=\"Input.Username\""), "The staff form must explain automatic identifiers without asking administrators to enter them.");
            check(form.Contains("At least 10 characters") && form.Contains("minlength=\"10\"") && !form.Contains("minlength=\"12\""), "The staff form must display and validate the ten-character password minimum.");
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
            var librarianPage = await client.GetStringAsync("/Staff/Workspace/librarian/books");
            check(!librarianPage.Contains("title=\"Manage staff\""), "Staff-only accounts must not see the administrator's Manage staff menu.");
            check((int)(await client.GetAsync("/Staff")).StatusCode == 403, "Native staff administration must remain administrator-only.");
            check((int)(await client.GetAsync("/Staff/Workspace/accountant")).StatusCode == 403, "A librarian must not render the accountant workspace through direct navigation.");
            check((await client.GetAsync("/Staff/Workspace/librarian/books")).IsSuccessStatusCode, "A librarian must be able to open their native book management page.");
        }
        finally { await app.StopAsync(); }
    }

    private static async Task CheckInlineScripts(string html, string page, Action<bool, string> check)
    {
        var directory = Path.GetFullPath(Path.Combine(".build", "rendered-script-checks"));
        Directory.CreateDirectory(directory);
        var index = 0;
        foreach (System.Text.RegularExpressions.Match script in System.Text.RegularExpressions.Regex.Matches(html, "<script[^>]*>([\\s\\S]*?)</script>"))
        {
            if (string.IsNullOrWhiteSpace(script.Groups[1].Value)) continue;
            var path = Path.Combine(directory, $"{page}-{index++}.js");
            await File.WriteAllTextAsync(path, script.Groups[1].Value);
            var start = new System.Diagnostics.ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            start.ArgumentList.Add("--check"); start.ArgumentList.Add(path);
            using var process = System.Diagnostics.Process.Start(start)!;
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            check(process.ExitCode == 0, $"The actual rendered {page} inline script must pass node --check: {error}");
        }
        check(index > 0, $"The {page} page must emit its real interaction script for syntax checking.");
    }
}

public sealed class PageTestIdentity
{
    public bool Authenticated { get; set; } = true;
    public string UserId { get; set; } = "";
    public string Role { get; set; } = "";
}
public sealed class PageTestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, PageTestIdentity identity)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!identity.Authenticated) return Task.FromResult(AuthenticateResult.NoResult());
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, identity.UserId), new Claim(ClaimTypes.Name, "Integration test user"), new Claim(ClaimTypes.Role, identity.Role)], Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
public sealed class NoPageAccountChanges : IStudentAccountService
{
    public Task<ActivationInvitation> CreateActivationAsync(ApplicationUser user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> SendActivationAsync(ApplicationUser user, string url, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IdentityResult> ActivateAsync(string email, string token, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
