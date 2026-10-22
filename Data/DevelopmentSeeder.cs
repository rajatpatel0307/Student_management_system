using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;

namespace Student_Management_System.Data;

public static class DevelopmentSeeder
{
    public static async Task SeedAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        IConfiguration configuration)
    {
        var password = configuration["DevelopmentSeed:Password"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "DevelopmentSeed:Password must be supplied through user-secrets or an environment variable when SeedDevelopmentData is enabled.");
        async Task<ApplicationUser> UserAsync(string email, string name, string role)
        {
            var user = await users.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = name };
                var result = await users.CreateAsync(user, password);
                if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }
            if (!await users.IsInRoleAsync(user, role)) await users.AddToRoleAsync(user, role);
            return user;
        }

        await UserAsync("superadmin@example.com", "Development Super Admin", "SuperAdmin");
        var adminUser = await UserAsync("admin@example.com", "Development Admin", "Admin");
        var hodUser = await UserAsync("hod@example.com", "Development HOD", "HOD");
        var professorUser = await UserAsync("professor@example.com", "Development Professor", "Professor");
        var studentUser = await UserAsync("student@example.com", "Development Student", "Student");
        var guardianUser = await UserAsync("guardian@example.com", "Development Guardian", "Guardian");
        await UserAsync("accountant@example.com", "Development Accountant", "Accountant");
        await UserAsync("librarian@example.com", "Development Librarian", "Librarian");
        await UserAsync("admissions@example.com", "Development Admission Officer", "AdmissionOfficer");
        await UserAsync("exams@example.com", "Development Examination Officer", "ExaminationOfficer");
        var permissions = await db.Permissions.ToListAsync();
        foreach (var permission in permissions)
            if (!await db.UserPermissions.AnyAsync(p => p.UserId == adminUser.Id && p.PermissionId == permission.Id))
                db.Add(new UserPermission { UserId = adminUser.Id, PermissionId = permission.Id });
        await db.SaveChangesAsync();

        var year = await db.AcademicYears.FirstOrDefaultAsync(y => y.Name == "2026-27");
        if (year == null)
        {
            year = new AcademicYear { Name = "2026-27", StartsOn = new DateTime(2026, 7, 1), EndsOn = new DateTime(2027, 6, 30), IsCurrent = true };
            db.Add(year); await db.SaveChangesAsync();
        }
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Code == "CSE");
        if (department == null)
        {
            department = new Department { Name = "Computer Science", Code = "CSE" };
            db.Add(department); await db.SaveChangesAsync();
        }
        var hod = await db.Teachers.FirstOrDefaultAsync(t => t.UserId == hodUser.Id);
        if (hod == null)
        {
            hod = new Teacher { FullName = hodUser.FullName, Email = hodUser.Email!, Phone = "9000000001", Qualification = "PhD", UserId = hodUser.Id, TeacherType = TeacherType.HOD, DepartmentId = department.Id };
            db.Add(hod); await db.SaveChangesAsync();
        }
        department.HodTeacherId = hod.Id;
        var professor = await db.Teachers.FirstOrDefaultAsync(t => t.UserId == professorUser.Id);
        if (professor == null)
        {
            professor = new Teacher { FullName = professorUser.FullName, Email = professorUser.Email!, Phone = "9000000002", Qualification = "M.Tech", UserId = professorUser.Id, TeacherType = TeacherType.Professor, DepartmentId = department.Id };
            db.Add(professor); await db.SaveChangesAsync();
        }
        var semester = await db.Semesters.FirstOrDefaultAsync(s => s.DepartmentId == department.Id && s.Number == 1);
        if (semester == null)
        {
            semester = new Semester { Name = "Semester 1", Number = 1, DepartmentId = department.Id, AcademicYearId = year.Id };
            db.Add(semester); await db.SaveChangesAsync();
        }
        var cls = await db.Classes.FirstOrDefaultAsync(c => c.Name == "BSc CS 1");
        if (cls == null)
        {
            cls = new Class { Name = "BSc CS 1", TeacherId = professor.Id, DepartmentId = department.Id, SemesterId = semester.Id };
            db.Add(cls); await db.SaveChangesAsync();
        }
        var section = await db.Sections.FirstOrDefaultAsync(s => s.ClassId == cls.Id && s.Name == "A");
        if (section == null) { section = new Section { Name = "A", ClassId = cls.Id }; db.Add(section); await db.SaveChangesAsync(); }
        var subject = await db.Subjects.FirstOrDefaultAsync(s => s.ClassId == cls.Id && s.Name == "Programming Fundamentals");
        if (subject == null)
        {
            subject = new Subject { Name = "Programming Fundamentals", ClassId = cls.Id, TeacherId = professor.Id, DepartmentId = department.Id, SemesterId = semester.Id };
            db.Add(subject); await db.SaveChangesAsync();
        }
        if (!await db.TeacherClassAssignments.AnyAsync(a => a.TeacherId == professor.Id && a.ClassId == cls.Id))
            db.Add(new TeacherClassAssignment { TeacherId = professor.Id, ClassId = cls.Id });
        if (!await db.TeacherSubjectAssignments.AnyAsync(a => a.TeacherId == professor.Id && a.SubjectId == subject.Id))
            db.Add(new TeacherSubjectAssignment { TeacherId = professor.Id, SubjectId = subject.Id });
        var student = await db.Students.FirstOrDefaultAsync(s => s.UserId == studentUser.Id);
        if (student == null)
        {
            student = new Student { FullName = studentUser.FullName, Email = studentUser.Email!, Phone = "9000000003", DOB = new DateTime(2006, 1, 1), UserId = studentUser.Id, DepartmentId = department.Id, SemesterId = semester.Id, ClassId = cls.Id, SectionId = section.Id };
            db.Add(student); await db.SaveChangesAsync();
        }
        if (!await db.StudentClassEnrollments.AnyAsync(e => e.StudentId == student.Id && e.AcademicYearId == year.Id))
            db.Add(new StudentClassEnrollment { StudentId = student.Id, ClassId = cls.Id, SectionId = section.Id, AcademicYearId = year.Id });
        if (!await db.StudentSubjectEnrollments.AnyAsync(e => e.StudentId == student.Id && e.SubjectId == subject.Id))
            db.Add(new StudentSubjectEnrollment { StudentId = student.Id, SubjectId = subject.Id });
        await db.SaveChangesAsync();

        var guardian = await db.GuardianProfiles.FirstOrDefaultAsync(g => g.UserId == guardianUser.Id);
        if (guardian == null)
        {
            guardian = new GuardianProfile
            {
                UserId = guardianUser.Id,
                FullName = guardianUser.FullName,
                Relationship = "Parent",
                Email = guardianUser.Email!,
                Phone = "9000000004"
            };
            db.Add(guardian);
            await db.SaveChangesAsync();
        }
        if (!await db.GuardianStudentLinks.AnyAsync(l => l.GuardianProfileId == guardian.Id && l.StudentId == student.Id))
        {
            db.Add(new GuardianStudentLink
            {
                GuardianProfileId = guardian.Id,
                StudentId = student.Id,
                IsPrimary = true,
                CanViewFinance = true
            });
            await db.SaveChangesAsync();
        }
    }
}
