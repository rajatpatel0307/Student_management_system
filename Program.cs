using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using Student_Management_System.Data;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Student_Management_System.Security;

var processFailureLog = Path.Combine(AppContext.BaseDirectory, "startup-failure.log");
AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
{
    try
    {
        var exceptionType = eventArgs.ExceptionObject?.GetType().FullName ?? "UnknownFailure";
        File.AppendAllText(
            processFailureLog,
            $"{DateTime.UtcNow:O} Startup terminated ({exceptionType}). Correlation details are available in the protected observability system.{Environment.NewLine}");
    }
    catch
    {
        // Never replace the original startup exception with a logging failure.
    }
};

var builder = WebApplication.CreateBuilder(args);

// Local development must not depend on permission to write Windows Event Log.
// Keep console/debug logging and the production logging configuration intact.
if (builder.Environment.IsDevelopment() && OperatingSystem.IsWindows())
    builder.Logging.AddFilter<Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider>(
        (_, _) => false);

// Support deployment-standard SMTP_* variables while retaining the normal
// ASP.NET Core Smtp:* / Smtp__* configuration hierarchy and User Secrets.
var smtpHostEnvironment = Environment.GetEnvironmentVariable("SMTP_HOST");
var smtpEnvironmentOverrides = new Dictionary<string, string?>
{
    ["Smtp:Enabled"] = Environment.GetEnvironmentVariable("SMTP_ENABLED") ??
        (string.IsNullOrWhiteSpace(smtpHostEnvironment) ? null : "true"),
    ["Smtp:Host"] = smtpHostEnvironment,
    ["Smtp:Port"] = Environment.GetEnvironmentVariable("SMTP_PORT"),
    ["Smtp:User"] = Environment.GetEnvironmentVariable("SMTP_USER"),
    ["Smtp:Password"] = Environment.GetEnvironmentVariable("SMTP_PASS"),
    ["Smtp:UseStartTls"] = Environment.GetEnvironmentVariable("SMTP_USE_STARTTLS")
};
builder.Configuration.AddInMemoryCollection(
    smtpEnvironmentOverrides.Where(item => item.Value != null));
SmtpDevelopmentConfiguration.DisableIncompletePlaceholderConfiguration(
    builder.Configuration,
    builder.Environment);

// 🔹 Services
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<ConcurrencyExceptionFilter>();
    options.Filters.Add<DashboardSetupExceptionFilter>();
    options.Filters.Add<AdminPermissionParityFilter>();
    options.Filters.Add<OverpostingGuardFilter>();
});
builder.Services.AddLocalization();
builder.Services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(options =>
{
    var cultures = new[] { new CultureInfo("en-IN"), new CultureInfo("hi-IN") };
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en-IN");
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
});
builder.Services.AddTransient<EmailSender>();
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender>(
    services => services.GetRequiredService<EmailSender>());
builder.Services.AddTransient<IApplicationEmailSender>(
    services => services.GetRequiredService<EmailSender>());
builder.Services.AddSingleton<IValidateOptions<SmtpOptions>, SmtpOptionsValidator>();
builder.Services.AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<IAuthorizationHandler, GranularPermissionHandler>();
builder.Services.AddScoped<ICampusScopeService, CampusScopeService>();
builder.Services.AddScoped<IResultService, ResultService>();
builder.Services.AddScoped<IFinanceLedgerService, FinanceLedgerService>();
builder.Services.AddScoped<IStudentAccountService, StudentAccountService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IAcademicStructureService, AcademicStructureService>();
builder.Services.AddScoped<IApprovalWorkflowService, ApprovalWorkflowService>();
builder.Services.AddScoped<IApprovalTargetValidator, ApprovalTargetValidator>();
builder.Services.AddScoped<ICommunicationService, CommunicationService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IPromotionService, PromotionService>();
builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped<IDashboardAnalyticsService, DashboardAnalyticsService>();
builder.Services.AddScoped<ITimetableService, TimetableService>();
builder.Services.AddScoped<IHodProfessorService, HodProfessorService>();
builder.Services.AddScoped<IProfileManagementAuthorizationService, ProfileManagementAuthorizationService>();
builder.Services.Configure<SmsGatewayOptions>(
    builder.Configuration.GetSection("SmsGateway"));
