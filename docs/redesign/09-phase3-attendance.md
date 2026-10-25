# Attendance scope, editing and session review

Routes: GET/POST `/Attendance/Mark`, GET/POST `/Attendance/Edit/{id}`,
GET `/Attendance/Sessions`, POST `/Attendance/ApproveSession`; existing history,
statistics, correction, listing and export actions retain their contracts.

Users: assigned Professors and canonical department HODs, with existing
attendance permissions. Administrators retain their existing read roles;
SuperAdmin retains global history access unless explicitly denied. Other staff
are not granted new access. Campus memberships must be active and authoritative.

Dependencies: AttendanceController, AttendanceService, AcademicRecordScope,
StudentRecordScope, Attendance/AttendanceSession/AttendanceCorrection, campus
memberships, assignments, PermissionService, AuditService, existing Razor shell.

Layout: shared EduFlow page header, stacked filter/form cards, scrollable table,
semantic column headings, named approval buttons, shared page-size selector and
server pager. Mobile/tablet stack the same components; desktop retains density.
Empty rosters explain why submission is unavailable. Errors remain visible in
validation summaries; successful commands use existing notifications. Unauthorized
record IDs return Forbid; stale edits return HTTP 409 with reload instructions.
Server-rendered navigation uses existing submit feedback rather than simulated
loading data. No browser-persistent student drafts are introduced.

Entry accepts only student identity and status; server owns all IDs, relationships
and metadata. A roster must match the actor's active permitted class roster.
Whole-session review excludes a partially visible historical roster. Revision
tokens on the edit form and EF concurrency checks protect existing SQL columns;
correction decisions also carry status concurrency protection. No DDL required.

Acceptance: executable SQL tests cover campus exclusion, forged teacher, duplicate
roster, posted graphs/metadata, mixed-campus session approval/listing and stale
edits preserving committed values. Real Razor entry/session rendering and inline
script syntax are tested. Responsive widths 375/768/1366/1920, keyboard/focus,
contrast, zoom and light/dark visual acceptance remain blocked by browser tooling.

Remaining: exhaustive correction-success/session-success interaction and lifecycle
race scenarios need additional tests. Do not infer full module acceptance from
these targeted checks.
