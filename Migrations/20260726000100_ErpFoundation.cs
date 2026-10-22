using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Student_Management_System.Models;

#nullable disable

namespace Student_Management_System.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260726000100_ErpFoundation")]
public sealed class ErpFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        ALTER TABLE [UserPermissions] ADD [IsGranted] bit NOT NULL
          CONSTRAINT [DF_UserPermissions_IsGranted] DEFAULT 1;

        CREATE TABLE [RolePermissions](
          [Id] int IDENTITY PRIMARY KEY,
          [RoleName] nvarchar(256) NOT NULL,
          [PermissionId] int NOT NULL,
          CONSTRAINT [FK_RolePermissions_Permissions] FOREIGN KEY([PermissionId])
            REFERENCES [Permissions]([Id]) ON DELETE CASCADE);
        CREATE UNIQUE INDEX [IX_RolePermissions_Role_Permission]
          ON [RolePermissions]([RoleName],[PermissionId]);

        CREATE TABLE [AdmissionApplications](
          [Id] bigint IDENTITY PRIMARY KEY,
          [ReferenceNumber] nvarchar(32) NOT NULL,
          [ApplicantName] nvarchar(160) NOT NULL,
          [Email] nvarchar(256) NOT NULL,
          [Phone] nvarchar(30) NULL,
          [DateOfBirth] datetime2 NOT NULL,
          [Address] nvarchar(1000) NULL,
          [AcademicYearId] int NOT NULL,
          [DepartmentId] int NOT NULL,
          [ClassId] int NULL,
          [ProgramName] nvarchar(120) NOT NULL,
          [Source] nvarchar(80) NOT NULL,
          [Stage] int NOT NULL,
          [InternalNotes] nvarchar(2000) NULL,
          [ApplicantNotes] nvarchar(2000) NULL,
          [CreatedByUserId] nvarchar(450) NULL,
          [CreatedUtc] datetime2 NOT NULL,
          [UpdatedUtc] datetime2 NOT NULL,
          [EnrolledStudentId] int NULL,
          CONSTRAINT [FK_AdmissionApplications_AcademicYears] FOREIGN KEY([AcademicYearId]) REFERENCES [AcademicYears]([Id]),
          CONSTRAINT [FK_AdmissionApplications_Departments] FOREIGN KEY([DepartmentId]) REFERENCES [Departments]([Id]),
          CONSTRAINT [FK_AdmissionApplications_Classes] FOREIGN KEY([ClassId]) REFERENCES [Classes]([Id]),
          CONSTRAINT [FK_AdmissionApplications_Students] FOREIGN KEY([EnrolledStudentId]) REFERENCES [Students]([Id]));
        CREATE UNIQUE INDEX [IX_AdmissionApplications_ReferenceNumber] ON [AdmissionApplications]([ReferenceNumber]);
        CREATE INDEX [IX_AdmissionApplications_Stage_AcademicYearId] ON [AdmissionApplications]([Stage],[AcademicYearId]);

        CREATE TABLE [AdmissionStageHistories](
          [Id] bigint IDENTITY PRIMARY KEY,
          [AdmissionApplicationId] bigint NOT NULL,
          [FromStage] int NOT NULL,
          [ToStage] int NOT NULL,
          [Comment] nvarchar(1000) NULL,
          [ChangedByUserId] nvarchar(450) NOT NULL,
          [ChangedUtc] datetime2 NOT NULL,
          CONSTRAINT [FK_AdmissionStageHistories_Applications] FOREIGN KEY([AdmissionApplicationId])
            REFERENCES [AdmissionApplications]([Id]) ON DELETE CASCADE);
        CREATE INDEX [IX_AdmissionStageHistories_ApplicationId] ON [AdmissionStageHistories]([AdmissionApplicationId]);

        CREATE TABLE [GuardianProfiles](
          [Id] int IDENTITY PRIMARY KEY,
          [UserId] nvarchar(450) NOT NULL,
          [FullName] nvarchar(160) NOT NULL,
          [Relationship] nvarchar(40) NOT NULL,
          [Email] nvarchar(256) NOT NULL,
          [Phone] nvarchar(30) NULL,
          CONSTRAINT [FK_GuardianProfiles_Users] FOREIGN KEY([UserId]) REFERENCES [AspNetUsers]([Id]));
        CREATE UNIQUE INDEX [IX_GuardianProfiles_UserId] ON [GuardianProfiles]([UserId]);

        CREATE TABLE [GuardianStudentLinks](
          [Id] int IDENTITY PRIMARY KEY,
          [GuardianProfileId] int NOT NULL,
          [StudentId] int NOT NULL,
          [IsPrimary] bit NOT NULL,
          [CanViewFinance] bit NOT NULL,
          CONSTRAINT [FK_GuardianStudentLinks_Guardians] FOREIGN KEY([GuardianProfileId])
            REFERENCES [GuardianProfiles]([Id]) ON DELETE CASCADE,
          CONSTRAINT [FK_GuardianStudentLinks_Students] FOREIGN KEY([StudentId]) REFERENCES [Students]([Id]));
        CREATE UNIQUE INDEX [IX_GuardianStudentLinks_Guardian_Student]
          ON [GuardianStudentLinks]([GuardianProfileId],[StudentId]);

        CREATE TABLE [FinancialTransactions](
          [Id] bigint IDENTITY PRIMARY KEY,
          [ReceiptNumber] nvarchar(40) NOT NULL,
          [StudentId] int NOT NULL,
          [FeeId] int NOT NULL,
          [Type] int NOT NULL,
          [Amount] decimal(18,2) NOT NULL,
          [PaymentMethod] nvarchar(40) NOT NULL,
          [TransactionReference] nvarchar(100) NULL,
          [Notes] nvarchar(1000) NULL,
          [CreatedByUserId] nvarchar(450) NOT NULL,
          [CreatedUtc] datetime2 NOT NULL,
          [ReversesTransactionId] bigint NULL,
          CONSTRAINT [FK_FinancialTransactions_Students] FOREIGN KEY([StudentId]) REFERENCES [Students]([Id]),
          CONSTRAINT [FK_FinancialTransactions_Fees] FOREIGN KEY([FeeId]) REFERENCES [Fees]([Id]),
          CONSTRAINT [FK_FinancialTransactions_Reversal] FOREIGN KEY([ReversesTransactionId])
            REFERENCES [FinancialTransactions]([Id]));
        CREATE UNIQUE INDEX [IX_FinancialTransactions_ReceiptNumber] ON [FinancialTransactions]([ReceiptNumber]);
        CREATE INDEX [IX_FinancialTransactions_Student_Fee_Date]
          ON [FinancialTransactions]([StudentId],[FeeId],[CreatedUtc]);

        CREATE TABLE [LeaveRequests](
          [Id] bigint IDENTITY PRIMARY KEY,
          [RequesterUserId] nvarchar(450) NOT NULL,
          [LeaveType] nvarchar(60) NOT NULL,
          [StartsOn] datetime2 NOT NULL,
          [EndsOn] datetime2 NOT NULL,
          [Reason] nvarchar(2000) NOT NULL,
          [Status] int NOT NULL,
          [ReviewedByUserId] nvarchar(450) NULL,
          [ReviewComment] nvarchar(1000) NULL,
          [CreatedUtc] datetime2 NOT NULL,
          [ReviewedUtc] datetime2 NULL);
        CREATE INDEX [IX_LeaveRequests_Requester_Status] ON [LeaveRequests]([RequesterUserId],[Status]);

        CREATE TABLE [UserNotifications](
          [Id] bigint IDENTITY PRIMARY KEY,
          [UserId] nvarchar(450) NOT NULL,
          [Title] nvarchar(160) NOT NULL,
          [Message] nvarchar(1000) NOT NULL,
          [ActionUrl] nvarchar(500) NULL,
          [Category] nvarchar(40) NOT NULL,
          [IsRead] bit NOT NULL,
          [CreatedUtc] datetime2 NOT NULL,
          [ReadUtc] datetime2 NULL,
          CONSTRAINT [FK_UserNotifications_Users] FOREIGN KEY([UserId]) REFERENCES [AspNetUsers]([Id]) ON DELETE CASCADE);
        CREATE INDEX [IX_UserNotifications_User_Read_Date] ON [UserNotifications]([UserId],[IsRead],[CreatedUtc]);

        CREATE TABLE [ApprovalRequests](
          [Id] bigint IDENTITY PRIMARY KEY,
          [WorkflowType] nvarchar(80) NOT NULL,
          [EntityType] nvarchar(100) NOT NULL,
          [EntityId] nvarchar(100) NOT NULL,
          [RequestedByUserId] nvarchar(450) NOT NULL,
          [CurrentApproverUserId] nvarchar(450) NULL,
          [Status] int NOT NULL,
          [Reason] nvarchar(2000) NULL,
          [CreatedUtc] datetime2 NOT NULL,
          [CompletedUtc] datetime2 NULL);
        CREATE INDEX [IX_ApprovalRequests_Workflow_Status] ON [ApprovalRequests]([WorkflowType],[Status]);

        CREATE TABLE [ApprovalSteps](
          [Id] bigint IDENTITY PRIMARY KEY,
          [ApprovalRequestId] bigint NOT NULL,
          [Sequence] int NOT NULL,
          [ApproverRole] nvarchar(80) NOT NULL,
          [ActedByUserId] nvarchar(450) NULL,
          [Status] int NOT NULL,
          [Comment] nvarchar(1000) NULL,
          [ActedUtc] datetime2 NULL,
          CONSTRAINT [FK_ApprovalSteps_Requests] FOREIGN KEY([ApprovalRequestId])
            REFERENCES [ApprovalRequests]([Id]) ON DELETE CASCADE);
        CREATE UNIQUE INDEX [IX_ApprovalSteps_Request_Sequence] ON [ApprovalSteps]([ApprovalRequestId],[Sequence]);

        CREATE TABLE [LibraryBooks](
          [Id] int IDENTITY PRIMARY KEY,
          [Title] nvarchar(200) NOT NULL,
          [Author] nvarchar(160) NOT NULL,
          [Isbn] nvarchar(20) NULL,
          [Category] nvarchar(80) NULL,
          [Publisher] nvarchar(80) NULL);
        CREATE UNIQUE INDEX [IX_LibraryBooks_Isbn] ON [LibraryBooks]([Isbn]) WHERE [Isbn] IS NOT NULL;

        CREATE TABLE [LibraryBookCopies](
          [Id] int IDENTITY PRIMARY KEY,
          [LibraryBookId] int NOT NULL,
          [Barcode] nvarchar(50) NOT NULL,
          [ShelfLocation] nvarchar(80) NULL,
          [IsAvailable] bit NOT NULL,
          CONSTRAINT [FK_LibraryBookCopies_Books] FOREIGN KEY([LibraryBookId])
            REFERENCES [LibraryBooks]([Id]) ON DELETE CASCADE);
        CREATE UNIQUE INDEX [IX_LibraryBookCopies_Barcode] ON [LibraryBookCopies]([Barcode]);

        CREATE TABLE [LibraryLoans](
          [Id] bigint IDENTITY PRIMARY KEY,
          [LibraryBookCopyId] int NOT NULL,
          [BorrowerUserId] nvarchar(450) NOT NULL,
          [IssuedUtc] datetime2 NOT NULL,
          [DueUtc] datetime2 NOT NULL,
          [ReturnedUtc] datetime2 NULL,
          [FineAmount] decimal(18,2) NOT NULL,
          CONSTRAINT [FK_LibraryLoans_Copies] FOREIGN KEY([LibraryBookCopyId]) REFERENCES [LibraryBookCopies]([Id]),
          CONSTRAINT [FK_LibraryLoans_Users] FOREIGN KEY([BorrowerUserId]) REFERENCES [AspNetUsers]([Id]));
        CREATE INDEX [IX_LibraryLoans_Copy_ReturnedUtc] ON [LibraryLoans]([LibraryBookCopyId],[ReturnedUtc]);
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "ERP workflow and finance records must be restored from a verified backup rather than destructively rolled back.");
    }
}