builder.Services.AddHttpClient<ISmsGateway, HttpSmsGateway>();
builder.Services.AddHostedService<CommunicationDispatcher>();
builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddScoped<AdminPermissionParityFilter>();
builder.Services.AddScoped<OverpostingGuardFilter>();
builder.Services.Configure<PrivateFileStorageOptions>(
    builder.Configuration.GetSection("PrivateUploads"));
builder.Services.Configure<UploadMalwareScannerOptions>(
    builder.Configuration.GetSection("UploadMalwareScanner"));
if (builder.Environment.IsProduction() &&
    !builder.Configuration.GetValue<bool>("UploadMalwareScanner:Enabled"))
    throw new InvalidOperationException(
        "UploadMalwareScanner:Enabled must be true outside Development.");
builder.Services.AddSingleton<IUploadMalwareScanner, CommandUploadMalwareScanner>();
builder.Services.AddSingleton<IPrivateFileStorage, PrivateFileStorage>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<SmtpConfigurationHealthCheck>("smtp-configuration");
var keyDirectory = builder.Configuration["DataProtection:KeyDirectory"];
if (builder.Environment.IsProduction() &&
    (string.IsNullOrWhiteSpace(keyDirectory) || !Path.IsPathRooted(keyDirectory)))
    throw new InvalidOperationException(
        "DataProtection:KeyDirectory must be an absolute durable shared path outside Development.");
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("EduFlow");
if (!string.IsNullOrWhiteSpace(keyDirectory))
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(keyDirectory)));
var keyCertificateThumbprint =
    builder.Configuration["DataProtection:CertificateThumbprint"];
if (builder.Environment.IsProduction())
{
    if (string.IsNullOrWhiteSpace(keyCertificateThumbprint))
        throw new InvalidOperationException(
            "DataProtection:CertificateThumbprint must protect shared keys at rest outside Development.");
    using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
    store.Open(OpenFlags.ReadOnly);
    var certificate = store.Certificates.Find(
        X509FindType.FindByThumbprint, keyCertificateThumbprint, validOnly: true)
        .OfType<X509Certificate2>()
        .SingleOrDefault()
        ?? throw new InvalidOperationException(
            "The configured data-protection certificate was not found or is invalid.");
    dataProtection.ProtectKeysWithCertificate(certificate);
}
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        if (System.Net.IPAddress.TryParse(proxy, out var address)) options.KnownProxies.Add(address);
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("authentication", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));
    options.AddPolicy("account-recovery", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));
    options.AddPolicy("profile-image", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0
            }));
});

var defaultConnection =
    builder.Configuration.GetConnectionString("DefaultConnection");

// A local Windows SQL Express installation can authenticate the developer's
// Windows account without storing any database password. This fallback is
// Development-only, explicitly configurable, and never applies in production.
if (string.IsNullOrWhiteSpace(defaultConnection) &&
    builder.Environment.IsDevelopment() &&
    OperatingSystem.IsWindows() &&
    builder.Configuration.GetValue<bool>(
        "Database:UseWindowsIntegratedSecurityFallback"))
{
    var developmentDatabase = builder.Configuration[
        "Database:LocalSqlExpressDatabase"];
    if (string.IsNullOrWhiteSpace(developmentDatabase))
        developmentDatabase = "StudentDB";

    defaultConnection = new SqlConnectionStringBuilder
    {
        DataSource = $@"{Environment.MachineName}\SQLEXPRESS",
        InitialCatalog = developmentDatabase,
        IntegratedSecurity = true,
        Encrypt = true,
        TrustServerCertificate = true,
        MultipleActiveResultSets = true,
        Pooling = true,
        ConnectTimeout = 30,
        ConnectRetryCount = 3,
        ConnectRetryInterval = 5
    }.ConnectionString;

    builder.Configuration["ConnectionStrings:DefaultConnection"] =
        defaultConnection;
}

