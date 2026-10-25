# Student profile history and metric accuracy

Existing `/Students/Details/{id}` and directory/export routes retain their current
roles, personal/finance/security visibility and shared EduFlow profile layout.
Purpose: expose complete authorized history with bounded queries and correct
full-history statistics. Attendance and marks receive independent 20-row pagers
that preserve the other history page and return to the corresponding section.
Desktop/tablet/mobile retain scrollable tables and shared page hierarchy.

Metrics must use SQL aggregates over all authorized records, not the displayed
page. Attendance uses the existing `Attendance.LateWeight` and
`Attendance.ExcusedWeight` settings and established service defaults; normalized
marks use each exam maximum. No new academic threshold is introduced.

Loading, validation, empty and success states retain the shared implementation;
unauthorized profiles remain inaccessible. Stable ordering ends with record ID.
Page numbers clamp safely, accessible pager names identify each history, and
table controls remain keyboard reachable. Browser/viewport acceptance is pending.

Dependencies: StudentsController, StudentDetailsViewModel, AttendanceService,
ResultService, existing PagedResult and shared pager. SQL regression tests must
check later pages, unchanged full-history totals and weighted attendance semantics.
