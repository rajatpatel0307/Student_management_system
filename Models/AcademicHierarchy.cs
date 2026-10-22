using System.ComponentModel.DataAnnotations;

namespace Student_Management_System.Models;

public class Department
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    [Required, MaxLength(20)] public string Code { get; set; } = "";
    public int? HodTeacherId { get; set; }
    public Teacher? HodTeacher { get; set; }
    public ICollection<Teacher> Teachers { get; set; } = [];
    public ICollection<Class> Classes { get; set; } = [];
    public ICollection<Student> Students { get; set; } = [];
    public ICollection<Semester> Semesters { get; set; } = [];
    public bool IsArchived { get; set; }
    public DateTime? ArchivedUtc { get; set; }
    [MaxLength(450)] public string? ArchivedByUserId { get; set; }
    [MaxLength(500)] public string? ArchiveReason { get; set; }
    public DateTime? RestoredUtc { get; set; }
    [MaxLength(450)] public string? RestoredByUserId { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public class Semester
{
    public int Id { get; set; }
    [Required, MaxLength(50)] public string Name { get; set; } = "";
    [Range(1, 12)] public int Number { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public bool IsArchived { get; set; }
    public DateTime? ArchivedUtc { get; set; }
    [MaxLength(450)] public string? ArchivedByUserId { get; set; }
    [MaxLength(500)] public string? ArchiveReason { get; set; }
    public DateTime? RestoredUtc { get; set; }
    [MaxLength(450)] public string? RestoredByUserId { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public class TeacherClassAssignment
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public int ClassId { get; set; }
    public Class Class { get; set; } = null!;
    public DateTime AssignedUtc { get; set; } = DateTime.UtcNow;
}

public class TeacherSubjectAssignment
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public DateTime AssignedUtc { get; set; } = DateTime.UtcNow;
}

public class StudentClassEnrollment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int ClassId { get; set; }
    public Class Class { get; set; } = null!;
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}

public class StudentSubjectEnrollment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public DateTime EnrolledUtc { get; set; } = DateTime.UtcNow;
}

public class Permission
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Key { get; set; } = "";
    [Required, MaxLength(160)] public string Description { get; set; } = "";
}

public class UserPermission
{
    public int Id { get; set; }
    [Required, MaxLength(450)] public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
    public bool IsGranted { get; set; } = true;
}

public class RolePermission
{
    public int Id { get; set; }
    [Required, MaxLength(256)] public string RoleName { get; set; } = "";
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}

public class LoginHistory
{
    public long Id { get; set; }
    [MaxLength(450)] public string? UserId { get; set; }
    [Required, MaxLength(256)] public string Email { get; set; } = "";
    public bool Succeeded { get; set; }
    [MaxLength(64)] public string? IpAddress { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class SystemSetting
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Key { get; set; } = "";
    [MaxLength(2000)] public string Value { get; set; } = "";
}

public class StudyMaterial
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public int UploadedByTeacherId { get; set; }
    public Teacher UploadedByTeacher { get; set; } = null!;
    [Required, MaxLength(160)] public string Title { get; set; } = "";
    [Required, MaxLength(500)] public string FilePath { get; set; } = "";
    [MaxLength(100)] public string ContentType { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
