# Student Management System

The [Phase 4 report](docs/audit/IMPLEMENTATION_PHASE4.md),
[acceptance matrix](docs/audit/FINAL_ACCEPTANCE_MATRIX.md) and
[production runbook](docs/deployment/PRODUCTION_READINESS.md) record the latest
fixes and release blockers. The final suite passes 9,242 SQL/HTTPS/Razor assertions,
131 non-SQL assertions and 35 JavaScript tests. Release licensing and browser/full
module acceptance still prevent production approval.

Run all local quality gates with
`powershell -NoProfile -File scripts/Verify-Quality.ps1 -Integration -Publish`.
It uses only disposable integration databases, reports each result and refuses
artifact creation after a failed gate. It never deploys. Omit `-Integration` only
when SQL checks are intentionally being recorded as Not Run.

The [Phase 3 continuation](docs/audit/IMPLEMENTATION_PHASE3.md) adds protected
retryable invitation delivery, expanded permission matrices, finance/attendance
record scoping, stale-edit protection and selected Razor improvements. Page and
visual acceptance limits remain explicit; this is not a completed project claim.

For invitation delivery, configure deployment-owned `ActivationDelivery:PublicOrigin`
as the trusted HTTPS application URL, alongside existing SMTP and durable shared
Data Protection configuration. Do not store secrets or license keys in source.
New student/admission/guardian creation now saves a durable invitation recovery
request in the account transaction. Older pending accounts without that event
still require authorized resend; no bulk account/token backfill is performed.

The [Phase 2 continuation report](docs/audit/IMPLEMENTATION_PHASE2.md) records
transactional student archives, approval/edit concurrency protection, scoped
assessment access, private tab drafts, paginated student history/admissions/
guardians, and real SQL/HTTPS/Razor verification. Run the disposable integration
suite with `dotnet run --project StaffTests/StaffTests.csproj --no-restore -- --integration`.
It creates and removes only its uniquely named `EduFlowStaffTest_*` database.
Release builds still require an appropriate Six Labors license. Browser viewport
and accessibility acceptance remain open.

The project now includes database-backed management summaries, server-side
search/filtering/pagination, permission-aware related-record navigation and
dedicated detail pages. The repository inspection, severity-ranked findings,
route/page inventories and implementation plan are in
[docs/audit/PROJECT_AUDIT.md](docs/audit/PROJECT_AUDIT.md). Actual check results
and remaining acceptance gaps are in
[docs/audit/VERIFICATION.md](docs/audit/VERIFICATION.md).

Secure ASP.NET Core MVC education ERP with portals for Super Admin, Admin, HOD,
Professor, Student, Guardian, Accountant, Librarian, Admission Officer, and
Examination Officer.

## Requirements

- .NET 8 SDK
- SQL Server 2019 or later

## Setup

1. Initialize User Secrets and set `ConnectionStrings:DefaultConnection` with a
   rotated local SQL credential. Never put it in an appsettings file.
2. For the one-time first Super Admin only, set:
   `BootstrapAdmin__Email` and `BootstrapAdmin__Password`.
3. Run `dotnet restore`, `dotnet ef database update`, then
   `dotnet run -- --bootstrap` once.
4. Start the portal normally with `dotnet run`.
5. Remove the bootstrap environment variables after the bootstrap succeeds.

Email uses the `Smtp` options section. Supply `Smtp:Host`, `Smtp:Port`,
`Smtp:User`, `Smtp:Password`, and `Smtp:UseStartTls` through User Secrets or
the equivalent protected `Smtp__*` environment variables.

Production uploads also require `UploadMalwareScanner__Enabled=true`,
`UploadMalwareScanner__Command` (for example `clamdscan`), its fixed
`UploadMalwareScanner__Arguments__N` values, and a persistent
`PrivateUploads__RootPath` outside `wwwroot`.

Password recovery requires those SMTP variables. When SMTP is not configured,
the recovery page safely asks the user to contact an administrator.

## Security

