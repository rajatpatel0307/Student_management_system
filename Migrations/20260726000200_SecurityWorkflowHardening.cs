using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Student_Management_System.Models;

#nullable disable

namespace Student_Management_System.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260726000200_SecurityWorkflowHardening")]
public sealed class SecurityWorkflowHardening : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        IF COL_LENGTH('AspNetUsers','MustChangePassword') IS NULL ALTER TABLE [AspNetUsers] ADD [MustChangePassword] bit NOT NULL CONSTRAINT [DF_AspNetUsers_MustChangePassword] DEFAULT 0;
        IF COL_LENGTH('AspNetUsers','IsActive') IS NULL ALTER TABLE [AspNetUsers] ADD [IsActive] bit NOT NULL CONSTRAINT [DF_AspNetUsers_IsActive] DEFAULT 1;
        IF COL_LENGTH('AspNetUsers','LastLoginUtc') IS NULL ALTER TABLE [AspNetUsers] ADD [LastLoginUtc] datetime2 NULL;

        IF COL_LENGTH('Exams','Status') IS NULL
        BEGIN
          ALTER TABLE [Exams] ADD [Status] int NOT NULL CONSTRAINT [DF_Exams_Status] DEFAULT 1;
          EXEC(N'UPDATE [Exams] SET [Status]=CASE WHEN [IsApproved]=1 THEN 4 WHEN [IsLocked]=1 THEN 3 ELSE 2 END;');
        END;
        IF COL_LENGTH('Exams','MaximumMarks') IS NULL ALTER TABLE [Exams] ADD [MaximumMarks] decimal(18,2) NOT NULL CONSTRAINT [DF_Exams_MaximumMarks] DEFAULT 100;
        IF COL_LENGTH('Exams','PassMarks') IS NULL ALTER TABLE [Exams] ADD [PassMarks] decimal(18,2) NOT NULL CONSTRAINT [DF_Exams_PassMarks] DEFAULT 40;
        IF COL_LENGTH('Exams','WeightPercent') IS NULL ALTER TABLE [Exams] ADD [WeightPercent] decimal(18,2) NOT NULL CONSTRAINT [DF_Exams_WeightPercent] DEFAULT 100;
        IF COL_LENGTH('Exams','RowVersion') IS NULL ALTER TABLE [Exams] ADD [RowVersion] rowversion NOT NULL;
        IF COL_LENGTH('Marks','ApprovedByUserId') IS NULL ALTER TABLE [Marks] ADD [ApprovedByUserId] nvarchar(450) NULL;
        IF COL_LENGTH('Marks','ApprovedUtc') IS NULL ALTER TABLE [Marks] ADD [ApprovedUtc] datetime2 NULL;
        ALTER TABLE [Marks] ALTER COLUMN [Score] decimal(18,2) NOT NULL;

        IF COL_LENGTH('FinancialTransactions','IdempotencyKey') IS NULL ALTER TABLE [FinancialTransactions] ADD [IdempotencyKey] nvarchar(100) NULL;
        IF COL_LENGTH('FinancialTransactions','RowVersion') IS NULL ALTER TABLE [FinancialTransactions] ADD [RowVersion] rowversion NOT NULL;
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_FinancialTransactions_IdempotencyKey')
          EXEC(N'CREATE UNIQUE INDEX [IX_FinancialTransactions_IdempotencyKey] ON [FinancialTransactions]([IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL;');

        IF OBJECT_ID('AccountActivations') IS NULL CREATE TABLE [AccountActivations](
          [Id] bigint IDENTITY PRIMARY KEY,[UserId] nvarchar(450) NOT NULL,[TokenHash] nvarchar(64) NOT NULL,
          [ExpiresUtc] datetime2 NOT NULL,[CreatedUtc] datetime2 NOT NULL,[UsedUtc] datetime2 NULL,
          [LastSentUtc] datetime2 NULL,[SendCount] int NOT NULL,[DeliveryPending] bit NOT NULL,
          CONSTRAINT [FK_AccountActivations_Users] FOREIGN KEY([UserId]) REFERENCES [AspNetUsers]([Id]));
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_AccountActivations_TokenHash')
          EXEC(N'CREATE UNIQUE INDEX [IX_AccountActivations_TokenHash] ON [AccountActivations]([TokenHash]);');
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_AccountActivations_UserId_UsedUtc')
          EXEC(N'CREATE INDEX [IX_AccountActivations_UserId_UsedUtc] ON [AccountActivations]([UserId],[UsedUtc]);');

        IF OBJECT_ID('ReceiptSequences') IS NULL CREATE TABLE [ReceiptSequences](
          [Id] int IDENTITY PRIMARY KEY,[Period] nvarchar(20) NOT NULL,[NextValue] bigint NOT NULL,[RowVersion] rowversion NOT NULL);
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_ReceiptSequences_Period')
          EXEC(N'CREATE UNIQUE INDEX [IX_ReceiptSequences_Period] ON [ReceiptSequences]([Period]);');

        IF OBJECT_ID('AttendanceSessions') IS NULL CREATE TABLE [AttendanceSessions](
          [Id] bigint IDENTITY PRIMARY KEY,[ClassId] int NOT NULL,[SubjectId] int NOT NULL,[TeacherId] int NOT NULL,
          [SessionDate] datetime2 NOT NULL,[Period] nvarchar(100) NOT NULL,[Status] int NOT NULL,
          [SubmittedUtc] datetime2 NULL,[ApprovedByUserId] nvarchar(450) NULL,[ApprovedUtc] datetime2 NULL,[RowVersion] rowversion NOT NULL,
          CONSTRAINT [FK_AttendanceSessions_Classes] FOREIGN KEY([ClassId]) REFERENCES [Classes]([Id]),
          CONSTRAINT [FK_AttendanceSessions_Subjects] FOREIGN KEY([SubjectId]) REFERENCES [Subjects]([Id]),
          CONSTRAINT [FK_AttendanceSessions_Teachers] FOREIGN KEY([TeacherId]) REFERENCES [Teachers]([Id]));
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_AttendanceSessions_Unique')
          EXEC(N'CREATE UNIQUE INDEX [IX_AttendanceSessions_Unique] ON [AttendanceSessions]([ClassId],[SubjectId],[SessionDate],[Period]);');
        IF COL_LENGTH('Attendances','AttendanceSessionId') IS NULL
        BEGIN
          ALTER TABLE [Attendances] ADD [AttendanceSessionId] bigint NULL;
          EXEC(N'ALTER TABLE [Attendances] ADD CONSTRAINT [FK_Attendances_AttendanceSessions] FOREIGN KEY([AttendanceSessionId]) REFERENCES [AttendanceSessions]([Id]);');
        END;
        IF OBJECT_ID('AttendanceCorrections') IS NULL CREATE TABLE [AttendanceCorrections](
          [Id] bigint IDENTITY PRIMARY KEY,[AttendanceId] int NOT NULL,[PreviousStatus] nvarchar(20) NOT NULL,
          [RequestedStatus] nvarchar(20) NOT NULL,[Reason] nvarchar(1000) NOT NULL,[RequestedByUserId] nvarchar(450) NOT NULL,
          [RequestedUtc] datetime2 NOT NULL,[Status] int NOT NULL,[ReviewedByUserId] nvarchar(450) NULL,
          [ReviewComment] nvarchar(1000) NULL,[ReviewedUtc] datetime2 NULL,
          CONSTRAINT [FK_AttendanceCorrections_Attendances] FOREIGN KEY([AttendanceId]) REFERENCES [Attendances]([Id]));
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_AttendanceCorrections_AttendanceId_Status')
          EXEC(N'CREATE INDEX [IX_AttendanceCorrections_AttendanceId_Status] ON [AttendanceCorrections]([AttendanceId],[Status]);');

        DECLARE @archiveTables TABLE([Name] sysname);
        INSERT INTO @archiveTables VALUES ('Students'),('Teachers'),('Departments'),('Classes'),('Sections'),('Subjects'),('Semesters'),('AcademicYears'),('Fees'),('AdmissionApplications');
        DECLARE @table sysname;
        DECLARE archive_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT [Name] FROM @archiveTables;
        OPEN archive_cursor; FETCH NEXT FROM archive_cursor INTO @table;
        WHILE @@FETCH_STATUS=0
        BEGIN
          IF COL_LENGTH(@table,'IsArchived') IS NULL EXEC('ALTER TABLE ['+@table+'] ADD [IsArchived] bit NOT NULL CONSTRAINT [DF_'+@table+'_IsArchived] DEFAULT 0');
          IF COL_LENGTH(@table,'ArchivedUtc') IS NULL EXEC('ALTER TABLE ['+@table+'] ADD [ArchivedUtc] datetime2 NULL');
          IF COL_LENGTH(@table,'ArchivedByUserId') IS NULL EXEC('ALTER TABLE ['+@table+'] ADD [ArchivedByUserId] nvarchar(450) NULL');
          IF COL_LENGTH(@table,'ArchiveReason') IS NULL EXEC('ALTER TABLE ['+@table+'] ADD [ArchiveReason] nvarchar(500) NULL');
          IF COL_LENGTH(@table,'RestoredUtc') IS NULL EXEC('ALTER TABLE ['+@table+'] ADD [RestoredUtc] datetime2 NULL');
          IF COL_LENGTH(@table,'RestoredByUserId') IS NULL EXEC('ALTER TABLE ['+@table+'] ADD [RestoredByUserId] nvarchar(450) NULL');
          IF COL_LENGTH(@table,'RowVersion') IS NULL EXEC('ALTER TABLE ['+@table+'] ADD [RowVersion] rowversion NOT NULL');
          FETCH NEXT FROM archive_cursor INTO @table;
        END
        CLOSE archive_cursor; DEALLOCATE archive_cursor;
        IF OBJECT_ID('AcademicPrograms') IS NULL CREATE TABLE [AcademicPrograms](
          [Id] int IDENTITY PRIMARY KEY,[Code] nvarchar(30) NOT NULL,[Name] nvarchar(160) NOT NULL,[DepartmentId] int NOT NULL,
          [DurationSemesters] int NOT NULL,[IsActive] bit NOT NULL,
          CONSTRAINT [FK_AcademicPrograms_Departments] FOREIGN KEY([DepartmentId]) REFERENCES [Departments]([Id]));
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_AcademicPrograms_Code') EXEC(N'CREATE UNIQUE INDEX [IX_AcademicPrograms_Code] ON [AcademicPrograms]([Code]);');
        IF OBJECT_ID('AcademicIntakes') IS NULL CREATE TABLE [AcademicIntakes](
          [Id] int IDENTITY PRIMARY KEY,[AcademicProgramId] int NOT NULL,[AcademicYearId] int NOT NULL,[SeatCapacity] int NOT NULL,
          [OpensUtc] datetime2 NOT NULL,[ClosesUtc] datetime2 NOT NULL,
          CONSTRAINT [FK_AcademicIntakes_Programs] FOREIGN KEY([AcademicProgramId]) REFERENCES [AcademicPrograms]([Id]),
          CONSTRAINT [FK_AcademicIntakes_Years] FOREIGN KEY([AcademicYearId]) REFERENCES [AcademicYears]([Id]));
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_AcademicIntakes_Program_Year')
          EXEC(N'CREATE UNIQUE INDEX [IX_AcademicIntakes_Program_Year] ON [AcademicIntakes]([AcademicProgramId],[AcademicYearId]);');

        IF OBJECT_ID('AdmissionDocuments') IS NULL CREATE TABLE [AdmissionDocuments](
          [Id] bigint IDENTITY PRIMARY KEY,[AdmissionApplicationId] bigint NOT NULL,[DocumentType] nvarchar(80) NOT NULL,
          [StorageKey] nvarchar(500) NOT NULL,[OriginalFileName] nvarchar(255) NOT NULL,[ContentType] nvarchar(100) NOT NULL,
          [SizeBytes] bigint NOT NULL,[Status] int NOT NULL,[VerifiedByUserId] nvarchar(450) NULL,[VerifiedUtc] datetime2 NULL,
          [VerificationComment] nvarchar(1000) NULL,[CreatedUtc] datetime2 NOT NULL,
          CONSTRAINT [FK_AdmissionDocuments_Applications] FOREIGN KEY([AdmissionApplicationId]) REFERENCES [AdmissionApplications]([Id]));
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_AdmissionDocuments_Application_Type')
          EXEC(N'CREATE INDEX [IX_AdmissionDocuments_Application_Type] ON [AdmissionDocuments]([AdmissionApplicationId],[DocumentType]);');

        IF COL_LENGTH('Assignments','MaximumScore') IS NULL ALTER TABLE [Assignments] ADD [MaximumScore] decimal(18,2) NOT NULL CONSTRAINT [DF_Assignments_MaximumScore] DEFAULT 100;
        ALTER TABLE [AssignmentSubmissions] ALTER COLUMN [Score] decimal(18,2) NULL;
        IF COL_LENGTH('AssignmentSubmissions','GradedUtc') IS NULL ALTER TABLE [AssignmentSubmissions] ADD [GradedUtc] datetime2 NULL,[GradedByUserId] nvarchar(450) NULL;
        IF OBJECT_ID('AssignmentRubricCriteria') IS NULL CREATE TABLE [AssignmentRubricCriteria](
          [Id] int IDENTITY PRIMARY KEY,[AssignmentId] int NOT NULL,[Name] nvarchar(160) NOT NULL,[MaximumScore] decimal(18,2) NOT NULL,[Description] nvarchar(1000) NULL,
          CONSTRAINT [FK_AssignmentRubricCriteria_Assignments] FOREIGN KEY([AssignmentId]) REFERENCES [Assignments]([Id]));
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_AssignmentRubricCriteria_Assignment_Name')
          EXEC(N'CREATE UNIQUE INDEX [IX_AssignmentRubricCriteria_Assignment_Name] ON [AssignmentRubricCriteria]([AssignmentId],[Name]);');
        IF OBJECT_ID('AssignmentSubmissionVersions') IS NULL CREATE TABLE [AssignmentSubmissionVersions](
          [Id] bigint IDENTITY PRIMARY KEY,[AssignmentSubmissionId] int NOT NULL,[VersionNumber] int NOT NULL,[Answer] nvarchar(4000) NOT NULL,
          [AttachmentStorageKey] nvarchar(500) NULL,[SubmittedUtc] datetime2 NOT NULL,
          CONSTRAINT [FK_AssignmentSubmissionVersions_Submissions] FOREIGN KEY([AssignmentSubmissionId]) REFERENCES [AssignmentSubmissions]([Id]));
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_AssignmentSubmissionVersions_Submission_Version')
          EXEC(N'CREATE UNIQUE INDEX [IX_AssignmentSubmissionVersions_Submission_Version] ON [AssignmentSubmissionVersions]([AssignmentSubmissionId],[VersionNumber]);');

        IF COL_LENGTH('LibraryBookCopies','Condition') IS NULL ALTER TABLE [LibraryBookCopies] ADD [Condition] int NOT NULL CONSTRAINT [DF_LibraryBookCopies_Condition] DEFAULT 1;
        IF COL_LENGTH('LibraryLoans','RenewalCount') IS NULL ALTER TABLE [LibraryLoans] ADD [RenewalCount] int NOT NULL CONSTRAINT [DF_LibraryLoans_RenewalCount] DEFAULT 0,[LastRenewedUtc] datetime2 NULL;
        IF OBJECT_ID('LibraryReservations') IS NULL CREATE TABLE [LibraryReservations](
          [Id] bigint IDENTITY PRIMARY KEY,[LibraryBookId] int NOT NULL,[UserId] nvarchar(450) NOT NULL,[ReservedUtc] datetime2 NOT NULL,
          [ExpiresUtc] datetime2 NULL,[FulfilledUtc] datetime2 NULL,[CancelledUtc] datetime2 NULL,
          CONSTRAINT [FK_LibraryReservations_Books] FOREIGN KEY([LibraryBookId]) REFERENCES [LibraryBooks]([Id]),
          CONSTRAINT [FK_LibraryReservations_Users] FOREIGN KEY([UserId]) REFERENCES [AspNetUsers]([Id]));
        IF OBJECT_ID('LibraryFineSettlements') IS NULL CREATE TABLE [LibraryFineSettlements](
          [Id] bigint IDENTITY PRIMARY KEY,[LibraryLoanId] bigint NOT NULL,[Amount] decimal(18,2) NOT NULL,[Method] nvarchar(40) NOT NULL,
          [Reference] nvarchar(100) NULL,[CollectedByUserId] nvarchar(450) NOT NULL,[SettledUtc] datetime2 NOT NULL,
          CONSTRAINT [FK_LibraryFineSettlements_Loans] FOREIGN KEY([LibraryLoanId]) REFERENCES [LibraryLoans]([Id]));

        IF OBJECT_ID('TransportRoutes') IS NULL CREATE TABLE [TransportRoutes]([Id] int IDENTITY PRIMARY KEY,[Code] nvarchar(30) NOT NULL,[Name] nvarchar(160) NOT NULL,[Stops] nvarchar(1000) NOT NULL,[IsActive] bit NOT NULL);
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_TransportRoutes_Code') EXEC(N'CREATE UNIQUE INDEX [IX_TransportRoutes_Code] ON [TransportRoutes]([Code]);');
        IF OBJECT_ID('TransportEnrollments') IS NULL CREATE TABLE [TransportEnrollments]([Id] bigint IDENTITY PRIMARY KEY,[TransportRouteId] int NOT NULL,[StudentId] int NOT NULL,[BoardingStop] nvarchar(120) NOT NULL,[StartsOn] datetime2 NOT NULL,[EndsOn] datetime2 NULL,CONSTRAINT [FK_TransportEnrollments_Routes] FOREIGN KEY([TransportRouteId]) REFERENCES [TransportRoutes]([Id]),CONSTRAINT [FK_TransportEnrollments_Students] FOREIGN KEY([StudentId]) REFERENCES [Students]([Id]));
        IF OBJECT_ID('HostelRooms') IS NULL CREATE TABLE [HostelRooms]([Id] int IDENTITY PRIMARY KEY,[HostelName] nvarchar(80) NOT NULL,[RoomNumber] nvarchar(30) NOT NULL,[Capacity] int NOT NULL,[IsActive] bit NOT NULL);
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_HostelRooms_Hostel_Room') EXEC(N'CREATE UNIQUE INDEX [IX_HostelRooms_Hostel_Room] ON [HostelRooms]([HostelName],[RoomNumber]);');
        IF OBJECT_ID('HostelAllocations') IS NULL CREATE TABLE [HostelAllocations]([Id] bigint IDENTITY PRIMARY KEY,[HostelRoomId] int NOT NULL,[StudentId] int NOT NULL,[StartsOn] datetime2 NOT NULL,[EndsOn] datetime2 NULL,CONSTRAINT [FK_HostelAllocations_Rooms] FOREIGN KEY([HostelRoomId]) REFERENCES [HostelRooms]([Id]),CONSTRAINT [FK_HostelAllocations_Students] FOREIGN KEY([StudentId]) REFERENCES [Students]([Id]));
        IF OBJECT_ID('PlacementDrives') IS NULL CREATE TABLE [PlacementDrives]([Id] bigint IDENTITY PRIMARY KEY,[Employer] nvarchar(160) NOT NULL,[RoleTitle] nvarchar(160) NOT NULL,[ApplicationDeadlineUtc] datetime2 NOT NULL,[Eligibility] nvarchar(2000) NULL);
        IF OBJECT_ID('PlacementApplications') IS NULL CREATE TABLE [PlacementApplications]([Id] bigint IDENTITY PRIMARY KEY,[PlacementDriveId] bigint NOT NULL,[StudentId] int NOT NULL,[Status] nvarchar(40) NOT NULL,[AppliedUtc] datetime2 NOT NULL,CONSTRAINT [FK_PlacementApplications_Drives] FOREIGN KEY([PlacementDriveId]) REFERENCES [PlacementDrives]([Id]),CONSTRAINT [FK_PlacementApplications_Students] FOREIGN KEY([StudentId]) REFERENCES [Students]([Id]));
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_PlacementApplications_Drive_Student') EXEC(N'CREATE UNIQUE INDEX [IX_PlacementApplications_Drive_Student] ON [PlacementApplications]([PlacementDriveId],[StudentId]);');
        IF OBJECT_ID('EmployeeProfiles') IS NULL CREATE TABLE [EmployeeProfiles]([Id] int IDENTITY PRIMARY KEY,[UserId] nvarchar(450) NOT NULL,[EmployeeNumber] nvarchar(40) NOT NULL,[JobTitle] nvarchar(120) NOT NULL,[DepartmentId] int NULL,[JoinedOn] datetime2 NOT NULL,[IsActive] bit NOT NULL,CONSTRAINT [FK_EmployeeProfiles_Users] FOREIGN KEY([UserId]) REFERENCES [AspNetUsers]([Id]),CONSTRAINT [FK_EmployeeProfiles_Departments] FOREIGN KEY([DepartmentId]) REFERENCES [Departments]([Id]));
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_EmployeeProfiles_UserId') EXEC(N'CREATE UNIQUE INDEX [IX_EmployeeProfiles_UserId] ON [EmployeeProfiles]([UserId]);');
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_EmployeeProfiles_EmployeeNumber') EXEC(N'CREATE UNIQUE INDEX [IX_EmployeeProfiles_EmployeeNumber] ON [EmployeeProfiles]([EmployeeNumber]);');
        IF OBJECT_ID('GradeBoundaries') IS NULL CREATE TABLE [GradeBoundaries]([Id] int IDENTITY PRIMARY KEY,[Grade] nvarchar(20) NOT NULL,[MinimumPercent] decimal(18,2) NOT NULL,[MaximumPercent] decimal(18,2) NOT NULL,[DepartmentId] int NULL,[IsActive] bit NOT NULL,CONSTRAINT [FK_GradeBoundaries_Departments] FOREIGN KEY([DepartmentId]) REFERENCES [Departments]([Id]));
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_GradeBoundaries_Department_Grade') EXEC(N'CREATE UNIQUE INDEX [IX_GradeBoundaries_Department_Grade] ON [GradeBoundaries]([DepartmentId],[Grade]);');
        IF OBJECT_ID('ResultLifecycleHistories') IS NULL CREATE TABLE [ResultLifecycleHistories]([Id] bigint IDENTITY PRIMARY KEY,[ExamId] int NOT NULL,[FromStatus] int NOT NULL,[ToStatus] int NOT NULL,[ChangedByUserId] nvarchar(450) NOT NULL,[Comment] nvarchar(1000) NULL,[ChangedUtc] datetime2 NOT NULL,CONSTRAINT [FK_ResultLifecycleHistories_Exams] FOREIGN KEY([ExamId]) REFERENCES [Exams]([Id]));
        IF OBJECT_ID('ResultCorrectionRequests') IS NULL CREATE TABLE [ResultCorrectionRequests]([Id] bigint IDENTITY PRIMARY KEY,[MarkId] int NOT NULL,[PreviousScore] decimal(18,2) NOT NULL,[RequestedScore] decimal(18,2) NOT NULL,[Reason] nvarchar(1000) NOT NULL,[RequestedByUserId] nvarchar(450) NOT NULL,[Status] int NOT NULL,[ReviewedByUserId] nvarchar(450) NULL,[RequestedUtc] datetime2 NOT NULL,[ReviewedUtc] datetime2 NULL,CONSTRAINT [FK_ResultCorrectionRequests_Marks] FOREIGN KEY([MarkId]) REFERENCES [Marks]([Id]));
        """);

        // Execute the backfill in a separate SQL batch. SQL Server compiles a
        // batch before running ALTER TABLE, so referencing IdempotencyKey in
        // the same batch that creates it fails on an upgraded legacy schema.
        migrationBuilder.Sql("""
        INSERT INTO [FinancialTransactions](
          [ReceiptNumber],[StudentId],[FeeId],[Type],[Amount],[PaymentMethod],
          [TransactionReference],[Notes],[CreatedByUserId],[CreatedUtc],
          [ReversesTransactionId],[IdempotencyKey])
        SELECT
          CASE WHEN NULLIF(p.[Reference],'') IS NULL
            THEN CONCAT('LEGACY-',p.[Id]) ELSE p.[Reference] END,
          p.[StudentId],p.[FeeId],1,p.[AmountPaid],'Legacy',p.[Reference],
          'Migrated from legacy Payments','migration',p.[PaymentDateUtc],NULL,
          CONCAT('legacy-payment:',p.[Id])
        FROM [Payments] p
        WHERE NOT EXISTS(
          SELECT 1 FROM [FinancialTransactions] f
          WHERE f.[IdempotencyKey]=CONCAT('legacy-payment:',p.[Id]))
          AND NOT EXISTS(
            SELECT 1 FROM [FinancialTransactions] f
            WHERE f.[ReceiptNumber]=p.[Reference]
              AND NULLIF(p.[Reference],'') IS NOT NULL);
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally non-destructive. This reconciliation migration backfills finance
        // history and adds audit-sensitive workflow data. Restore a verified database
        // backup for rollback instead of dropping records automatically.
    }
}
