using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Student_Management_System.Models;

#nullable disable

namespace Student_Management_System.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260729000200_ProductionHardeningReconciliation")]
public sealed class ProductionHardeningReconciliation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'dbo.FinancialTransactions', N'Status') IS NULL
                ALTER TABLE [dbo].[FinancialTransactions] ADD [Status] int NOT NULL
                    CONSTRAINT [DF_FinancialTransactions_Status] DEFAULT 2;
            IF COL_LENGTH(N'dbo.AspNetUsers', N'MfaCodeLastSentUtc') IS NULL
                ALTER TABLE [dbo].[AspNetUsers] ADD [MfaCodeLastSentUtc] datetime2 NULL;
            IF COL_LENGTH(N'dbo.ApprovalRequests', N'RowVersion') IS NULL
                ALTER TABLE [dbo].[ApprovalRequests] ADD [RowVersion] rowversion NOT NULL;
            IF COL_LENGTH(N'dbo.ApprovalSteps', N'RowVersion') IS NULL
                ALTER TABLE [dbo].[ApprovalSteps] ADD [RowVersion] rowversion NOT NULL;
            IF COL_LENGTH(N'dbo.StudentAidAwards', N'RowVersion') IS NULL
                ALTER TABLE [dbo].[StudentAidAwards] ADD [RowVersion] rowversion NOT NULL;
            IF COL_LENGTH(N'dbo.StudentInvoiceLines', N'FeeId') IS NULL
                ALTER TABLE [dbo].[StudentInvoiceLines] ADD [FeeId] int NULL;
            IF COL_LENGTH(N'dbo.StudentInvoiceLines', N'StudentClassEnrollmentId') IS NULL
                ALTER TABLE [dbo].[StudentInvoiceLines] ADD [StudentClassEnrollmentId] int NULL;

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

            IF COL_LENGTH(N'dbo.OutboxMessages', N'ClaimOwner') IS NULL
                ALTER TABLE [dbo].[OutboxMessages] ADD [ClaimOwner] nvarchar(64) NULL;
            IF COL_LENGTH(N'dbo.OutboxMessages', N'ClaimedUtc') IS NULL
                ALTER TABLE [dbo].[OutboxMessages] ADD [ClaimedUtc] datetime2 NULL;
            IF COL_LENGTH(N'dbo.OutboxMessages', N'ClaimExpiresUtc') IS NULL
                ALTER TABLE [dbo].[OutboxMessages] ADD [ClaimExpiresUtc] datetime2 NULL;
            IF COL_LENGTH(N'dbo.OutboxMessages', N'IsDeadLetter') IS NULL
                ALTER TABLE [dbo].[OutboxMessages] ADD [IsDeadLetter] bit NOT NULL
                    CONSTRAINT [DF_OutboxMessages_IsDeadLetter] DEFAULT 0;
            IF COL_LENGTH(N'dbo.OutboxMessages', N'RowVersion') IS NULL
                ALTER TABLE [dbo].[OutboxMessages] ADD [RowVersion] rowversion NOT NULL;

            IF OBJECT_ID(N'[dbo].[StudentAidAllocations]', N'U') IS NULL
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

            IF EXISTS
            (
                SELECT LibraryBookCopyId
                FROM [dbo].[LibraryLoans]
                WHERE ReturnedUtc IS NULL
                GROUP BY LibraryBookCopyId
                HAVING COUNT_BIG(*) > 1
            )
                THROW 51001,
                    'Duplicate active library loans must be resolved before applying production hardening.',
                    1;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LibraryLoans_OneActiveLoanPerCopy' AND object_id = OBJECT_ID(N'[dbo].[LibraryLoans]'))
                CREATE UNIQUE INDEX [IX_LibraryLoans_OneActiveLoanPerCopy] ON [dbo].[LibraryLoans] ([LibraryBookCopyId]) WHERE [ReturnedUtc] IS NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StudentAidAllocations_Award_Invoice_Active' AND object_id = OBJECT_ID(N'[dbo].[StudentAidAllocations]'))
                CREATE UNIQUE INDEX [IX_StudentAidAllocations_Award_Invoice_Active] ON [dbo].[StudentAidAllocations] ([StudentAidAwardId], [StudentInvoiceId]) WHERE [ReversedUtc] IS NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StudentAidAllocations_StudentInvoiceId' AND object_id = OBJECT_ID(N'[dbo].[StudentAidAllocations]'))
                CREATE INDEX [IX_StudentAidAllocations_StudentInvoiceId] ON [dbo].[StudentAidAllocations] ([StudentInvoiceId]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CommunicationDeliveries_Claim' AND object_id = OBJECT_ID(N'[dbo].[CommunicationDeliveries]'))
                CREATE INDEX [IX_CommunicationDeliveries_Claim] ON [dbo].[CommunicationDeliveries] ([Status], [NextAttemptUtc], [ClaimExpiresUtc]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StudentInvoiceLines_FeeId' AND object_id = OBJECT_ID(N'[dbo].[StudentInvoiceLines]'))
                CREATE INDEX [IX_StudentInvoiceLines_FeeId] ON [dbo].[StudentInvoiceLines] ([FeeId]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StudentInvoiceLines_StudentClassEnrollmentId' AND object_id = OBJECT_ID(N'[dbo].[StudentInvoiceLines]'))
                CREATE INDEX [IX_StudentInvoiceLines_StudentClassEnrollmentId] ON [dbo].[StudentInvoiceLines] ([StudentClassEnrollmentId]);

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_StudentInvoiceLines_Fees_FeeId' AND parent_object_id = OBJECT_ID(N'[dbo].[StudentInvoiceLines]'))
                ALTER TABLE [dbo].[StudentInvoiceLines] WITH CHECK
                    ADD CONSTRAINT [FK_StudentInvoiceLines_Fees_FeeId]
                    FOREIGN KEY ([FeeId]) REFERENCES [dbo].[Fees] ([Id]);
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_StudentInvoiceLines_StudentClassEnrollments_StudentClassEnrollmentId' AND parent_object_id = OBJECT_ID(N'[dbo].[StudentInvoiceLines]'))
                ALTER TABLE [dbo].[StudentInvoiceLines] WITH CHECK
                    ADD CONSTRAINT [FK_StudentInvoiceLines_StudentClassEnrollments_StudentClassEnrollmentId]
                    FOREIGN KEY ([StudentClassEnrollmentId]) REFERENCES [dbo].[StudentClassEnrollments] ([Id]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            "FK_StudentInvoiceLines_Fees_FeeId", "StudentInvoiceLines");
        migrationBuilder.DropForeignKey(
            "FK_StudentInvoiceLines_StudentClassEnrollments_StudentClassEnrollmentId",
            "StudentInvoiceLines");
        migrationBuilder.DropTable("StudentAidAllocations");
        migrationBuilder.DropIndex(
            "IX_LibraryLoans_OneActiveLoanPerCopy", "LibraryLoans");
        migrationBuilder.DropIndex(
            "IX_CommunicationDeliveries_Claim", "CommunicationDeliveries");
        migrationBuilder.DropIndex(
            "IX_StudentInvoiceLines_FeeId", "StudentInvoiceLines");
        migrationBuilder.DropIndex(
            "IX_StudentInvoiceLines_StudentClassEnrollmentId", "StudentInvoiceLines");
        migrationBuilder.DropColumn("Status", "FinancialTransactions");
        migrationBuilder.DropColumn("MfaCodeLastSentUtc", "AspNetUsers");
        migrationBuilder.DropColumn("RowVersion", "ApprovalRequests");
        migrationBuilder.DropColumn("RowVersion", "ApprovalSteps");
        migrationBuilder.DropColumn("RowVersion", "StudentAidAwards");
        migrationBuilder.DropColumn("FeeId", "StudentInvoiceLines");
        migrationBuilder.DropColumn("StudentClassEnrollmentId", "StudentInvoiceLines");
        migrationBuilder.DropColumn("NextAttemptUtc", "CommunicationDeliveries");
        migrationBuilder.DropColumn("IsDeadLetter", "OutboxMessages");
        foreach (var table in new[] { "CommunicationDeliveries", "OutboxMessages" })
        {
            migrationBuilder.DropColumn("ClaimOwner", table);
            migrationBuilder.DropColumn("ClaimedUtc", table);
            migrationBuilder.DropColumn("ClaimExpiresUtc", table);
            migrationBuilder.DropColumn("RowVersion", table);
        }
    }
}
