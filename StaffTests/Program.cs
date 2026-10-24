using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Student_Management_System.Controllers;
using Student_Management_System.Models;
using Student_Management_System.Services;

var assertions = 0;
void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
await StudentDirectoryVerification.RunAsync(Check);
var autoInput = new StaffInput { FullName = "Auto identifiers", Email = "auto@test.invalid", Role = "Librarian" };
Check(Validator.TryValidateObject(autoInput, new ValidationContext(autoInput), [], true), "Staff creation must not require a supplied employee ID or username.");
autoInput.Password = "Staff!1234"; autoInput.ConfirmPassword = autoInput.Password;
Check(Validator.TryValidateObject(autoInput, new ValidationContext(autoInput), [], true), "The staff model must accept a matching ten-character password.");
autoInput.Password = "Staff!123"; autoInput.ConfirmPassword = autoInput.Password;
Check(!Validator.TryValidateObject(autoInput, new ValidationContext(autoInput), [], true), "The staff model must reject a password shorter than ten characters.");
Check(StaffIdentityFields.NormalizeMobile("98765 43210") == "+919876543210" && StaffIdentityFields.NormalizeMobile("+91-98765-43210") == "+919876543210", "Equivalent mobile number formats must share one canonical value.");
try { StaffIdentityFields.NormalizeMobile(null); throw new Exception("A missing phone number was accepted."); } catch (StaffValidationException) { Check(true, "Missing mobile rejected before persistence."); }
foreach (var authenticated in new[] { false, true })
foreach (var active in new[] { false, true })
foreach (var admin in new[] { false, true })
foreach (var matches in new[] { false, true })
foreach (var grant in new bool?[] { null, false, true })
{
    var actual = StaffAccessRules.CanAccess(authenticated, active, admin, matches, grant);
    if (!authenticated || !active || grant == false) Check(!actual, "Inactive, unauthenticated, or explicitly denied accounts must never enter a workspace.");
    else if (grant == true || admin || matches) Check(actual, "Valid role, administrator or explicit grant must permit workspace access.");
    else Check(!actual, "Cross-role access must default to deny.");
}
foreach (var terminal in new[] { "Completed", "Rejected" })
foreach (var next in new[] { "Pending", "Verified", "Approved", "Completed", "Rejected" }) Check(!StaffAccessRules.CanTransition(terminal, next), "Terminal requests cannot change state.");
Check(!StaffAccessRules.CanTransition("Pending", "Approved"), "Approval cannot bypass verification.");
Check(!StaffAccessRules.CanTransition("Pending", "Completed"), "Completion cannot bypass approval.");
Check(StaffAccessRules.CanTransition("Pending", "Verified") && StaffAccessRules.CanTransition("Verified", "Approved") && StaffAccessRules.CanTransition("Approved", "Completed"), "The review chain must be supported.");
Check(StaffWorkspaceCatalog.All.Length == 5 && StaffWorkspaceCatalog.All.Select(w => w.Role).Distinct().Count() == 5, "Exactly five primary staff roles are defined.");
foreach (var workspace in StaffWorkspaceCatalog.All) Check(workspace.Pages.Select(p => p.Slug).Distinct().Count() == workspace.Pages.Length, "Workspace page slugs must be unique.");
Check(typeof(StaffApiController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true).Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Any(a => a.Roles == "Admin,SuperAdmin"), "Staff administration must require administrator roles.");
foreach (var controller in new[] { typeof(StaffApiController), typeof(StaffResourcesController), typeof(StaffWorkspaceApiController), typeof(StaffCertificatesController) })
foreach (var method in controller.GetMethods().Where(m => m.GetCustomAttributes(typeof(HttpPostAttribute), true).Length > 0 || m.GetCustomAttributes(typeof(HttpPutAttribute), true).Length > 0))
    Check(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).Length > 0, $"{controller.Name}.{method.Name} requires CSRF protection.");
Console.WriteLine($"PASS: {assertions} authorization, lifecycle and endpoint protection assertions.");
if (!args.Contains("--integration")) return;

