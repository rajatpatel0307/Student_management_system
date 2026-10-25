# Phase 3 implementation continuation

Date: 25 October 2026. This is an implemented, tested increment, not a declaration
that all pages or record-level operations have completed acceptance. Phase 1/2
reports and unrelated working-tree changes are retained.

## Architecture and baseline

ASP.NET Core 8 MVC, Razor, Bootstrap/EduFlow, JavaScript/Chart.js, Identity,
EF Core SQL Server, existing SMTP sender, Data Protection and SQL outbox remain.
Current source counts: 49 controllers, 167 Razor files (including partials),
116 DbSet declarations. One new Razor partial accounts for the increase from 166.
No new controller, database table, migration or frontend framework was added.

Existing reports, route/page inventory, all redesign specifications, current
source and Git changes were inspected. Starting HEAD was `37975c1` (`fix pages`),
with substantial pre-existing uncommitted Phase 1/2 work. Selected source backups
were placed in ignored `.build/checkpoints/phase3`; the repository was not reset,
cleaned or committed. No environment files, uploads or production records changed.

Configured roles are SuperAdmin, Admin, HOD, Professor, Student, Guardian,
Accountant, Librarian, AdmissionOfficer, ExaminationOfficer, TransportManager,
HostelManager, Registrar, PlacementOfficer and Employee. Shared definitions now
serve production startup and tests; existing names/default grants are preserved.

## Verified defects and implemented fixes

| Severity | Root cause / verified issue | Implemented solution |
| --- | --- | --- |
| High | Invitations stored a token hash but delivery depended on the initiating request, with no durable retry | Token and encrypted outbox payload saved atomically; existing worker retries with backoff, leases and dead-letter limit |
| High | Emailed activation links could depend on request-derived URLs | Delivery uses deployment-owned trusted HTTPS PublicOrigin; Development permits loopback only |
| High | Legacy override filter skipped non-admin explicit denials and SuperAdmin; action matching could be case-sensitive | Honor explicit denials, normalize action names and correct per-action permission mapping |
| High | Excel alias called another action, which does not execute that action's authorization filters | Explicit reports.export attribute on StudentsExcel; finance export also enforces export permission |
| High | Finance queries and commands lacked consistent campus/current-guardian scope | Shared FinancialRecordScope on ledger, receipts, invoices, collection, adjustment, reversal, refund, issuance and finance reports |
| High | Attendance used a global class roster and accepted posted entity graphs/metadata | Shared active campus/assignment roster; teacher identity/subject validation; persist new entities from only StudentId and Status |
| High | Attendance record/session operations could expose or act on another campus's records | Shared scope on listings/history/statistics/reports/edit/corrections/exports; whole-session approval rejects partially visible rosters |
| Medium | Stale attendance edits/reviewer decisions could overwrite changes | Concurrency checks on existing Status/UpdatedUtc and correction Status; edit revision fields and controlled HTTP 409 |
| Medium | Results/report paths duplicated incomplete scope logic | Reuse ResultService and shared campus scope; retain canonical HOD department relationship |
| Medium | Low-attendance report counted Late/Excused as full attendance | Reuse configured weights and weighted totals |
| Medium | Report default was unavailable to some roles; saved filters had no working open action | Allowed role-specific report types/default, owned/shared saved-filter loading, current permission re-evaluation and date validation |
| Medium | Invoice, communications and promotion nonempty views referenced absent _Pagination | Use existing _ServerPagination partial |
| Low | Invitation messages falsely implied delivery and gave little operational feedback | Shared real delivery-state partial, generic queued confirmation and permission-aware resend/workflow controls |
| Low | Outbox finance notification linked to nonexistent StudentDashboard/Fees | Link to existing role-aware Home/Dashboard |

Invitation payloads use the application's Data Protection purpose isolation. Raw
tokens and recipient data are not stored in plaintext payload JSON; provider
exception messages are excluded from activation retry/audit logging. Delivered,
expired, superseded, activated, disabled and changed-email states are checked
before sending. Tests use a controlled fake mail sender, not external recipients.
Delivery is **at least once**: a crash after SMTP acceptance but before recording
success may resend the same one-time link. No exactly-once or mailbox receipt claim.

## Permission and record-scope evidence

[Action policy matrix](ACTION_POLICY_MATRIX_PHASE3.md): 353 distinct action
methods across all 49 controllers and 15 roles, evaluated using production named
policies/default grants, permission attributes and the override filter.
PASS means an action can reach its record/service guards, not successful CRUD or
record ownership. Public actions still have independent rate-limit/CSRF rules.

[HTTP role-boundary matrix](ROLE_BOUNDARY_MATRIX_PHASE3.md): representative MVC,
API, dashboard, student, admission, finance and marks routes; denied roles and
anonymous challenges execute through real local HTTP. Allowed destructive
commands are not submitted merely to test role admission.

Targeted real SQL tests separately cover HOD/campus result IDs, finance campus
CRUD denial without ledger writes, current/revoked guardian flags, own-student
access, attendance record/session scope and stale edits. This is not a completed
ownership matrix for every controller/API, nor a full privileged-role audit.