if (string.IsNullOrWhiteSpace(defaultConnection))
    throw new InvalidOperationException(
        builder.Environment.IsDevelopment()
            ? "Local SQL configuration is missing. Windows integrated SQL Express fallback was unavailable or disabled. Close the debugger, run Start-EduFlow.cmd from the solution folder, enter rotated database credentials, and then restart Visual Studio."
            : "ConnectionStrings:DefaultConnection must be supplied by the protected deployment environment or secret manager.");
SqlConnectionStringBuilder connectionSettings;
try
{
    connectionSettings = new SqlConnectionStringBuilder(defaultConnection);
}
catch (ArgumentException)
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is invalid. Verify protected configuration.");
}
connectionSettings.Pooling = true;
if (connectionSettings.ConnectTimeout < 30)
    connectionSettings.ConnectTimeout = 30;
if (connectionSettings.ConnectRetryCount < 3)
    connectionSettings.ConnectRetryCount = 3;
if (connectionSettings.ConnectRetryInterval < 5)
    connectionSettings.ConnectRetryInterval = 5;
if (!connectionSettings.Encrypt)
    throw new InvalidOperationException("SQL Server encryption must be enabled.");
var localSqlExpress = connectionSettings.DataSource.Contains(
        "\\SQLEXPRESS", StringComparison.OrdinalIgnoreCase) ||
    connectionSettings.DataSource.StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase) ||
    connectionSettings.DataSource.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) ||
    connectionSettings.DataSource.StartsWith(".", StringComparison.Ordinal);
if (connectionSettings.TrustServerCertificate &&
    (!builder.Environment.IsDevelopment() || !localSqlExpress))
    throw new InvalidOperationException(
        "TrustServerCertificate is permitted only for local SQL Express Development.");
var commandTimeoutSeconds =
    builder.Configuration.GetValue("Database:CommandTimeoutSeconds", 45);
if (commandTimeoutSeconds is < 30 or > 60)
    throw new InvalidOperationException(
        "Database:CommandTimeoutSeconds must be between 30 and 60.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        connectionSettings.ConnectionString,
        sqlOptions =>
        {
            sqlOptions.CommandTimeout(commandTimeoutSeconds);
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }));
// 🔐 Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 10;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = true;
});
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    // Role, password, MFA and active-account changes revoke live sessions quickly.
    options.ValidationInterval = TimeSpan.FromMinutes(1);
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminMfaOnly", policy =>
        policy.RequireRole("SuperAdmin", "Admin"))
    .AddPolicy("ConfigurationHealth", policy =>
        policy.RequireRole("SuperAdmin"))
    .AddPolicy(ProfilePolicies.ImageEditSelf, policy =>
        policy.AddRequirements(new PermissionRequirement(PermissionKeys.ProfileImageEditSelf)))
    .AddPolicy(ProfilePolicies.StudentEdit, policy =>
        policy.AddRequirements(new PermissionRequirement(PermissionKeys.ProfileStudentEdit)))
    .AddPolicy(ProfilePolicies.ProfessorEdit, policy =>
        policy.AddRequirements(new PermissionRequirement(PermissionKeys.ProfileProfessorEdit)))
    .AddPolicy(ProfilePolicies.HodEdit, policy =>
        policy.AddRequirements(new PermissionRequirement(PermissionKeys.ProfileHodEdit)))
    .AddPolicy(ProfilePolicies.OperationalStaffEdit, policy =>
        policy.AddRequirements(new PermissionRequirement(PermissionKeys.ProfileOperationalStaffEdit)))
    .AddPolicy(ProfilePolicies.AdminEdit, policy =>
        policy.AddRequirements(new PermissionRequirement(PermissionKeys.ProfileAdminEdit)))
    .AddPolicy(ProfilePolicies.SuperAdminEdit, policy =>
        policy.AddRequirements(new PermissionRequirement(PermissionKeys.ProfileSuperAdminEdit)))
    .AddPolicy(ProfilePolicies.ProfessorViewDepartment, policy =>
        policy.AddRequirements(new PermissionRequirement(PermissionKeys.ProfessorViewDepartment)))
    .AddPolicy(ProfilePolicies.ProfessorManageDepartment, policy =>
        policy.AddRequirements(new PermissionRequirement(PermissionKeys.ProfessorManageDepartment)))
    .AddPolicy(DashboardPolicies.View, policy =>
        policy.AddRequirements(new PermissionRequirement(PermissionKeys.DashboardView)))
    .AddPolicy(DashboardPolicies.Export, policy =>
        policy.AddRequirements(new PermissionRequirement(PermissionKeys.DashboardExport)))
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// 🔐 Cookie config (ONLY ONCE)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Cookie.MaxAge = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

