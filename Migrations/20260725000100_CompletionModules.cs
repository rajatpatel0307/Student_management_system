using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Student_Management_System.Models;

#nullable disable

namespace Student_Management_System.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260725000100_CompletionModules")]
public class CompletionModules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        CREATE TABLE [AcademicYears](
          [Id] int IDENTITY PRIMARY KEY,[Name] nvarchar(40) NOT NULL,
          [StartsOn] datetime2 NOT NULL,[EndsOn] datetime2 NOT NULL,[IsCurrent] bit NOT NULL);
        CREATE UNIQUE INDEX [IX_AcademicYears_Name] ON [AcademicYears]([Name]);

        CREATE TABLE [AuditLogs](
          [Id] bigint IDENTITY PRIMARY KEY,[UserId] nvarchar(450) NULL,
          [Action] nvarchar(100) NOT NULL,[EntityName] nvarchar(100) NOT NULL,
          [EntityId] nvarchar(100) NULL,[IpAddress] nvarchar(64) NULL,[CreatedUtc] datetime2 NOT NULL);

        CREATE TABLE [Fees](
          [Id] int IDENTITY PRIMARY KEY,[ClassId] int NOT NULL,[Amount] decimal(18,2) NOT NULL,
          [Term] nvarchar(80) NOT NULL,[DueDate] datetime2 NOT NULL,
          CONSTRAINT [FK_Fees_Classes_ClassId] FOREIGN KEY([ClassId]) REFERENCES [Classes]([Id]));
        CREATE INDEX [IX_Fees_ClassId] ON [Fees]([ClassId]);

        CREATE TABLE [TimetableEntries](
          [Id] int IDENTITY PRIMARY KEY,[ClassId] int NOT NULL,[SubjectId] int NOT NULL,[TeacherId] int NOT NULL,
          [DayOfWeek] int NOT NULL,[StartsAt] time NOT NULL,[EndsAt] time NOT NULL,[Room] nvarchar(80) NOT NULL,
          CONSTRAINT [FK_Timetable_Classes] FOREIGN KEY([ClassId]) REFERENCES [Classes]([Id]),
          CONSTRAINT [FK_Timetable_Subjects] FOREIGN KEY([SubjectId]) REFERENCES [Subjects]([Id]),
          CONSTRAINT [FK_Timetable_Teachers] FOREIGN KEY([TeacherId]) REFERENCES [Teachers]([Id]));
        CREATE UNIQUE INDEX [IX_Timetable_Class_Day_Start] ON [TimetableEntries]([ClassId],[DayOfWeek],[StartsAt]);

        CREATE TABLE [Assignments](
          [Id] int IDENTITY PRIMARY KEY,[SubjectId] int NOT NULL,[Title] nvarchar(160) NOT NULL,
          [Instructions] nvarchar(4000) NOT NULL,[DueUtc] datetime2 NOT NULL,[CreatedUtc] datetime2 NOT NULL,
          CONSTRAINT [FK_Assignments_Subjects] FOREIGN KEY([SubjectId]) REFERENCES [Subjects]([Id]));
        CREATE INDEX [IX_Assignments_SubjectId] ON [Assignments]([SubjectId]);

        CREATE TABLE [Payments](
          [Id] int IDENTITY PRIMARY KEY,[StudentId] int NOT NULL,[FeeId] int NOT NULL,
          [AmountPaid] decimal(18,2) NOT NULL,[PaymentDateUtc] datetime2 NOT NULL,[Reference] nvarchar(100) NOT NULL,
          CONSTRAINT [FK_Payments_Students] FOREIGN KEY([StudentId]) REFERENCES [Students]([Id]),
          CONSTRAINT [FK_Payments_Fees] FOREIGN KEY([FeeId]) REFERENCES [Fees]([Id]));
        CREATE INDEX [IX_Payments_StudentId] ON [Payments]([StudentId]);
        CREATE INDEX [IX_Payments_FeeId] ON [Payments]([FeeId]);

        CREATE TABLE [AssignmentSubmissions](
          [Id] int IDENTITY PRIMARY KEY,[AssignmentId] int NOT NULL,[StudentId] int NOT NULL,
          [Answer] nvarchar(4000) NOT NULL,[SubmittedUtc] datetime2 NOT NULL,[Score] int NULL,[Feedback] nvarchar(1000) NULL,
          CONSTRAINT [FK_Submissions_Assignments] FOREIGN KEY([AssignmentId]) REFERENCES [Assignments]([Id]),
          CONSTRAINT [FK_Submissions_Students] FOREIGN KEY([StudentId]) REFERENCES [Students]([Id]));
        CREATE UNIQUE INDEX [IX_Submissions_Assignment_Student] ON [AssignmentSubmissions]([AssignmentId],[StudentId]);

        ALTER TABLE [Sections] ALTER COLUMN [Name] nvarchar(20) NOT NULL;
        ALTER TABLE [Subjects] ALTER COLUMN [Name] nvarchar(120) NOT NULL;
        CREATE UNIQUE INDEX [IX_Attendances_StudentId_Date] ON [Attendances]([StudentId],[Date]);
        CREATE UNIQUE INDEX [IX_Marks_Student_Subject_Exam] ON [Marks]([StudentId],[SubjectId],[ExamId]);
        CREATE UNIQUE INDEX [IX_Sections_ClassId_Name] ON [Sections]([ClassId],[Name]);
        CREATE UNIQUE INDEX [IX_Subjects_ClassId_Name] ON [Subjects]([ClassId],[Name]);
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        DROP TABLE [AssignmentSubmissions]; DROP TABLE [Payments]; DROP TABLE [Assignments];
        DROP TABLE [TimetableEntries]; DROP TABLE [Fees]; DROP TABLE [AuditLogs]; DROP TABLE [AcademicYears];
        DROP INDEX [IX_Attendances_StudentId_Date] ON [Attendances];
        DROP INDEX [IX_Marks_Student_Subject_Exam] ON [Marks];
        DROP INDEX [IX_Sections_ClassId_Name] ON [Sections];
        DROP INDEX [IX_Subjects_ClassId_Name] ON [Subjects];
        """);
    }
}
