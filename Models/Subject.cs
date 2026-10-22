using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Student_Management_System.Models;
using System.Collections.Generic;




namespace Student_Management_System.Models
{
    public class Subject
    {
        public int Id { get; set; }

        [Required, MaxLength(120)]
        public string Name { get; set; } = "";

        // 🎓 Class relation
        public int ClassId { get; set; }

        [ForeignKey("ClassId")]
        public Class Class { get; set; } = null!;

        // 👨‍🏫 Teacher relation
        public int? TeacherId { get; set; }

        [ForeignKey("TeacherId")]
        public Teacher? Teacher { get; set; }

        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }
        public int? SemesterId { get; set; }
        public Semester? Semester { get; set; }
        public int? AcademicSubjectTypeId { get; set; }
        public AcademicSubjectType? AcademicSubjectType { get; set; }
        [Column(TypeName = "decimal(8,2)")] public decimal CreditValue { get; set; }
        [MaxLength(80)] public string? ElectiveGroup { get; set; }

        // Add this property to fix CS1061
        public ICollection<Mark> Marks { get; set; } = [];
        public ICollection<TeacherSubjectAssignment> TeacherAssignments { get; set; } = [];
        public ICollection<StudentSubjectEnrollment> StudentEnrollments { get; set; } = [];
        public bool IsArchived { get; set; }
        public DateTime? ArchivedUtc { get; set; }
        [MaxLength(450)] public string? ArchivedByUserId { get; set; }
        [MaxLength(500)] public string? ArchiveReason { get; set; }
        public DateTime? RestoredUtc { get; set; }
        [MaxLength(450)] public string? RestoredByUserId { get; set; }
        [Timestamp] public byte[] RowVersion { get; set; } = [];
    }
}
