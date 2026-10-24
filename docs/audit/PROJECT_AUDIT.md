# Repository audit and implementation plan

Audit started: 24 October 2026. Source: the actual working tree, not an assumed
prior report. This is a static repository audit with a detailed student-flow
review and a project-wide inventory. Runtime and database verification remain
separate gates. Findings below describe observed code, not proven exploitation
or production database contents. Existing user edits are preserved.

## Architecture and coverage

ASP.NET Core MVC on .NET 8; Razor views and tag helpers; EF Core 8.0.23 with SQL
Server; ASP.NET Identity and cookies; local Bootstrap, Font Awesome, jQuery
validation, and Chart.js assets; plain JavaScript with no frontend package.json,
TypeScript compiler, or configured frontend lint task. ClosedXML supplies reports;
ImageSharp 4.1.2 supplies image processing. Its missing license produces a build
warning; the csproj comment still describes version 3.1.12 and is stale.

The repository contains 49 controllers, 353 statically identified action
declarations, 166 Razor files including partials, 47 service files, and 116 DbSet
declarations in ApplicationDbContext. See [routes](ROUTES.md) and [page dispositions](PAGES.md).
Counts are inventory, not a claim that every action has been exercised.

Program.cs registers controllers, global CSRF validation, concurrency/setup,
Admin-permission-parity and overposting filters. The fallback authorization policy
requires authentication. Routing uses /{controller=Account}/{action=Login}/{id?}
plus attribute API routes and health endpoints. Authentication is followed by
staff isolation, password-rotation, privileged-MFA, and authorization middleware.
Health/configuration requires a policy; /health is intentionally anonymous.

Controllers mix direct EF queries, view-model construction, and domain commands.
Services already encapsulate attendance, results, finance ledger, academic
structure, campus scope, promotion, reporting, uploads, and staff workflows.
StudentsController still owns identity creation, enrollment synchronization,
private-file compensation, invitation delivery, profile authorization, and lists;
these responsibilities should move incrementally into tested services rather
than creating a second API/frontend stack.

The database model uses explicit foreign keys, unique indexes (attendance,
marks, enrollment, campus membership, permissions, payment idempotency), row
versions, archive fields, and restricted deletes. There is no universal EF tenant
query filter: isolation depends on the query/service used at each endpoint.
Migration files and the model snapshot are present. No migration, account,
production data, or schema is changed as part of this audit.

Student data flow reviewed: Index -> ListQuery -> scoped EF query -> summaries,
paging and Razor -> Details/Photo/Transcript -> record query -> result policy;
Create/Edit -> model binding/relationship checks -> Identity + Student +
enrollment transaction -> private-photo lifecycle -> activation/audit;
Delete -> recoverable archive and login deactivation. Related reads cover
HodManagement, Lookup, GuardianDashboard, Admissions, ResultService, attendance,
shared layout, and student views. Remaining modules have inventory and selected
cross-contract review; they still require individual deep audits.

## Findings (recorded before this audit's implementation pass)

