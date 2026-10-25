using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Student_Management_System.Services;

/// <summary>
/// Enforces administrator permission overrides on legacy role-authorized controllers.
/// SuperAdmin retains its default grants, but explicit per-user denials are authoritative.
/// </summary>
public sealed class AdminPermissionParityFilter : IAsyncAuthorizationFilter
{
    private readonly PermissionService _permissions;

    public AdminPermissionParityFilter(PermissionService permissions) =>
        _permissions = permissions;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var principal = context.HttpContext.User;
        if (principal.Identity?.IsAuthenticated != true) return;

        var controller = context.RouteData.Values["controller"]?.ToString() ?? "";
        var action = context.RouteData.Values["action"]?.ToString() ?? "Index";
        var method = context.HttpContext.Request.Method;
        var permission = Resolve(controller, action, method);
        if (permission != null && (principal.IsInRole("Admin") || principal.IsInRole("SuperAdmin")
            ? !await _permissions.HasAsync(principal, permission, context.HttpContext.RequestAborted)
            : await _permissions.IsExplicitlyDeniedAsync(principal, permission, context.HttpContext.RequestAborted)))
            context.Result = new ForbidResult();
    }

    private static string? Resolve(string controller, string action, string method)
    {
        action = action.ToLowerInvariant();
        var read = HttpMethods.IsGet(method) || HttpMethods.IsHead(method);
        if (controller.Equals("Students", StringComparison.OrdinalIgnoreCase))
            return action switch
            {
                "create" => "student.create",
                "edit" => "student.update",
                "delete" => "student.delete",
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
            return action is "mark" or "selectclass" ? "attendance.create"
                : action is "approvesession" or "reviewcorrection" ? "attendance.approve"
                : action == "export" ? "reports.export"
                : action is "edit" or "update" or "requestcorrection" ? "attendance.correct" : "attendance.view";
        if (controller.Equals("Mark", StringComparison.OrdinalIgnoreCase))
            return action == "reviewcorrection" ? "results.publish" : action == "approve" ? "marks.approve"
                : read ? "marks.view" : "marks.enter";
        if (controller.Equals("Exam", StringComparison.OrdinalIgnoreCase))
            return action is "publish" or "setlocked" ? "results.publish"
                : action is "create" or "edit" ? "marks.approve" : "marks.view";
        if (controller.Equals("Reports", StringComparison.OrdinalIgnoreCase))
            return action.Contains("export", StringComparison.Ordinal) || action == "studentsexcel"
                ? "reports.export"
                : "reports.view";
        if (controller.Equals("Admissions", StringComparison.OrdinalIgnoreCase))
            return action is "create" or "createinquiry" ? "admission.create" : read ? "admission.view"
                : action is "enroll" or "resendactivation" ? "admission.approve"
                : "admission.review";
        if (controller.Equals("Finance", StringComparison.OrdinalIgnoreCase))
            return action.Equals("Collect", StringComparison.OrdinalIgnoreCase)
                ? "fees.collect"
                : action.Equals("Reverse", StringComparison.OrdinalIgnoreCase)
                    ? "fees.refund"
                    : "fees.view";
        if (controller.Equals("Fee", StringComparison.OrdinalIgnoreCase))
            return read ? "fees.view" : "fees.adjust";
        if (controller.Equals("Notice", StringComparison.OrdinalIgnoreCase))
            return read ? null : "ManageAnnouncements";
        return null;
    }
}