var app = builder.Build();

// The local launcher checks the database rather than treating missing setup
// secrets as evidence that an existing installation needs another account.
if (args.Any(argument => string.Equals(argument, "--check-bootstrap", StringComparison.OrdinalIgnoreCase)))
{
    using var accountScope = app.Services.CreateScope();
    var accountDb = accountScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        var hasAdministrator = await (
        from user in accountDb.Users
        join membership in accountDb.UserRoles on user.Id equals membership.UserId
        join role in accountDb.Roles on membership.RoleId equals role.Id
        where user.IsActive && role.Name == "SuperAdmin"
        select user.Id).AnyAsync();
        Environment.ExitCode = hasAdministrator ? 0 : 1;
    }
    catch (Exception exception)
    {
        app.Logger.LogError("Administrator database check failed ({FailureType}). Verify DefaultConnection and run Setup-Database.cmd.",
            exception.GetType().Name);
        Environment.ExitCode = 2;
    }
    return;
}

var bootstrapOnly = args.Any(argument =>
    string.Equals(argument, "--bootstrap", StringComparison.OrdinalIgnoreCase));
var skipDatabaseBootstrap =
    builder.Configuration.GetValue<bool>("SkipDatabaseBootstrap");
var initializeDevelopment = app.Environment.IsDevelopment() &&
    !skipDatabaseBootstrap;

// Normal Development launches must not die before Kestrel starts just because
// a legacy local database is missing the request-context audit columns. This
// narrow repair is safe to repeat and does not replace controlled EF migration.
if (app.Environment.IsDevelopment() && skipDatabaseBootstrap)
{
    try
    {
        using var compatibilityScope = app.Services.CreateScope();
        var compatibilityDb = compatibilityScope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        await DatabaseCompatibilityRepair.RepairLatestSchemaAsync(compatibilityDb);
    }
    catch (Exception exception)
    {
        var failureType = exception.GetType().FullName ?? "DatabaseFailure";
        app.Logger.LogError(
            "Development database compatibility repair failed ({FailureType}). The web server will continue starting.",
            failureType);
        try
        {
            await File.AppendAllTextAsync(
                processFailureLog,
                $"{DateTime.UtcNow:O} Compatibility repair failed ({failureType}).{Environment.NewLine}");
        }
        catch
        {
            // The server must still start when diagnostic logging is unavailable.
        }
    }
}

// Production startup validates schema state but never mutates Identity or the
// database. Run `dotnet run -- --bootstrap` as a controlled deployment job.
if (!bootstrapOnly && !initializeDevelopment && !skipDatabaseBootstrap)
{
    using var validationScope = app.Services.CreateScope();
    var validationDb = validationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (!await validationDb.Database.CanConnectAsync())
        throw new InvalidOperationException("The EduFlow database is unavailable.");
    var pending = (await validationDb.Database.GetPendingMigrationsAsync()).ToArray();
    if (pending.Length != 0)
        throw new InvalidOperationException(
            "The EduFlow database schema is behind the application. Pending migrations: " +
            string.Join(", ", pending) + ". Apply migrations as a controlled deployment step.");
}

