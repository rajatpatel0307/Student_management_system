using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Student_Management_System.Models;

#nullable disable

namespace Student_Management_System.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260725000200_AcademicRoleHierarchy")]
public class AcademicRoleHierarchy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        DROP INDEX [IX_Attendances_StudentId_Date] ON [Attendances];

        CREATE TABLE [Departments](
          [Id] int IDENTITY PRIMARY KEY,[Name] nvarchar(120) NOT NULL,[Code] nvarchar(20) NOT NULL,
          [HodTeacherId] int NULL);
        CREATE UNIQUE INDEX [IX_Departments_Code] ON [Departments]([Code]);

        ALTER TABLE [Teachers] ADD [TeacherType] int NOT NULL CONSTRAINT [DF_Teachers_TeacherType] DEFAULT 2;
        ALTER TABLE [Teachers] ADD [DepartmentId] int NULL;
        ALTER TABLE [Students] ADD [DepartmentId] int NULL;
        ALTER TABLE [Students] ADD [SemesterId] int NULL;
        ALTER TABLE [Classes] ADD [DepartmentId] int NULL;
        ALTER TABLE [Classes] ADD [SemesterId] int NULL;
        ALTER TABLE [Subjects] ADD [DepartmentId] int NULL;
        ALTER TABLE [Subjects] ADD [SemesterId] int NULL;
        ALTER TABLE [Attendances] ADD [SubjectId] int NULL;
        ALTER TABLE [Attendances] ADD [TeacherId] int NULL;
        ALTER TABLE [Attendances] ADD [Lecture] nvarchar(100) NULL;
        ALTER TABLE [Attendances] ADD [UpdatedUtc] datetime2 NULL;
        ALTER TABLE [Exams] ADD [ExamType] nvarchar(50) NOT NULL CONSTRAINT [DF_Exams_ExamType] DEFAULT 'Internal';
        ALTER TABLE [Exams] ADD [IsLocked] bit NOT NULL CONSTRAINT [DF_Exams_IsLocked] DEFAULT 0;
        ALTER TABLE [Exams] ADD [IsApproved] bit NOT NULL CONSTRAINT [DF_Exams_IsApproved] DEFAULT 0;
        ALTER TABLE [Marks] ADD [EnteredByTeacherId] int NULL;
        ALTER TABLE [Marks] ADD [Remarks] nvarchar(500) NULL;
        ALTER TABLE [Marks] ADD [IsApproved] bit NOT NULL CONSTRAINT [DF_Marks_IsApproved] DEFAULT 0;
        ALTER TABLE [Marks] ADD [UpdatedUtc] datetime2 NOT NULL CONSTRAINT [DF_Marks_UpdatedUtc] DEFAULT GETUTCDATE();
        ALTER TABLE [Notices] ADD [DepartmentId] int NULL;

        CREATE TABLE [Semesters](
          [Id] int IDENTITY PRIMARY KEY,[Name] nvarchar(50) NOT NULL,[Number] int NOT NULL,
          [DepartmentId] int NOT NULL,[AcademicYearId] int NOT NULL,
          CONSTRAINT [FK_Semesters_Departments] FOREIGN KEY([DepartmentId]) REFERENCES [Departments]([Id]),
          CONSTRAINT [FK_Semesters_AcademicYears] FOREIGN KEY([AcademicYearId]) REFERENCES [AcademicYears]([Id]));
        CREATE UNIQUE INDEX [IX_Semesters_Department_Year_Number] ON [Semesters]([DepartmentId],[AcademicYearId],[Number]);

        CREATE TABLE [TeacherClassAssignments](
          [Id] int IDENTITY PRIMARY KEY,[TeacherId] int NOT NULL,[ClassId] int NOT NULL,[AssignedUtc] datetime2 NOT NULL,
          CONSTRAINT [FK_TCA_Teachers] FOREIGN KEY([TeacherId]) REFERENCES [Teachers]([Id]),
          CONSTRAINT [FK_TCA_Classes] FOREIGN KEY([ClassId]) REFERENCES [Classes]([Id]));
        CREATE UNIQUE INDEX [IX_TCA_Teacher_Class] ON [TeacherClassAssignments]([TeacherId],[ClassId]);

        CREATE TABLE [TeacherSubjectAssignments](
          [Id] int IDENTITY PRIMARY KEY,[TeacherId] int NOT NULL,[SubjectId] int NOT NULL,[AssignedUtc] datetime2 NOT NULL,
          CONSTRAINT [FK_TSA_Teachers] FOREIGN KEY([TeacherId]) REFERENCES [Teachers]([Id]),
          CONSTRAINT [FK_TSA_Subjects] FOREIGN KEY([SubjectId]) REFERENCES [Subjects]([Id]));
        CREATE UNIQUE INDEX [IX_TSA_Teacher_Subject] ON [TeacherSubjectAssignments]([TeacherId],[SubjectId]);

        CREATE TABLE [StudentClassEnrollments](
          [Id] int IDENTITY PRIMARY KEY,[StudentId] int NOT NULL,[ClassId] int NOT NULL,[SectionId] int NOT NULL,
          [AcademicYearId] int NOT NULL,[IsActive] bit NOT NULL,
          CONSTRAINT [FK_SCE_Students] FOREIGN KEY([StudentId]) REFERENCES [Students]([Id]),
          CONSTRAINT [FK_SCE_Classes] FOREIGN KEY([ClassId]) REFERENCES [Classes]([Id]),
          CONSTRAINT [FK_SCE_Sections] FOREIGN KEY([SectionId]) REFERENCES [Sections]([Id]),
          CONSTRAINT [FK_SCE_Years] FOREIGN KEY([AcademicYearId]) REFERENCES [AcademicYears]([Id]));
        CREATE UNIQUE INDEX [IX_SCE_Student_Year] ON [StudentClassEnrollments]([StudentId],[AcademicYearId]);

        CREATE TABLE [StudentSubjectEnrollments](
          [Id] int IDENTITY PRIMARY KEY,[StudentId] int NOT NULL,[SubjectId] int NOT NULL,[EnrolledUtc] datetime2 NOT NULL,
          CONSTRAINT [FK_SSE_Students] FOREIGN KEY([StudentId]) REFERENCES [Students]([Id]),
          CONSTRAINT [FK_SSE_Subjects] FOREIGN KEY([SubjectId]) REFERENCES [Subjects]([Id]));
        CREATE UNIQUE INDEX [IX_SSE_Student_Subject] ON [StudentSubjectEnrollments]([StudentId],[SubjectId]);

        CREATE TABLE [Permissions]([Id] int IDENTITY PRIMARY KEY,[Key] nvarchar(100) NOT NULL,[Description] nvarchar(160) NOT NULL);
        CREATE UNIQUE INDEX [IX_Permissions_Key] ON [Permissions]([Key]);
        CREATE TABLE [UserPermissions](
          [Id] int IDENTITY PRIMARY KEY,[UserId] nvarchar(450) NOT NULL,[PermissionId] int NOT NULL,
          CONSTRAINT [FK_UP_Users] FOREIGN KEY([UserId]) REFERENCES [AspNetUsers]([Id]),
          CONSTRAINT [FK_UP_Permissions] FOREIGN KEY([PermissionId]) REFERENCES [Permissions]([Id]));
        CREATE UNIQUE INDEX [IX_UP_User_Permission] ON [UserPermissions]([UserId],[PermissionId]);

        CREATE TABLE [LoginHistories](
          [Id] bigint IDENTITY PRIMARY KEY,[UserId] nvarchar(450) NULL,[Email] nvarchar(256) NOT NULL,
          [Succeeded] bit NOT NULL,[IpAddress] nvarchar(64) NULL,[CreatedUtc] datetime2 NOT NULL);
        CREATE TABLE [SystemSettings](
          [Id] int IDENTITY PRIMARY KEY,[Key] nvarchar(100) NOT NULL,[Value] nvarchar(2000) NOT NULL);
        CREATE UNIQUE INDEX [IX_SystemSettings_Key] ON [SystemSettings]([Key]);
        CREATE TABLE [StudyMaterials](
          [Id] int IDENTITY PRIMARY KEY,[SubjectId] int NOT NULL,[UploadedByTeacherId] int NOT NULL,
          [Title] nvarchar(160) NOT NULL,[FilePath] nvarchar(500) NOT NULL,[ContentType] nvarchar(100) NOT NULL,[CreatedUtc] datetime2 NOT NULL,
          CONSTRAINT [FK_SM_Subjects] FOREIGN KEY([SubjectId]) REFERENCES [Subjects]([Id]),
          CONSTRAINT [FK_SM_Teachers] FOREIGN KEY([UploadedByTeacherId]) REFERENCES [Teachers]([Id]));

        ALTER TABLE [Departments] ADD CONSTRAINT [FK_Departments_Hod] FOREIGN KEY([HodTeacherId]) REFERENCES [Teachers]([Id]);
        ALTER TABLE [Teachers] ADD CONSTRAINT [FK_Teachers_Departments] FOREIGN KEY([DepartmentId]) REFERENCES [Departments]([Id]);
        ALTER TABLE [Students] ADD CONSTRAINT [FK_Students_Departments] FOREIGN KEY([DepartmentId]) REFERENCES [Departments]([Id]);
        ALTER TABLE [Students] ADD CONSTRAINT [FK_Students_Semesters] FOREIGN KEY([SemesterId]) REFERENCES [Semesters]([Id]);
        ALTER TABLE [Classes] ADD CONSTRAINT [FK_Classes_Departments] FOREIGN KEY([DepartmentId]) REFERENCES [Departments]([Id]);
        ALTER TABLE [Classes] ADD CONSTRAINT [FK_Classes_Semesters] FOREIGN KEY([SemesterId]) REFERENCES [Semesters]([Id]);
        ALTER TABLE [Subjects] ADD CONSTRAINT [FK_Subjects_Departments] FOREIGN KEY([DepartmentId]) REFERENCES [Departments]([Id]);
        ALTER TABLE [Subjects] ADD CONSTRAINT [FK_Subjects_Semesters] FOREIGN KEY([SemesterId]) REFERENCES [Semesters]([Id]);
        ALTER TABLE [Attendances] ADD CONSTRAINT [FK_Attendances_Subjects] FOREIGN KEY([SubjectId]) REFERENCES [Subjects]([Id]);
        ALTER TABLE [Attendances] ADD CONSTRAINT [FK_Attendances_Teachers] FOREIGN KEY([TeacherId]) REFERENCES [Teachers]([Id]);
        ALTER TABLE [Marks] ADD CONSTRAINT [FK_Marks_EnteredBy] FOREIGN KEY([EnteredByTeacherId]) REFERENCES [Teachers]([Id]);
        ALTER TABLE [Notices] ADD CONSTRAINT [FK_Notices_Departments] FOREIGN KEY([DepartmentId]) REFERENCES [Departments]([Id]);
        CREATE UNIQUE INDEX [IX_Attendances_Student_Date_Subject_Lecture] ON [Attendances]([StudentId],[Date],[SubjectId],[Lecture]);
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException("This hierarchy migration contains user assignments and must be rolled back from a database backup.");
    }
}
