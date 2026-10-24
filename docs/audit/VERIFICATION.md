# Verification and change record

24 October 2026. Scope: initial repository audit, student directory/form safety,
and connected admissions/results/search contracts. This is not a full production
release gate or a claim that every inventoried endpoint was executed.

## Actual results

| Check | Result | Meaning / limitation |
| --- | --- | --- |
| dotnet build StaffTests/StaffTests.csproj --no-restore -o .build/audit-checks | Passed | Compiles the application, Razor views and in-repository test runner; zero errors; existing ImageSharp license warning |
| dotnet run --project StaffTests/StaffTests.csproj --no-restore | Passed: 107 assertions | Includes student placement rules, HOD campus/department scope, active institution, query translation, guardian link flags, stable ordering and endpoint role restrictions, plus existing staff checks; no live database needed |
| node --test Tests/DependentSelects.test.mjs Tests/StaffNative.test.mjs Tests/StaffCache.test.mjs | Passed: 17 tests | Three new production-handler tests cover cascading resets, request races and lookup error feedback; existing 14 staff/offline tests retained |
| node --check wwwroot/js/site.js | Passed | JavaScript syntax only |
| git diff --check | Passed | No whitespace errors; Git emits existing Windows line-ending notices |
| dotnet build Student_Management_System.sln --no-restore | Failed: MSB3202 | Sibling D:/Student_Management_System.Tests/Student_Management_System.Tests.csproj is absent. Application itself still builds. No external project reference removed |
| dotnet list Student_Management_System.csproj package --vulnerable --include-transitive --no-restore | Unavailable | Network/socket access to api.nuget.org:443 denied. No statement of vulnerability clearance is made |
| In-app browser bootstrap | Unavailable | Missing sandboxPolicy metadata prevents connection. No navigation, screenshot, viewport or screen-reader checks claimed |
| Read-only sqlcmd to local SQL Express / StudentDB fallback | Connection failed | Driver reported encryption/client-credential/connection errors. Windows SQL services are running, but application schema/data contents were not verified |
| Frontend lint / TypeScript | Not configured | No package.json, lint configuration/task, or TypeScript project was found; no fabricated lint/typecheck result |

The earlier .NET invocation was blocked by Windows Application Control
(0x800711C7). A subsequent ordinary invocation succeeded; no security policy was
changed or bypassed. Its latest successful result supersedes the earlier blocked
status. The optional StaffTests --integration mode was not run. No production or
existing database was modified for verification. The application was not launched
against existing data because Development startup contains schema repair/migration
paths; audit checks do not authorize silently running a migration.

## Changed files in this audit pass

| Files | Functionality / reason |
| --- | --- |
| Controllers/StudentsController.cs | Shared HOD record scope; active-account semantics; shared placement validation; membership in creation transaction; subject initialization independent of year; narrower edit contract; scoped HOD options; archive reason/retry checks; transcript result scope |
| Services/StudentRecordScope.cs | One query boundary for directory, private files, profiles, transcripts and student search |
| Services/StudentPlacementRules.cs | Testable class/section/department/semester validation shared by creation/editing/admission enrollment |
| Controllers/AdmissionsController.cs | Enrollment placement revalidation, campus/program/semester/subject transfer, authorized return navigation, optional normal-transition comment |
| Controllers/SearchController.cs | HOD student scope, admission campus scope, guardian unlink protection, Admin student.view override |
| Services/ResultService.cs | Guardian academic permission and active-link checks |
| Views/Students/Index.cshtml, Details.cshtml | Correct disabled-account state in counts/filter contract and row/profile badges |
| Views/Students/Create.cshtml | Existing dependent lookup pattern, activation guidance, visible validation, client validation scripts and unsaved-change tracking |
| Views/Students/Delete.cshtml | Visible server validation errors |
| Views/Shared/_Layout.cshtml | Visible warning feedback after partially successful workflows; pre-existing layout edits preserved |
| wwwroot/js/site.js | Clear descendant selects immediately; disable pending choices; prevent aborted/stale request results and busy-state races |
| StaffTests/StudentDirectoryVerification.cs, StaffTests/Program.cs | Production SQL translation and student/guardian/placement scope regression checks |
| Tests/DependentSelects.test.mjs | Production dependent-select handler tested with controlled asynchronous responses |
| README.md, Student_Management_System.csproj | Real local documentation/check commands, missing external suite explanation, accurate license comment; no package changes |
| docs/audit/*, docs/redesign/* | Findings, route/page inventory, per-page contracts, implementation status and verification |

The preceding increment's directory layout/CSS and reusable _PageSizeSelector,
_StatCards, _ManagementHeader and _ServerPagination changes are retained. See
[Page 01](../redesign/01-student-directory.md) for that file/change record.
Pre-existing STAFF_MANAGEMENT.md, _AnalyticsDashboard.cshtml,
_StaffManagementLink.cshtml and unrelated _Layout.cshtml edits were not reverted.

## Required live acceptance before completion

1. In a working browser and disposable configured database, test Admin/SuperAdmin/
   HOD/Professor/Student/Guardian plus denied Admin permission overrides.
2. Verify matching directory/CSV/profile/photo/transcript scope for shared campus,
   other campus, disabled campus/institution, absent HOD department, and revoked
   guardian links. Validate HTTP denial as well as hidden controls.
3. Exercise real Identity/enrollment transactions, valid/invalid placement,
   duplicate email, rollback/file compensation, concurrent updates, invitation
   delivery failure/recovery, archive and restore. New membership behavior does
   not repair historical orphaned membership records.
4. Confirm admissions preserves subjects/semester/program/campus, and both
   AdmissionOfficer and Registrar finish enrollment on an accessible route.
5. Check combined filters, export download, sorting/page-size persistence,
   dependent-selector changes and errors, page links/back navigation, and warnings.
6. Inspect 1440px/768px/390px, 200% zoom, light/dark, keyboard navigation and table
   screen-reader semantics. Browser acceptance remains open.

Remaining work is tracked explicitly in [PROJECT_AUDIT.md](PROJECT_AUDIT.md)
and [ADMISSIONS_AND_SEARCH.md](ADMISSIONS_AND_SEARCH.md): durable invitation
delivery, transactional archive/Identity updates, historical membership repair,
attendance formula consistency, profile-history pagination, admissions paging,
inquiry ownership and action-permission UX, shared lookup policy, and individual
deep audits/redesigns for the remaining modules. No page, schema or existing
record has been removed.
