using System.ComponentModel.DataAnnotations;

namespace Student_Management_System.Models;

public sealed class MarksEntryViewModel : IValidatableObject
{
    [Required]
    public int SubjectId { get; set; }

    [Required]
    public int ExamId { get; set; }

    public decimal MaximumMarks { get; set; } = 100;
    public decimal PassingMarks { get; set; } = 40;
    public List<StudentMarkEntryViewModel> Entries { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Entries.Count == 0)
            yield return new ValidationResult("Load the student roster before saving marks.", [nameof(Entries)]);
        if (Entries.Select(entry => entry.StudentId).Distinct().Count() != Entries.Count)
            yield return new ValidationResult("The roster contains duplicate students. Reload it before saving.", [nameof(Entries)]);
    }
}

public sealed class StudentMarkEntryViewModel
{
    public DateTime? ExpectedUpdatedUtc { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = "";
    public string SectionName { get; set; } = "";

    [Range(0, 1000000)]
    public decimal? Score { get; set; }

    [MaxLength(500)]
    public string? Remarks { get; set; }

    public bool IsApproved { get; set; }
}
