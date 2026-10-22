# Student Management System

The project now includes database-backed management summaries, server-side
search/filtering/pagination, permission-aware related-record navigation and
dedicated detail pages. See `../DATA_MANAGEMENT_ENHANCEMENT_REPORT.md` for the
complete page inventory, implementation list and verification checklist.
The complete Details/Edit repair inventory, authorization matrix and local
verification results are in `../DETAILS_EDIT_REPAIR_REPORT.md`.

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

See `../UI_REDESIGN_REPORT.md` for the complete page inventory and UI
verification checklist.

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

From the parent directory run:

`dotnet test Student_Management_System.Tests/Student_Management_System.Tests.csproj`

For the complete release gate and the exact verification status of this
delivery, see `../docs/VERIFICATION_RESULTS.md` and run
`../ops/verify-release.ps1`.

The `20260726000100_ErpFoundation` migration adds admissions, guardian links,
the immutable finance ledger, leave requests, personal notifications, approval
workflow records, granular role permissions, and library circulation tables.
Run `dotnet ef database update` against a verified backup before starting the
updated application.

## Build output is locked

If a rebuild says `Student_Management_System.exe` is being used by another
process—or reports that
`obj\Debug\net8.0\ref\Student_Management_System.dll` is missing—stop debugging
with **Shift+F5**, then double-click `..\Fix-Build.cmd`. The script stops only
this project's processes, clears the generated output for both projects,
restores packages, and rebuilds the solution. The missing-metadata and
copy-retry messages are secondary errors caused by the locked application.
