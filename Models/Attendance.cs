using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Student_Management_System.Models;

public class Attendance
{
    public int Id { get; set; }
    public long? AttendanceSessionId { get; set; }
    public AttendanceSession? AttendanceSession { get; set; }

    // 👨‍🎓 Student
    public int StudentId { get; set; }

    [ForeignKey("StudentId")]
    public virtual Student Student { get; set; } = null!;
    public int? SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public int? TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
    [MaxLength(100)] public string? Lecture { get; set; }
    [ConcurrencyCheck] public DateTime? UpdatedUtc { get; set; }

    // 📅 Date
    public DateTime Date { get; set; }

    // ✅ Status (Present / Absent)
    [Required, MaxLength(20)]
    [ConcurrencyCheck]
    public string Status { get; set; } = "";
}
