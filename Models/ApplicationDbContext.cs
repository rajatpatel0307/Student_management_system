using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Student_Management_System.Models
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Student> Students { get; set; }
        public DbSet<StaffMember> StaffMembers { get; set; }
        public DbSet<StaffWorkItem> StaffWorkItems { get; set; }
        public DbSet<TransportDriver> TransportDrivers { get; set; }
        public DbSet<StaffExpense> StaffExpenses { get; set; }
        public DbSet<StaffMessage> StaffMessages { get; set; }
        public DbSet<RegistrarCertificate> RegistrarCertificates { get; set; }
        public DbSet<StaffAttendanceRecord> StaffAttendanceRecords { get; set; }
        public DbSet<TransportTrip> TransportTrips { get; set; }
        public DbSet<VehicleFuelLog> VehicleFuelLogs { get; set; }
        public DbSet<Class> Classes { get; set; }
        public DbSet<Section> Sections { get; set; }
        public DbSet<Teacher> Teachers { get; set; }
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<Exam> Exams { get; set; }
        public DbSet<Mark> Marks { get; set; }

        public DbSet<Notice> Notices { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<AcademicYear> AcademicYears { get; set; }
        public DbSet<Fee> Fees { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<TimetableEntry> TimetableEntries { get; set; }
        public DbSet<Assignment> Assignments { get; set; }
        public DbSet<AssignmentSubmission> AssignmentSubmissions { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Semester> Semesters { get; set; }
        public DbSet<TeacherClassAssignment> TeacherClassAssignments { get; set; }
        public DbSet<TeacherSubjectAssignment> TeacherSubjectAssignments { get; set; }
        public DbSet<StudentClassEnrollment> StudentClassEnrollments { get; set; }
        public DbSet<StudentSubjectEnrollment> StudentSubjectEnrollments { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<LoginHistory> LoginHistories { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<StudyMaterial> StudyMaterials { get; set; }
        public DbSet<AdmissionApplication> AdmissionApplications { get; set; }
        public DbSet<AdmissionStageHistory> AdmissionStageHistories { get; set; }
        public DbSet<GuardianProfile> GuardianProfiles { get; set; }
        public DbSet<GuardianStudentLink> GuardianStudentLinks { get; set; }
        public DbSet<FinancialTransaction> FinancialTransactions { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }
        public DbSet<ApprovalRequest> ApprovalRequests { get; set; }
        public DbSet<ApprovalStep> ApprovalSteps { get; set; }
        public DbSet<LibraryBook> LibraryBooks { get; set; }
        public DbSet<LibraryBookCopy> LibraryBookCopies { get; set; }
        public DbSet<LibraryLoan> LibraryLoans { get; set; }
        public DbSet<AccountActivation> AccountActivations { get; set; }
        public DbSet<ReceiptSequence> ReceiptSequences { get; set; }
        public DbSet<AttendanceSession> AttendanceSessions { get; set; }
        public DbSet<AttendanceCorrection> AttendanceCorrections { get; set; }
        public DbSet<AcademicProgram> AcademicPrograms { get; set; }
        public DbSet<AcademicIntake> AcademicIntakes { get; set; }
        public DbSet<AdmissionDocument> AdmissionDocuments { get; set; }
        public DbSet<AssignmentRubricCriterion> AssignmentRubricCriteria { get; set; }
        public DbSet<AssignmentSubmissionVersion> AssignmentSubmissionVersions { get; set; }
        public DbSet<LibraryReservation> LibraryReservations { get; set; }
        public DbSet<LibraryFineSettlement> LibraryFineSettlements { get; set; }
        public DbSet<TransportRoute> TransportRoutes { get; set; }
        public DbSet<TransportEnrollment> TransportEnrollments { get; set; }
        public DbSet<HostelRoom> HostelRooms { get; set; }
        public DbSet<HostelAllocation> HostelAllocations { get; set; }
        public DbSet<PlacementDrive> PlacementDrives { get; set; }
        public DbSet<PlacementApplication> PlacementApplications { get; set; }
        public DbSet<EmployeeProfile> EmployeeProfiles { get; set; }
        public DbSet<GradeBoundary> GradeBoundaries { get; set; }
        public DbSet<ResultLifecycleHistory> ResultLifecycleHistories { get; set; }
        public DbSet<ResultCorrectionRequest> ResultCorrectionRequests { get; set; }
        public DbSet<Institution> Institutions { get; set; }
        public DbSet<Campus> Campuses { get; set; }
        public DbSet<UserCampusMembership> UserCampusMemberships { get; set; }
        public DbSet<ProgramType> ProgramTypes { get; set; }
        public DbSet<AcademicSession> AcademicSessions { get; set; }
        public DbSet<AcademicSubjectType> AcademicSubjectTypes { get; set; }
        public DbSet<Curriculum> Curricula { get; set; }
        public DbSet<CurriculumVersion> CurriculumVersions { get; set; }
        public DbSet<CurriculumSubject> CurriculumSubjects { get; set; }
        public DbSet<AcademicCalendarEvent> AcademicCalendarEvents { get; set; }
        public DbSet<PromotionBatch> PromotionBatches { get; set; }
        public DbSet<PromotionEntry> PromotionEntries { get; set; }
        public DbSet<AdmissionInquiry> AdmissionInquiries { get; set; }
        public DbSet<AdmissionAppointment> AdmissionAppointments { get; set; }
        public DbSet<GuardianLinkHistory> GuardianLinkHistories { get; set; }
        public DbSet<FeeCategory> FeeCategories { get; set; }
        public DbSet<StudentInvoice> StudentInvoices { get; set; }
        public DbSet<StudentInvoiceLine> StudentInvoiceLines { get; set; }
        public DbSet<PaymentAllocation> PaymentAllocations { get; set; }
        public DbSet<StudentAidAward> StudentAidAwards { get; set; }
        public DbSet<StudentAidAllocation> StudentAidAllocations { get; set; }
        public DbSet<FinancialPeriod> FinancialPeriods { get; set; }
        public DbSet<DailyCollectionClose> DailyCollectionCloses { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<TimetableSubstitution> TimetableSubstitutions { get; set; }
        public DbSet<CommunicationMessage> CommunicationMessages { get; set; }
        public DbSet<CommunicationAudience> CommunicationAudiences { get; set; }
        public DbSet<CommunicationDelivery> CommunicationDeliveries { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        public DbSet<LibraryCategory> LibraryCategories { get; set; }
        public DbSet<LibraryAuthor> LibraryAuthors { get; set; }
        public DbSet<LibraryPublisher> LibraryPublishers { get; set; }
        public DbSet<LibraryAcquisition> LibraryAcquisitions { get; set; }
        public DbSet<LibraryBorrowingPolicy> LibraryBorrowingPolicies { get; set; }
        public DbSet<LibraryStockVerification> LibraryStockVerifications { get; set; }
        public DbSet<HostelBuilding> HostelBuildings { get; set; }
        public DbSet<HostelBed> HostelBeds { get; set; }
        public DbSet<CampusServiceTicket> CampusServiceTickets { get; set; }
        public DbSet<TransportVehicle> TransportVehicles { get; set; }
        public DbSet<TransportStop> TransportStops { get; set; }
        public DbSet<PlacementInterview> PlacementInterviews { get; set; }
        public DbSet<PlacementOffer> PlacementOffers { get; set; }
        public DbSet<EmployeeContract> EmployeeContracts { get; set; }
        public DbSet<EmployeePayroll> EmployeePayrolls { get; set; }
        public DbSet<SavedReport> SavedReports { get; set; }
        public DbSet<ReportSchedule> ReportSchedules { get; set; }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            EnforceImmutableRecords();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            EnforceImmutableRecords();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void EnforceImmutableRecords()
        {
            var ledgerMutation = ChangeTracker.Entries<FinancialTransaction>()
                .FirstOrDefault(e => e.State is EntityState.Modified or EntityState.Deleted);
            if (ledgerMutation != null)
                throw new InvalidOperationException(
                    "Posted financial transactions are immutable. Create a reversal, refund, or adjustment entry instead.");

            var auditMutation = ChangeTracker.Entries<AuditLog>()
                .FirstOrDefault(e => e.State is EntityState.Modified or EntityState.Deleted);
            if (auditMutation != null)
                throw new InvalidOperationException(
                    "Finalized audit entries are immutable and cannot be updated or deleted.");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Identity updates during MFA and login must work with the existing
            // AspNetUsers triggers. SQL Server disallows bare OUTPUT with triggers.
            modelBuilder.Entity<ApplicationUser>()
                .ToTable("AspNetUsers", table => table.UseSqlOutputClause(false));
            modelBuilder.Entity<ApplicationUser>().Property(u => u.NormalizedMobileNumber)
                .HasComputedColumnSql("[dbo].[NormalizeUserMobile]([PhoneNumber])", stored: true);
            modelBuilder.Entity<EmployeeProfile>()
                .ToTable("EmployeeProfiles", table => table.UseSqlOutputClause(false));
            modelBuilder.Entity<RegistrarCertificate>().HasIndex(c => c.CertificateNumber).IsUnique().HasFilter("[CertificateNumber] IS NOT NULL");
            modelBuilder.Entity<RegistrarCertificate>().HasIndex(c => new { c.CampusId, c.Status, c.CertificateType });
            modelBuilder.Entity<RegistrarCertificate>().HasOne(c => c.Student).WithMany().HasForeignKey(c => c.StudentId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<RegistrarCertificate>().HasOne(c => c.Campus).WithMany().HasForeignKey(c => c.CampusId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<StaffAttendanceRecord>().HasIndex(a => new { a.Workspace, a.StudentId, a.AttendanceDate }).IsUnique();
            modelBuilder.Entity<StaffAttendanceRecord>().HasOne(a => a.Student).WithMany().HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<StaffAttendanceRecord>().HasOne(a => a.Campus).WithMany().HasForeignKey(a => a.CampusId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TransportTrip>().HasOne(t => t.TransportVehicle).WithMany().HasForeignKey(t => t.TransportVehicleId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TransportTrip>().HasOne(t => t.TransportDriver).WithMany().HasForeignKey(t => t.TransportDriverId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TransportTrip>().HasOne(t => t.TransportRoute).WithMany().HasForeignKey(t => t.TransportRouteId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TransportTrip>().HasIndex(t => new { t.ScheduledOn, t.Status });
            modelBuilder.Entity<TransportTrip>().HasIndex(t => new { t.TransportVehicleId, t.ScheduledOn, t.DepartureTime }).IsUnique().HasFilter("[Status] <> 'Cancelled'");
            modelBuilder.Entity<TransportTrip>().HasIndex(t => new { t.TransportDriverId, t.ScheduledOn, t.DepartureTime }).IsUnique().HasFilter("[Status] <> 'Cancelled'");
            modelBuilder.Entity<VehicleFuelLog>().HasIndex(f => new { f.TransportVehicleId, f.FilledOn });
            modelBuilder.Entity<VehicleFuelLog>().HasOne(f => f.TransportVehicle).WithMany().HasForeignKey(f => f.TransportVehicleId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TransportDriver>().HasIndex(d => d.LicenseNumber).IsUnique();
            modelBuilder.Entity<TransportVehicle>().HasOne(v => v.TransportDriver).WithMany().HasForeignKey(v => v.TransportDriverId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TransportVehicle>().HasOne(v => v.TransportRoute).WithMany().HasForeignKey(v => v.TransportRouteId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<StaffExpense>().HasIndex(e => new { e.Status, e.ExpenseDate });
            modelBuilder.Entity<StaffMessage>().HasOne(m => m.SenderUser).WithMany().HasForeignKey(m => m.SenderUserId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<StaffMessage>().HasOne(m => m.RecipientUser).WithMany().HasForeignKey(m => m.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<StaffMessage>().HasIndex(m => new { m.RecipientUserId, m.ReadUtc, m.CreatedUtc });
            modelBuilder.Entity<StaffMember>().HasIndex(s => s.EmployeeProfileId).IsUnique();
            modelBuilder.Entity<StaffMember>().HasIndex(s => new { s.PrimaryRole, s.IsDeleted });
            modelBuilder.Entity<StaffMember>().HasOne(s => s.EmployeeProfile).WithOne()
                .HasForeignKey<StaffMember>(s => s.EmployeeProfileId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<StaffWorkItem>().HasIndex(w => new { w.Workspace, w.Category, w.Status });
            modelBuilder.Entity<StaffWorkItem>().HasOne(w => w.Student).WithMany()
                .HasForeignKey(w => w.StudentId).OnDelete(DeleteBehavior.Restrict);

            // 🔥 Section → Class (FIX)
            modelBuilder.Entity<Section>()
                .HasOne(s => s.Class)
                .WithMany(c => c.Sections)
                .HasForeignKey(s => s.ClassId)
                .OnDelete(DeleteBehavior.NoAction);

            // 🔥 Mark fixes (keep these)
            modelBuilder.Entity<Mark>()
                .HasOne(m => m.Student)
                .WithMany(s => s.Marks)
                .HasForeignKey(m => m.StudentId)
                .OnDelete(DeleteBehavior.NoAction);

            // SQL Server must never infer default decimal precision for marks,
            // percentages, corrections, or assessment limits.
            modelBuilder.Entity<Exam>().Property(e => e.MaximumMarks)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Exam>().Property(e => e.PassMarks)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Exam>().Property(e => e.WeightPercent)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Mark>().Property(m => m.Score)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Mark>().Property(m => m.GraceMarks)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Assignment>().Property(a => a.MaximumScore)
                .HasPrecision(18, 2);
            modelBuilder.Entity<AssignmentRubricCriterion>()
                .Property(r => r.MaximumScore).HasPrecision(18, 2);
            modelBuilder.Entity<GradeBoundary>().Property(g => g.MinimumPercent)
                .HasPrecision(18, 2);
            modelBuilder.Entity<GradeBoundary>().Property(g => g.MaximumPercent)
                .HasPrecision(18, 2);
            modelBuilder.Entity<ResultCorrectionRequest>()
                .Property(r => r.PreviousScore).HasPrecision(18, 2);
            modelBuilder.Entity<ResultCorrectionRequest>()
                .Property(r => r.RequestedScore).HasPrecision(18, 2);

            modelBuilder.Entity<Attendance>()
                .HasIndex(a => new { a.StudentId, a.Date, a.SubjectId, a.Lecture })
                .IsUnique();
            modelBuilder.Entity<Attendance>().HasIndex(a => new { a.Date, a.Status, a.StudentId });
            modelBuilder.Entity<Mark>()
                .HasIndex(m => new { m.StudentId, m.SubjectId, m.ExamId })
                .IsUnique();
            modelBuilder.Entity<Section>()
                .HasIndex(s => new { s.ClassId, s.Name })
                .IsUnique();
            modelBuilder.Entity<Subject>()
                .HasIndex(s => new { s.ClassId, s.Name })
                .IsUnique();
            modelBuilder.Entity<AssignmentSubmission>()
                .HasIndex(s => new { s.AssignmentId, s.StudentId }).IsUnique();
            modelBuilder.Entity<TimetableEntry>()
                .HasIndex(t => new { t.ClassId, t.DayOfWeek, t.StartsAt }).IsUnique();
            modelBuilder.Entity<AcademicYear>()
                .HasIndex(a => a.Name).IsUnique();
            modelBuilder.Entity<Department>().HasIndex(d => d.Code).IsUnique();
            modelBuilder.Entity<Semester>()
                .HasIndex(s => new { s.DepartmentId, s.AcademicYearId, s.Number }).IsUnique();
            modelBuilder.Entity<TeacherClassAssignment>()
                .HasIndex(a => new { a.TeacherId, a.ClassId }).IsUnique();
            modelBuilder.Entity<TeacherSubjectAssignment>()
                .HasIndex(a => new { a.TeacherId, a.SubjectId }).IsUnique();
            modelBuilder.Entity<StudentClassEnrollment>()
                .HasIndex(e => new { e.StudentId, e.AcademicYearId }).IsUnique();
            modelBuilder.Entity<StudentSubjectEnrollment>()
                .HasIndex(e => new { e.StudentId, e.SubjectId }).IsUnique();
            modelBuilder.Entity<Permission>().HasIndex(p => p.Key).IsUnique();
            modelBuilder.Entity<UserPermission>()
                .HasIndex(p => new { p.UserId, p.PermissionId }).IsUnique();
            modelBuilder.Entity<RolePermission>()
                .HasIndex(p => new { p.RoleName, p.PermissionId }).IsUnique();
            modelBuilder.Entity<SystemSetting>().HasIndex(s => s.Key).IsUnique();
            modelBuilder.Entity<AdmissionApplication>().HasIndex(a => a.ReferenceNumber).IsUnique();
            modelBuilder.Entity<AdmissionApplication>().HasIndex(a => new { a.Stage, a.AcademicYearId });
            modelBuilder.Entity<AdmissionApplication>().HasIndex(a => new { a.CampusId, a.CreatedUtc, a.Stage });
            modelBuilder.Entity<GuardianProfile>().HasIndex(g => g.UserId).IsUnique();
            modelBuilder.Entity<GuardianStudentLink>()
                .HasIndex(g => new { g.GuardianProfileId, g.StudentId }).IsUnique();
            modelBuilder.Entity<FinancialTransaction>().HasIndex(t => t.ReceiptNumber).IsUnique();
            modelBuilder.Entity<FinancialTransaction>().HasIndex(t => t.IdempotencyKey)
                .IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
            modelBuilder.Entity<FinancialTransaction>().HasIndex(t => new { t.StudentId, t.FeeId, t.CreatedUtc });
            modelBuilder.Entity<FinancialTransaction>().HasIndex(t => new { t.CreatedUtc, t.Status, t.Type });
            modelBuilder.Entity<LeaveRequest>().HasIndex(l => new { l.RequesterUserId, l.Status });
            modelBuilder.Entity<UserNotification>().HasIndex(n => new { n.UserId, n.IsRead, n.CreatedUtc });
            modelBuilder.Entity<ApprovalRequest>().HasIndex(a => new { a.WorkflowType, a.Status });
            modelBuilder.Entity<ApprovalStep>().HasIndex(s => new { s.ApprovalRequestId, s.Sequence }).IsUnique();
            modelBuilder.Entity<LibraryBook>().HasIndex(b => b.Isbn).IsUnique().HasFilter("[Isbn] IS NOT NULL");
            modelBuilder.Entity<LibraryBookCopy>().HasIndex(c => c.Barcode).IsUnique();
            modelBuilder.Entity<LibraryLoan>().HasIndex(l => new { l.LibraryBookCopyId, l.ReturnedUtc });
            modelBuilder.Entity<LibraryLoan>().HasIndex(l => new { l.IssuedUtc, l.ReturnedUtc, l.DueUtc });
            modelBuilder.Entity<Assignment>().HasIndex(a => new { a.Status, a.DueUtc, a.SubjectId });
            modelBuilder.Entity<LoginHistory>().HasIndex(l => new { l.CreatedUtc, l.Succeeded });
            modelBuilder.Entity<AccountActivation>().HasIndex(a => a.TokenHash).IsUnique();
            modelBuilder.Entity<AccountActivation>().HasIndex(a => new { a.UserId, a.UsedUtc });
            modelBuilder.Entity<ReceiptSequence>().HasIndex(s => s.Period).IsUnique();
            modelBuilder.Entity<AttendanceSession>()
                .HasIndex(s => new { s.ClassId, s.SubjectId, s.SessionDate, s.Period }).IsUnique();
            modelBuilder.Entity<AttendanceCorrection>()
                .HasIndex(c => new { c.AttendanceId, c.Status });
            modelBuilder.Entity<AcademicProgram>().HasIndex(p => p.Code).IsUnique();
            modelBuilder.Entity<AcademicIntake>()
                .HasIndex(i => new { i.AcademicProgramId, i.AcademicYearId }).IsUnique();
            modelBuilder.Entity<AdmissionDocument>()
                .HasIndex(d => new { d.AdmissionApplicationId, d.DocumentType });
            modelBuilder.Entity<AssignmentRubricCriterion>()
                .HasIndex(r => new { r.AssignmentId, r.Name }).IsUnique();
            modelBuilder.Entity<AssignmentSubmissionVersion>()
                .HasIndex(v => new { v.AssignmentSubmissionId, v.VersionNumber }).IsUnique();
            modelBuilder.Entity<LibraryReservation>()
                .HasIndex(r => new { r.LibraryBookId, r.UserId, r.FulfilledUtc, r.CancelledUtc });
            modelBuilder.Entity<LibraryFineSettlement>().HasIndex(s => s.LibraryLoanId);
            modelBuilder.Entity<TransportRoute>().HasIndex(r => r.Code).IsUnique();
            modelBuilder.Entity<TransportEnrollment>()
                .HasIndex(e => new { e.StudentId, e.EndsOn });
            modelBuilder.Entity<HostelRoom>()
                .HasIndex(r => new { r.HostelName, r.RoomNumber }).IsUnique();
            modelBuilder.Entity<HostelAllocation>()
                .HasIndex(a => new { a.StudentId, a.EndsOn });
            modelBuilder.Entity<PlacementApplication>()
                .HasIndex(a => new { a.PlacementDriveId, a.StudentId }).IsUnique();
            modelBuilder.Entity<EmployeeProfile>().HasIndex(e => e.UserId).IsUnique();
            modelBuilder.Entity<EmployeeProfile>().HasIndex(e => e.EmployeeNumber).IsUnique();
            modelBuilder.Entity<GradeBoundary>()
                .HasIndex(g => new { g.DepartmentId, g.Grade }).IsUnique();
            modelBuilder.Entity<ResultLifecycleHistory>()
                .HasIndex(h => new { h.ExamId, h.ChangedUtc });
            modelBuilder.Entity<ResultCorrectionRequest>()
                .HasIndex(r => new { r.MarkId, r.Status });
            modelBuilder.Entity<Institution>().HasIndex(i => i.Code).IsUnique();
            modelBuilder.Entity<Campus>().HasIndex(c => new { c.InstitutionId, c.Code }).IsUnique();
            modelBuilder.Entity<ProgramType>().HasIndex(p => p.Name).IsUnique();
            modelBuilder.Entity<AcademicSession>().HasIndex(s => s.Name).IsUnique();
            modelBuilder.Entity<AcademicSubjectType>().HasIndex(t => t.Name).IsUnique();
            modelBuilder.Entity<Curriculum>().HasIndex(c => new { c.AcademicProgramId, c.Name }).IsUnique();
            modelBuilder.Entity<CurriculumVersion>().HasIndex(v => new { v.CurriculumId, v.Version }).IsUnique();
            modelBuilder.Entity<CurriculumSubject>().HasIndex(s => new { s.CurriculumVersionId, s.SubjectId }).IsUnique();
            modelBuilder.Entity<AcademicCalendarEvent>().HasIndex(e => new { e.AcademicSessionId, e.StartsUtc });
            modelBuilder.Entity<PromotionBatch>().HasIndex(b => b.BatchNumber).IsUnique();
            modelBuilder.Entity<PromotionEntry>().HasIndex(e => new { e.PromotionBatchId, e.StudentId }).IsUnique();
            modelBuilder.Entity<AdmissionInquiry>().HasIndex(i => new { i.Status, i.CreatedUtc });
            modelBuilder.Entity<AdmissionAppointment>().HasIndex(a => new { a.AdmissionApplicationId, a.StartsUtc });
            modelBuilder.Entity<GuardianLinkHistory>().HasIndex(h => new { h.GuardianStudentLinkId, h.ChangedUtc });
            modelBuilder.Entity<FeeCategory>().HasIndex(c => c.Code).IsUnique();
            modelBuilder.Entity<StudentInvoice>().HasIndex(i => i.InvoiceNumber).IsUnique();
            modelBuilder.Entity<StudentInvoice>().HasIndex(i => new { i.StudentId, i.Status, i.DueUtc });
            modelBuilder.Entity<PaymentAllocation>().HasIndex(a => new { a.FinancialTransactionId, a.StudentInvoiceLineId }).IsUnique();
            modelBuilder.Entity<StudentAidAward>().HasIndex(a => new { a.StudentId, a.Status });
            modelBuilder.Entity<StudentAidAllocation>()
                .HasIndex(a => new { a.StudentAidAwardId, a.StudentInvoiceId })
                .IsUnique()
                .HasFilter("[ReversedUtc] IS NULL");
            modelBuilder.Entity<FinancialPeriod>().HasIndex(p => p.Name).IsUnique();
            modelBuilder.Entity<DailyCollectionClose>().HasIndex(c => new { c.BusinessDate, c.CashierUserId }).IsUnique();
            modelBuilder.Entity<Room>().HasIndex(r => new { r.CampusId, r.Code }).IsUnique();
            modelBuilder.Entity<UserCampusMembership>()
                .HasIndex(m => new { m.UserId, m.CampusId }).IsUnique();
            modelBuilder.Entity<UserCampusMembership>()
                .HasIndex(m => new { m.UserId, m.IsDefault })
                .IsUnique()
                .HasFilter("[IsDefault] = 1 AND [IsActive] = 1");
            modelBuilder.Entity<AuditLog>().HasIndex(a => new { a.EntityName, a.EntityId, a.CreatedUtc });
            modelBuilder.Entity<AuditLog>().HasIndex(a => a.CorrelationId);
            modelBuilder.Entity<TimetableSubstitution>().HasIndex(s => new { s.TimetableEntryId, s.OccursOn }).IsUnique();
            modelBuilder.Entity<CommunicationMessage>().HasIndex(m => new { m.Status, m.ScheduledUtc });
            modelBuilder.Entity<CommunicationAudience>().HasIndex(a => new { a.CommunicationMessageId, a.AudienceType, a.AudienceValue }).IsUnique();
            modelBuilder.Entity<CommunicationDelivery>().HasIndex(d => new { d.CommunicationMessageId, d.UserId, d.Channel }).IsUnique();
            modelBuilder.Entity<CommunicationDelivery>().HasIndex(d => new { d.Status, d.Id });
            modelBuilder.Entity<CommunicationDelivery>().HasIndex(d => new { d.Status, d.NextAttemptUtc, d.ClaimExpiresUtc });
            modelBuilder.Entity<OutboxMessage>().HasIndex(o => new { o.ProcessedUtc, o.NextAttemptUtc });
            modelBuilder.Entity<LibraryCategory>().HasIndex(c => c.Name).IsUnique();
            modelBuilder.Entity<LibraryAuthor>().HasIndex(a => a.Name).IsUnique();
            modelBuilder.Entity<LibraryPublisher>().HasIndex(p => p.Name).IsUnique();
            modelBuilder.Entity<LibraryBorrowingPolicy>().HasIndex(p => p.RoleName).IsUnique();
            modelBuilder.Entity<HostelBuilding>().HasIndex(b => b.Name).IsUnique();
            modelBuilder.Entity<HostelBed>().HasIndex(b => new { b.HostelRoomId, b.BedNumber }).IsUnique();
            modelBuilder.Entity<TransportVehicle>().HasIndex(v => v.RegistrationNumber).IsUnique();
            modelBuilder.Entity<TransportStop>().HasIndex(s => new { s.TransportRouteId, s.Sequence }).IsUnique();
            modelBuilder.Entity<PlacementOffer>().HasIndex(o => o.PlacementApplicationId).IsUnique();
            modelBuilder.Entity<EmployeePayroll>().HasIndex(p => new { p.EmployeeProfileId, p.Year, p.Month }).IsUnique();
            modelBuilder.Entity<SavedReport>().HasIndex(r => new { r.OwnerUserId, r.Name }).IsUnique();

            modelBuilder.Entity<FinancialTransaction>()
                .HasOne(t => t.ReversesTransaction).WithMany()
                .HasForeignKey(t => t.ReversesTransactionId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<AdmissionApplication>()
                .HasOne(a => a.EnrolledStudent).WithMany()
                .HasForeignKey(a => a.EnrolledStudentId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<LibraryLoan>()
                .HasOne(l => l.BorrowerUser).WithMany()
                .HasForeignKey(l => l.BorrowerUserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Attendance>()
                .HasOne(a => a.AttendanceSession).WithMany(s => s.Records)
                .HasForeignKey(a => a.AttendanceSessionId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<AttendanceCorrection>()
                .HasOne(c => c.Attendance).WithMany()
                .HasForeignKey(c => c.AttendanceId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<PromotionBatch>()
                .HasOne(b => b.SourceClass).WithMany()
                .HasForeignKey(b => b.SourceClassId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<PromotionBatch>()
                .HasOne(b => b.DestinationClass).WithMany()
                .HasForeignKey(b => b.DestinationClassId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<PromotionBatch>()
                .HasOne(b => b.SourceSection).WithMany()
                .HasForeignKey(b => b.SourceSectionId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<PromotionBatch>()
                .HasOne(b => b.DestinationSection).WithMany()
                .HasForeignKey(b => b.DestinationSectionId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<PromotionEntry>()
                .HasOne(e => e.Student).WithMany()
                .HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<PaymentAllocation>()
                .HasOne(a => a.FinancialTransaction).WithMany()
                .HasForeignKey(a => a.FinancialTransactionId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<StudentAidAllocation>()
                .HasOne(a => a.StudentAidAward).WithMany(a => a.Allocations)
                .HasForeignKey(a => a.StudentAidAwardId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<StudentAidAllocation>()
                .HasOne(a => a.StudentInvoice).WithMany(i => i.AidAllocations)
                .HasForeignKey(a => a.StudentInvoiceId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<StudentInvoiceLine>()
                .HasOne(l => l.Fee).WithMany()
                .HasForeignKey(l => l.FeeId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<StudentInvoiceLine>()
                .HasOne(l => l.StudentClassEnrollment).WithMany()
                .HasForeignKey(l => l.StudentClassEnrollmentId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<LibraryLoan>()
                .HasIndex(l => l.LibraryBookCopyId).IsUnique()
                .HasFilter("[ReturnedUtc] IS NULL");
            modelBuilder.Entity<TimetableEntry>()
                .HasOne(t => t.RoomEntity).WithMany()
                .HasForeignKey(t => t.RoomId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<TimetableSubstitution>()
                .HasOne(s => s.SubstituteTeacher).WithMany()
                .HasForeignKey(s => s.SubstituteTeacherId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<AdmissionInquiry>()
                .HasOne(i => i.AdmissionApplication).WithMany()
                .HasForeignKey(i => i.AdmissionApplicationId).OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Department>()
                .HasOne(d => d.HodTeacher).WithMany()
                .HasForeignKey(d => d.HodTeacherId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Teacher>()
                .HasOne(t => t.Department).WithMany(d => d.Teachers)
                .HasForeignKey(t => t.DepartmentId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Student>()
                .HasOne(s => s.Department).WithMany(d => d.Students)
                .HasForeignKey(s => s.DepartmentId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Class>()
                .HasOne(c => c.Department).WithMany(d => d.Classes)
                .HasForeignKey(c => c.DepartmentId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Semester>()
                .HasOne(s => s.Department).WithMany(d => d.Semesters)
                .HasForeignKey(s => s.DepartmentId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Subject>()
                .HasOne(s => s.Department).WithMany()
                .HasForeignKey(s => s.DepartmentId).OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Mark>()
                .HasOne(m => m.Subject)
                .WithMany(s => s.Marks)
                .HasForeignKey(m => m.SubjectId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Mark>()
                .HasOne(m => m.Exam)
                .WithMany(e => e.Marks)
                .HasForeignKey(m => m.ExamId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
