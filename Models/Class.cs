using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Student_Management_System.Models;

namespace Student_Management_System.Models
{
    public class Class
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = "";

        // 👨‍🏫 Class Teacher
        [Required]
        public int TeacherId { get; set; }

        public Teacher? Teacher { get; set; }

        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }
        public int? SemesterId { get; set; }
        public Semester? Semester { get; set; }

        // 👨‍🎓 Students
        public virtual ICollection<Student> Students { get; set; } = [];

        // 🏫 Sections
        public virtual ICollection<Section> Sections { get; set; } = [];
        public virtual ICollection<TeacherClassAssignment> TeacherAssignments { get; set; } = [];
        public bool IsArchived { get; set; }
        public DateTime? ArchivedUtc { get; set; }
        [MaxLength(450)] public string? ArchivedByUserId { get; set; }
        [MaxLength(500)] public string? ArchiveReason { get; set; }
        public DateTime? RestoredUtc { get; set; }
        [MaxLength(450)] public string? RestoredByUserId { get; set; }
        [Timestamp] public byte[] RowVersion { get; set; } = [];
    }
}