Passwords are verified by ASP.NET Core Identity. Do not use phone numbers as
passwords. Production secrets must remain outside source control. The previously
embedded mail credential must be revoked before deployment.

## Modules

The staff directory and five operational workspaces are described in
[STAFF_MANAGEMENT.md](STAFF_MANAGEMENT.md), including database rollout, verification
commands, completed workflows and remaining specialized integrations. Administrators
open the module from **Staff Management** in the dashboard sidebar.

- Super Admin account control and audit history
- Student, teacher, class, section, and subject management
- Admission applications with controlled stage history and secure enrollment
- Guardian portal with record-level Student and finance isolation
- Immutable finance transaction ledger with receipts and reversals
- Leave requests with department-scoped approval and notifications
- Personal notification center and role-based delivery
- Library catalog, uniquely barcoded copies, issue, return, overdue status, and fines
- Permission-aware global search
- Attendance with teacher ownership enforcement
- Exams, marks, grades, and dashboards
- Notices by audience
- Academic years, timetable, assignments and submissions
- Programs/intakes, private assignment attachments, rubrics and version history
- Transport, hostel, placements and employee onboarding
- Fees and payments
- Excel student-register report

## Interface

The application uses the shared EduFlow design system across every Razor view.
It includes a responsive role-specific sidebar, keyboard command search
(`Ctrl+K`), breadcrumbs, accessible focus states, light/dark themes, loading
feedback, reusable empty/error states, confirmation dialogs, toast messages,
server-side list paging/export, opt-in client tables for complete small datasets,
and responsive forms.

Details/Edit workflows additionally provide real record IDs, relationship-aware
selectors, identity synchronization, backend uniqueness checks, audit logging,
sticky mobile actions, submit loading states, unsaved-change protection, and
authenticated dependent lookups under `/api/lookups`.

The theme choice is stored in browser local storage. Attendance and marks entry
also support optional browser-local drafts; submitting either workflow clears
its saved draft.

See [docs/redesign/README.md](docs/redesign/README.md) for page specifications,
the incremental redesign queue, and UI acceptance status.

## Development accounts

Development data seeding is disabled by default. If explicitly enabled for a
disposable local database, all account identifiers and the password must be
supplied through User Secrets. No reusable login is committed to this project.

## Production checklist

- Configure a production SQL connection and SMTP service.
- Put the application behind a TLS reverse proxy.
- Configure backups at the SQL Server/platform layer.
- Run migrations on a database backup first if upgrading existing data; new
  unique constraints require existing duplicates to be cleaned.
- Configure centralized logs and data-protection key persistence.

## Tests

From this repository directory run:

`dotnet run --project StaffTests/StaffTests.csproj --no-restore`

`node --test Tests/*.test.mjs`

`dotnet build Student_Management_System.csproj --no-restore`

The solution references the in-repository `StaffTests` project. The unavailable
sibling test project has been replaced in the solution; its tests are not part
of this checkout. StaffTests is a console regression runner, not a
`dotnet test` discovery project. Its optional `--integration` mode requires
a disposable SQL database. See [verification](docs/audit/VERIFICATION.md)
for actual results and environment limitations.

The `20260726000100_ErpFoundation` migration adds admissions, guardian links,
the immutable finance ledger, leave requests, personal notifications, approval
workflow records, granular role permissions, and library circulation tables.
Run `dotnet ef database update` against a verified backup before starting the
updated application.

## Build output is locked

Current implementation and check results are recorded in
[the authentication, shell and assessment report](docs/audit/IMPLEMENTATION_20261024.md).
Debug compilation succeeds with an ImageSharp license warning. Release builds
require an appropriate Six Labors license; its build-time enforcement is retained.

If a rebuild says `Student_Management_System.exe` is being used by another
process—or reports that
`obj\Debug\net8.0\ref\Student_Management_System.dll` is missing—stop debugging
with **Shift+F5**, then double-click `..\Fix-Build.cmd`. The script stops only
this project's processes, clears the generated output for both projects,
restores packages, and rebuilds the solution. The missing-metadata and
copy-retry messages are secondary errors caused by the locked application.
