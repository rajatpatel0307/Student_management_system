using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Student_Management_System.Models;

#nullable disable

namespace Student_Management_System.Migrations;

/// <summary>
/// Reconciles databases whose migration history says that the tenant/audit
/// migration ran even though one or more AuditLogs columns were not created.
/// Every operation is guarded so clean databases and correctly upgraded
/// databases remain unchanged.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260801000400_RepairAuditLogSchema")]
public sealed class RepairAuditLogSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[dbo].[AuditLogs]', N'U') IS NULL
                THROW 51020, 'The AuditLogs table is missing. Apply the complete EduFlow migration chain first.', 1;

            IF COL_LENGTH(N'dbo.AuditLogs', N'CorrelationId') IS NULL
                ALTER TABLE [dbo].[AuditLogs] ADD [CorrelationId] nvarchar(36) NULL;

            IF COL_LENGTH(N'dbo.AuditLogs', N'DetailsJson') IS NULL
                ALTER TABLE [dbo].[AuditLogs] ADD [DetailsJson] nvarchar(max) NULL;

            IF COL_LENGTH(N'dbo.AuditLogs', N'HttpMethod') IS NULL
                ALTER TABLE [dbo].[AuditLogs] ADD [HttpMethod] nvarchar(10) NULL;

            IF COL_LENGTH(N'dbo.AuditLogs', N'Outcome') IS NULL
                ALTER TABLE [dbo].[AuditLogs]
                    ADD [Outcome] nvarchar(40) NOT NULL
                    CONSTRAINT [DF_AuditLogs_Outcome_Repair] DEFAULT N'Succeeded';

            IF COL_LENGTH(N'dbo.AuditLogs', N'RequestPath') IS NULL
                ALTER TABLE [dbo].[AuditLogs] ADD [RequestPath] nvarchar(500) NULL;

            IF COL_LENGTH(N'dbo.AuditLogs', N'UserAgent') IS NULL
                ALTER TABLE [dbo].[AuditLogs] ADD [UserAgent] nvarchar(500) NULL;

            IF NOT EXISTS
            (
                SELECT 1 FROM sys.indexes
                WHERE [name] = N'IX_AuditLogs_CorrelationId'
                  AND [object_id] = OBJECT_ID(N'[dbo].[AuditLogs]')
            )
                CREATE INDEX [IX_AuditLogs_CorrelationId]
                    ON [dbo].[AuditLogs] ([CorrelationId]);

            IF NOT EXISTS
            (
                SELECT 1 FROM sys.indexes
                WHERE [name] = N'IX_AuditLogs_Entity_CreatedUtc'
                  AND [object_id] = OBJECT_ID(N'[dbo].[AuditLogs]')
            )
                CREATE INDEX [IX_AuditLogs_Entity_CreatedUtc]
                    ON [dbo].[AuditLogs] ([EntityName], [EntityId], [CreatedUtc]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This is a reconciliation migration. Dropping columns here could
        // destroy audit evidence or remove columns owned by the earlier
        // TenantAuditAndIndiaFoundations migration, so rollback is intentionally
        // non-destructive.
    }
}
