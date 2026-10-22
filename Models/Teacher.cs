using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Student_Management_System.Models;

namespace Student_Management_System.Models
{
    public class Teacher
    {
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; } = "";

        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";

        public string Qualification { get; set; } = "";

        [Required]
        public TeacherType TeacherType { get; set; } = TeacherType.Professor;

        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        public string UserId { get; set; } = "";

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;

        // ✅ ONE → MANY
        public ICollection<Class> Classes { get; set; } = [];

        // 📚 Subjects
        public virtual ICollection<Subject> Subjects { get; set; } = [];
        public virtual ICollection<TeacherClassAssignment> ClassAssignments { get; set; } = [];
        public virtual ICollection<TeacherSubjectAssignment> SubjectAssignments { get; set; } = [];
        public bool IsArchived { get; set; }
        public DateTime? ArchivedUtc { get; set; }
        [MaxLength(450)] public string? ArchivedByUserId { get; set; }
        [MaxLength(500)] public string? ArchiveReason { get; set; }
        public DateTime? RestoredUtc { get; set; }
        [MaxLength(450)] public string? RestoredByUserId { get; set; }
        [Timestamp] public byte[] RowVersion { get; set; } = [];
    }

    public enum TeacherType
    {
        HOD = 1,
        Professor = 2
    }
}