## Page implementation and acceptance

All rows reuse the existing shell, data contracts, real EF queries and existing
roles. Browser acceptance is blocked for all rows; no WCAG certification is made.

| Page / existing route | Source changes and preserved behavior | Executable acceptance / remaining |
| --- | --- | --- |
| Admission details `/Admissions/Details/{id}` | Permission-aware edit/review/enrollment/document controls; shared invitation status/resend; retain document/history/enrollment workflows | SQL status-query and real enrolled-details Razor render; full transitions/upload UI not accepted |
| Guardian directory `/Guardians` | Batch invitation status, safe resend, existing search/paging/relationship controls retained | Real Razor and prior relationship regressions; full resend UI interaction pending |
| Attendance entry `/Attendance/Mark` | Scoped active roster, real empty state, existing private tab drafts and submission retained | SQL submission rejection/persistence, real Razor/script syntax; full browser interaction pending |
| Attendance edit `/Attendance/Edit/{id}` | Revision fields, announced validation, native validation, conflict response; retain status/lecture edit | SQL foreign-ID denial and stale edit preservation; keyboard/browser success flow pending |
| Attendance sessions `/Attendance/Sessions` | Real paging/size, semantic headings/caption, specific approval names; retain server approval token | Mixed-campus session SQL denial and real populated Razor rendering; complete approval-success UI pending |
| Finance ledger `/Finance` | Permission-aware export/collection, invoice navigation; existing filters/sort/metrics retained | SQL ledger/receipt scope and command denials; full ledger browser acceptance pending |
| Finance invoices `/Finance/Invoices` | Working shared pager, semantic headers/caption, permission-aware issuance, recoverable empty state | Populated real Razor invoice render; issuance-success UI pending |
| Reports `/Reports` | Allowed report types, working saved filters, semantic preview, permission-aware export | SQL type/default/saved ownership/scope checks and populated Razor render; full export/browser acceptance pending |
| Communications `/Communications` and promotions `/Promotions` | Correct shared pagination partial, existing actions retained | Razor compilation only; individual runtime/visual acceptance not performed |

Specifications: redesign 08 (invitations), 09 (attendance), 10 (finance/reports),
11 (permissions). Page inventory records the same partial acceptance states.

## Files created, modified and deleted in this increment

Pre-existing Phase 1/2 modified/untracked files remain outside this increment's
attribution. No files were deleted.

Created:

- `Security/ApplicationRoles.cs`, `AuthorizationConfiguration.cs`,
  `DefaultRolePermissions.cs`: shared existing registration values.
- `Services/ActivationDeliveryService.cs`, `InvitationStatusQuery.cs`,
  `Models/InvitationDeliveryStatus.cs`, `Views/Shared/_InvitationStatus.cshtml`:
  protected durable delivery and operational status.
- `Services/FinancialRecordScope.cs`, `AcademicRecordScope.cs`: shared record scopes.
- `StaffTests/InvitationDeliveryVerification.cs`, `PermissionMatrixVerification.cs`,
  `FinancialScopeVerification.cs`, `AttendanceScopeVerification.cs`: regressions.
- `docs/redesign/08-phase3-invitations.md` through
  `11-phase3-permissions.md`, this report and both matrix artifacts.

Modified:

- `Program.cs`: shared authorization/role/default registration and delivery options.
- `Controllers/AccountController.cs`, `AdmissionsController.cs`,
  `GuardiansController.cs`: delivery/status wording and queries.
- `Controllers/AttendanceController.cs`, `FinanceController.cs`,
  `MarkController.cs`, `ReportsController.cs`: record scope, validation,
  permission/export/default/saved-filter/paging/concurrency fixes.
- `Services/StudentAccountService.cs`, `OutboxDispatcher.cs`: transactional
  activation/queue, safe dispatch and retry integration.
- `Services/AdminPermissionParityFilter.cs`, `PermissionService.cs`: explicit
  denial handling and corrected permission mappings.
- `Services/StudentRecordScope.cs`, `ResultService.cs`, `ReportingService.cs`,
  `AttendanceService.cs`: shared scope, weighted report and safe input persistence.
- `Models/Attendance.cs`, `ErpModules.cs`,
  `Migrations/ApplicationDbContextModelSnapshot.cs`: concurrency metadata only.
- `Views/Admissions/Details.cshtml`, `Guardians/Index.cshtml`,
  `Attendance/Mark.cshtml`, `Attendance/Edit.cshtml`, `Attendance/Sessions.cshtml`,
  `Finance/Index.cshtml`, `Finance/Invoices.cshtml`, `Reports/Index.cshtml`,
  `Communications/Index.cshtml`, `Promotions/Index.cshtml`: changes listed above.
- `StaffTests/Program.cs`, `NativePageVerification.cs`: suite integration,
  production-shared policy registration/filter and populated real Razor checks.
