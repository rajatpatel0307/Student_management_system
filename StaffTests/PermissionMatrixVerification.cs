using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Student_Management_System.Models;
using Student_Management_System.Security;
using Student_Management_System.Services;

internal static class PermissionMatrixVerification
{
    private sealed record Case(string Controller, string Action, string Path, string[] Roles, bool Post = false);
    public static async Task RunAsync(IServiceProvider services, HttpClient client, StaffUiTests.PageTestIdentity identity, string actorId, Action<bool, string> check)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await EvaluateAllActionPolicies(services, db, check);
        var keys = new[] { "staff.manage", "student.view", "student.create", "student.delete", "admission.view", "admission.create", "admission.review", "fees.view", "dashboard.view", "marks.enter", "marks.approve", "results.publish", "reports.view", "reports.export" };
        var existing = await db.UserPermissions.Where(p => p.UserId == actorId && keys.Contains(p.Permission.Key)).ToListAsync();
        var originals = existing.ToDictionary(p => p.PermissionId, p => p.IsGranted);
        foreach (var key in keys)
        {
            var permission = await db.Permissions.SingleOrDefaultAsync(p => p.Key == key);
            if (permission == null) { permission = new Permission { Key = key, Description = "Isolated matrix fixture" }; db.Permissions.Add(permission); await db.SaveChangesAsync(); }
            var grant = await db.UserPermissions.SingleOrDefaultAsync(p => p.UserId == actorId && p.PermissionId == permission.Id);
            if (grant == null) db.UserPermissions.Add(new UserPermission { UserId = actorId, PermissionId = permission.Id, IsGranted = true });
            else grant.IsGranted = true;
        }
        await db.SaveChangesAsync();
        var cases = new Case[]
        {
            new("SuperAdmin", "Index", "/SuperAdmin", ["SuperAdmin"]),
            new("Staff", "Index", "/Staff", ["Admin", "SuperAdmin"]),
            new("StaffApi", "List", "/api/staff", ["Admin", "SuperAdmin"]),
            new("Students", "Index", "/Students", ["Admin", "SuperAdmin", "HOD"]),
            new("Students", "Delete", "/Students/Delete/1", ["Admin", "SuperAdmin"], true),
            new("Guardians", "Index", "/Guardians", ["Admin", "SuperAdmin"]),
            new("Admissions", "Index", "/Admissions", ["Admin", "SuperAdmin", "AdmissionOfficer", "Registrar"]),
            new("Finance", "Index", "/Finance", ["Admin", "SuperAdmin", "Accountant"]),
            new("Mark", "Create", "/Mark/Create", ["HOD", "Professor"]),
            new("StudentDashboard", "Index", "/StudentDashboard", ["Student"]),
            new("ProfessorDashboard", "Index", "/ProfessorDashboard", ["Professor"]),
            new("HodDashboard", "Index", "/HodDashboard", ["HOD"]),
            new("GuardianDashboard", "Index", "/GuardianDashboard", ["Guardian"])
        };
        var descriptors = services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.OfType<ControllerActionDescriptor>().ToList();
        var provider = services.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var output = new List<string> { "# Executed role-boundary matrix", "", "A = production authorization policy allowed; D = real HTTP role denial verified. Allowed CRUD is not executed by this matrix. Separate integration tests verify selected successful workflows and row scope.", "", "| Route | " + string.Join(" | ", ApplicationRoles.All) + " | Anonymous |", "| --- | " + string.Join(" | ", ApplicationRoles.All.Select(_ => "---")) + " | --- |" };
        try
        {
            foreach (var item in cases)
            {
                var action = descriptors.Where(d => d.ControllerName == item.Controller && d.ActionName == item.Action &&
                    (d.MethodInfo.GetCustomAttributes(typeof(HttpPostAttribute), true).Length > 0) == item.Post)
                    .GroupBy(d => d.MethodInfo).Select(group => group.First()).Single();
                var metadata = action.EndpointMetadata.OfType<IAuthorizeData>();
                var policy = (await AuthorizationPolicy.CombineAsync(provider, metadata))!;
                var results = new List<string>();
                foreach (var role in ApplicationRoles.All)
                {
                    identity.UserId = actorId; identity.Role = role; identity.Authenticated = true;
                    var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId), new Claim(ClaimTypes.Role, role)], "matrix"));
                    var allowed = (await authorization.AuthorizeAsync(principal, null, policy)).Succeeded;
                    var expected = item.Roles.Contains(role);
                    check(allowed == expected, $"Production authorization policy mismatch for {role} {item.Path}.");
                    if (!expected)
                    {
                        using var response = item.Post ? await client.PostAsync(item.Path, new FormUrlEncodedContent([])) : await client.GetAsync(item.Path);
                        check((int)response.StatusCode == 403, $"Direct HTTP access must deny {role} {item.Path} before CRUD or data access.");
                    }
                    results.Add(expected ? "A" : "D");
                }
                identity.Authenticated = false;
                using var anonymous = item.Post ? await client.PostAsync(item.Path, new FormUrlEncodedContent([])) : await client.GetAsync(item.Path);
                check((int)anonymous.StatusCode == 401, $"Anonymous HTTP requests must not reach {item.Path}.");
                output.Add("| " + (item.Post ? "POST " : "GET ") + item.Path + " | " + string.Join(" | ", results) + " | 401 |" );
            }
            identity.Authenticated = true;
            async Task Set(string key, bool value)
            {
                var grant = await db.UserPermissions.SingleAsync(p => p.UserId == actorId && p.Permission.Key == key);
                grant.IsGranted = value; await db.SaveChangesAsync();
            }
            async Task<bool> Denied(string role, string controller, string action, string method)
            {
                var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId), new Claim(ClaimTypes.Role, role)], "matrix")) };
                context.Request.Method = method;
                var routes = new RouteData(); routes.Values["controller"] = controller; routes.Values["action"] = action;
                var filterContext = new AuthorizationFilterContext(new ActionContext(context, routes, new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()), []);
                await new AdminPermissionParityFilter(new PermissionService(db)).OnAuthorizationAsync(filterContext);
                return filterContext.Result is ForbidResult;
            }
            await Set("student.delete", false);
            check(await Denied("Admin", "Students", "DELETE", "POST") && await Denied("SuperAdmin", "Students", "dElEtE", "POST"),
                "Mixed-case routes and SuperAdmin must honor an explicit student-delete denial.");
            await Set("marks.enter", false);
            check(await Denied("Professor", "Mark", "SaveBulk", "POST"), "Teaching roles must honor explicit write denials too.");
            await Set("admission.review", false);
            check(!await Denied("Admin", "Admissions", "Create", "POST"), "Creating applications must require create permission, without adding review permission.");
            await Set("marks.approve", false);
            check(!await Denied("Admin", "Mark", "ReviewCorrection", "POST"), "Published-result corrections must use the results.publish permission.");
            await Set("reports.export", false);
            check(await Denied("SuperAdmin", "Reports", "StudentsExcel", "GET"), "The legacy Excel route must enforce export denial.");
            var legacy = typeof(Student_Management_System.Controllers.ReportsController).GetMethod("StudentsExcel")!;
            check(legacy.GetCustomAttributes(typeof(PermissionAuthorizeAttribute), true).Cast<PermissionAuthorizeAttribute>().Any(a => Equals(a.Arguments![0], "reports.export")),
                "Legacy export aliases need their own export filter; calling another action does not run its filters.");
            Directory.CreateDirectory(".build");
            await File.WriteAllLinesAsync(Path.Combine(".build", "permission-matrix-phase3.md"), output);
        }
        finally
        {
            var grants = await db.UserPermissions.Where(p => p.UserId == actorId && keys.Contains(p.Permission.Key)).ToListAsync();
            foreach (var grant in grants) { if (originals.TryGetValue(grant.PermissionId, out var value)) grant.IsGranted = value; else db.UserPermissions.Remove(grant); }
            await db.SaveChangesAsync();
            identity.Authenticated = true; identity.UserId = actorId; identity.Role = "Admin";
        }
    }

    private static async Task EvaluateAllActionPolicies(IServiceProvider services, ApplicationDbContext db, Action<bool, string> check)
    {
        // Seed the exact production defaults into the disposable fixture, rather
        // than maintaining a second independently drifting permission catalogue.
        var defaults = DefaultRolePermissions.All.SelectMany(role => role.Value).Distinct().ToDictionary(key => key, key => "Policy matrix fixture: " + key);
        await Student_Management_System.Services.DatabaseBootstrapOperations.EnsurePermissionsAsync(db, defaults);
        await Student_Management_System.Services.DatabaseBootstrapOperations.EnsureRolePermissionsAsync(db, DefaultRolePermissions.All);
        var actor = new ApplicationUser { UserName = "policy-matrix", FullName = "Policy matrix fixture", Email = "policy-matrix@test.invalid", PhoneNumber = "7999999970" };
        db.Users.Add(actor); await db.SaveChangesAsync();
        var descriptors = services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .GroupBy(action => action.MethodInfo).Select(group => group.First()).ToList();
        var policyProvider = services.GetRequiredService<IAuthorizationPolicyProvider>();
        var output = new List<string> { "# Production-default action policy matrix", "", "Evaluates the application's shared production policies, default grants, permission attributes and override filter for every discovered MVC action and all configured roles. PASS means the request may reach its record/service guards; it does not certify record ownership, staff isolation, MFA enrollment or successful CRUD. Public actions still require their configured CSRF/rate-limit checks.", "", "| Action and verb | " + string.Join(" | ", ApplicationRoles.All) + " |", "| --- | " + string.Join(" | ", ApplicationRoles.All.Select(_ => "---")) + " |" };
        var results = descriptors.ToDictionary(action => action, _ => new List<string>());
        foreach (var role in ApplicationRoles.All)
        {
            using var roleScope = services.CreateScope();
            var authorization = roleScope.ServiceProvider.GetRequiredService<IAuthorizationService>();
            var permissions = roleScope.ServiceProvider.GetRequiredService<PermissionService>();
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.Id), new Claim(ClaimTypes.Role, role)], "policy-matrix"));
            foreach (var action in descriptors)
            {
                if (action.EndpointMetadata.OfType<IAllowAnonymous>().Any()) { results[action].Add("Public"); continue; }
                var metadata = action.EndpointMetadata.OfType<IAuthorizeData>().ToList();
                var policy = (await AuthorizationPolicy.CombineAsync(policyProvider, metadata))!;
                var allowed = (await authorization.AuthorizeAsync(principal, null, policy)).Succeeded;
                var matchesDeclaredRoles = metadata.All(attribute => string.IsNullOrEmpty(attribute.Roles) || attribute.Roles.Split(',').Any(allowedRole => allowedRole.Trim() == role));
                if (!matchesDeclaredRoles) check(!allowed, $"{role} must fail declared role constraints on {action.ControllerName}.{action.ActionName}.");
                foreach (var permission in action.EndpointMetadata.OfType<PermissionAuthorizeAttribute>())
                    allowed &= await permissions.HasAsync(principal, (string)permission.Arguments![0]);
                var httpMethod = action.MethodInfo.GetCustomAttributes(true).OfType<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().SelectMany(attribute => attribute.HttpMethods).FirstOrDefault() ?? "GET";
                var context = new DefaultHttpContext { User = principal }; context.Request.Method = httpMethod;
                var route = new RouteData(); route.Values["controller"] = action.ControllerName; route.Values["action"] = action.ActionName;
                var filter = new AuthorizationFilterContext(new ActionContext(context, route, action), []);
                await new AdminPermissionParityFilter(permissions).OnAuthorizationAsync(filter);
                results[action].Add(allowed && filter.Result == null ? "PASS" : "DENY");
                var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
                check(!(await authorization.AuthorizeAsync(anonymous, null, policy)).Succeeded, $"Nonpublic action {action.ControllerName}.{action.ActionName} must reject anonymous policy evaluation.");
            }
        }
        foreach (var action in descriptors.OrderBy(action => action.ControllerName).ThenBy(action => action.ActionName))
        {
            var method = action.MethodInfo.GetCustomAttributes(true).OfType<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().SelectMany(attribute => attribute.HttpMethods).FirstOrDefault() ?? "GET";
            output.Add($"| {method} {action.ControllerName}.{action.ActionName} ({string.Join(", ", action.Parameters.Select(parameter => parameter.Name))}) | " + string.Join(" | ", results[action]) + " |");
        }
        Directory.CreateDirectory(".build");
        await File.WriteAllLinesAsync(Path.Combine(".build", "action-policy-matrix-phase3.md"), output);
        check(descriptors.Select(action => action.ControllerName).Distinct().Count() == 49, "The action policy matrix must include all currently discovered controllers.");
    }
}
