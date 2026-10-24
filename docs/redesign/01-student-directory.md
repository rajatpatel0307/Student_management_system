# Page 01: Student directory

Status: implemented; build verified; authenticated and visual acceptance pending.
Date: 24 October 2026.

## Evidence and priority

No audit existed when the first version of this specification was drafted.
The new [repository audit](../audit/PROJECT_AUDIT.md) now records direct code
inspection and findings. README previously referenced
`../UI_REDESIGN_REPORT.md`, `../DATA_MANAGEMENT_ENHANCEMENT_REPORT.md`, and
`../DETAILS_EDIT_REPAIR_REPORT.md`; these files were not found at those locations.
This specification is based on inspection of StudentsController, Student,
ApplicationDbContext, ListQuery/PagedResult, the shared layout and partials,
ClassesController, AdminPermissionParityFilter, PermissionService, and site.js/CSS.
The user confirmed the student directory as the first priority.

This page is the first provisional priority because it connects enrollment,
identity, academic placement, attendance intervention, profiles, and export.
Inspection found divergent export authorization/filtering, lost page-size filters,
summary links discarding query context, and selection controls without an action.

## Purpose and users

Find current students, identify account or attendance concerns, open academic
profiles, and maintain enrollment. Primary users: Admin, SuperAdmin, and HOD.
Students, guardians, and professors retain their existing scoped profile routes;
they do not gain access to this directory.

Reference product: Linear's compact, filterable list pattern, used as a conceptual
interaction reference rather than a claim about its current interface. Adapt it
to a campus register: explicit filters, summary context, compact identity rows,
and predictable drill-down. Keep EduFlow's existing tokens and components.

## Route, permissions, data, and API contract

| Surface | Existing route/contract | Access |
| --- | --- | --- |
| Directory | GET /Students or /Students/Index, ListQuery | Admin, SuperAdmin, HOD |
| Export | GET /Students/Export, same ListQuery | Same directory role restriction and row scope |
| Profile | GET /Students/Details/{id} | Existing record-level checks |
| Private photo | GET /Students/Photo/{id} | Existing private-file authorization |
| Create | GET/POST /Students/Create | Existing Admin/SuperAdmin/HOD rules and CSRF |
| Edit | GET/POST /Students/Edit/{id} | Admin/SuperAdmin and existing policy checks |
| HOD edit | GET/POST /HodManagement/EditStudent/{id} | Existing StudentEdit eligibility policy |
| Delete confirmation | GET /Students/Delete/{id}, existing POST | Admin/SuperAdmin; mutation retains CSRF |
| Class drill-down | GET /Classes/Details/{id} | Existing class authorization; link shown only for eligible context |

Admin permission overrides remain enforced server-side by
AdminPermissionParityFilter: student.view/create/update/delete. The directory
also hides denied Admin create/update/delete controls. Class navigation respects
Admin ManageAcademics. SuperAdmin behavior follows the existing filter's bypass.
HOD queries require an assigned HOD department and active campus overlap with
the student. Missing department returns Forbid; no fallback to an unscoped list.

The data model is Student with ApplicationUser, Department, Class, Section, and
Semester navigation properties. Attendance supplies the intervention statistic.
Account status means Identity linkage, IsActive and lockout, not Student.StudentStatus.
Missing placement means DepartmentId or SemesterId is null. Low attendance means
fewer than 75% Present records; students without attendance are not classified low.

Query keys: search, departmentId, classId, sectionId, semesterId, status, sort,
direction, page, pageSize. Search covers name/email/phone. Status supports active,
inactive, unassigned, low-attendance. Sort supports name/email/department/class,
asc/desc; student ID breaks ties. Page sizes: 10/20/50/100. Existing normalization
and out-of-range page clamping remain. CSV contains all matches, without paging,
in the same order, with the existing spreadsheet-formula escaping.

No new API, database columns, migrations, packages, or synthetic data are required.
Both directory and CSV now use BuildDirectoryQueryAsync and SortDirectory.

## Navigation and component hierarchy

Keep the shared role workspace header, notification/account menus, sidebar,
keyboard quick navigation, and Home > Student directory breadcrumb. Admin uses
the existing People > Students entry; HOD uses its Students entry. Do not add a
parallel directory, another dashboard, or a separate attendance-alert page.

1. _ManagementHeader: title, task description, refresh, CSV export, permitted create.
2. Scope context: institution or HOD department/shared campuses; archive exclusion.
3. _StatCards: total, active, inactive, low attendance for the current filters.
4. GET filter form: search, department, class, section, record status, semester,
   sort field/order, Apply, Reset, and an explanation of count/export semantics.
5. Results card: count/context, _PageSizeSelector, semantic student table or empty state.
6. _ServerPagination: current range, pages, and available previous/next controls.

Table columns: linked identity/private photo/email/record ID; department/semester;
class/section; textual account badge; authorized View/Edit/Delete actions. Delete
opens the existing confirmation workflow. Remove unused selection checkboxes.
Charts are unnecessary here: the four counts and low-attendance filter support
the task without duplicating analytics dashboards.

