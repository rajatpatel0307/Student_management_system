# Assessment and attendance continuation specification

Routes: `/Mark/Create`, `/Mark/SaveBulk`, `/Mark/Edit/{id}`, `/Mark/Approve/{id}`,
correction commands, and `/Attendance/Mark`. Existing HOD/Professor entry roles,
subject authorization and approval/publication permissions remain authoritative.

Purpose: enter accurate assessment/attendance data using the existing EduFlow
register and shared shell. Desktop: header, exam/subject or attendance controls,
scrollable roster and save actions; tablet/mobile retain adaptive controls and
horizontal table scrolling. No new page or frontend stack is introduced.

Components: existing labels, filters, roster tables, approved read-only inputs,
progress, draft action, confirmation and sticky edit actions. Edit adds an original
revision field; entry models carry original revision values. Native number input
uses invariant numeric formatting and validation; errors use announced summaries.

States: existing blank/empty/locked rosters remain; submit loading uses shared JS;
success uses real TempData; validation errors keep drafts; concurrent changes use
the existing HTTP 409 conflict view; unauthorized writes still fail at the backend.
Draft storage failure gives a visible warning, not a false success.

Privacy: drafts now store only student IDs and editable values in account-scoped,
tab-scoped session storage, expire after four hours, and are invalidated when the
roster/revision changes. Old unscoped persistent drafts are removed. A successful
server save clears the corresponding draft; attempted/failed submits do not.
Logout clears private tab drafts while preserving preferences.

Dependencies: existing Razor/Bootstrap/site.js, new private-drafts.js, real
Student/Subject/Exam/Mark/Attendance/Correction data, ResultService and Identity.
Mark concurrency uses existing Score, IsApproved and UpdatedUtc columns; correction
review uses its existing Status column. No schema change or backfill is required.

Verification: Node privacy tests pass; SQL integration verifies approval-before-edit,
edit-before-approval, remarks-only conflicts and correction-review conflicts.
Browser/375/768/1366/1920px/keyboard/contrast acceptance remains open because the
browser connector fails before navigation. Full page acceptance is not claimed.
