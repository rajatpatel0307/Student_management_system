using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;

namespace Student_Management_System.Data;

/// <summary>
/// An additive, idempotent compatibility repair for legacy Development
/// databases whose migration history is ahead of their physical schema.
/// Full schema deployment remains owned by Entity Framework migrations.
/// </summary>
public static class DatabaseCompatibilityRepair
{
    public static Task RepairLatestSchemaAsync(
        ApplicationDbContext database,
        CancellationToken cancellationToken = default) =>
        database.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[Students]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.Students', N'ApaarId') IS NULL
                    ALTER TABLE [dbo].[Students] ADD [ApaarId] nvarchar(20) NULL;
                IF COL_LENGTH(N'dbo.Students', N'AcademicBankOfCreditsId') IS NULL
                    ALTER TABLE [dbo].[Students] ADD [AcademicBankOfCreditsId] nvarchar(40) NULL;
                IF COL_LENGTH(N'dbo.Students', N'DigiLockerSubjectId') IS NULL
                    ALTER TABLE [dbo].[Students] ADD [DigiLockerSubjectId] nvarchar(100) NULL;
            END;

            IF OBJECT_ID(N'[dbo].[AdmissionApplications]', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.AdmissionApplications', N'CampusId') IS NULL
                ALTER TABLE [dbo].[AdmissionApplications] ADD [CampusId] int NULL;

            IF OBJECT_ID(N'[dbo].[AuditLogs]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.AuditLogs', N'CorrelationId') IS NULL
                    ALTER TABLE [dbo].[AuditLogs] ADD [CorrelationId] nvarchar(36) NULL;
                IF COL_LENGTH(N'dbo.AuditLogs', N'DetailsJson') IS NULL
                    ALTER TABLE [dbo].[AuditLogs] ADD [DetailsJson] nvarchar(max) NULL;
                IF COL_LENGTH(N'dbo.AuditLogs', N'HttpMethod') IS NULL
                    ALTER TABLE [dbo].[AuditLogs] ADD [HttpMethod] nvarchar(10) NULL;
                IF COL_LENGTH(N'dbo.AuditLogs', N'Outcome') IS NULL
                    ALTER TABLE [dbo].[AuditLogs] ADD [Outcome] nvarchar(40) NOT NULL
                        CONSTRAINT [DF_AuditLogs_Outcome_Compatibility] DEFAULT N'Succeeded';
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
            END;

            IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.AspNetUsers', N'MfaCodeLastSentUtc') IS NULL
                ALTER TABLE [dbo].[AspNetUsers] ADD [MfaCodeLastSentUtc] datetime2 NULL;

            IF OBJECT_ID(N'[dbo].[FinancialTransactions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.FinancialTransactions', N'Status') IS NULL
                ALTER TABLE [dbo].[FinancialTransactions] ADD [Status] int NOT NULL
                    CONSTRAINT [DF_FinancialTransactions_Status_Compatibility] DEFAULT 2;

            IF OBJECT_ID(N'[dbo].[ApprovalRequests]', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.ApprovalRequests', N'RowVersion') IS NULL
                ALTER TABLE [dbo].[ApprovalRequests] ADD [RowVersion] rowversion NOT NULL;
            IF OBJECT_ID(N'[dbo].[ApprovalSteps]', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.ApprovalSteps', N'RowVersion') IS NULL
                ALTER TABLE [dbo].[ApprovalSteps] ADD [RowVersion] rowversion NOT NULL;
            IF OBJECT_ID(N'[dbo].[StudentAidAwards]', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.StudentAidAwards', N'RowVersion') IS NULL
                ALTER TABLE [dbo].[StudentAidAwards] ADD [RowVersion] rowversion NOT NULL;

            IF OBJECT_ID(N'[dbo].[StudentInvoiceLines]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.StudentInvoiceLines', N'FeeId') IS NULL
                    ALTER TABLE [dbo].[StudentInvoiceLines] ADD [FeeId] int NULL;
                IF COL_LENGTH(N'dbo.StudentInvoiceLines', N'StudentClassEnrollmentId') IS NULL
                    ALTER TABLE [dbo].[StudentInvoiceLines] ADD [StudentClassEnrollmentId] int NULL;
            END;

            IF OBJECT_ID(N'[dbo].[CommunicationDeliveries]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.CommunicationDeliveries', N'ClaimOwner') IS NULL
                    ALTER TABLE [dbo].[CommunicationDeliveries] ADD [ClaimOwner] nvarchar(64) NULL;
                IF COL_LENGTH(N'dbo.CommunicationDeliveries', N'ClaimedUtc') IS NULL
                    ALTER TABLE [dbo].[CommunicationDeliveries] ADD [ClaimedUtc] datetime2 NULL;
                IF COL_LENGTH(N'dbo.CommunicationDeliveries', N'ClaimExpiresUtc') IS NULL
                    ALTER TABLE [dbo].[CommunicationDeliveries] ADD [ClaimExpiresUtc] datetime2 NULL;
                IF COL_LENGTH(N'dbo.CommunicationDeliveries', N'NextAttemptUtc') IS NULL
                    ALTER TABLE [dbo].[CommunicationDeliveries] ADD [NextAttemptUtc] datetime2 NULL;
                IF COL_LENGTH(N'dbo.CommunicationDeliveries', N'RowVersion') IS NULL
                    ALTER TABLE [dbo].[CommunicationDeliveries] ADD [RowVersion] rowversion NOT NULL;
            END;

            IF OBJECT_ID(N'[dbo].[OutboxMessages]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.OutboxMessages', N'ClaimOwner') IS NULL
                    ALTER TABLE [dbo].[OutboxMessages] ADD [ClaimOwner] nvarchar(64) NULL;
                IF COL_LENGTH(N'dbo.OutboxMessages', N'ClaimedUtc') IS NULL
                    ALTER TABLE [dbo].[OutboxMessages] ADD [ClaimedUtc] datetime2 NULL;
                IF COL_LENGTH(N'dbo.OutboxMessages', N'ClaimExpiresUtc') IS NULL
                    ALTER TABLE [dbo].[OutboxMessages] ADD [ClaimExpiresUtc] datetime2 NULL;
                IF COL_LENGTH(N'dbo.OutboxMessages', N'IsDeadLetter') IS NULL
                    ALTER TABLE [dbo].[OutboxMessages] ADD [IsDeadLetter] bit NOT NULL
                        CONSTRAINT [DF_OutboxMessages_IsDeadLetter_Compatibility] DEFAULT 0;
                IF COL_LENGTH(N'dbo.OutboxMessages', N'RowVersion') IS NULL
                    ALTER TABLE [dbo].[OutboxMessages] ADD [RowVersion] rowversion NOT NULL;
            END;

            IF OBJECT_ID(N'[dbo].[UserCampusMemberships]', N'U') IS NULL
               AND OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[Campuses]', N'U') IS NOT NULL
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

            IF OBJECT_ID(N'[dbo].[StudentAidAllocations]', N'U') IS NULL
               AND OBJECT_ID(N'[dbo].[StudentAidAwards]', N'U') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[StudentInvoices]', N'U') IS NOT NULL
            BEGIN
                CREATE TABLE [dbo].[StudentAidAllocations]
                (
                    [Id] bigint IDENTITY(1,1) NOT NULL,
                    [StudentAidAwardId] bigint NOT NULL,
                    [StudentInvoiceId] bigint NOT NULL,
                    [Amount] decimal(18,2) NOT NULL,
                    [AllocatedUtc] datetime2 NOT NULL,
                    [AllocatedByUserId] nvarchar(450) NOT NULL,
                    [ReversedUtc] datetime2 NULL,
                    [ReversedByUserId] nvarchar(450) NULL,
                    [ReversalReason] nvarchar(500) NULL,
                    [RowVersion] rowversion NOT NULL,
                    CONSTRAINT [PK_StudentAidAllocations] PRIMARY KEY ([Id]),
                    CONSTRAINT [CK_StudentAidAllocations_Amount] CHECK ([Amount] > 0),
                    CONSTRAINT [FK_StudentAidAllocations_StudentAidAwards_StudentAidAwardId]
                        FOREIGN KEY ([StudentAidAwardId]) REFERENCES [dbo].[StudentAidAwards] ([Id]),
                    CONSTRAINT [FK_StudentAidAllocations_StudentInvoices_StudentInvoiceId]
                        FOREIGN KEY ([StudentInvoiceId]) REFERENCES [dbo].[StudentInvoices] ([Id])
                );
            END;
            """, cancellationToken);
}