- `README.md`, `docs/audit/PAGES.md`, `ROUTES.md`, `VERIFICATION.md`,
  `docs/redesign/README.md`: current status and configuration guidance.

## APIs, database and deployment impact

No endpoint removed or response contract silently replaced. Existing optional
parameters added: Attendance Sessions page/pageSize, Edit expectedUpdatedUtc and
checkRevision; Reports Index savedReportId. Unauthorized/stale operations can now
return Forbid/409 instead of modifying records. Existing form routes/names remain.

No DDL/migration or data repair was executed. Existing columns provide optimistic
concurrency; snapshot metadata matches. Integration fixtures create and guardedly
remove only uniquely named `EduFlowStaffTest_*` databases. Missing historical
campus memberships are not guessed/backfilled; affected non-global actors may
need an administrator to assign authoritative memberships.

Production delivery requires existing SMTP configuration, trusted
`ActivationDelivery:PublicOrigin` (HTTPS, optionally an application base path),
and the existing durable Data Protection key/certificate setup shared by workers.
Keep credentials/license material external. No paid mail provider was added.

## Exact verification

| Command / check | Actual result |
| --- | --- |
| `dotnet build Student_Management_System.sln --no-restore` | PASS; 0 errors, missing-license Debug warning |
| `dotnet build Student_Management_System.sln --no-restore -c Release` | FAIL; 1 licensing error, 0 warnings; ImageSharp 4.1.2 cannot find a Six Labors license |
| `node --test Tests/*.test.mjs` | PASS, 35 tests |
| `dotnet run --project StaffTests/StaffTests.csproj --no-restore` | PASS, 121 non-SQL assertions |
| `dotnet run --project StaffTests/StaffTests.csproj --no-restore -- --integration` | PASS, 9,009 total assertions, including enrolled-admission invitation status and populated invoice/report/session views |
| `dotnet list Student_Management_System.csproj package --vulnerable --include-transitive --no-restore` | PASS query; no vulnerable packages reported by configured nuget.org/local sources, not a security guarantee |
| `node --check wwwroot/js/preferences.js`; `node --check wwwroot/js/private-drafts.js`; `node --check wwwroot/js/site.js`; `node --check wwwroot/service-worker.js` | PASS; existing modified scripts checked |
| Rendered marks and attendance inline scripts, `node --check` | PASS, run by real Razor integration host |
| `git diff --check` | PASS; CRLF conversion notices only |
| In-app browser Node connector invocation | NOT RUN; fails before JavaScript executes: `codex/sandbox-state-meta: missing field sandboxPolicy` |
| 375/768/1366/1920 visual, keyboard, contrast, zoom, focus and dark-mode acceptance | NOT RUN; browser blocker |
| External SMTP, real mailbox delivery | NOT RUN; controlled test sender used |

The first new attendance test run failed because its Class fixture omitted the
required TeacherId. The fixture was corrected, then the suite passed. Earlier
transient Windows Application Control execution failures were followed by normal
successful invocations; no policies or security controls were changed.

## Release blocker and compliant resolution

The installed ImageSharp 4.1.2 package's licensing target emits the above Release
failure. No target override, license bypass, downgrade, replacement or removal of
image processing was applied. The owner must obtain an appropriate valid license
and supply `SixLaborsLicenseKey` or `SixLaborsLicenseFile` through protected build
configuration. A purchase must not be assumed: Six Labors also offers an
application-based Community license for eligible users. Eligibility is external;
see [the vendor's licensing information](https://sixlabors.com/pricing/).

A library migration is not implemented without approval and comparative tests
for PNG/WebP/JPEG, orientation, transparency, metadata removal, malformed inputs,
animation rejection, decompression limits and deployment dependencies. Existing
image functionality remains intact.

## Outstanding work and final status

Implemented and executable-verified: listed scopes/denials, durable token+outbox
creation, retry/lease/expiry/consumption/audit behavior, targeted concurrency,
policy matrices, selected real Razor pages and existing Phase 1/2 regressions.

Implemented but not fully runtime/visually verified: SMTP deployment/mailbox
delivery; all redesigned interactions and visual accessibility at four widths;
communications/promotions populated routes; full finance command-success UI.

Not implemented / not certified:

- Complete remaining-page redesign (dashboards, academic administration,
  staff/exams/operations/library/materials/settings and other inventory rows).
- Complete record-ownership/campus/privilege-escalation acceptance for all APIs,
  including other report modules and global lookup metadata scoping.
- Account creation and invitation creation remain separate transactions at legacy
  call sites. Once a token is created its queue is durable, but a process failure
  between account commit and token creation still needs the existing resend
  recovery action. No claim of atomic account+invite creation is made.
- Existing old pending invitations have no recoverable raw token and cannot be
  retroactively queued; authorized resend rotates them safely after cooldown.
- Historical campus repair, guardian primary-link races, exam-lifecycle/marks
  transactional races and broader history panel pagination remain open.

Blocked: Release by valid-license setup; browser acceptance by connector metadata.
The project is not declared complete or production-ready.
