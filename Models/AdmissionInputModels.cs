using System.ComponentModel.DataAnnotations;

namespace Student_Management_System.Models;

// Public admission forms deliberately expose only applicant-editable fields.
// Workflow state, archive metadata, reviewer assignment, audit ownership and
// enrollment links can only be changed by their dedicated commands.
public sealed class AdmissionApplicationInput
{
    public long Id { get; set; }

    [Required, MaxLength(160)]
    public string ApplicantName { get; set; } = "";

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    [MaxLength(30)]
    public string? Phone { get; set; }

    [DataType(DataType.Date)]
    public DateTime DateOfBirth { get; set; }

    [MaxLength(1000)]
    public string? Address { get; set; }

    [Range(1, int.MaxValue)]
    public int AcademicYearId { get; set; }

    [Range(1, int.MaxValue)]
    public int DepartmentId { get; set; }

    public int? ClassId { get; set; }

    [Required, MaxLength(120)]
    public string ProgramName { get; set; } = "";

    [MaxLength(80)]
    public string Source { get; set; } = "Direct";

    [MaxLength(2000)]
    public string? InternalNotes { get; set; }

    [MaxLength(2000)]
    public string? ApplicantNotes { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public static AdmissionApplicationInput FromEntity(AdmissionApplication application) => new()
    {
        Id = application.Id,
        ApplicantName = application.ApplicantName,
        Email = application.Email,
        Phone = application.Phone,
        DateOfBirth = application.DateOfBirth,
        Address = application.Address,
        AcademicYearId = application.AcademicYearId,
        DepartmentId = application.DepartmentId,
        ClassId = application.ClassId,
        ProgramName = application.ProgramName,
        Source = application.Source,
        InternalNotes = application.InternalNotes,
        ApplicantNotes = application.ApplicantNotes,
        RowVersion = application.RowVersion
    };
}