| ID | Severity | Evidence / affected files | Impact | Recommended fix / initial status |
| --- | --- | --- | --- | --- |
| S01 | High | StudentsController.BuildAllowedStudentsQueryAsync HOD branch checks department only; BuildDirectoryQueryAsync also requires campus overlap | Guessing a same-department student ID outside shared campuses can reach Details, Photo, and Transcript despite directory exclusion | Reuse one HOD record scope; include active campus/institution checks. Open |
| S02 | High | StudentsController.Create checks HOD class department, but lacks Edit's section/class and semester/department validation | A POST can create contradictory academic placement and enrollment; foreign keys only establish existence | Shared server-side placement validation before identity/file writes. Open |
| S03 | High | StudentsController.Create and AdmissionsController.Enroll create Identity users without UserCampusMembership | New students can be absent from HOD directory and campus-scoped dashboard access | Add membership atomically using authorized creator default campus for direct create and application campus for admissions. Open |
| S04 | Medium | StudentsController active/inactive filters/counts and Students/Index, Details use lockout only; AccountController and StaffIsolationMiddleware deny IsActive=false | Disabled users are labeled active; inactive filtering gives misleading results | Include IsActive in queries and use consistent display rule. Open |
| S05 | Medium | StudentsController.Edit binds admission/roll/status/emergency/program fields but only assigns personal/basic placement fields | Form/API clients can submit changes that are silently ignored; responsibility and contract are unclear | Narrow edit binding to the actual form contract; expose extra fields only through specified validated workflows. Open |
| S06 | Medium | Students/Create lacks dependent lookup attributes and contains initial-password sharing instructions despite invitation activation | Users select incompatible records and receive contradictory onboarding guidance | Reuse existing lookup behavior, validation messages, and activation-only wording. Open |
| S07 | Medium | StudentsController.Create writes subject enrollments only when a current academic year exists | Student subject scope silently differs when no year is configured | Decouple subject enrollment from optional class-year history; define setup behavior. Open |
| S08 | Medium | StudentsController.Create/AdmissionsController.Enroll commit before invitation generation; TempData Warning not handled by _Layout | Delivery failures may look like success or cause a post-commit error; retry risks confusion | Surface warning feedback; preserve created records and route to recovery. Invitation/outbox redesign needs a separate transactional design. Open |
| S09 | Medium | AdmissionsController.Enroll redirects every successful actor to Students.Details; admission officer is not allowed by its record query; Registrar is also isolated by middleware | Enrollment succeeds then lands on forbidden/missing page | Return to the already-authorized application details. Open |
| S10 | High | ResultService.ScopeAsync Guardian branch checks guardian link but not UnlinkedUtc or CanViewAcademics | Reusing the service in another endpoint can admit revoked or finance-only links; GuardianDashboard currently adds its own checks | Make the shared result service authoritative for link flags; inspect all consumers. Open |
| S11 | Medium | StudentsController.Transcript scopes the student but loads all published subject marks, bypassing ResultService.ScopeAsync | Professor can receive published marks outside their teaching subjects | Apply the result service before materialization while retaining publication requirements. Open |
| S12 | Medium | Directory CSV previously had independent HOD department-only query, archive inclusion, and omitted phone/semester/status filters | Export differed from displayed scope/data | Fixed in preceding directory increment; shared BuildDirectoryQueryAsync/SortDirectory, runtime acceptance pending |
| S13 | Medium | Directory previously discarded query state on summary/page-size navigation and offered actionless selection checkboxes | Wrong result set and misleading controls | Fixed in preceding directory increment; query-preserving shared controls and selection removal; runtime acceptance pending |
| P01 | High (review) | CampusScopeService excludes inactive campuses/institutions; several legacy queries only test membership.IsActive; AdminPermissionParityFilter explicitly bypasses SuperAdmin and applies only to Admin | Permission and tenancy semantics vary by module | Document intended role boundary and audit each command/query; do not impose a global role policy without dependency review |
| P02 | Medium | Student directory lookup options and LookupController expose institution-wide academic metadata to authenticated users | Row scope is stricter than lookup metadata scope | Scope HOD directory options to its department; separately review shared lookup consumers before broad API policy changes |
| P03 | Medium | Students.DeleteConfirmed sets archive fields before unchecked IdentityResult updates; archive UI has no validation summary | Partial failure/retry and invisible validation errors possible | Add visible validation, reason length/idempotence guards; transactional identity/archive workflow needs integration coverage |
| P04 | Medium | Student profile loads entire attendance/marks/enrollment/assignment history before rendering | Large histories increase latency and response size | Paginate related history using a page specification and stable query contract; preserve aggregate accuracy |
| P05 | Medium | AttendanceService.GetPercentagesAsync weights Late/Excused; student directory/detail uses Present-only percentage | Different attendance definitions across modules | Identify institutional rule and centralize calculation; do not silently change academic thresholds in a UI patch |
| P06 | Medium | .sln points to ../Student_Management_System.Tests, absent; README references absent sibling docs/ops scripts | Advertised release commands are not reproducible from this repository | Document actual local checks and missing external assets; retain the solution reference until its ownership is established |
| P07 | Medium | No UI/permission integration suite can currently run: .NET runner blocked by Windows Application Control; browser setup failed | Build success does not establish real authorization, navigation, or responsive correctness | Record exact outcomes; continue static review/build/available JS checks; run full acceptance in a working environment |
| P08 | Low | RoleDashboard legacy views coexist with Staff workspaces; Home.Index redirects while Home/Index view exists | Potential dead or overlapping surfaces | Keep until incoming links, role grants, and behavior are compared; no removals in this pass |

High means potential unauthorized disclosure or materially inconsistent persisted
records. Medium means broken flows, misleading data, or an important assurance
gap. Review findings describe architectural risk, not a confirmed exploit.

## Page decisions and design direction