// The first Super Admin is supplied through environment/secret configuration;
// no predictable production credential is stored in source.
if (bootstrapOnly || initializeDevelopment)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var applyMigrationsOnStartup =
        builder.Configuration.GetValue<bool?>(
            "DatabaseBootstrap:ApplyMigrationsOnStartup")
        ?? app.Environment.IsDevelopment();

    if (applyMigrationsOnStartup)
    {
        app.Logger.LogInformation(
            "Applying pending EduFlow database migrations before Identity initialization.");
        try
        {
            await db.Database.MigrateAsync();
        }
        catch (Exception exception)
        {
            var failureType = exception.GetType().FullName ?? "DatabaseFailure";
            app.Logger.LogCritical(
                "EduFlow database migration failed before web-server startup ({FailureType}).",
                failureType);
            try
            {
                var logDirectory = Path.Combine(
                    app.Environment.ContentRootPath,
                    "App_Data",
                    "Logs");
                Directory.CreateDirectory(logDirectory);
                await File.AppendAllTextAsync(
                    Path.Combine(logDirectory, "startup-failure.log"),
                    $"{DateTime.UtcNow:O} Migration failed ({failureType}).{Environment.NewLine}");
            }
            catch (Exception logException)
            {
                Console.Error.WriteLine(
                    $"Could not write the startup failure log: {logException.Message}");
            }
            throw;
        }
    }
    else
    {
        if (!await db.Database.CanConnectAsync())
            throw new InvalidOperationException(
                "The EduFlow database is unavailable. Verify the configured connection string before starting the application.");

        var pendingMigrations =
            (await db.Database.GetPendingMigrationsAsync()).ToArray();
        if (pendingMigrations.Length != 0)
            throw new InvalidOperationException(
                "The EduFlow database schema is behind the application. " +
                $"Pending migrations: {string.Join(", ", pendingMigrations)}. " +
                "Run 'dotnet ef database update' as a controlled deployment step before starting the application.");
    }

    // Move legacy files only after the schema is ready so a database failure
    // cannot leave the application half-upgraded.
    var privateFileStorage =
        scope.ServiceProvider.GetRequiredService<IPrivateFileStorage>();
    await privateFileStorage.MigrateLegacyFilesAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    var roles = new[]
    {
        "SuperAdmin", "Admin", "HOD", "Professor", "Student", "Guardian",
        "Accountant", "Librarian", "AdmissionOfficer", "ExaminationOfficer",
        "TransportManager", "HostelManager", "PlacementOfficer", "Employee"
    };

    // 🔥 Create roles
    foreach (var role in roles)
    {
        var roleExists = await roleManager.RoleExistsAsync(role);
        if (!roleExists)
        {
            var createRole = await roleManager.CreateAsync(new IdentityRole(role));
            if (!createRole.Succeeded)
                throw new InvalidOperationException(
                    $"Could not initialize role '{role}': " +
                    string.Join("; ", createRole.Errors.Select(e => e.Description)));
        }
    }

    await DatabaseBootstrapOperations.EnsureInitialSuperAdminAsync(
        db,
        roleManager,
        userManager,
        builder.Configuration);
    var permissionCatalog = new Dictionary<string, string>
    {
        ["student.view"] = "View authorized student profiles",
        ["student.create"] = "Create student records",
        ["student.update"] = "Update student records",
        ["student.delete"] = "Delete student records",
        ["student.promote"] = "Promote students",
        ["attendance.view"] = "View attendance",
        ["attendance.create"] = "Record attendance",
        ["attendance.correct"] = "Correct submitted attendance",
        ["attendance.approve"] = "Approve attendance corrections",
        ["marks.view"] = "View marks",
        ["marks.enter"] = "Enter marks",
        ["marks.approve"] = "Approve marks",
        ["results.publish"] = "Publish results",
        ["admission.view"] = "View admission applications",
        ["admission.create"] = "Create admission applications",
        ["admission.review"] = "Review admission applications",
        ["admission.approve"] = "Approve and enroll applicants",
        ["fees.view"] = "View authorized finance records",
        ["fees.collect"] = "Collect fee payments",
        ["fees.adjust"] = "Create finance adjustments",
        ["fees.refund"] = "Create refunds",
        ["library.manage"] = "Manage the library catalog",
        ["library.issue"] = "Issue library items",
        ["library.return"] = "Return library items",
        ["leave.review"] = "Review leave requests",
        ["reports.view"] = "View reports",
        ["reports.export"] = "Export reports",
        ["notifications.send"] = "Send notifications",
        ["documents.generate"] = "Generate official documents",
        ["settings.manage"] = "Manage system settings",
        ["academic.structure.manage"] = "Manage institution, campuses, curricula, and academic calendars",
        ["student.lifecycle.manage"] = "Prepare and apply student promotion batches",
        ["student.lifecycle.approve"] = "Approve or reject student promotion batches",
        ["timetable.manage"] = "Manage timetables, rooms, substitutions, and cancellations",
        ["communications.manage"] = "Create and schedule targeted communications",
        ["communications.dispatch"] = "Dispatch and retry communications",
        ["approvals.manage"] = "Create, review, delegate, and escalate approval requests",
        ["finance.invoice"] = "Issue and manage student invoices",
        ["library.audit"] = "Run stock verification and circulation reports",
        [PermissionKeys.ProfileImageEditSelf] = "Update only the signed-in user's profile image",
        [PermissionKeys.ProfileStudentEdit] = "Edit an eligible student profile",
        [PermissionKeys.ProfileProfessorEdit] = "Edit an eligible professor profile",
        [PermissionKeys.ProfileHodEdit] = "Edit an eligible HOD profile",
        [PermissionKeys.ProfileOperationalStaffEdit] = "Edit eligible operational staff profiles",
        [PermissionKeys.ProfileAdminEdit] = "Edit an eligible Admin profile",
        [PermissionKeys.ProfileSuperAdminEdit] = "Use the protected SuperAdmin profile workflow",
        [PermissionKeys.ProfessorViewDepartment] = "View professors in the actor's department and campuses",
        [PermissionKeys.ProfessorManageDepartment] = "Manage professors in the actor's department and campuses",
        [PermissionKeys.DashboardView] = "View the scoped analytics dashboard",
        [PermissionKeys.DashboardExport] = "Export scoped dashboard analytics",

        // Legacy keys remain during the compatibility period.
        ["ManageDepartments"] = "Create and manage departments",
        ["ManageTeachers"] = "Create and manage HOD and Professor accounts",
        ["ManageStudents"] = "Create and manage students",
        ["ManageAcademics"] = "Manage classes, sections, semesters, subjects, and academic years",
        ["ViewReports"] = "View attendance, marks, and academic reports",
        ["ManageAnnouncements"] = "Create and manage announcements"
    };
    await DatabaseBootstrapOperations.EnsurePermissionsAsync(
        db,
        permissionCatalog);

    var defaultRolePermissions = new Dictionary<string, string[]>
    {
        ["Admin"] = [
            "student.view", "student.create", "student.update", "student.promote",
            "attendance.view", "marks.view", "marks.approve",
            "admission.view", "admission.create", "admission.review", "admission.approve",
            "fees.view", "fees.collect", "reports.view", "reports.export",
            "notifications.send", "academic.structure.manage",
            "student.lifecycle.manage", "student.lifecycle.approve",
            "timetable.manage", "communications.manage", "approvals.manage",
            "finance.invoice", PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView, PermissionKeys.DashboardExport,
            PermissionKeys.ProfileHodEdit, PermissionKeys.ProfileOperationalStaffEdit,
            "ManageDepartments", "ManageTeachers",
            "ManageStudents", "ManageAcademics", "ViewReports", "ManageAnnouncements"
        ],
        ["AdmissionOfficer"] = ["admission.view", "admission.create", "admission.review", "admission.approve", "communications.manage", PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView, PermissionKeys.DashboardExport],
        ["Accountant"] = ["fees.view", "fees.collect", "fees.adjust", "fees.refund", "finance.invoice", "approvals.manage", "reports.view", "reports.export", PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView, PermissionKeys.DashboardExport],
        ["Librarian"] = ["library.manage", "library.issue", "library.return", "library.audit", "reports.view", PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView, PermissionKeys.DashboardExport],
        ["ExaminationOfficer"] = ["marks.view", "marks.approve", "results.publish", "approvals.manage", "reports.view", "reports.export", PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView, PermissionKeys.DashboardExport],
        ["HOD"] = ["student.view", "student.lifecycle.approve", "attendance.view", "attendance.create", "attendance.correct", "attendance.approve", "marks.view", "marks.enter", "marks.approve", "approvals.manage", "reports.view", "reports.export", PermissionKeys.ProfileImageEditSelf, PermissionKeys.ProfileStudentEdit, PermissionKeys.ProfileProfessorEdit, PermissionKeys.ProfessorViewDepartment, PermissionKeys.ProfessorManageDepartment, PermissionKeys.DashboardView, PermissionKeys.DashboardExport],
        ["Professor"] = ["student.view", "attendance.view", "attendance.create", "marks.view", "marks.enter", "timetable.manage", "reports.export", PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView, PermissionKeys.DashboardExport],
        ["Student"] = ["attendance.view", "marks.view", PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView],
        ["Guardian"] = ["attendance.view", "marks.view", "fees.view", PermissionKeys.ProfileImageEditSelf],
        ["TransportManager"] = [PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView],
        ["HostelManager"] = [PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView],
        ["PlacementOfficer"] = [PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView],
        ["Employee"] = [PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView]
    };
    await DatabaseBootstrapOperations.EnsureRolePermissionsAsync(
        db,
        defaultRolePermissions);

    // Upgrade legacy Teacher accounts to the explicit HOD/Professor hierarchy.
    await DatabaseBootstrapOperations.UpgradeLegacyTeachersAsync(
        db,
        roleManager,
        userManager);
    if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("SeedDevelopmentData"))
        await DevelopmentSeeder.SeedAsync(db, userManager, builder.Configuration);
}

