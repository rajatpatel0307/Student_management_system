# Final acceptance matrix — Phase 4

25 October 2026. **Release decision: NO-GO.** This is a complete controller/module
inventory with explicitly limited runtime evidence, not acceptance of every
workflow. Source counts remain 49 controllers, 167 Razor files and 116 DbSets.

Statuses: PASS = the named check executed successfully; FAIL = an observed failed
gate/defect; BLOCKED = an external dependency prevents execution; NOT RUN = not
executed. A PASS for policy evaluation, compilation or a named SQL test does not
certify a whole module, record ownership or browser behavior.

Actual routes/actions are listed in [ROUTES.md](ROUTES.md), all Razor files in
[PAGES.md](PAGES.md). The [353-action/15-role policy matrix](ACTION_POLICY_MATRIX_PHASE3.md)
and [HTTP denial matrix](ROLE_BOUNDARY_MATRIX_PHASE3.md) were re-executed by the
Phase 4 suite. They deliberately distinguish policy eligibility from record scope.

## Critical workflow checks

| Workflow / exact scope | Functional | Authorization | Integrity / runtime | Evidence | Browser E2E |
| --- | --- | --- | --- | --- | --- |
| Real HTTPS login/logout, invalid credentials, disabled/locked fixtures, CSRF, cookie behavior, local redirects, rate limiting | PASS | PASS for tested cases | PASS | AuthenticationVerification / AuthenticationHttpVerification | BLOCKED |
| MFA challenges, recovery codes and password reset in isolated host | PASS for tested paths | PASS for tested paths | PASS | AuthenticationHttpVerification; no real recovery email delivered | BLOCKED |
| Student archive/account deactivation rollback and retry | PASS | PASS for targeted tests | PASS | StudentArchiveVerification | BLOCKED |
| Student profile attendance/marks history paging and statistics | PASS | PASS for tested scope | PASS | StudentHistoryVerification and real Razor rendering | BLOCKED |
| Full student create/edit/profile/photo journeys | NOT RUN | NOT RUN for full journey | NOT RUN for full journey | Existing directory/placement/translation tests cover only parts | BLOCKED |
| Approved admission enrollment, Identity phone/placement/campus/history, initial invitation setup failure and duplicate enrollment | PASS | PASS for prerequisite/direct checks; full RBAC NOT RUN | PASS, real SQL/controller | AdmissionWorkflowVerification | BLOCKED |
| Guardian creation/link/unlink/relink and finance flag restrictions | PASS for listed cases | PASS for listed cases | PASS | GuardianRelationshipVerification / FinancialScopeVerification | BLOCKED |
| Account-commit interruption recovery, duplicate recovery jobs and rollback | PASS | NOT RUN for all creation entry points | PASS | InvitationDeliveryVerification; real SQL/dispatcher | BLOCKED |
| Protected invitation payload, retry, lease recovery, expiry, one-use activation and safe status | PASS for tested cases | PASS for tested cases | PASS | InvitationDeliveryVerification; controlled mail sender | BLOCKED |
| Real SMTP/mailbox delivery | NOT RUN | NOT RUN | NOT RUN | Deployment credentials/delivery not exercised | BLOCKED |
| Attendance allowed-roster persistence, forged IDs/navigation rejection, foreign record/session denial and stale edit | PASS | PASS for tested campus boundaries | PASS | AttendanceScopeVerification | BLOCKED |
| Complete attendance approval/correction and historical roster lifecycle | NOT RUN | NOT RUN | NOT RUN | Additional success/race scenarios outstanding | BLOCKED |
| Marks overposting/approved-score protection and competing edit/approval/correction decisions | PASS for listed cases | PASS for tested scopes | PASS | AssessmentConcurrencyVerification and legacy input regressions | BLOCKED |
| Full exam publication/lifecycle versus simultaneous marks writes | NOT RUN | NOT RUN | NOT RUN | Previously documented transactional race review remains open | BLOCKED |
| Invoice issuance, payment allocation, matching/mismatched duplicate keys, partial refund, refund ceiling and reversal | PASS | NOT RUN for full successful RBAC journey | PASS, SQL/controller/service | FinanceWorkflowVerification | BLOCKED |
| Foreign-campus finance commands, revoked guardian link and cross-student receipt denial | PASS | PASS for listed cases | PASS, no denied ledger writes | FinancialScopeVerification | BLOCKED |
| Report type/default, owned/shared saved filters, result/attendance/finance scope | PASS for tested cases | PASS for tested cases | PASS | FinancialScopeVerification and real Razor | BLOCKED |
| Staff operational registers, selected workspace SQL metrics and role isolation | PASS for existing tested operations | PASS for listed boundaries | PASS | Existing StaffTests runner / NativePageVerification | BLOCKED |
| Batched navigation permissions equal authoritative decisions for all 15 roles | PASS | PASS, including explicit SuperAdmin denial | PASS | PermissionDisplayVerification; authorization remains independent | BLOCKED |