Keep the student directory as one server-paged register; keep the student profile
as the canonical academic record. Keep Create/Edit separate route actions while
sharing relationship validation, controls, and identity/enrollment services.
Keep HOD EditStudent separate: it is a narrower authoritative-profile workflow,
not a duplicate of Admin enrollment editing. Keep GuardianDashboard.Student:
guardian link flags and personal/finance boundaries differ from Students.Details.
Keep admissions application history and finance ledger separate from profile
summaries. Review RoleDashboard/Staff overlap and Home/Index reachability before
any redirect or deletion. No new standalone alerts/status pages are justified.

Use compact filterable-register patterns (Linear-style lists), institution
identity/placement summaries, the existing EduFlow token system and shared shell,
semantic tables, keyboard-accessible actions, explicit empty/error states, and
single-column mobile forms. A reference pattern is guidance, not a dependency or
a reason to rewrite the application. Each subsequent page needs its own purpose,
users, navigation, hierarchy, contracts, states, responsive and accessibility spec.

## Implementation plan

1. Preserve the current working tree and write this audit plus route/page inventories.
2. Student safety patch: unify HOD record scope, fix account-state semantics,
   validate placement before writes, assign campus membership atomically, and
   correct the edit input contract. Add focused regression checks.
3. Connected student UX: dependent create selectors, activation guidance and
   warning feedback, archive validation, published/transcript permissions.
4. Admissions/results continuation: fix authorized enrollment return navigation,
   assign admitted student campus membership, and enforce guardian result flags.
5. Build application and test project, run existing JavaScript tests and syntax
   checks; attempt .NET tests/browser connection without weakening machine policy.
   Record unavailable database/browser evidence instead of claiming success.
6. Complete student acceptance in an operational environment. Continue the next
   module audit with Admissions, then attendance/results, role dashboards/shared
   navigation, finance, academic structure, staff, library, operations,
   communication, and account/security. Expand this report per module.

## Verification baseline and constraints

The preceding increment compiled application/Razor and StaffTests, and passed
14 Node tests. Windows blocked StaffTests.dll execution with 0x800711C7; browser
setup failed with missing sandboxPolicy metadata. These are baseline results,
not verification of the changes planned above. No authenticated accounts were
used, no database contents/migration state were inspected, and no destructive
commands or schema changes were run. New results belong in VERIFICATION.md.

## Implementation status after the first two passes

The table above is the initial findings snapshot. The changes below were made
after it was written; live acceptance remains distinct from code/test completion.

| Finding | Current status |
| --- | --- |
| S01 | Implemented shared StudentRecordScope.ForHod for directory, profile/photo/transcript, and global student search; active campus/institution and department tests pass |
| S02 | Implemented shared StudentPlacementRules before Create/Edit writes; relationship/archived-placement assertions pass |
| S03 | New direct/admitted student membership is written inside the enrollment transaction; existing student memberships are not backfilled; live transaction test pending |
| S04 | IsActive included in directory account filters, counts, row badges, and profile badge |
| S05 | Edit binding narrowed to actual supported fields |
| S06 | Create uses existing dependent lookups, visible validation and activation-only guidance; shared handler cascade/race/error tests pass |
| S07 | Subject enrollment moved outside the optional current-year branch |
| S08 | Partial: shared Warning feedback is visible. Durable invitation delivery and exceptions after commit remain open |
| S09 | Successful enrollment returns to authorized application details |
| S10 | Guardian result scope now checks UnlinkedUtc and CanViewAcademics; SQL translation test passes |
| S11 | Transcript uses ResultService scope in addition to publication requirements |
| S12/S13 | Preceding directory changes retained; no claim of browser acceptance |
| P02 | Partial: HOD directory/create selector lists are scoped to department. Shared LookupController policy review remains open |
| P03 | Partial: visible archive errors, reason length and repeat-archive guards added. Transaction/IdentityResult redesign remains open |
| P06 | README now links real local reports/checks; external solution reference retained and failure documented |
| P07 | .NET execution recovered: latest run passes 107 assertions. Browser connection still fails; live SQL/viewport acceptance pending |

The next connected audit and implemented changes are in
[ADMISSIONS_AND_SEARCH.md](ADMISSIONS_AND_SEARCH.md). Application/test builds
pass; Node tests pass 17/17. See [VERIFICATION.md](VERIFICATION.md) for exact
commands, limits, changed files, and remaining issues. No migration, data
backfill, page deletion, dependency upgrade, or security-policy bypass was made.

Design references: [Linear filters](https://linear.app/docs/filters) supports
URL-preserved filter context; [W3C tables tutorial](https://www.w3.org/WAI/tutorials/tables/)
supports explicit captions and header relationships. The implementation adapts
these patterns to the existing EduFlow shell rather than adopting another UI stack.
