using Microsoft.AspNetCore.Authorization;
using Student_Management_System.Services;

namespace Student_Management_System.Security;

public static class AuthorizationConfiguration
{
    public static IServiceCollection AddEduFlowAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, GranularPermissionHandler>();
        services.AddAuthorizationBuilder()
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
        return services;
    }
}