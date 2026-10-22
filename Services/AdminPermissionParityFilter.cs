using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Student_Management_System.Services;

/// <summary>
/// Enforces Admin permission overrides on legacy role-authorized controllers.
/// Other roles retain their scoped controller rules; SuperAdmin always bypasses.
/// </summary>
public sealed class AdminPermissionParityFilter : IAsyncAuthorizationFilter
{
    private readonly PermissionService _permissions;

    public AdminPermissionParityFilter(PermissionService permissions) =>
        _permissions = permissions;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var principal = context.HttpContext.User;
        if (!principal.IsInRole("Admin") || principal.IsInRole("SuperAdmin"))
            return;

        var controller = context.RouteData.Values["controller"]?.ToString() ?? "";
        var action = context.RouteData.Values["action"]?.ToString() ?? "Index";
        var method = context.HttpContext.Request.Method;
        var permission = Resolve(controller, action, method);
        if (permission != null &&
            !await _permissions.HasAsync(
                principal,
                permission,
                context.HttpContext.RequestAborted))
            context.Result = new ForbidResult();
    }

    private static string? Resolve(string controller, string action, string method)
    {
        if (controller.Equals("Students", StringComparison.OrdinalIgnoreCase))
            return action switch
            {
                "Create" => "student.create",
                "Edit" => "student.update",
                "Delete" => "student.delete",
                _ => "student.view"
            };
        if (controller.Equals("Teachers", StringComparison.OrdinalIgnoreCase) ||
            controller.Equals("HodManagement", StringComparison.OrdinalIgnoreCase))
            return "ManageTeachers";
        if (controller.Equals("Departments", StringComparison.OrdinalIgnoreCase))
            return "ManageDepartments";
        if (new[] { "Classes", "Section", "Subject", "Semesters", "AcademicYear" }
            .Contains(controller, StringComparer.OrdinalIgnoreCase))
            return "ManageAcademics";
        if (controller.Equals("Attendance", StringComparison.OrdinalIgnoreCase))
            return method == HttpMethods.Get ? "attendance.view" : "attendance.correct";
        if (controller.Equals("Mark", StringComparison.OrdinalIgnoreCase))
            return action is "Approve" or "ReviewCorrection" ? "marks.approve"
                : method == HttpMethods.Get ? "marks.view" : "marks.enter";
        if (controller.Equals("Exam", StringComparison.OrdinalIgnoreCase))
            return action is "Publish" or "SetLocked" ? "results.publish"
                : action is "Create" or "Edit" ? "marks.approve" : "marks.view";
        if (controller.Equals("Reports", StringComparison.OrdinalIgnoreCase))
            return action.Contains("Export", StringComparison.OrdinalIgnoreCase)
                ? "reports.export"
                : "reports.view";
        if (controller.Equals("Admissions", StringComparison.OrdinalIgnoreCase))
            return method == HttpMethods.Get ? "admission.view"
                : action.Equals("Enroll", StringComparison.OrdinalIgnoreCase) ? "admission.approve"
                : "admission.review";
        if (controller.Equals("Finance", StringComparison.OrdinalIgnoreCase))
            return action.Equals("Collect", StringComparison.OrdinalIgnoreCase)
                ? "fees.collect"
                : action.Equals("Reverse", StringComparison.OrdinalIgnoreCase)
                    ? "fees.refund"
                    : "fees.view";
        if (controller.Equals("Fee", StringComparison.OrdinalIgnoreCase))
            return method == HttpMethods.Get ? "fees.view" : "fees.adjust";
        if (controller.Equals("Notice", StringComparison.OrdinalIgnoreCase))
            return method == HttpMethods.Get ? null : "ManageAnnouncements";
        return null;
    }
}
