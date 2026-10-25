using System.ComponentModel.DataAnnotations;

namespace Student_Management_System.Models;

public sealed class MarkEntryInputModel
{
    public DateTime? ExpectedUpdatedUtc { get; set; }
    [Range(1, int.MaxValue)] public int StudentId { get; set; }
    [Range(1, int.MaxValue)] public int SubjectId { get; set; }
    [Range(1, int.MaxValue)] public int ExamId { get; set; }
    [Required, Range(typeof(decimal), "0", "1000000")]
    public decimal? Score { get; set; }
    [MaxLength(500)] public string? Remarks { get; set; }
}