Reuse _ManagementHeader, _StatCards, _ServerPagination, existing forms/buttons,
avatars, badges, tokens, and shell. Introduce _PageSizeSelector as the reusable
query-preserving page-size form. Summary links preserve filters and reset page.
Page-size changes preserve every query key except page/pageSize. Applying filters
preserves page size and resets page. Explicit sorting remains usable on mobile.
Export links carry download so the shared navigation loader does not stay active
after a file response. Disabled pagination controls are noninteractive spans.

## States

| State | Behavior |
| --- | --- |
| Initial loading/navigation | Existing shared navigation loader; server renders authoritative data. No fake skeleton records. GET filters use ordinary browser navigation. |
| Empty institution | Explain directory purpose and offer Add student if permitted. |
| No matches | Explain filter mismatch and offer Clear filters. |
| Error | Preserve the existing production /Home/Error and HTTP status handlers; do not replace database failures with empty results. Development retains diagnostic middleware. |
| Success | Existing layout TempData success feedback after mutations; refreshed real counts/results. CSV uses the browser's download completion feedback. |
| Permission denied | Existing authentication challenge/Forbid and access-denied page; no sensitive rows rendered. Denied actions are hidden. |

## Responsive layout and accessibility

Desktop (992px+): existing sidebar/topbar; three-column filter grid with a
two-column search; four shared summary cards and a full table.
Tablet (576–991px): existing responsive navigation; two-column filters; shared
summary grid breakpoints; keyboard-reachable table scroll region when needed.
Mobile (575px and below): single-column filters, stacked toolbar, labeled student
rows using the same table DOM. Identity and actions remain visible; no duplicate
mobile data fetch. Keep explicit sort/order selects because table headers are
visually clipped in this layout.

Use one H1, labeled search/filter controls, a results H2, table caption, column
scope and aria-sort, explicit table roles to retain semantics with mobile display
rules, descriptive per-student action names, textual badges, visible focus, and
44px row-action targets. Existing sidebar focus handling and skip link remain.
Use shared theme tokens, including filter surfaces in dark mode. Reduced-motion
and contrast follow the existing design system; browser validation is pending.

## Verification and handoff

Changed files:

- Controllers/StudentsController.cs: one scope/filter/sort contract for list/export.
- Views/Students/Index.cshtml: directory layout, filters, permissions, states, table.
- Views/Shared/_PageSizeSelector.cshtml: reusable query-preserving page-size form.
- Views/Shared/_StatCards.cshtml: query-preserving summary navigation across pages.
- Views/Shared/_ManagementHeader.cshtml: export download behavior across pages.
- Views/Shared/_ServerPagination.cshtml: accessible unavailable paging controls.
- wwwroot/css/site.css: scoped directory layout and responsive rules.
- StaffTests/StudentDirectoryVerification.cs and StaffTests/Program.cs: SQL
  translation, deterministic ordering, and endpoint role regression checks.
- docs/redesign/README.md and this specification: rollout plan and acceptance record.

Verification performed:

- Application/Razor build passed with zero errors. Existing Six Labors license
  warning remains; no dependency was changed.
- node --test Tests/StaffNative.test.mjs Tests/StaffCache.test.mjs: 14/14 passed.
  These cover shared staff/offline regressions, not directory UI acceptance.
- .NET execution initially failed with Windows Application Control (0x800711C7),
  then recovered. The latest complete runner passes 107 assertions, including
  student scope, placement and query translation checks.
- Static review confirms list/export share scope, archive exclusion, all filters,
  and sort; route role attributes and mutation endpoints remain in place.
- Browser setup failed before connection: missing sandboxPolicy metadata.
  No authenticated browser navigation, screenshot, or viewport test is claimed.

Pending acceptance:

1. On an approved .NET runner, run dotnet run --project StaffTests/StaffTests.csproj.
2. With real local accounts/data, compare directory/export for phone, semester,
   status, archive exclusion, and HOD department/campus overlap; test denied HOD.
3. Verify combined filters survive summaries, page size, sort, pages, and export.
4. Check View/Edit/Create/Delete confirmation and class navigation for Admin,
   denied Admin permissions, SuperAdmin, eligible/ineligible HOD, and outsider roles.
5. Exercise empty/no-match/error/success states and keyboard-only navigation.
6. Inspect at 1440px, 768px, and 390px, 200% zoom, light/dark themes, and screen
   reader table/sort semantics. Check shared partials on another management page.

Remaining issues: live and visual verification are pending as above. HOD lookup
dropdowns are now limited to their department, and HOD profile/photo/transcript
queries share the directory campus boundary. The shared lookup API's broader
metadata policy remains under review. Large CSV exports retain the existing
in-memory implementation. Revisit both when the audit establishes their priority.
Existing uncommitted layout/analytics/staff changes were preserved.
