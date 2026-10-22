using Student_Management_System.Models;

namespace Student_Management_System.Security;

public static class PermissionKeys
{
    public const string ProfileImageEditSelf = "profile.image.edit-self";
    public const string ProfileStudentEdit = "profile.student.edit";
    public const string ProfileProfessorEdit = "profile.professor.edit";
    public const string ProfileHodEdit = "profile.hod.edit";
    public const string ProfileOperationalStaffEdit = "profile.operational-staff.edit";
    public const string ProfileAdminEdit = "profile.admin.edit";
    public const string ProfileSuperAdminEdit = "profile.superadmin.edit";
    public const string ProfessorViewDepartment = "professor.view-department";
    public const string ProfessorManageDepartment = "professor.manage-department";
    public const string DashboardView = "dashboard.view";
    public const string DashboardExport = "dashboard.export";
}

public static class ProfilePolicies
{
    public const string ImageEditSelf = "Profile.Image.EditSelf";
    public const string StudentEdit = "Profile.Student.Edit";
    public const string ProfessorEdit = "Profile.Professor.Edit";
    public const string HodEdit = "Profile.Hod.Edit";
    public const string OperationalStaffEdit = "Profile.OperationalStaff.Edit";
    public const string AdminEdit = "Profile.Admin.Edit";
    public const string SuperAdminEdit = "Profile.SuperAdmin.Edit";
    public const string ProfessorViewDepartment = "Professor.ViewDepartment";
    public const string ProfessorManageDepartment = "Professor.ManageDepartment";
}

public static class DashboardPolicies
{
    public const string View = "Dashboard.View";
    public const string Export = "Dashboard.Export";
}

public static class AcademicStaffRules
{
    public static bool IsProfessor(Teacher teacher) =>
        teacher.TeacherType == TeacherType.Professor && !teacher.IsArchived;
}
