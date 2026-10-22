using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Student_Management_System.Models;

#nullable disable

namespace Student_Management_System.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260729000100_ErpSixteenPhaseExpansion")]
public sealed class ErpSixteenPhaseExpansion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        /* Existing-record expansion: every operation is guarded for clean and upgraded databases. */
        IF COL_LENGTH('Students','AdmissionNumber') IS NULL ALTER TABLE [Students] ADD [AdmissionNumber] nvarchar(40) NULL;
        IF COL_LENGTH('Students','RollNumber') IS NULL ALTER TABLE [Students] ADD [RollNumber] nvarchar(40) NULL;
        IF COL_LENGTH('Students','StudentStatus') IS NULL ALTER TABLE [Students] ADD [StudentStatus] nvarchar(40) NOT NULL CONSTRAINT [DF_Students_StudentStatus] DEFAULT 'Active';
        IF COL_LENGTH('Students','AdmissionDate') IS NULL ALTER TABLE [Students] ADD [AdmissionDate] datetime2 NULL;
        IF COL_LENGTH('Students','ExpectedGraduationDate') IS NULL ALTER TABLE [Students] ADD [ExpectedGraduationDate] datetime2 NULL;
        IF COL_LENGTH('Students','EmergencyContactName') IS NULL ALTER TABLE [Students] ADD [EmergencyContactName] nvarchar(160) NULL;
        IF COL_LENGTH('Students','EmergencyContactPhone') IS NULL ALTER TABLE [Students] ADD [EmergencyContactPhone] nvarchar(30) NULL;
        IF COL_LENGTH('Students','BloodGroup') IS NULL ALTER TABLE [Students] ADD [BloodGroup] nvarchar(10) NULL;
        IF COL_LENGTH('Students','AcademicProgramId') IS NULL ALTER TABLE [Students] ADD [AcademicProgramId] int NULL;
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Students_AdmissionNumber') EXEC(N'CREATE UNIQUE INDEX [IX_Students_AdmissionNumber] ON [Students]([AdmissionNumber]) WHERE [AdmissionNumber] IS NOT NULL;');
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Students_RollNumber') EXEC(N'CREATE INDEX [IX_Students_RollNumber] ON [Students]([RollNumber]) WHERE [RollNumber] IS NOT NULL;');

        IF COL_LENGTH('AcademicPrograms','ProgramTypeId') IS NULL ALTER TABLE [AcademicPrograms] ADD [ProgramTypeId] int NULL;
        IF COL_LENGTH('AcademicPrograms','CampusId') IS NULL ALTER TABLE [AcademicPrograms] ADD [CampusId] int NULL;
        IF COL_LENGTH('AcademicIntakes','ReservedSeats') IS NULL ALTER TABLE [AcademicIntakes] ADD [ReservedSeats] int NOT NULL CONSTRAINT [DF_AcademicIntakes_ReservedSeats] DEFAULT 0;
        IF COL_LENGTH('AdmissionApplications','AcademicProgramId') IS NULL ALTER TABLE [AdmissionApplications] ADD [AcademicProgramId] int NULL;
        IF COL_LENGTH('AdmissionApplications','AcademicIntakeId') IS NULL ALTER TABLE [AdmissionApplications] ADD [AcademicIntakeId] int NULL;
        IF COL_LENGTH('AdmissionApplications','AssignedReviewerUserId') IS NULL ALTER TABLE [AdmissionApplications] ADD [AssignedReviewerUserId] nvarchar(450) NULL;

        IF COL_LENGTH('GuardianStudentLinks','CanViewAcademics') IS NULL ALTER TABLE [GuardianStudentLinks] ADD [CanViewAcademics] bit NOT NULL CONSTRAINT [DF_GuardianLinks_CanViewAcademics] DEFAULT 1;
        IF COL_LENGTH('GuardianStudentLinks','CanReceiveEmergencyContact') IS NULL ALTER TABLE [GuardianStudentLinks] ADD [CanReceiveEmergencyContact] bit NOT NULL CONSTRAINT [DF_GuardianLinks_Emergency] DEFAULT 1;
        IF COL_LENGTH('GuardianStudentLinks','Relationship') IS NULL ALTER TABLE [GuardianStudentLinks] ADD [Relationship] nvarchar(40) NOT NULL CONSTRAINT [DF_GuardianLinks_Relationship] DEFAULT 'Guardian';
        IF COL_LENGTH('GuardianStudentLinks','CommunicationPreferences') IS NULL ALTER TABLE [GuardianStudentLinks] ADD [CommunicationPreferences] nvarchar(500) NULL;
        IF COL_LENGTH('GuardianStudentLinks','LinkedUtc') IS NULL ALTER TABLE [GuardianStudentLinks] ADD [LinkedUtc] datetime2 NOT NULL CONSTRAINT [DF_GuardianLinks_LinkedUtc] DEFAULT SYSUTCDATETIME();
        IF COL_LENGTH('GuardianStudentLinks','UnlinkedUtc') IS NULL ALTER TABLE [GuardianStudentLinks] ADD [UnlinkedUtc] datetime2 NULL;

        IF COL_LENGTH('AttendanceSessions','SectionId') IS NULL ALTER TABLE [AttendanceSessions] ADD [SectionId] int NULL;
        IF COL_LENGTH('AttendanceSessions','StartsAt') IS NULL ALTER TABLE [AttendanceSessions] ADD [StartsAt] time NULL;
        IF COL_LENGTH('AttendanceSessions','EndsAt') IS NULL ALTER TABLE [AttendanceSessions] ADD [EndsAt] time NULL;
        IF COL_LENGTH('ApprovalRequests','DueUtc') IS NULL ALTER TABLE [ApprovalRequests] ADD [DueUtc] datetime2 NULL;
        IF COL_LENGTH('ApprovalRequests','EscalatedUtc') IS NULL ALTER TABLE [ApprovalRequests] ADD [EscalatedUtc] datetime2 NULL;
        IF COL_LENGTH('ApprovalSteps','ApproverUserId') IS NULL ALTER TABLE [ApprovalSteps] ADD [ApproverUserId] nvarchar(450) NULL;
        IF COL_LENGTH('ApprovalSteps','DelegatedToUserId') IS NULL ALTER TABLE [ApprovalSteps] ADD [DelegatedToUserId] nvarchar(450) NULL;
        IF COL_LENGTH('ApprovalSteps','DueUtc') IS NULL ALTER TABLE [ApprovalSteps] ADD [DueUtc] datetime2 NULL;
        IF COL_LENGTH('ApprovalSteps','EscalatedUtc') IS NULL ALTER TABLE [ApprovalSteps] ADD [EscalatedUtc] datetime2 NULL;

        IF COL_LENGTH('Assignments','PublishUtc') IS NULL ALTER TABLE [Assignments] ADD [PublishUtc] datetime2 NULL;
        IF COL_LENGTH('Assignments','Status') IS NULL ALTER TABLE [Assignments] ADD [Status] nvarchar(40) NOT NULL CONSTRAINT [DF_Assignments_Status] DEFAULT 'Published';
        IF COL_LENGTH('Assignments','SubmissionType') IS NULL ALTER TABLE [Assignments] ADD [SubmissionType] nvarchar(40) NOT NULL CONSTRAINT [DF_Assignments_SubmissionType] DEFAULT 'TextOrFile';
        IF COL_LENGTH('Assignments','MaximumSubmissions') IS NULL ALTER TABLE [Assignments] ADD [MaximumSubmissions] int NOT NULL CONSTRAINT [DF_Assignments_MaximumSubmissions] DEFAULT 1;
        IF COL_LENGTH('Assignments','AllowLateSubmission') IS NULL ALTER TABLE [Assignments] ADD [AllowLateSubmission] bit NOT NULL CONSTRAINT [DF_Assignments_AllowLate] DEFAULT 0;
        IF COL_LENGTH('Assignments','LatePenaltyPercent') IS NULL ALTER TABLE [Assignments] ADD [LatePenaltyPercent] decimal(8,2) NOT NULL CONSTRAINT [DF_Assignments_LatePenalty] DEFAULT 0;
        IF COL_LENGTH('AssignmentSubmissions','EvaluationStatus') IS NULL ALTER TABLE [AssignmentSubmissions] ADD [EvaluationStatus] nvarchar(40) NOT NULL CONSTRAINT [DF_Submissions_EvaluationStatus] DEFAULT 'Pending';
        IF COL_LENGTH('TimetableEntries','RoomId') IS NULL ALTER TABLE [TimetableEntries] ADD [RoomId] int NULL;
        IF COL_LENGTH('TimetableEntries','IsCancelled') IS NULL ALTER TABLE [TimetableEntries] ADD [IsCancelled] bit NOT NULL CONSTRAINT [DF_Timetable_IsCancelled] DEFAULT 0;
        IF COL_LENGTH('TimetableEntries','CancellationReason') IS NULL ALTER TABLE [TimetableEntries] ADD [CancellationReason] nvarchar(500) NULL;
        IF COL_LENGTH('TimetableEntries','RowVersion') IS NULL ALTER TABLE [TimetableEntries] ADD [RowVersion] rowversion NOT NULL;
        IF COL_LENGTH('Subjects','AcademicSubjectTypeId') IS NULL ALTER TABLE [Subjects] ADD [AcademicSubjectTypeId] int NULL;
        IF COL_LENGTH('Subjects','CreditValue') IS NULL ALTER TABLE [Subjects] ADD [CreditValue] decimal(8,2) NOT NULL CONSTRAINT [DF_Subjects_CreditValue] DEFAULT 0;
        IF COL_LENGTH('Subjects','ElectiveGroup') IS NULL ALTER TABLE [Subjects] ADD [ElectiveGroup] nvarchar(80) NULL;
        IF COL_LENGTH('Exams','EntryOpensUtc') IS NULL ALTER TABLE [Exams] ADD [EntryOpensUtc] datetime2 NULL;
        IF COL_LENGTH('Exams','EntryClosesUtc') IS NULL ALTER TABLE [Exams] ADD [EntryClosesUtc] datetime2 NULL;
        IF COL_LENGTH('Exams','PublicationScheduledUtc') IS NULL ALTER TABLE [Exams] ADD [PublicationScheduledUtc] datetime2 NULL;
        IF COL_LENGTH('Exams','PublishedUtc') IS NULL ALTER TABLE [Exams] ADD [PublishedUtc] datetime2 NULL;
        IF COL_LENGTH('Exams','PublishedByUserId') IS NULL ALTER TABLE [Exams] ADD [PublishedByUserId] nvarchar(450) NULL;
        IF COL_LENGTH('Exams','IsSupplementary') IS NULL ALTER TABLE [Exams] ADD [IsSupplementary] bit NOT NULL CONSTRAINT [DF_Exams_IsSupplementary] DEFAULT 0;
        IF COL_LENGTH('Exams','ModerationStatus') IS NULL ALTER TABLE [Exams] ADD [ModerationStatus] nvarchar(40) NOT NULL CONSTRAINT [DF_Exams_ModerationStatus] DEFAULT 'Pending';
        IF COL_LENGTH('Marks','ResultStatus') IS NULL ALTER TABLE [Marks] ADD [ResultStatus] nvarchar(40) NOT NULL CONSTRAINT [DF_Marks_ResultStatus] DEFAULT 'Recorded';
        IF COL_LENGTH('Marks','GraceMarks') IS NULL ALTER TABLE [Marks] ADD [GraceMarks] decimal(18,2) NOT NULL CONSTRAINT [DF_Marks_GraceMarks] DEFAULT 0;
        IF COL_LENGTH('Marks','IsWithheld') IS NULL ALTER TABLE [Marks] ADD [IsWithheld] bit NOT NULL CONSTRAINT [DF_Marks_IsWithheld] DEFAULT 0;
        IF COL_LENGTH('LibraryBooks','LibraryCategoryId') IS NULL ALTER TABLE [LibraryBooks] ADD [LibraryCategoryId] int NULL;
        IF COL_LENGTH('LibraryBooks','LibraryPublisherId') IS NULL ALTER TABLE [LibraryBooks] ADD [LibraryPublisherId] int NULL;
        IF COL_LENGTH('LibraryBooks','IsArchived') IS NULL ALTER TABLE [LibraryBooks] ADD [IsArchived] bit NOT NULL CONSTRAINT [DF_LibraryBooks_IsArchived] DEFAULT 0;

        IF OBJECT_ID('Institutions') IS NULL CREATE TABLE [Institutions](
          [Id] int IDENTITY PRIMARY KEY,[Name] nvarchar(180) NOT NULL,[Code] nvarchar(30) NOT NULL,[Address] nvarchar(500) NULL,
          [Email] nvarchar(256) NULL,[Phone] nvarchar(30) NULL,[TimeZoneId] nvarchar(80) NOT NULL,[IsActive] bit NOT NULL,[RowVersion] rowversion NOT NULL);
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Institutions_Code') EXEC(N'CREATE UNIQUE INDEX [IX_Institutions_Code] ON [Institutions]([Code]);');
        IF OBJECT_ID('Campuses') IS NULL CREATE TABLE [Campuses](
          [Id] int IDENTITY PRIMARY KEY,[InstitutionId] int NOT NULL,[Name] nvarchar(160) NOT NULL,[Code] nvarchar(30) NOT NULL,
          [Address] nvarchar(500) NULL,[IsActive] bit NOT NULL,[RowVersion] rowversion NOT NULL,
          CONSTRAINT [FK_Campuses_Institutions] FOREIGN KEY([InstitutionId]) REFERENCES [Institutions]([Id]));
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Campuses_Institution_Code') EXEC(N'CREATE UNIQUE INDEX [IX_Campuses_Institution_Code] ON [Campuses]([InstitutionId],[Code]);');
        IF OBJECT_ID('ProgramTypes') IS NULL CREATE TABLE [ProgramTypes]([Id] int IDENTITY PRIMARY KEY,[Name] nvarchar(80) NOT NULL,[Description] nvarchar(300) NULL,[UsesCredits] bit NOT NULL,[IsActive] bit NOT NULL);
        IF OBJECT_ID('AcademicSessions') IS NULL CREATE TABLE [AcademicSessions]([Id] int IDENTITY PRIMARY KEY,[Name] nvarchar(80) NOT NULL,[StartsOn] datetime2 NOT NULL,[EndsOn] datetime2 NOT NULL,[AcademicYearId] int NULL,[IsCurrent] bit NOT NULL,[IsClosed] bit NOT NULL,[RowVersion] rowversion NOT NULL);
        IF OBJECT_ID('AcademicSubjectTypes') IS NULL CREATE TABLE [AcademicSubjectTypes]([Id] int IDENTITY PRIMARY KEY,[Name] nvarchar(80) NOT NULL,[IsPractical] bit NOT NULL,[IsElective] bit NOT NULL);
        IF OBJECT_ID('Curricula') IS NULL CREATE TABLE [Curricula]([Id] int IDENTITY PRIMARY KEY,[AcademicProgramId] int NOT NULL,[Name] nvarchar(160) NOT NULL,[IsArchived] bit NOT NULL,CONSTRAINT [FK_Curricula_Programs] FOREIGN KEY([AcademicProgramId]) REFERENCES [AcademicPrograms]([Id]));
        IF OBJECT_ID('CurriculumVersions') IS NULL CREATE TABLE [CurriculumVersions]([Id] int IDENTITY PRIMARY KEY,[CurriculumId] int NOT NULL,[Version] nvarchar(30) NOT NULL,[EffectiveAcademicSessionId] int NOT NULL,[ApprovedUtc] datetime2 NOT NULL,[ApprovedByUserId] nvarchar(450) NULL,[IsActive] bit NOT NULL,CONSTRAINT [FK_CurriculumVersions_Curricula] FOREIGN KEY([CurriculumId]) REFERENCES [Curricula]([Id]),CONSTRAINT [FK_CurriculumVersions_Sessions] FOREIGN KEY([EffectiveAcademicSessionId]) REFERENCES [AcademicSessions]([Id]));
        IF OBJECT_ID('CurriculumSubjects') IS NULL CREATE TABLE [CurriculumSubjects]([Id] int IDENTITY PRIMARY KEY,[CurriculumVersionId] int NOT NULL,[SubjectId] int NOT NULL,[SemesterNumber] int NOT NULL,[Credits] decimal(8,2) NOT NULL,[ElectiveGroup] nvarchar(80) NULL,[IsRequired] bit NOT NULL,CONSTRAINT [FK_CurriculumSubjects_Versions] FOREIGN KEY([CurriculumVersionId]) REFERENCES [CurriculumVersions]([Id]),CONSTRAINT [FK_CurriculumSubjects_Subjects] FOREIGN KEY([SubjectId]) REFERENCES [Subjects]([Id]));
        IF OBJECT_ID('AcademicCalendarEvents') IS NULL CREATE TABLE [AcademicCalendarEvents]([Id] bigint IDENTITY PRIMARY KEY,[AcademicSessionId] int NOT NULL,[Title] nvarchar(160) NOT NULL,[Description] nvarchar(1000) NULL,[Kind] int NOT NULL,[StartsUtc] datetime2 NOT NULL,[EndsUtc] datetime2 NOT NULL,[DepartmentId] int NULL,[IsPublished] bit NOT NULL,CONSTRAINT [FK_AcademicEvents_Sessions] FOREIGN KEY([AcademicSessionId]) REFERENCES [AcademicSessions]([Id]));

        IF OBJECT_ID('PromotionBatches') IS NULL CREATE TABLE [PromotionBatches](
          [Id] bigint IDENTITY PRIMARY KEY,[BatchNumber] nvarchar(40) NOT NULL,[SourceAcademicYearId] int NOT NULL,[DestinationAcademicYearId] int NOT NULL,
          [DepartmentId] int NULL,[SourceClassId] int NOT NULL,[SourceSectionId] int NOT NULL,[DestinationClassId] int NULL,[DestinationSectionId] int NULL,
          [Status] int NOT NULL,[CreatedByUserId] nvarchar(450) NOT NULL,[CreatedUtc] datetime2 NOT NULL,[ReviewedByUserId] nvarchar(450) NULL,
          [ReviewedUtc] datetime2 NULL,[AppliedByUserId] nvarchar(450) NULL,[AppliedUtc] datetime2 NULL,[ReviewComment] nvarchar(1000) NULL,[RowVersion] rowversion NOT NULL);
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_PromotionBatches_BatchNumber') EXEC(N'CREATE UNIQUE INDEX [IX_PromotionBatches_BatchNumber] ON [PromotionBatches]([BatchNumber]);');
        IF OBJECT_ID('PromotionEntries') IS NULL CREATE TABLE [PromotionEntries](
          [Id] bigint IDENTITY PRIMARY KEY,[PromotionBatchId] bigint NOT NULL,[StudentId] int NOT NULL,[Decision] int NOT NULL,[Reason] nvarchar(500) NULL,
          [AttendancePercent] decimal(6,2) NOT NULL,[ResultPercent] decimal(6,2) NOT NULL,[OutstandingBalance] decimal(18,2) NOT NULL,[IsEligible] bit NOT NULL,
          CONSTRAINT [FK_PromotionEntries_Batches] FOREIGN KEY([PromotionBatchId]) REFERENCES [PromotionBatches]([Id]),
          CONSTRAINT [FK_PromotionEntries_Students] FOREIGN KEY([StudentId]) REFERENCES [Students]([Id]));
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_PromotionEntries_Batch_Student') EXEC(N'CREATE UNIQUE INDEX [IX_PromotionEntries_Batch_Student] ON [PromotionEntries]([PromotionBatchId],[StudentId]);');

        IF OBJECT_ID('AdmissionInquiries') IS NULL CREATE TABLE [AdmissionInquiries]([Id] bigint IDENTITY PRIMARY KEY,[ProspectName] nvarchar(160) NOT NULL,[Email] nvarchar(256) NULL,[Phone] nvarchar(30) NULL,[AcademicProgramId] int NULL,[Source] nvarchar(80) NOT NULL,[Status] nvarchar(40) NOT NULL,[AssignedToUserId] nvarchar(450) NULL,[CreatedUtc] datetime2 NOT NULL,[ConvertedUtc] datetime2 NULL,[AdmissionApplicationId] bigint NULL);
        IF OBJECT_ID('AdmissionAppointments') IS NULL CREATE TABLE [AdmissionAppointments]([Id] bigint IDENTITY PRIMARY KEY,[AdmissionApplicationId] bigint NOT NULL,[AppointmentType] nvarchar(40) NOT NULL,[StartsUtc] datetime2 NOT NULL,[Location] nvarchar(160) NULL,[ReviewerUserId] nvarchar(450) NULL,[Status] nvarchar(40) NOT NULL,[Outcome] nvarchar(2000) NULL,CONSTRAINT [FK_AdmissionAppointments_Applications] FOREIGN KEY([AdmissionApplicationId]) REFERENCES [AdmissionApplications]([Id]));
        IF OBJECT_ID('GuardianLinkHistories') IS NULL CREATE TABLE [GuardianLinkHistories]([Id] bigint IDENTITY PRIMARY KEY,[GuardianStudentLinkId] int NOT NULL,[Action] nvarchar(40) NOT NULL,[ChangedByUserId] nvarchar(450) NOT NULL,[Comment] nvarchar(1000) NULL,[ChangedUtc] datetime2 NOT NULL,CONSTRAINT [FK_GuardianLinkHistory_Links] FOREIGN KEY([GuardianStudentLinkId]) REFERENCES [GuardianStudentLinks]([Id]));

        IF OBJECT_ID('FeeCategories') IS NULL CREATE TABLE [FeeCategories]([Id] int IDENTITY PRIMARY KEY,[Code] nvarchar(30) NOT NULL,[Name] nvarchar(120) NOT NULL,[IsRefundable] bit NOT NULL,[IsActive] bit NOT NULL);
        IF OBJECT_ID('StudentInvoices') IS NULL CREATE TABLE [StudentInvoices]([Id] bigint IDENTITY PRIMARY KEY,[InvoiceNumber] nvarchar(40) NOT NULL,[StudentId] int NOT NULL,[AcademicYearId] int NOT NULL,[IssuedUtc] datetime2 NOT NULL,[DueUtc] datetime2 NOT NULL,[Status] int NOT NULL,[TotalAmount] decimal(18,2) NOT NULL,[AidAmount] decimal(18,2) NOT NULL,[PaidAmount] decimal(18,2) NOT NULL,[RowVersion] rowversion NOT NULL,CONSTRAINT [FK_StudentInvoices_Students] FOREIGN KEY([StudentId]) REFERENCES [Students]([Id]),CONSTRAINT [FK_StudentInvoices_Years] FOREIGN KEY([AcademicYearId]) REFERENCES [AcademicYears]([Id]));
        IF OBJECT_ID('StudentInvoiceLines') IS NULL CREATE TABLE [StudentInvoiceLines]([Id] bigint IDENTITY PRIMARY KEY,[StudentInvoiceId] bigint NOT NULL,[FeeCategoryId] int NOT NULL,[Description] nvarchar(160) NOT NULL,[Amount] decimal(18,2) NOT NULL,[DiscountAmount] decimal(18,2) NOT NULL,[LateFeeAmount] decimal(18,2) NOT NULL,CONSTRAINT [FK_InvoiceLines_Invoices] FOREIGN KEY([StudentInvoiceId]) REFERENCES [StudentInvoices]([Id]),CONSTRAINT [FK_InvoiceLines_Categories] FOREIGN KEY([FeeCategoryId]) REFERENCES [FeeCategories]([Id]));
        IF OBJECT_ID('PaymentAllocations') IS NULL CREATE TABLE [PaymentAllocations]([Id] bigint IDENTITY PRIMARY KEY,[FinancialTransactionId] bigint NOT NULL,[StudentInvoiceLineId] bigint NOT NULL,[Amount] decimal(18,2) NOT NULL,CONSTRAINT [FK_PaymentAllocations_Transactions] FOREIGN KEY([FinancialTransactionId]) REFERENCES [FinancialTransactions]([Id]),CONSTRAINT [FK_PaymentAllocations_Lines] FOREIGN KEY([StudentInvoiceLineId]) REFERENCES [StudentInvoiceLines]([Id]));
        IF OBJECT_ID('StudentAidAwards') IS NULL CREATE TABLE [StudentAidAwards]([Id] bigint IDENTITY PRIMARY KEY,[StudentId] int NOT NULL,[AwardType] nvarchar(40) NOT NULL,[Name] nvarchar(160) NOT NULL,[Amount] decimal(18,2) NOT NULL,[Status] int NOT NULL,[StartsOn] datetime2 NOT NULL,[EndsOn] datetime2 NULL,CONSTRAINT [FK_StudentAidAwards_Students] FOREIGN KEY([StudentId]) REFERENCES [Students]([Id]));
        IF OBJECT_ID('FinancialPeriods') IS NULL CREATE TABLE [FinancialPeriods]([Id] int IDENTITY PRIMARY KEY,[Name] nvarchar(40) NOT NULL,[StartsOn] datetime2 NOT NULL,[EndsOn] datetime2 NOT NULL,[IsClosed] bit NOT NULL,[ClosedUtc] datetime2 NULL,[ClosedByUserId] nvarchar(450) NULL);
        IF OBJECT_ID('DailyCollectionCloses') IS NULL CREATE TABLE [DailyCollectionCloses]([Id] bigint IDENTITY PRIMARY KEY,[BusinessDate] datetime2 NOT NULL,[CashierUserId] nvarchar(450) NOT NULL,[ExpectedAmount] decimal(18,2) NOT NULL,[CountedAmount] decimal(18,2) NOT NULL,[Variance] decimal(18,2) NOT NULL,[Notes] nvarchar(1000) NULL,[ClosedUtc] datetime2 NOT NULL);

        IF OBJECT_ID('Rooms') IS NULL CREATE TABLE [Rooms]([Id] int IDENTITY PRIMARY KEY,[CampusId] int NULL,[Code] nvarchar(30) NOT NULL,[Name] nvarchar(120) NOT NULL,[Capacity] int NOT NULL,[IsActive] bit NOT NULL);
        IF OBJECT_ID('TimetableSubstitutions') IS NULL CREATE TABLE [TimetableSubstitutions]([Id] bigint IDENTITY PRIMARY KEY,[TimetableEntryId] int NOT NULL,[OccursOn] datetime2 NOT NULL,[SubstituteTeacherId] int NULL,[RoomId] int NULL,[Status] nvarchar(40) NOT NULL,[Reason] nvarchar(500) NULL,[CreatedByUserId] nvarchar(450) NOT NULL,[CreatedUtc] datetime2 NOT NULL,CONSTRAINT [FK_TimetableSubstitutions_Entries] FOREIGN KEY([TimetableEntryId]) REFERENCES [TimetableEntries]([Id]));

        IF OBJECT_ID('CommunicationMessages') IS NULL CREATE TABLE [CommunicationMessages]([Id] bigint IDENTITY PRIMARY KEY,[Title] nvarchar(160) NOT NULL,[Body] nvarchar(max) NOT NULL,[Priority] nvarchar(40) NOT NULL,[Status] int NOT NULL,[ScheduledUtc] datetime2 NULL,[ExpiresUtc] datetime2 NULL,[CreatedByUserId] nvarchar(450) NOT NULL,[CreatedUtc] datetime2 NOT NULL);
        IF OBJECT_ID('CommunicationAudiences') IS NULL CREATE TABLE [CommunicationAudiences]([Id] bigint IDENTITY PRIMARY KEY,[CommunicationMessageId] bigint NOT NULL,[AudienceType] nvarchar(40) NOT NULL,[AudienceValue] nvarchar(450) NOT NULL,CONSTRAINT [FK_CommunicationAudiences_Messages] FOREIGN KEY([CommunicationMessageId]) REFERENCES [CommunicationMessages]([Id]));
        IF OBJECT_ID('CommunicationDeliveries') IS NULL CREATE TABLE [CommunicationDeliveries]([Id] bigint IDENTITY PRIMARY KEY,[CommunicationMessageId] bigint NOT NULL,[UserId] nvarchar(450) NOT NULL,[Channel] int NOT NULL,[Status] int NOT NULL,[AttemptCount] int NOT NULL,[DeliveredUtc] datetime2 NULL,[ReadUtc] datetime2 NULL,[LastError] nvarchar(1000) NULL,CONSTRAINT [FK_CommunicationDeliveries_Messages] FOREIGN KEY([CommunicationMessageId]) REFERENCES [CommunicationMessages]([Id]),CONSTRAINT [FK_CommunicationDeliveries_Users] FOREIGN KEY([UserId]) REFERENCES [AspNetUsers]([Id]));
        IF OBJECT_ID('OutboxMessages') IS NULL CREATE TABLE [OutboxMessages]([Id] bigint IDENTITY PRIMARY KEY,[Type] nvarchar(100) NOT NULL,[PayloadJson] nvarchar(max) NOT NULL,[OccurredUtc] datetime2 NOT NULL,[ProcessedUtc] datetime2 NULL,[AttemptCount] int NOT NULL,[NextAttemptUtc] datetime2 NULL,[LastError] nvarchar(2000) NULL);

        IF OBJECT_ID('LibraryCategories') IS NULL CREATE TABLE [LibraryCategories]([Id] int IDENTITY PRIMARY KEY,[Name] nvarchar(120) NOT NULL,[IsActive] bit NOT NULL);
        IF OBJECT_ID('LibraryAuthors') IS NULL CREATE TABLE [LibraryAuthors]([Id] int IDENTITY PRIMARY KEY,[Name] nvarchar(160) NOT NULL);
        IF OBJECT_ID('LibraryPublishers') IS NULL CREATE TABLE [LibraryPublishers]([Id] int IDENTITY PRIMARY KEY,[Name] nvarchar(160) NOT NULL);
        IF OBJECT_ID('LibraryAcquisitions') IS NULL CREATE TABLE [LibraryAcquisitions]([Id] bigint IDENTITY PRIMARY KEY,[LibraryBookId] int NOT NULL,[Supplier] nvarchar(80) NOT NULL,[AcquiredOn] datetime2 NOT NULL,[Quantity] int NOT NULL,[TotalCost] decimal(18,2) NOT NULL,[Reference] nvarchar(100) NULL);
        IF OBJECT_ID('LibraryBorrowingPolicies') IS NULL CREATE TABLE [LibraryBorrowingPolicies]([Id] int IDENTITY PRIMARY KEY,[RoleName] nvarchar(40) NOT NULL,[MaximumLoans] int NOT NULL,[LoanDays] int NOT NULL,[MaximumRenewals] int NOT NULL,[DailyFine] decimal(18,2) NOT NULL);
        IF OBJECT_ID('LibraryStockVerifications') IS NULL CREATE TABLE [LibraryStockVerifications]([Id] bigint IDENTITY PRIMARY KEY,[StartedUtc] datetime2 NOT NULL,[CompletedUtc] datetime2 NULL,[StartedByUserId] nvarchar(450) NOT NULL,[ExpectedCopies] int NOT NULL,[LocatedCopies] int NOT NULL,[MissingCopies] int NOT NULL,[Notes] nvarchar(1000) NULL);

        IF OBJECT_ID('HostelBuildings') IS NULL CREATE TABLE [HostelBuildings]([Id] int IDENTITY PRIMARY KEY,[Name] nvarchar(120) NOT NULL,[Address] nvarchar(500) NULL,[IsActive] bit NOT NULL);
        IF OBJECT_ID('HostelBeds') IS NULL CREATE TABLE [HostelBeds]([Id] int IDENTITY PRIMARY KEY,[HostelRoomId] int NOT NULL,[BedNumber] nvarchar(30) NOT NULL,[IsActive] bit NOT NULL);
        IF OBJECT_ID('CampusServiceTickets') IS NULL CREATE TABLE [CampusServiceTickets]([Id] bigint IDENTITY PRIMARY KEY,[Module] nvarchar(40) NOT NULL,[Title] nvarchar(160) NOT NULL,[Description] nvarchar(2000) NOT NULL,[Priority] nvarchar(40) NOT NULL,[Status] nvarchar(40) NOT NULL,[ReportedByUserId] nvarchar(450) NOT NULL,[AssignedToUserId] nvarchar(450) NULL,[CreatedUtc] datetime2 NOT NULL,[ResolvedUtc] datetime2 NULL);
        IF OBJECT_ID('TransportVehicles') IS NULL CREATE TABLE [TransportVehicles]([Id] int IDENTITY PRIMARY KEY,[RegistrationNumber] nvarchar(30) NOT NULL,[Model] nvarchar(80) NOT NULL,[Capacity] int NOT NULL,[IsActive] bit NOT NULL);
        IF OBJECT_ID('TransportStops') IS NULL CREATE TABLE [TransportStops]([Id] int IDENTITY PRIMARY KEY,[TransportRouteId] int NOT NULL,[Name] nvarchar(120) NOT NULL,[Sequence] int NOT NULL,[ScheduledTime] time NULL);
        IF OBJECT_ID('PlacementInterviews') IS NULL CREATE TABLE [PlacementInterviews]([Id] bigint IDENTITY PRIMARY KEY,[PlacementApplicationId] bigint NOT NULL,[Stage] nvarchar(80) NOT NULL,[ScheduledUtc] datetime2 NOT NULL,[Location] nvarchar(160) NULL,[Status] nvarchar(40) NOT NULL,[Feedback] nvarchar(1000) NULL);
        IF OBJECT_ID('PlacementOffers') IS NULL CREATE TABLE [PlacementOffers]([Id] bigint IDENTITY PRIMARY KEY,[PlacementApplicationId] bigint NOT NULL,[AnnualCompensation] decimal(18,2) NOT NULL,[OfferedUtc] datetime2 NOT NULL,[Status] nvarchar(40) NOT NULL,[JoiningDate] datetime2 NULL);
        IF OBJECT_ID('EmployeeContracts') IS NULL CREATE TABLE [EmployeeContracts]([Id] bigint IDENTITY PRIMARY KEY,[EmployeeProfileId] int NOT NULL,[ContractType] nvarchar(80) NOT NULL,[StartsOn] datetime2 NOT NULL,[EndsOn] datetime2 NULL,[BaseSalary] decimal(18,2) NOT NULL,[DocumentStorageKey] nvarchar(500) NULL);
        IF OBJECT_ID('EmployeePayrolls') IS NULL CREATE TABLE [EmployeePayrolls]([Id] bigint IDENTITY PRIMARY KEY,[EmployeeProfileId] int NOT NULL,[Year] int NOT NULL,[Month] int NOT NULL,[GrossAmount] decimal(18,2) NOT NULL,[DeductionAmount] decimal(18,2) NOT NULL,[NetAmount] decimal(18,2) NOT NULL,[Status] nvarchar(40) NOT NULL,[PaidUtc] datetime2 NULL);
        IF OBJECT_ID('SavedReports') IS NULL CREATE TABLE [SavedReports]([Id] bigint IDENTITY PRIMARY KEY,[Name] nvarchar(160) NOT NULL,[ReportType] nvarchar(80) NOT NULL,[FilterJson] nvarchar(max) NOT NULL,[OwnerUserId] nvarchar(450) NOT NULL,[IsShared] bit NOT NULL,[CreatedUtc] datetime2 NOT NULL);
        IF OBJECT_ID('ReportSchedules') IS NULL CREATE TABLE [ReportSchedules]([Id] bigint IDENTITY PRIMARY KEY,[SavedReportId] bigint NOT NULL,[CronExpression] nvarchar(80) NOT NULL,[RecipientEmail] nvarchar(256) NOT NULL,[Format] nvarchar(20) NOT NULL,[IsActive] bit NOT NULL,[NextRunUtc] datetime2 NULL,[LastRunUtc] datetime2 NULL,CONSTRAINT [FK_ReportSchedules_SavedReports] FOREIGN KEY([SavedReportId]) REFERENCES [SavedReports]([Id]));

        /* Business indexes required by services. */
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_StudentInvoices_InvoiceNumber') EXEC(N'CREATE UNIQUE INDEX [IX_StudentInvoices_InvoiceNumber] ON [StudentInvoices]([InvoiceNumber]);');
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_CommunicationDeliveries_Message_User_Channel') EXEC(N'CREATE UNIQUE INDEX [IX_CommunicationDeliveries_Message_User_Channel] ON [CommunicationDeliveries]([CommunicationMessageId],[UserId],[Channel]);');
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_TimetableSubstitutions_Entry_Date') EXEC(N'CREATE UNIQUE INDEX [IX_TimetableSubstitutions_Entry_Date] ON [TimetableSubstitutions]([TimetableEntryId],[OccursOn]);');
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_SavedReports_Owner_Name') EXEC(N'CREATE UNIQUE INDEX [IX_SavedReports_Owner_Name] ON [SavedReports]([OwnerUserId],[Name]);');
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "This data-preserving reconciliation migration is intentionally non-destructive. Restore a verified backup for rollback.");
    }
}
