using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Student_Management_System.Models;

#nullable disable

namespace Student_Management_System.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260801000300_TenantAuditAndIndiaFoundations")]
public sealed class TenantAuditAndIndiaFoundations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'dbo.AdmissionApplications', N'CampusId') IS NULL
                ALTER TABLE [dbo].[AdmissionApplications] ADD [CampusId] int NULL;

            IF COL_LENGTH(N'dbo.Students', N'ApaarId') IS NULL
                ALTER TABLE [dbo].[Students] ADD [ApaarId] nvarchar(20) NULL;
            IF COL_LENGTH(N'dbo.Students', N'AcademicBankOfCreditsId') IS NULL
                ALTER TABLE [dbo].[Students] ADD [AcademicBankOfCreditsId] nvarchar(40) NULL;
            IF COL_LENGTH(N'dbo.Students', N'DigiLockerSubjectId') IS NULL
                ALTER TABLE [dbo].[Students] ADD [DigiLockerSubjectId] nvarchar(100) NULL;

            IF COL_LENGTH(N'dbo.AuditLogs', N'CorrelationId') IS NULL
                ALTER TABLE [dbo].[AuditLogs] ADD [CorrelationId] nvarchar(36) NULL;
            IF COL_LENGTH(N'dbo.AuditLogs', N'DetailsJson') IS NULL
                ALTER TABLE [dbo].[AuditLogs] ADD [DetailsJson] nvarchar(max) NULL;
            IF COL_LENGTH(N'dbo.AuditLogs', N'HttpMethod') IS NULL
                ALTER TABLE [dbo].[AuditLogs] ADD [HttpMethod] nvarchar(10) NULL;
            IF COL_LENGTH(N'dbo.AuditLogs', N'Outcome') IS NULL
                ALTER TABLE [dbo].[AuditLogs] ADD [Outcome] nvarchar(40) NOT NULL
                    CONSTRAINT [DF_AuditLogs_Outcome] DEFAULT N'Succeeded';
            IF COL_LENGTH(N'dbo.AuditLogs', N'RequestPath') IS NULL
                ALTER TABLE [dbo].[AuditLogs] ADD [RequestPath] nvarchar(500) NULL;
            IF COL_LENGTH(N'dbo.AuditLogs', N'UserAgent') IS NULL
                ALTER TABLE [dbo].[AuditLogs] ADD [UserAgent] nvarchar(500) NULL;

            IF OBJECT_ID(N'[dbo].[UserCampusMemberships]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[UserCampusMemberships]
                (
                    [Id] bigint IDENTITY(1,1) NOT NULL,
                    [UserId] nvarchar(450) NOT NULL,
                    [CampusId] int NOT NULL,
                    [IsDefault] bit NOT NULL,
                    [IsActive] bit NOT NULL,
                    [GrantedUtc] datetime2 NOT NULL,
                    [GrantedByUserId] nvarchar(450) NULL,
                    [RowVersion] rowversion NOT NULL,
                    CONSTRAINT [PK_UserCampusMemberships] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_UserCampusMemberships_AspNetUsers_UserId]
                        FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_UserCampusMemberships_Campuses_CampusId]
                        FOREIGN KEY ([CampusId]) REFERENCES [dbo].[Campuses] ([Id])
                );
            END;
            """);

        // SQL Server compiles an entire command before executing it. Keep all
        // statements that reference newly added columns/tables in a subsequent
        // command so fresh and partially upgraded databases both succeed.
        migrationBuilder.Sql("""
            UPDATE a SET CampusId = p.CampusId
            FROM [dbo].[AdmissionApplications] a
            CROSS APPLY
            (
                SELECT TOP (1) Id AS CampusId
                FROM [dbo].[Campuses]
                WHERE IsActive = 1
                ORDER BY Id
            ) p
            WHERE a.CampusId IS NULL;

            INSERT INTO [dbo].[UserCampusMemberships]
                (UserId, CampusId, IsDefault, IsActive, GrantedUtc)
            SELECT u.Id, c.Id,
                   CASE WHEN c.Id = (SELECT MIN(c2.Id) FROM Campuses c2 WHERE c2.IsActive = 1) THEN 1 ELSE 0 END,
                   1, SYSUTCDATETIME()
            FROM [dbo].[AspNetUsers] u
            CROSS JOIN [dbo].[Campuses] c
            WHERE u.IsActive = 1 AND c.IsActive = 1
              AND EXISTS
              (
                  SELECT 1 FROM [dbo].[AspNetUserRoles] ur
                  JOIN [dbo].[AspNetRoles] r ON r.Id = ur.RoleId
                  WHERE ur.UserId = u.Id AND r.Name IN
                      ('SuperAdmin','Admin','AdmissionOfficer','Accountant','ExaminationOfficer','Librarian')
              )
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [dbo].[UserCampusMemberships] existing
                  WHERE existing.UserId = u.Id AND existing.CampusId = c.Id
              );

            ;WITH duplicateDefaults AS
            (
                SELECT Id,
                       ROW_NUMBER() OVER (PARTITION BY UserId ORDER BY Id) AS DefaultOrder
                FROM [dbo].[UserCampusMemberships]
                WHERE IsDefault = 1 AND IsActive = 1
            )
            UPDATE membership
            SET IsDefault = 0
            FROM [dbo].[UserCampusMemberships] membership
            INNER JOIN duplicateDefaults duplicate ON duplicate.Id = membership.Id
            WHERE duplicate.DefaultOrder > 1;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AdmissionApplications_CampusId' AND object_id = OBJECT_ID(N'[dbo].[AdmissionApplications]'))
                CREATE INDEX [IX_AdmissionApplications_CampusId] ON [dbo].[AdmissionApplications] ([CampusId]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserCampusMemberships_CampusId' AND object_id = OBJECT_ID(N'[dbo].[UserCampusMemberships]'))
                CREATE INDEX [IX_UserCampusMemberships_CampusId] ON [dbo].[UserCampusMemberships] ([CampusId]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserCampusMemberships_User_Campus' AND object_id = OBJECT_ID(N'[dbo].[UserCampusMemberships]'))
                CREATE UNIQUE INDEX [IX_UserCampusMemberships_User_Campus] ON [dbo].[UserCampusMemberships] ([UserId], [CampusId]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserCampusMemberships_OneDefault' AND object_id = OBJECT_ID(N'[dbo].[UserCampusMemberships]'))
                CREATE UNIQUE INDEX [IX_UserCampusMemberships_OneDefault] ON [dbo].[UserCampusMemberships] ([UserId], [IsDefault]) WHERE [IsDefault] = 1 AND [IsActive] = 1;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_CorrelationId' AND object_id = OBJECT_ID(N'[dbo].[AuditLogs]'))
                CREATE INDEX [IX_AuditLogs_CorrelationId] ON [dbo].[AuditLogs] ([CorrelationId]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_Entity_CreatedUtc' AND object_id = OBJECT_ID(N'[dbo].[AuditLogs]'))
                CREATE INDEX [IX_AuditLogs_Entity_CreatedUtc] ON [dbo].[AuditLogs] ([EntityName], [EntityId], [CreatedUtc]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Students_ApaarId' AND object_id = OBJECT_ID(N'[dbo].[Students]'))
                CREATE UNIQUE INDEX [IX_Students_ApaarId] ON [dbo].[Students] ([ApaarId]) WHERE [ApaarId] IS NOT NULL;

            UPDATE admission
            SET CampusId = NULL
            FROM [dbo].[AdmissionApplications] admission
            LEFT JOIN [dbo].[Campuses] campus ON campus.Id = admission.CampusId
            WHERE admission.CampusId IS NOT NULL AND campus.Id IS NULL;

            IF NOT EXISTS
            (
                SELECT 1 FROM sys.foreign_keys
                WHERE name = N'FK_AdmissionApplications_Campuses_CampusId'
                  AND parent_object_id = OBJECT_ID(N'[dbo].[AdmissionApplications]')
            )
                ALTER TABLE [dbo].[AdmissionApplications] WITH CHECK
                    ADD CONSTRAINT [FK_AdmissionApplications_Campuses_CampusId]
                    FOREIGN KEY ([CampusId]) REFERENCES [dbo].[Campuses] ([Id]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_AdmissionApplications_Campuses_CampusId", "AdmissionApplications");
        migrationBuilder.DropTable("UserCampusMemberships");
        migrationBuilder.DropIndex("IX_AdmissionApplications_CampusId", "AdmissionApplications");
        migrationBuilder.DropIndex("IX_AuditLogs_CorrelationId", "AuditLogs");
        migrationBuilder.DropIndex("IX_AuditLogs_Entity_CreatedUtc", "AuditLogs");
        migrationBuilder.DropIndex("IX_Students_ApaarId", "Students");
        migrationBuilder.DropColumn("CampusId", "AdmissionApplications");
        migrationBuilder.DropColumn("ApaarId", "Students");
        migrationBuilder.DropColumn("AcademicBankOfCreditsId", "Students");
        migrationBuilder.DropColumn("DigiLockerSubjectId", "Students");
        foreach (var column in new[] { "CorrelationId", "HttpMethod", "RequestPath", "UserAgent", "Outcome", "DetailsJson" })
            migrationBuilder.DropColumn(column, "AuditLogs");
    }
}
