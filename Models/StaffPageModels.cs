using Student_Management_System.Services;
namespace Student_Management_System.Models;
public sealed class StaffDirectoryModel
{
    public IReadOnlyList<StaffMember> Items { get; set; } = [];
    public string? Search { get; set; }
    public string? Role { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; }
    public int Total { get; set; }
    public int TotalStaff { get; set; }
    public int ActiveStaff { get; set; }
    public int Pages => Math.Max(1, (int)Math.Ceiling(Total / 10d));
}
public sealed class StaffEditorModel
{
    public int? Id { get; set; }
    public StaffInput Input { get; set; } = new() { JoiningDate = DateTime.Today };
    public IReadOnlyList<Department> Departments { get; set; } = [];
}
public sealed record StaffWorkspacePageModel(StaffWorkspace Workspace, string Page, StaffPage? Definition);
