using System.Collections.ObjectModel;

namespace Student_Management_System.Security;

public static class DefaultRolePermissions
{
    public static IReadOnlyDictionary<string, string[]> All { get; } =
        new ReadOnlyDictionary<string, string[]>(
            new Dictionary<string, string[]>
    {
        ["Registrar"] = ["registrar.manage", "admission.view", "admission.create", "admission.review", "admission.approve", "documents.generate", PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView],
        ["Admin"] = [
            "staff.manage", "transport.manage", "hostel.manage", "registrar.manage", "finance.expenses.manage", "library.manage",
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
        ["Accountant"] = ["finance.expenses.manage", "fees.view", "fees.collect", "fees.adjust", "fees.refund", "finance.invoice", "approvals.manage", "reports.view", "reports.export", PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView, PermissionKeys.DashboardExport],
        ["Librarian"] = ["library.manage", "library.issue", "library.return", "library.audit", "reports.view", PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView, PermissionKeys.DashboardExport],
        ["ExaminationOfficer"] = ["marks.view", "marks.approve", "results.publish", "approvals.manage", "reports.view", "reports.export", PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView, PermissionKeys.DashboardExport],
        ["HOD"] = ["student.view", "student.lifecycle.approve", "attendance.view", "attendance.create", "attendance.correct", "attendance.approve", "marks.view", "marks.enter", "marks.approve", "approvals.manage", "reports.view", "reports.export", PermissionKeys.ProfileImageEditSelf, PermissionKeys.ProfileStudentEdit, PermissionKeys.ProfileProfessorEdit, PermissionKeys.ProfessorViewDepartment, PermissionKeys.ProfessorManageDepartment, PermissionKeys.DashboardView, PermissionKeys.DashboardExport],
        ["Professor"] = ["student.view", "attendance.view", "attendance.create", "marks.view", "marks.enter", "timetable.manage", "reports.export", PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView, PermissionKeys.DashboardExport],
        ["Student"] = ["attendance.view", "marks.view", PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView],
        ["Guardian"] = ["attendance.view", "marks.view", "fees.view", PermissionKeys.ProfileImageEditSelf],
        ["TransportManager"] = ["transport.manage", PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView],
        ["HostelManager"] = ["hostel.manage", PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView],
        ["PlacementOfficer"] = [PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView],
        ["Employee"] = [PermissionKeys.ProfileImageEditSelf, PermissionKeys.DashboardView]
    }
        );
}