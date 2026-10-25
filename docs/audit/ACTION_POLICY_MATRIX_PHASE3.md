# Production-default action policy matrix

Evaluates the application's shared production policies, default grants, permission attributes and override filter for every discovered MVC action and all configured roles. PASS means the request may reach its record/service guards; it does not certify record ownership, staff isolation, MFA enrollment or successful CRUD. Public actions still require their configured CSRF/rate-limit checks.

| Action and verb | SuperAdmin | Admin | HOD | Professor | Student | Guardian | Accountant | Librarian | AdmissionOfficer | ExaminationOfficer | TransportManager | HostelManager | Registrar | PlacementOfficer | Employee |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| POST AcademicStructure.AssignCampus (userId, campusId, isDefault, cancellationToken) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST AcademicStructure.CreateCurriculum (input, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST AcademicStructure.CreateEvent (input, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST AcademicStructure.CreateLookup (lookupType, name, option, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST AcademicStructure.CreateRoom (campusId, code, name, capacity, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET AcademicStructure.Index (cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST AcademicStructure.SaveCampus (input, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST AcademicStructure.SaveInstitution (input, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST AcademicStructure.SaveSession (input, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET AcademicYear.Create () | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST AcademicYear.Create (model) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET AcademicYear.Details (id, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET AcademicYear.Edit (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST AcademicYear.Edit (id, input) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET AcademicYear.Index (request, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Account.AccessDenied () | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public |
| GET Account.Activate (email, token) | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public |
| POST Account.Activate (email, token, newPassword, confirmPassword, cancellationToken) | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public |
| GET Account.ChangePassword () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Account.ChangePassword (currentPassword, newPassword) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Account.EnableMfa (code) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Account.ForgotPassword () | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public |
| POST Account.ForgotPassword (email) | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public |
| GET Account.Login (returnUrl) | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public |
| POST Account.Login (input) | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public |
| POST Account.Logout () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Account.Mfa () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Account.RecoveryCode (returnUrl) | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public |
| POST Account.RecoveryCode (code, returnUrl) | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public |
| POST Account.ResendActivation (email, cancellationToken) | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public |
| GET Account.ResetPassword (email, token) | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public |
| POST Account.ResetPassword (email, token, newPassword, confirmPassword) | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public |
| GET Account.VerifyTwoFactor (returnUrl) | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public |
| POST Account.VerifyTwoFactor (code, returnUrl) | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public |
| GET Admin.Index () | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Admissions.Create () | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | PASS | DENY | DENY |
| POST Admissions.Create (input) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | PASS | DENY | DENY |
| POST Admissions.CreateInquiry (prospectName, email, phone, academicProgramId, source, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | PASS | DENY | DENY |
| GET Admissions.Details (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | PASS | DENY | DENY |
| GET Admissions.DownloadDocument (id, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | PASS | DENY | DENY |
| GET Admissions.Edit (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | PASS | DENY | DENY |
| POST Admissions.Edit (id, input) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | PASS | DENY | DENY |
| POST Admissions.Enroll (id, sectionId) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | PASS | DENY | DENY |
| GET Admissions.Index (search, stage, academicYearId, page, pageSize, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | PASS | DENY | DENY |
| POST Admissions.ResendActivation (id, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | PASS | DENY | DENY |
| POST Admissions.ScheduleAppointment (id, appointmentType, startsUtc, location, reviewerUserId, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | PASS | DENY | DENY |
| POST Admissions.Transition (id, comment) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | PASS | DENY | DENY |
| POST Admissions.TransitionTo (id, stage, reason) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | PASS | DENY | DENY |
| POST Admissions.UploadDocument (id, documentType, file, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | PASS | DENY | DENY |
| POST Admissions.VerifyDocument (id, verified, comment, rowVersion, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | PASS | DENY | DENY |
| POST Approvals.Act (id, decision, comment) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Approvals.Cancel (id) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Approvals.Create () | PASS | PASS | PASS | PASS | DENY | DENY | PASS | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY |
| POST Approvals.Create (input) | PASS | PASS | PASS | PASS | DENY | DENY | PASS | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY |
| GET Approvals.Index (status) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Archive.Index (cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Archive.Set (entity, id, archived, reason) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Assignment.AddRubric (assignmentId, name, maximumScore, description) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Assignment.Create () | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Assignment.Create (input) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Assignment.Details (id, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Assignment.DownloadAttachment (id, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Assignment.Edit (id) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Assignment.Edit (id, input) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Assignment.Grade (submissionId, score, feedback) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Assignment.Index (request, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Assignment.Submit (assignmentId, answer, attachment, cancellationToken) | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Attendance.ApproveSession (id, rowVersion, cancellationToken) | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Attendance.Details (id) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Attendance.Edit (id) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Attendance.Edit (id, status, lecture, expectedUpdatedUtc, checkRevision) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Attendance.Export (request, cancellationToken) | PASS | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Attendance.History (classId) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Attendance.Index (request, cancellationToken) | PASS | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Attendance.Mark (classId) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Attendance.Mark (model, subjectId, lecture) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Attendance.MonthlyReport (classId, month, year) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Attendance.RequestCorrection (id, requestedStatus, reason) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Attendance.ReviewCorrection (id, approve, comment) | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Attendance.SelectClass () | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Attendance.Sessions (cancellationToken, page, pageSize) | PASS | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Attendance.StudentStats (classId) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Attendance.Update (id, status) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Classes.Create () | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Classes.Create (cls) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Classes.Delete (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Classes.Delete (id, reason) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Classes.Details (id) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Classes.Edit (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Classes.Edit (id, input) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Classes.Export (cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Classes.Index (request, teacherId, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Communications.Cancel (id, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Communications.Create (cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Communications.Create (input, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Communications.Details (id, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Communications.Index (request, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Dashboard.Index (filter, cancellationToken) | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Departments.Create () | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Departments.Create (department) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Departments.Details (id, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Departments.Edit (id, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Departments.Edit (id, input, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Departments.Export (request, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Departments.Index (request, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Exam.Create () | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY |
| POST Exam.Create (exam) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY |
| POST Exam.DeactivateGradeBoundary (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY |
| GET Exam.Details (id, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Exam.Edit (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY |
| POST Exam.Edit (id, input) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY |
| GET Exam.GradeScale (departmentId) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY |
| GET Exam.Index (request, cancellationToken) | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY |
| POST Exam.Publish (id) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY |
| POST Exam.SaveGradeBoundary (input) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY |
| POST Exam.SetLocked (id, locked) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY |
| GET Fee.Create () | PASS | PASS | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Fee.Create (fee) | PASS | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Fee.Details (id) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Fee.Edit (id) | PASS | PASS | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Fee.Edit (id, input) | PASS | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Fee.Index (search, classId, status) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Fee.RecordPayment (payment) | PASS | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Finance.Adjust (studentId, feeId, amount, reason) | PASS | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Finance.Collect (studentId) | PASS | PASS | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Finance.Collect (input) | PASS | PASS | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Finance.Details (id) | PASS | PASS | DENY | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Finance.Export (request, type, paymentMethod) | PASS | PASS | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Finance.Index (request, type, paymentMethod) | PASS | PASS | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Finance.Invoices (request, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Finance.IssueInvoice (studentId, academicYearId, feeIds, cancellationToken) | PASS | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Finance.Ledger (studentId, request, type) | PASS | PASS | DENY | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Finance.Refund (id, amount, reason, idempotencyKey) | PASS | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Finance.Reverse (id, reason) | PASS | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET GuardianDashboard.Index (cancellationToken) | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET GuardianDashboard.Student (id, cancellationToken) | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Guardians.Create (fullName, email, relationship, phone, studentId, canViewFinance) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Guardians.Index (search, page, pageSize, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Guardians.Link (guardianId, studentId, relationship, isPrimary, canViewFinance, canViewAcademics, canReceiveEmergencyContact, communicationPreferences) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Guardians.Unlink (id, reason) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET HodDashboard.Index (filter, cancellationToken) | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST HodManagement.AssignProfessor (teacherId, classId, subjectId, departmentId) | PASS | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST HodManagement.AssignStudent (studentId, classId, sectionId, subjectId, departmentId) | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET HodManagement.EditProfessor (id, cancellationToken) | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST HodManagement.EditProfessor (id, input, cancellationToken) | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET HodManagement.EditStudent (id, cancellationToken) | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST HodManagement.EditStudent (id, input, cancellationToken) | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET HodManagement.Index (departmentId) | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET HodManagement.ProfessorImage (id, cancellationToken) | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET HodManagement.Professors (request, cancellationToken) | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Home.AccessDenied () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Home.Dashboard () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Home.Error () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Home.Index () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Home.Privacy () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Home.StatusCodePage (code) | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public | Public |
| GET Home.Workspace () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Leave.Cancel (id, reason) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Leave.Create () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Leave.Create (input) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Leave.Index (status) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Leave.Review (id, decision, comment) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Library.ArchiveBook (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Library.Create () | PASS | PASS | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Library.Create (input, copyCount) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Library.CreateCategory (name) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Library.Details (id) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Library.Index (search, availableOnly) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Library.Issue (copyId, borrowerEmail, dueDate) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Library.MyLoans () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Library.Renew (loanId) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Library.RenewMyLoan (loanId) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Library.Reserve (bookId) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Library.Return (loanId) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Library.SetCopyCondition (copyId, condition) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Library.SettleFine (loanId, amount, method, reference) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Library.StartStockVerification (name) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Lookup.Classes (departmentId, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Lookup.Sections (classId, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Lookup.Semesters (departmentId, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Lookup.Subjects (classId, semesterId, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Mark.Approve (id) | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY |
| GET Mark.Create (subjectId, examId) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Mark.Create (input) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Mark.Details (id) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Mark.Edit (id) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Mark.Edit (id, score, remarks, expectedUpdatedUtc) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Mark.Index (request, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY |
| POST Mark.RequestCorrection (id, score, reason) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Mark.ReviewCorrection (id, approve) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY |
| POST Mark.SaveBulk (model) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Notice.Create () | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Notice.Create (notice) | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Notice.Details (id, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Notice.Edit (id) | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Notice.Edit (id, input) | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Notice.Index (request, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Notifications.Index (unreadOnly) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Notifications.Read (id) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Notifications.ReadAll () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Notifications.Send () | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Notifications.Send (role, title, message, actionUrl) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Operations.AllocateHostel (roomId, studentId, startsOn) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY |
| POST Operations.ApplyForPlacement (driveId) | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Operations.CreateEmployee (fullName, email, employeeNumber, jobTitle, departmentId, joinedOn) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Operations.CreateEmployeeContract (employeeProfileId, contractType, startsOn, endsOn, baseSalary) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Operations.CreateHostelBuilding (name, address) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY |
| POST Operations.CreateHostelRoom (input) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY |
| POST Operations.CreateIntake (input) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Operations.CreatePlacementDrive (input) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY |
| POST Operations.CreateProgram (input) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Operations.CreateServiceTicket (module, title, description, priority) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Operations.CreateTransportRoute (input) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY |
| POST Operations.CreateVehicle (registrationNumber, model, capacity) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY |
| POST Operations.EndHostel (id, endsOn) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY |
| POST Operations.EndTransport (id, endsOn) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY |
| POST Operations.EnrollTransport (routeId, studentId, boardingStop, startsOn) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY |
| GET Operations.Index () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Operations.ReviewPlacement (id, status) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY |
| POST Operations.SchedulePlacementInterview (placementApplicationId, stage, scheduledUtc, location) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY |
| POST Operations.SetEmployeeActive (id, active) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET ProfessorDashboard.Index (filter, cancellationToken) | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Profile.Image (cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Profile.Index () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Profile.RemoveImage (cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Profile.UpdateImage (image, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Promotions.Apply (id, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Promotions.Create (cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Promotions.Create (input, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Promotions.Details (id, cancellationToken) | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Promotions.Index (request, cancellationToken) | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Promotions.Review (input, cancellationToken) | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Promotions.SaveDecisions (id, decisions, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Promotions.Submit (id, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Reports.Export (query, cancellationToken) | PASS | PASS | PASS | DENY | DENY | DENY | PASS | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY |
| GET Reports.Index (query, cancellationToken, savedReportId) | PASS | PASS | PASS | DENY | DENY | DENY | PASS | PASS | DENY | PASS | DENY | DENY | DENY | DENY | DENY |
| POST Reports.Save (name, query, isShared, cancellationToken) | PASS | PASS | PASS | DENY | DENY | DENY | PASS | PASS | DENY | PASS | DENY | DENY | DENY | DENY | DENY |
| GET Reports.StudentsExcel (cancellationToken) | PASS | PASS | PASS | DENY | DENY | DENY | PASS | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY |
| GET RoleDashboard.Admissions (filter, cancellationToken) | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY |
| GET RoleDashboard.Examinations (filter, cancellationToken) | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY |
| GET RoleDashboard.Export (filter, cancellationToken) | PASS | PASS | PASS | PASS | DENY | DENY | PASS | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY |
| GET RoleDashboard.Finance (filter, cancellationToken) | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET RoleDashboard.Library (filter, cancellationToken) | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET RoleDashboard.Operations (filter, cancellationToken) | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | PASS | PASS | DENY | PASS | PASS |
| GET Search.Index (q, page, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Section.Create () | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Section.Create (section) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Section.Details (id, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Section.Edit (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Section.Edit (id, input) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Section.Index (request, classId, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Semesters.Create () | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Semesters.Create (model) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Semesters.Details (id, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Semesters.Edit (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Semesters.Edit (id, input) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Semesters.Index (request, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Staff.Accountant () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Staff.Certificate (id) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Staff.Details (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Staff.Edit (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Staff.Hostel () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Staff.Index (search, role, status, page) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Staff.Librarian () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Staff.Personal (page) | PASS | PASS | DENY | DENY | DENY | DENY | PASS | PASS | DENY | DENY | PASS | PASS | PASS | DENY | DENY |
| GET Staff.Registrar () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Staff.Save (id, input, photo) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Staff.Transport () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Staff.Workspace (workspace, page) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET StaffApi.Activity (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST StaffApi.Create (input) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET StaffApi.Get (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET StaffApi.List (search, role, status, page, export) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST StaffApi.Password (id, input) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET StaffApi.Permissions (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST StaffApi.Permissions (id, granted) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST StaffApi.Photo (id, photo, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET StaffApi.Photo (id, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST StaffApi.Status (id, input) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| PUT StaffApi.Update (id, input) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST StaffCertificates.Review (id, input) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST StaffResources.ApproveExpense (workspace, resource, id, input) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET StaffResources.List (workspace, resource, search, page) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST StaffResources.Save (workspace, resource, input) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET StaffWorkspaceApi.Activity () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST StaffWorkspaceApi.Create (workspace, category, input) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET StaffWorkspaceApi.Dashboard (workspace) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET StaffWorkspaceApi.Messages (box, page) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST StaffWorkspaceApi.ReadMessage (id) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET StaffWorkspaceApi.Records (workspace, category, search, status, page) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST StaffWorkspaceApi.Review (workspace, id, input) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST StaffWorkspaceApi.SendMessage (input) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET StaffWorkspaceApi.Session () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET StudentDashboard.Index (filter, cancellationToken) | DENY | DENY | DENY | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Students.Create () | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Students.Create (student, PhotoFile, cancellationToken) | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Students.Delete (id) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Students.Delete (id, reason, cancellationToken) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Students.Details (id, cancellationToken, attendancePage, marksPage) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Students.Edit (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Students.Edit (id, student, PhotoFile, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Students.Export (request, cancellationToken) | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Students.Index (request, cancellationToken) | PASS | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Students.Photo (id, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Students.Transcript (id, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET StudyMaterials.Create (cancellationToken) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST StudyMaterials.Create (subjectId, title, file, cancellationToken) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET StudyMaterials.Details (id, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET StudyMaterials.Download (id, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET StudyMaterials.Edit (id) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST StudyMaterials.Edit (id, input) | DENY | DENY | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET StudyMaterials.Index (request, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Subject.Create () | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Subject.Create (model) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Subject.Delete (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Subject.Delete (id, reason) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Subject.Details (id) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Subject.Edit (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Subject.Edit (id, vm) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Subject.Export (cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Subject.Index (request, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET SuperAdmin.AdminDetails (id, cancellationToken) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET SuperAdmin.Admins (request, cancellationToken) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET SuperAdmin.Audit (request, cancellationToken) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET SuperAdmin.CreateAdmin () | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST SuperAdmin.CreateAdmin (fullName, email) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET SuperAdmin.EditAdmin (id) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST SuperAdmin.EditAdmin (id, input) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET SuperAdmin.Index (filter, cancellationToken) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET SuperAdmin.LoginHistory (request, cancellationToken) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET SuperAdmin.Permissions (id) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST SuperAdmin.ResetPassword (id, newPassword) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST SuperAdmin.SaveSetting (key, value) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST SuperAdmin.SetAdminActive (id, active) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST SuperAdmin.SetPermission (userId, permissionId, effect) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET SuperAdmin.Settings () | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST SuperAdmin.TogglePermission (userId, permissionId) | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Teachers.Create () | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Teachers.Create (teacher, SubjectIds) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Teachers.Delete (id, reason) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Teachers.Details (id, cancellationToken) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Teachers.Edit (id) | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Teachers.Edit (id, input, ClassIds, SubjectIds) | DENY | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Teachers.Export (request, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Teachers.Index (request, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Timetable.Create () | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Timetable.Create (model) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Timetable.Details (id) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| GET Timetable.Edit (id) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST Timetable.Edit (id, input) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| GET Timetable.Index () | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| POST Timetable.Substitute (id, occursOn, substituteTeacherId, roomId, status, reason, cancellationToken) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
| POST UserAdministration.ResetPassword (id, newPassword, returnUrl) | PASS | PASS | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY | DENY |