// Only a fresh, unique database is created. No existing application data is used.
var databaseName = "EduFlowStaffTest_" + Guid.NewGuid().ToString("N");
var connection = new SqlConnectionStringBuilder { DataSource = Environment.MachineName + "\\SQLEXPRESS", InitialCatalog = databaseName, IntegratedSecurity = true, Encrypt = true, TrustServerCertificate = true, ConnectTimeout = 8 }.ConnectionString;
var services = new ServiceCollection();
services.AddLogging(); services.AddDataProtection().UseEphemeralDataProtectionProvider(); services.AddHttpContextAccessor(); services.AddAntiforgery();
services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connection, s => s.CommandTimeout(15)));
services.AddIdentityCore<ApplicationUser>(o => { o.Password.RequiredLength = 10; o.User.RequireUniqueEmail = true; })
    .AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
services.AddScoped<AuditService>(); services.AddScoped<PermissionService>(); services.AddScoped<StaffRepository>(); services.AddScoped<StaffManagementService>(); services.AddScoped<StaffAccessService>(); services.AddScoped<ICampusScopeService, CampusScopeService>();
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var sp = scope.ServiceProvider;
var db = sp.GetRequiredService<ApplicationDbContext>();
try
{
    Console.WriteLine("Testing isolated SQL database: " + databaseName);
    await db.GetService<IMigrator>().MigrateAsync("20260804000700_RepairMarkScorePrecision");
    // Reproduce the deployed Identity columns before testing legacy staff import.
    await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.AspNetUsers ADD PublicUserId nvarchar(40) NULL, NormalizedMobileNumber nvarchar(16) NULL;");
    var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
    var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var legacyRole in new[] { "Librarian", "Accountant", "Admin" })
        if (!await roles.RoleExistsAsync(legacyRole)) await roles.CreateAsync(new IdentityRole(legacyRole));
    var legacy = new ApplicationUser { FullName = "Legacy librarian", Email = "legacy@test.invalid", UserName = "legacy" };
    var legacyPassword = "Legacy!" + Guid.NewGuid().ToString("N") + "1a";
    Check((await users.CreateAsync(legacy, legacyPassword)).Succeeded, "Legacy migration fixture creation failed.");
    await users.AddToRoleAsync(legacy, "Librarian");
    db.EmployeeProfiles.Add(new EmployeeProfile { UserId = legacy.Id, EmployeeNumber = "LEGACY-001", JobTitle = "Librarian", JoinedOn = DateTime.Today.AddYears(-1) });
    var legacyWithoutProfile = new ApplicationUser { FullName = "Legacy accountant", Email = "legacy-accountant@test.invalid", UserName = "legacy-accountant" };
    await users.CreateAsync(legacyWithoutProfile, "Legacy!" + Guid.NewGuid().ToString("N") + "1a");
    await users.AddToRoleAsync(legacyWithoutProfile, "Accountant");
    await db.SaveChangesAsync();
    await db.Database.MigrateAsync();
    // Reproduce deployed Identity tables that have enabled audit triggers.
    // Account creation, MFA lockout counters and successful-login updates must
    // remain compatible without disabling those triggers.
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TRIGGER [dbo].[TR_StaffTest_AspNetUsers] ON [dbo].[AspNetUsers]
        AFTER INSERT, UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id WHERE d.Id IS NULL AND NULLIF(i.PublicUserId,N'') IS NULL)
                THROW 51001, 'New users require a generated user ID.', 1;
            IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id WHERE d.Id IS NULL AND i.NormalizedMobileNumber IS NULL)
                THROW 51003, 'Enter a valid mobile number before saving the account.', 1;
        END;
        """);
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TRIGGER [dbo].[TR_StaffTest_EmployeeProfiles] ON [dbo].[EmployeeProfiles]
        AFTER INSERT, UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id JOIN dbo.AspNetUsers u ON u.Id=i.UserId WHERE d.Id IS NULL AND i.EmployeeNumber<>u.PublicUserId)
                THROW 51001, 'New employees must use the generated user ID as their employee number.', 1;
        END;
        """);
    legacy.LastLoginUtc = DateTime.UtcNow;
    Check((await users.UpdateAsync(legacy)).Succeeded, "Identity successful-login updates must work with an enabled AspNetUsers trigger.");
    Check((await users.AccessFailedAsync(legacy)).Succeeded, "Identity MFA failure updates must work with an enabled AspNetUsers trigger.");
    Check((await users.ResetAccessFailedCountAsync(legacy)).Succeeded, "Identity MFA success updates must work with an enabled AspNetUsers trigger.");
    Check(await db.StaffMembers.AnyAsync(s => s.EmployeeProfile.UserId == legacy.Id && s.EmployeeProfile.EmployeeNumber == "LEGACY-001" && s.PrimaryRole == "Librarian"), "Migration must import existing employee profiles without replacing their IDs.");
    Check(await db.StaffMembers.AnyAsync(s => s.EmployeeProfile.UserId == legacyWithoutProfile.Id && s.PrimaryRole == "Accountant"), "Migration must create an employment profile for an existing staff account without one.");
    Check(await users.CheckPasswordAsync(legacy, legacyPassword), "Migration must preserve legacy password hashes.");
    foreach (var role in StaffWorkspaceCatalog.All.Select(w => w.Role).Append("Admin"))
        if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));
    var admin = new ApplicationUser { FullName = "Test Administrator", Email = "admin@test.invalid", UserName = "test-admin", PublicUserId = "ADM-TEST", PhoneNumber = "8999999999" };
    Check((await users.CreateAsync(admin, "Test!" + Guid.NewGuid().ToString("N") + "1a")).Succeeded, "Administrator creation failed.");
    await users.AddToRoleAsync(admin, "Admin");
    var institution = new Institution { Name = "Isolated staff test institution", Code = "STAFFTEST" };
    var campus = new Campus { Name = "Test Campus", Code = "TEST", Institution = institution };
    db.Campuses.Add(campus);
    await db.SaveChangesAsync();
    db.UserCampusMemberships.Add(new UserCampusMembership { UserId = admin.Id, CampusId = campus.Id, IsDefault = true });
    foreach (var workspace in StaffWorkspaceCatalog.All)
    {
        var permissionKey = StaffAccessService.ManagePermission(workspace.Key);
        var permission = await db.Permissions.SingleOrDefaultAsync(p => p.Key == permissionKey);
        if (permission == null) { permission = new Permission { Key = permissionKey, Description = "Test module permission" }; db.Permissions.Add(permission); }
        if (!await db.RolePermissions.AnyAsync(p => p.RoleName == workspace.Role && p.Permission.Key == permissionKey))
            db.RolePermissions.Add(new RolePermission { RoleName = workspace.Role, Permission = permission });
    }
    await db.SaveChangesAsync();
    foreach (var workspace in StaffWorkspaceCatalog.All)
    {
        var key = StaffAccessService.ManagePermission(workspace.Key);
        var permission = await db.Permissions.SingleAsync(p => p.Key == key);
        if (!await db.RolePermissions.AnyAsync(p => p.RoleName == "Admin" && p.PermissionId == permission.Id))
            db.RolePermissions.Add(new RolePermission { RoleName = "Admin", PermissionId = permission.Id });
    }
    await db.SaveChangesAsync();
    ClaimsPrincipal Principal(ApplicationUser user, string role) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Role, role)], "StaffTest"));
    var context = new DefaultHttpContext { User = Principal(admin, "Admin"), RequestServices = sp };
    sp.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
    var service = sp.GetRequiredService<StaffManagementService>();
    var repository = sp.GetRequiredService<StaffRepository>();
    var access = sp.GetRequiredService<StaffAccessService>();
    var phonesByTag = new Dictionary<string, string>();
    string StaffPhone(string tag) { if (!phonesByTag.ContainsKey(tag)) phonesByTag[tag] = (9000000000L + phonesByTag.Count).ToString(); return phonesByTag[tag]; }
    StaffInput Input(string role, string tag) => new() { FullName = "Test " + role, Email = tag + "@test.invalid", Phone = StaffPhone(tag), Role = role, JoiningDate = DateTime.Today.AddDays(-1), Password = "Staff!1234", IsActive = true };
    var created = new List<int>();
    var generatedIds = new HashSet<string>();
    var generatedUsernames = new HashSet<string>();
    foreach (var workspace in StaffWorkspaceCatalog.All)
    {
        var input = Input(workspace.Role, workspace.Key); input.ConfirmPassword = input.Password;
        if (workspace.Key == "librarian") { input.EmployeeId = "PROVIDED-ID"; input.Username = "provided-username"; }
        var id = await service.SaveAsync(null, input, admin.Id); created.Add(id);
        var staff = (await repository.FindAsync(id))!;
        Check(staff.EmployeeProfile.EmployeeNumber.StartsWith("EMP-") && generatedIds.Add(staff.EmployeeProfile.EmployeeNumber), "Every staff account must receive a distinct generated employee ID.");
        Check(staff.EmployeeProfile.User.UserName!.StartsWith("staff.") && generatedUsernames.Add(staff.EmployeeProfile.User.UserName), "Every staff account must receive a distinct generated username.");
        Check(!string.IsNullOrEmpty(staff.EmployeeProfile.User.PublicUserId) && staff.EmployeeProfile.User.NormalizedMobileNumber == StaffIdentityFields.NormalizeMobile(input.Phone), "Staff creation must supply the user ID and canonical phone required by the deployed identity guard.");
        Check(staff.EmployeeProfile.EmployeeNumber == staff.EmployeeProfile.User.PublicUserId, "The employee number must match the generated user ID required by the employee guard.");
        Check(await users.FindByNameAsync(staff.EmployeeProfile.User.UserName) != null, "The generated username must resolve through Identity for sign-in.");
        Check(staff.PrimaryRole == workspace.Role && (await users.GetRolesAsync(staff.EmployeeProfile.User)).SequenceEqual(new[] { workspace.Role }), "Staff must have exactly one primary Identity role.");
        Check(staff.EmployeeProfile.User.MustChangePassword, "Initial password rotation must be required.");
        Check(await db.UserCampusMemberships.AnyAsync(m => m.UserId == staff.EmployeeProfile.UserId && m.CampusId == campus.Id && m.IsActive), "New staff must inherit their creator's default campus.");
        Check(await users.CheckPasswordAsync(staff.EmployeeProfile.User, input.Password!), "Identity must hash and verify staff passwords.");
        var principal = Principal(staff.EmployeeProfile.User, workspace.Role);
        Check(await access.CanManageAsync(principal, workspace.Key), "The primary role's action permission must allow module management.");
        foreach (var other in StaffWorkspaceCatalog.All) Check(await access.AllowedAsync(principal, other.Key) == (other.Key == workspace.Key), "Stored authorization must isolate all five roles.");
    }
    var first = (await repository.FindAsync(created[0]))!;
    var duplicatePhone = Input("Librarian", "duplicate-mobile");
    duplicatePhone.Phone = StaffIdentityFields.NormalizeMobile(first.EmployeeProfile.User.PhoneNumber);
    duplicatePhone.ConfirmPassword = duplicatePhone.Password;
    try { await service.SaveAsync(null, duplicatePhone, admin.Id); throw new Exception("A duplicate canonical mobile was accepted."); }
    catch (StaffValidationException ex) { Check(ex.Message.Contains("mobile number is already registered") && !await db.Users.AnyAsync(u => u.Email == duplicatePhone.Email), "Duplicate mobile formats must receive a clear validation error without creating a partial account."); }
    var firstUser = first.EmployeeProfile.User;
    var grant = await db.Permissions.SingleAsync(p => p.Key == "workspace.accountant");
    db.UserPermissions.Add(new UserPermission { UserId = firstUser.Id, PermissionId = grant.Id, IsGranted = true }); await db.SaveChangesAsync();
    Check(await access.AllowedAsync(Principal(firstUser, "Librarian"), "accountant"), "An explicit administrator grant must enable another workspace.");
    var manageLibrary = await db.Permissions.SingleAsync(p => p.Key == "library.manage");
    db.UserPermissions.Add(new UserPermission { UserId = firstUser.Id, PermissionId = manageLibrary.Id, IsGranted = false });
    await db.SaveChangesAsync();
    Check(!await access.CanManageAsync(Principal(firstUser, "Librarian"), "librarian"), "An action-level denial must override the primary role's module permission.");
    var update = Input("Registrar", "librarian"); update.Password = null; update.ConfirmPassword = null; update.RowVersion = Convert.ToBase64String(first.RowVersion);
    var originalEmployeeId = first.EmployeeProfile.EmployeeNumber;
    var originalUsername = firstUser.UserName;
    update.EmployeeId = "FORGED-ID"; update.Username = "forged-username";
    var oldStamp = firstUser.SecurityStamp;
    await service.SaveAsync(first.Id, update, admin.Id);
    var changed = (await repository.FindAsync(first.Id))!;
    Check(changed.EmployeeProfile.EmployeeNumber == originalEmployeeId && changed.EmployeeProfile.User.UserName == originalUsername, "Staff edits must preserve the assigned identifiers even when a caller supplies replacements.");
    Check((await users.GetRolesAsync(changed.EmployeeProfile.User)).SequenceEqual(new[] { "Registrar" }), "Role changes must remove old roles.");
    Check(!await db.UserPermissions.AnyAsync(p => p.UserId == firstUser.Id), "Role changes must clear previous permission overrides.");
    Check(changed.EmployeeProfile.User.SecurityStamp != oldStamp, "Role changes must revoke existing Identity sessions.");
    try { await service.SaveAsync(first.Id, update, admin.Id); throw new Exception("Stale update was accepted."); } catch (DbUpdateConcurrencyException) { Check(true, "Stale edit rejected."); }
    var bad = Input("Librarian", "bad-password"); bad.Password = "short"; bad.ConfirmPassword = "short";
    try { await service.SaveAsync(null, bad, admin.Id); throw new Exception("Invalid password was accepted."); } catch (StaffValidationException) { db.ChangeTracker.Clear(); Check(!await db.Users.AnyAsync(u => u.Email == bad.Email), "Failed account creation must roll back without orphan users."); }
    var api = new StaffWorkspaceApiController(db, sp.GetRequiredService<ICampusScopeService>(), users, sp.GetRequiredService<IAntiforgery>(), sp.GetRequiredService<AuditService>(), access)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(changed.EmployeeProfile.User, "Registrar"), RequestServices = sp } } };
    Check(await api.Dashboard("accountant") is ForbidResult, "Direct cross-role dashboard requests must fail server-side.");
    Check(await api.ReadMessage(999) is NotFoundResult, "Other users' messages must never be accessible by ID.");

    ControllerContext Context(ApplicationUser user, string role)
    {
        var http = new DefaultHttpContext { User = Principal(user, role), RequestServices = sp };
        sp.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
        return new ControllerContext { HttpContext = http };
    }
    var resources = new StaffResourcesController(db, access, sp.GetRequiredService<AuditService>(), sp.GetRequiredService<ICampusScopeService>()) { ControllerContext = Context(admin, "Admin") };
    JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);
    Check(await resources.Save("librarian", "books", Json(new { title = "Book without ISBN one", author = "Author", copyCount = 2 })) is OkResult, "A title must create its uniquely barcoded copies.");
    Check(await resources.Save("librarian", "books", Json(new { title = "Book without ISBN two", author = "Author", copyCount = 1 })) is OkResult, "Multiple books may omit ISBN without violating its unique index.");
    Check(await db.LibraryBookCopies.CountAsync() == 3, "Book creation must create the requested inventory count.");
    Check(await resources.List("librarian", "books", null) is JsonResult, "Catalog paging and projection must translate on SQL Server.");
    Check(await resources.Save("transport", "drivers", Json(new { fullName = "Test Driver", phone = "9000000000", licenseNumber = "TEST-LICENSE", licenseExpiry = DateTime.Today.AddYears(1), isActive = true })) is OkResult, "Driver creation failed.");
    Check(await resources.Save("transport", "routes", Json(new { name = "Test route", code = "TEST-ROUTE", stops = "Campus, Main stop", isActive = true })) is OkResult, "Route creation failed.");
    var driverId = await db.TransportDrivers.Select(d => d.Id).SingleAsync();
    var routeId = await db.TransportRoutes.Select(r => r.Id).SingleAsync();
    Check(await resources.Save("transport", "vehicles", Json(new { registrationNumber = "TEST-BUS-1", vehicleType = "Bus", model = "Test Bus", capacity = 30, transportDriverId = driverId, transportRouteId = routeId, maintenanceStatus = "Ready", insuranceExpiry = DateTime.Today.AddYears(1), fitnessExpiry = DateTime.Today.AddYears(1), permitExpiry = DateTime.Today.AddYears(1), isActive = true })) is OkResult, "Vehicle creation with compliance information failed.");
    var vehicleId = await db.TransportVehicles.Select(v => v.Id).SingleAsync();
    Check(await resources.Save("transport", "trips", Json(new { transportVehicleId = vehicleId, transportDriverId = driverId, transportRouteId = routeId, scheduledOn = DateTime.Today, departureTime = "08:00", status = "Planned" })) is OkResult, "Trip scheduling failed.");
    var trip = await db.TransportTrips.SingleAsync();
    Check(await resources.Save("transport", "trips", Json(new { id = trip.Id, transportVehicleId = vehicleId, transportDriverId = driverId, transportRouteId = routeId, scheduledOn = DateTime.Today, departureTime = "08:00", status = "In progress", rowVersion = Convert.ToBase64String(trip.RowVersion) })) is OkResult, "A compliant scheduled trip must be able to start.");
    Check(await resources.Save("transport", "fuel", Json(new { transportVehicleId = vehicleId, filledOn = DateTime.Today, litres = 30.5m, unitPrice = 95m, odometerKm = 1000 })) is OkResult, "Fuel entry with calculated cost failed.");
    Check(await resources.Save("transport", "fuel", Json(new { transportVehicleId = vehicleId, filledOn = DateTime.Today, litres = 0.001m, unitPrice = 95m, odometerKm = 1001 })) is BadRequestObjectResult, "Sub-cent precision must be rejected before SQL rounding can produce a zero-value fuel record.");

    var studentUser = new ApplicationUser { FullName = "Test Resident", Email = "resident@test.invalid", UserName = "resident", PublicUserId = "STU-TEST", PhoneNumber = "8999999998" };
    await users.CreateAsync(studentUser, "Student!" + Guid.NewGuid().ToString("N") + "1a");
    var teacher = new Teacher { FullName = "Fixture teacher", UserId = admin.Id, Email = "teacher@test.invalid" };
    var fixtureClass = new Class { Name = "Fixture class", Teacher = teacher };
    var section = new Section { Name = "A", Class = fixtureClass };
    var student = new Student { FullName = studentUser.FullName, Email = studentUser.Email!, UserId = studentUser.Id, DOB = DateTime.Today.AddYears(-20), Class = fixtureClass, Section = section };
    db.Students.Add(student);
    db.UserCampusMemberships.Add(new UserCampusMembership { UserId = studentUser.Id, CampusId = campus.Id, IsDefault = true });
    await db.SaveChangesAsync();
    var room = new HostelRoom { HostelName = "Test hostel", RoomNumber = "101", Capacity = 2 };
    db.HostelAllocations.Add(new HostelAllocation { StudentId = student.Id, HostelRoom = room, StartsOn = DateTime.Today.AddDays(-1) });
    await db.SaveChangesAsync();
    Check(await resources.Save("hostel", "attendance", Json(new { studentId = student.Id, attendanceDate = DateTime.Today, status = "Present" })) is OkResult, "Attendance requires and accepts an active resident allocation.");
    Check(await resources.Save("registrar", "certificates", Json(new { studentId = student.Id, certificateType = "Bonafide", purpose = "Institutional verification", evidenceReference = "VERIFIED-TEST-RECORD" })) is OkResult, "Certificate draft creation failed.");
    var certificate = await db.RegistrarCertificates.SingleAsync();
    var certificateService = new Student_Management_System.Modules.Registrar.CertificateService(db, sp.GetRequiredService<AuditService>());
    var registrar = (await repository.FindAsync(created[3]))!.EmployeeProfile.User;
    await certificateService.ReviewAsync(certificate, new ReviewWorkInput { Status = "Verified", Comment = "Evidence checked", RowVersion = Convert.ToBase64String(certificate.RowVersion) }, registrar.Id);
    try { await certificateService.ReviewAsync(certificate, new ReviewWorkInput { Status = "Approved", Comment = "Self approval attempt", RowVersion = Convert.ToBase64String(certificate.RowVersion) }, registrar.Id); throw new Exception("Certificate self-approval was accepted."); }
    catch (StaffValidationException) { Check(true, "Certificate verification and approval must be separated."); }
    // The creator is the test administrator; use another authorized registrar
    // identity to represent independent approval of this fixture.
    await certificateService.ReviewAsync(certificate, new ReviewWorkInput { Status = "Approved", Comment = "Independent approval", RowVersion = Convert.ToBase64String(certificate.RowVersion) }, changed.EmployeeProfile.UserId);
    await certificateService.ReviewAsync(certificate, new ReviewWorkInput { Status = "Issued", Comment = "Issued after approval", RowVersion = Convert.ToBase64String(certificate.RowVersion) }, registrar.Id);
    Check(certificate.CertificateNumber != null && certificate.Status == "Issued", "Only a verified and independently approved certificate may receive an official number.");

    foreach (var workspace in StaffWorkspaceCatalog.All)
    {
        var dashboard = new StaffWorkspaceApiController(db, sp.GetRequiredService<ICampusScopeService>(), users, sp.GetRequiredService<IAntiforgery>(), sp.GetRequiredService<AuditService>(), access) { ControllerContext = Context(admin, "Admin") };
        var result = await dashboard.Dashboard(workspace.Key) as JsonResult;
        Check(result != null && Json(result.Value!).GetProperty("metrics").GetArrayLength() == 8, "Every authorized dashboard must execute its real SQL queries and return eight metrics.");
    }
    foreach (var (workspace, resource) in new[] { ("transport", "trips"), ("transport", "fuel"), ("hostel", "attendance"), ("registrar", "certificates") })
        Check(await resources.List(workspace, resource, null) is JsonResult, "Operational register projections must translate on SQL Server.");
    db.LoginHistories.Add(new LoginHistory { UserId = firstUser.Id, Email = firstUser.Email!, Succeeded = true });
    await db.SaveChangesAsync();
    var staffApi = new StaffApiController(db, repository, service, users, sp.GetRequiredService<AuditService>(), null!) { ControllerContext = Context(admin, "Admin") };
    var loginActivity = await staffApi.Activity(first.Id) as JsonResult;
    Check(loginActivity != null && Json(loginActivity.Value!).GetProperty("logins")[0].GetProperty("createdUtc").GetString()!.EndsWith("Z"), "Login timestamps must retain UTC offset information after SQL materialization.");
    await StaffUiTests.NativePageVerification.RunAsync(connection, admin.Id, legacy.Id, Check);
    Console.WriteLine($"PASS: {assertions} total assertions including SQL Server, Identity, role boundaries, operational workflows and authenticated native Razor page rendering.");
}
finally
{
    if (!databaseName.StartsWith("EduFlowStaffTest_", StringComparison.Ordinal) || databaseName.Length != 49) throw new Exception("Refusing unexpected test database cleanup.");
    await db.Database.EnsureDeletedAsync();
}
