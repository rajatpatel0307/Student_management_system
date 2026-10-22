using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Student_Management_System.Models;

namespace Student_Management_System.Models
{
    public class Student
    {
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; } = "";

        [Required, EmailAddress]
        public string Email { get; set; } = "";
        public string? Phone { get; set; }

        public DateTime DOB { get; set; }

        public string? Address { get; set; }

        public string? PhotoPath { get; set; }

        [MaxLength(40)] public string? AdmissionNumber { get; set; }
        [MaxLength(40)] public string? RollNumber { get; set; }
        [MaxLength(20)] public string? ApaarId { get; set; }
        [MaxLength(40)] public string? AcademicBankOfCreditsId { get; set; }
        [MaxLength(100)] public string? DigiLockerSubjectId { get; set; }
        [MaxLength(40)] public string StudentStatus { get; set; } = "Active";
        public DateTime? AdmissionDate { get; set; }
        public DateTime? ExpectedGraduationDate { get; set; }
        [MaxLength(160)] public string? EmergencyContactName { get; set; }
        [MaxLength(30)] public string? EmergencyContactPhone { get; set; }
        [MaxLength(10)] public string? BloodGroup { get; set; }

        public string? UserId { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        public int ClassId { get; set; }
        public int SectionId { get; set; }
        public int? DepartmentId { get; set; }
        public int? SemesterId { get; set; }
        public int? AcademicProgramId { get; set; }
        public AcademicProgram? AcademicProgram { get; set; }

        public Class? Class { get; set; }
        public Section? Section { get; set; }
        public Department? Department { get; set; }
        public Semester? Semester { get; set; }

        // Fix for CS1061: Add navigation property for Marks
        public virtual ICollection<Mark> Marks { get; set; } = [];
        public virtual ICollection<StudentClassEnrollment> ClassEnrollments { get; set; } = [];
        public virtual ICollection<StudentSubjectEnrollment> SubjectEnrollments { get; set; } = [];
        public bool IsArchived { get; set; }
        public DateTime? ArchivedUtc { get; set; }
        public string? ArchivedByUserId { get; set; }
        [MaxLength(500)] public string? ArchiveReason { get; set; }
        public DateTime? RestoredUtc { get; set; }
        public string? RestoredByUserId { get; set; }
        [Timestamp] public byte[] RowVersion { get; set; } = [];
    }
}
