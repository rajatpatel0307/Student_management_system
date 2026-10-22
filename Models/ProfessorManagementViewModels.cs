using System.ComponentModel.DataAnnotations;

namespace Student_Management_System.Models;

public sealed class ProfessorListItemViewModel
{
    public int TeacherId { get; init; }
    public string ProfessorNumber { get; init; } = "";
    public string FullName { get; init; } = "";
    public string Email { get; init; } = "";
    public string? Phone { get; init; }
    public string DepartmentName { get; init; } = "";
    public string Designation { get; init; } = "Professor";
    public bool IsActive { get; init; }
    public bool HasProfileImage { get; init; }
    public IReadOnlyList<string> AssignedSubjects { get; init; } = [];
    public IReadOnlyList<string> AssignedClasses { get; init; } = [];
}

public sealed class ProfessorEditInputModel
{
    [Required] public int TeacherId { get; set; }
    [Required, MaxLength(160)] public string FullName { get; set; } = "";
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = "";
    [MaxLength(30)] public string? Phone { get; set; }
    [MaxLength(160)] public string Qualification { get; set; } = "";
    public bool IsActive { get; set; }
    [Required] public byte[] RowVersion { get; set; } = [];
}

public sealed class StudentAuthoritativeEditInputModel
{
    [Required] public int StudentId { get; set; }
    [Required, MaxLength(160)] public string FullName { get; set; } = "";
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = "";
    [MaxLength(30)] public string? Phone { get; set; }
    [MaxLength(40)] public string? AdmissionNumber { get; set; }
    [MaxLength(40)] public string? RollNumber { get; set; }
    public bool IsActive { get; set; }
    [Required] public byte[] RowVersion { get; set; } = [];
}