## Global gates and operational checks

| Gate | Status | Evidence / remaining action |
| --- | --- | --- |
| Dependency restore | PASS | Existing solution restored without package changes |
| Debug compilation/Razor | PASS | Exact results in Phase 4 report |
| Release compilation | FAIL | ImageSharp license missing; external valid license required |
| .NET execution | PASS | Runner executes normally; no policy bypass |
| JavaScript tests/syntax | PASS | 35 tests and all seven application/worker scripts |
| Configured NuGet advisory scan | PASS | Query completed with no reported vulnerable packages; not a general security certification |
| Full record ownership / campus / privilege-escalation matrix | NOT RUN | Targeted boundaries pass; other controller/service guards remain unaccepted |
| Unsafe-upload/XSS/SQL injection full matrix | NOT RUN | Source controls retained; no exhaustive runtime payload suite executed |
| Production activation origin validation | PASS | Invalid/missing origin rejected outside Development; safe HTTPS/base path and development loopback tests |
| Browser automation bootstrap | BLOCKED | Connector rejects execution with missing sandboxPolicy metadata |
| Browser E2E and 375/768/1366/1920 visual checks | BLOCKED | No screenshots or browser acceptance claimed |
| Keyboard/screen reader/focus/contrast/zoom/WCAG 2.2 AA | BLOCKED | CSS and Razor inspection do not prove compliance |
| Permission database workload benchmark | PASS | Before/after measured on same machine/workload; scoped results in report |
| Whole-page latency/asset/CPU/memory/load benchmark | NOT RUN | Microbenchmark is not a whole-system or browser benchmark |
| Concurrent traffic / pool saturation / soak | NOT RUN | No production or disruptive load performed |
| Production monitoring and alert delivery | NOT RUN | Runbook requirements only |
| Backup restore and DB rollback drill | NOT RUN | Requires approved isolated production-like recovery environment |
| Hosted CI execution | NOT RUN | Workflow created; local quality script executed |
| Release artifact | BLOCKED | Quality script refuses publication after a failed gate |
| Production deployment | NOT RUN | Not authorized or performed |

## Background processes

| Process | Status for full process | Executed subset / remaining |
| --- | --- | --- |
| OutboxDispatcher | NOT RUN | PASS: token/recovery request handling, retries and leases with disposable SQL; all other event types and multihost soak not fully accepted |
| CommunicationDispatcher | NOT RUN | Real scheduled email/SMS recipient delivery, retry/load and provider integration remain unaccepted |
| Hosted web lifecycle / worker restart in production host | NOT RUN | Recovery dispatch tested locally; production idle/recycle/scale behavior remains external |

## Complete controller/module inventory

For each row, full functional, record-authorization and integrity acceptance is
NOT RUN unless its exact sub-workflow is listed above. Policy checks PASS for all
discovered actions, but do not substitute for complete module acceptance. All
responsive/browser acceptance is BLOCKED. Shared Release licensing and the global
unaccepted gates apply to every module. No unknown defect is invented to fill a
row; a missing test is recorded as NOT RUN.

