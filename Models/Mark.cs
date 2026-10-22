using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Student_Management_System.Models;

public class Mark
{
    public int Id { get; set; }

    // 👨‍🎓 Student
    public int StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    // 📚 Subject
    public int SubjectId { get; set; }
    public virtual Subject Subject { get; set; } = null!;

    // 📝 Exam
    public int ExamId { get; set; }
    public virtual Exam Exam { get; set; } = null!;

    // 🎯 Marks
    [Range(0, 1000000)]
    public decimal Score { get; set; }

    public string Grade { get; set; } = "";
    public int? EnteredByTeacherId { get; set; }
    public Teacher? EnteredByTeacher { get; set; }
    [MaxLength(500)] public string? Remarks { get; set; }
    [MaxLength(40)] public string ResultStatus { get; set; } = "Recorded";
    public decimal GraceMarks { get; set; }
    public bool IsWithheld { get; set; }
    public bool IsApproved { get; set; }
    public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