if (bootstrapOnly)
{
    app.Logger.LogInformation("EduFlow controlled bootstrap completed successfully.");
    return;
}

// 🔹 Middleware
app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRequestLocalization();
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    var cspNonce = Convert.ToBase64String(
        System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    context.Items["CspNonce"] = cspNonce;
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; " +
        $"script-src 'self' 'nonce-{cspNonce}'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "font-src 'self' data:; " +
        "img-src 'self' data: blob:; " +
        "connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    await next();
});
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseMiddleware<DatabaseUnavailableMiddleware>();
app.UseStatusCodePagesWithReExecute("/Home/StatusCode/{0}");

// Legacy private-upload URLs must never reach StaticFileMiddleware. Files are
// migrated out of wwwroot and can only be served by authorized controller actions.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads/materials") ||
        context.Request.Path.StartsWithSegments("/uploads/students") ||
        context.Request.Path.StartsWithSegments("/uploads/admissions") ||
        context.Request.Path.StartsWithSegments("/uploads/assignments") ||
        context.Request.Path.StartsWithSegments("/uploads/profiles"))
    {
        context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});

app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseMiddleware<PrivilegedMfaMiddleware>();
app.UseAuthorization();

// 🔥 SINGLE DEFAULT ROUTE (IMPORTANT)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/configuration")
    .RequireAuthorization("ConfigurationHealth");

app.Run();

public partial class Program { }