| Actual controller | Functional (full) | Authorization (full record scope) | Integrity (full) | UI/responsive | Executed automation/runtime evidence | Remaining / release gate |
| --- | --- | --- | --- | --- | --- | --- |
| [AcademicStructure](../../Controllers/AcademicStructureController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [AcademicYear](../../Controllers/AcademicYearController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Account](../../Controllers/AccountController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: real HTTPS authentication/recovery and core unit checks | Complete workflow/record tests and global release gates |
| [Admin](../../Controllers/AdminController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Admissions](../../Controllers/AdmissionsController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: paged register, enrollment/recovery and Razor | Complete workflow/record tests and global release gates |
| [Approvals](../../Controllers/ApprovalsController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Archive](../../Controllers/ArchiveController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Assignment](../../Controllers/AssignmentController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Attendance](../../Controllers/AttendanceController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: roster/ID/scope/concurrency and entry/session Razor | Complete workflow/record tests and global release gates |
| [Classes](../../Controllers/ClassesController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Communications](../../Controllers/CommunicationsController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Dashboard](../../Controllers/DashboardController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Departments](../../Controllers/DepartmentsController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Exam](../../Controllers/ExamController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Fee](../../Controllers/FeeController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Finance](../../Controllers/FinanceController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: invoice/payment/refund/reversal, foreign-ID denial and Razor | Complete workflow/record tests and global release gates |
| [GuardianDashboard](../../Controllers/GuardianDashboardController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: policy/representative denied roles only | Complete workflow/record tests and global release gates |
| [Guardians](../../Controllers/GuardiansController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: relationship operations, creation failure feedback and Razor | Complete workflow/record tests and global release gates |
| [HodDashboard](../../Controllers/HodDashboardController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: policy/representative denied roles only | Complete workflow/record tests and global release gates |
| [HodManagement](../../Controllers/HodManagementController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Home](../../Controllers/HomeController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Leave](../../Controllers/LeaveController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Library](../../Controllers/LibraryController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Lookup](../../Controllers/LookupController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: selected anonymous API denial and lookup script tests; tenancy NOT RUN | Complete workflow/record tests and global release gates |
| [Mark](../../Controllers/MarkController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: input/approval/edit races, scoped IDs and entry Razor | Complete workflow/record tests and global release gates |
| [Notice](../../Controllers/NoticeController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Notifications](../../Controllers/NotificationsController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Operations](../../Controllers/OperationsController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [ProfessorDashboard](../../Controllers/ProfessorDashboardController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: policy/representative denied roles only | Complete workflow/record tests and global release gates |
| [Profile](../../Controllers/ProfileController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Promotions](../../Controllers/PromotionsController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Reports](../../Controllers/ReportsController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: tested scopes/default/saved filter ownership and Razor | Complete workflow/record tests and global release gates |
| [RoleDashboard](../../Controllers/RoleDashboardController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Search](../../Controllers/SearchController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Section](../../Controllers/SectionController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Semesters](../../Controllers/SemestersController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [StaffApi](../../Controllers/StaffApiController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: existing staff CRUD and representative HTTP role denials | Complete workflow/record tests and global release gates |
| [StaffCertificates](../../Controllers/StaffCertificatesController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: selected certificate checks | Complete workflow/record tests and global release gates |
| [Staff](../../Controllers/StaffController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: selected staff CRUD/workspace routes and Razor | Complete workflow/record tests and global release gates |
| [StaffResources](../../Controllers/StaffResourcesController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: selected operational register writes/projections | Complete workflow/record tests and global release gates |
| [StaffWorkspaceApi](../../Controllers/StaffWorkspaceApiController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: tested workspace SQL summaries and role boundaries | Complete workflow/record tests and global release gates |
| [StudentDashboard](../../Controllers/StudentDashboardController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: policy/representative denied roles only | Complete workflow/record tests and global release gates |
| [Students](../../Controllers/StudentsController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: directory SQL, archive rollback, scoped profile histories and selected Razor | Complete workflow/record tests and global release gates |
| [StudyMaterials](../../Controllers/StudyMaterialsController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Subject](../../Controllers/SubjectController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [SuperAdmin](../../Controllers/SuperAdminController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: policy/representative denied roles only | Complete workflow/record tests and global release gates |
| [Teachers](../../Controllers/TeachersController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [Timetable](../../Controllers/TimetableController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
| [UserAdministration](../../Controllers/UserAdministrationController.cs) | NOT RUN | NOT RUN | NOT RUN | BLOCKED | PASS: action policies and Razor compilation only; functional runtime NOT RUN | Complete workflow/record tests and global release gates |
