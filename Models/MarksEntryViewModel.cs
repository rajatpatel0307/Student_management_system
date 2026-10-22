using System.ComponentModel.DataAnnotations;

namespace Student_Management_System.Models;

public sealed class MarksEntryViewModel
{
    [Required]
    public int SubjectId { get; set; }

    [Required]
    public int ExamId { get; set; }

    public decimal MaximumMarks { get; set; } = 100;
    public decimal PassingMarks { get; set; } = 40;
    public List<StudentMarkEntryViewModel> Entries { get; set; } = new();
}

public sealed class StudentMarkEntryViewModel
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = "";
    public string SectionName { get; set; } = "";

    [Range(0, 1000000)]
    public decimal? Score { get; set; }

    [MaxLength(500)]
    public string? Remarks { get; set; }

    public bool IsApproved { get; set; }
}
