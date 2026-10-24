using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Controllers;
using Student_Management_System.Models;
using Student_Management_System.Services;

internal static class StudentDirectoryVerification
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        // Translate with the production SQL Server provider, without opening a connection.
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=DirectoryTranslationOnly;Integrated Security=true;Encrypt=true")
            .Options);
        var controller = new StudentsController(db, null!, null!, null!, null!, null!, null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "directory-test"),
                        new Claim(ClaimTypes.Role, "Admin")
                    }, "test"))
                }
            }
        };
        var build = typeof(StudentsController).GetMethod("BuildDirectoryQueryAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        async Task<IQueryable<Student>> Query(ListQuery request) =>
            (await (Task<IQueryable<Student>?>)build.Invoke(controller, new object[] { request, CancellationToken.None })!)!;

        var sql = (await Query(new ListQuery { Search = "987", DepartmentId = 3, ClassId = 4, SectionId = 5, SemesterId = 6 })).ToQueryString();
        check(sql.Contains("[IsArchived] = CAST(0 AS bit)"), "Directory/export SQL must exclude archived students.");
        check(sql.Contains("[Phone] LIKE"), "Phone searches must work in both the directory and export.");
        foreach (var field in new[] { "DepartmentId", "ClassId", "SectionId", "SemesterId" })
            check(sql.Contains($"[{field}] = @"), $"Directory/export SQL must apply {field}.");
        sql = (await Query(new ListQuery { Status = "low-attendance" })).ToQueryString();
        check(sql.Contains("HAVING") && sql.Contains("75"), "Low-attendance export must translate the recorded-session threshold to SQL.");
        sql = (await Query(new ListQuery { Status = "inactive" })).ToQueryString();
        check(sql.Contains("[LockoutEnd]") && sql.Contains("IS NULL"), "Inactive accounts include locked and unlinked students.");
        check(sql.Contains("[IsActive]"), "Disabled accounts must be included in inactive filtering.");
        var guardian = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "guardian-test"), new Claim(ClaimTypes.Role, "Guardian")
        }, "test"));
        var visibleResults = await new ResultService(db, null!).ScopeAsync(db.Marks, guardian);
        sql = visibleResults.ToQueryString();
        check(sql.Contains("[UnlinkedUtc] IS NULL") && sql.Contains("[CanViewAcademics] = CAST(1 AS bit)"),
            "Guardian results must translate active academic link restrictions to SQL.");
        sql = StudentRecordScope.ForHod(db, db.Students, "hod-test", 3).ToQueryString();
        check(sql.Contains("UserCampusMemberships") && sql.Contains("Institutions") && sql.Contains("Campuses"),
            "HOD row scope must translate active campus/institution membership into SQL.");

        var campus = new Campus { Id = 1, IsActive = true, Institution = new Institution { IsActive = true } };
        var members = new[]
        {
            new UserCampusMembership { UserId = "hod", CampusId = 1, Campus = campus },
            new UserCampusMembership { UserId = "shared", CampusId = 1, Campus = campus },
            new UserCampusMembership { UserId = "revoked", CampusId = 1, Campus = campus, IsActive = false },
            new UserCampusMembership { UserId = "outside", CampusId = 2, Campus = campus }
        }.AsQueryable();
        var students = new[]
        {
            new Student { Id = 1, UserId = "shared", DepartmentId = 3 },
            new Student { Id = 2, UserId = "outside", DepartmentId = 3 },
            new Student { Id = 3, UserId = "revoked", DepartmentId = 3 },
            new Student { Id = 4, UserId = "shared", DepartmentId = 4 },
            new Student { Id = 5, UserId = null, DepartmentId = 3 }
        }.AsQueryable();
        check(StudentRecordScope.ForHod(students, members, "hod", 3).Select(s => s.Id).SequenceEqual(new[] { 1 }),
            "HOD cannot read other campuses/departments, revoked memberships, or unlinked student records.");
        check(!StudentRecordScope.ForHod(students, members, null, 3).Any(), "Missing HOD identity must deny all rows.");
        campus.Institution.IsActive = false;
        check(!StudentRecordScope.ForHod(students, members, "hod", 3).Any(), "Inactive institutions must deny HOD rows.");

        var input = new Student { ClassId = 10, SectionId = 11, DepartmentId = 3, SemesterId = 12 };
        var placement = new Class { Id = 10, DepartmentId = 3 };
        var section = new Section { Id = 11, ClassId = 10 };
        var semester = new Semester { Id = 12, DepartmentId = 3 };
        check(StudentPlacementRules.Validate(input, placement, section, semester).Count == 0, "Compatible placement must remain valid.");
        section.ClassId = 99;
        check(StudentPlacementRules.Validate(input, placement, section, semester).Any(e => e.Field == "SectionId"), "Cross-class sections must be rejected before persistence.");
        section.ClassId = 10;
        input.DepartmentId = null;
        semester.DepartmentId = 4;
        check(StudentPlacementRules.Validate(input, placement, section, semester).Any(e => e.Field == "SemesterId"), "Omitting department cannot bypass class-department semester validation.");
        semester.DepartmentId = 3;
        placement.IsArchived = true;
        check(StudentPlacementRules.Validate(input, placement, section, semester).Any(e => e.Field == "ClassId"), "Archived classes must reject placement.");

        var sort = typeof(StudentsController).GetMethod("SortDirectory", BindingFlags.Static | BindingFlags.NonPublic)!;
        var sameNames = new[] { new Student { Id = 9, FullName = "Alex" }, new Student { Id = 2, FullName = "Alex" } }.AsQueryable();
        var ordered = (IQueryable<Student>)sort.Invoke(null, new object[] { sameNames, new ListQuery() })!;
        check(ordered.Select(s => s.Id).SequenceEqual(new[] { 2, 9 }), "Students with identical names need stable paging order.");
        foreach (var endpoint in new[] { "Index", "Export" })
        {
            var roles = typeof(StudentsController).GetMethod(endpoint)!
                .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()?.Roles;
            check(roles == "Admin,SuperAdmin,HOD", $"{endpoint} must retain directory role restrictions.");
        }
    }
}
