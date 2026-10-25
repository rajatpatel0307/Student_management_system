# Incremental page redesign

Phase 3 specifications:
[invitations](08-phase3-invitations.md),
[attendance](09-phase3-attendance.md),
[finance/reports](10-phase3-finance-reports.md), and
[permission enforcement](11-phase3-permissions.md).
Current count is 167 Razor files, including partials. The historical roadmap
below is preserved; current per-page acceptance is in
[PAGES.md](../audit/PAGES.md) and
[IMPLEMENTATION_PHASE3.md](../audit/IMPLEMENTATION_PHASE3.md).

Use the existing EduFlow design system and real MVC/EF data. Finish a page's
specification, implementation, shared-contract review, and acceptance record
before treating it as complete. No replacement frontend or duplicate API layer.

The [repository audit](../audit/PROJECT_AUDIT.md) now records the initial static
review and student-flow findings. There are 166 Razor files,
including partials; that is not a count of independent pages.

| Order | Page or connected flow | Reason / dependency | Status |
| --- | --- | --- | --- |
| 1 | Student directory | Cross-module entry point; list/export scope mismatch | Implemented; live/visual acceptance pending; see [specification](01-student-directory.md) |
| 2 | Student profile and create/edit | Directory drill-down, identity and enrollment contracts | Safety/interaction pass implemented; see [specification](02-student-forms.md); live acceptance pending |
| 3 | Role dashboards and shared navigation | Common landing pages and permission parity | Not started; preserve existing dashboard edits |
| 4 | Attendance and marks entry | Frequent workflows; teacher ownership and result visibility | Not started |
| 5 | Admissions and finance | Enrollment handoff and immutable payment contracts | Admissions/search contract audit and fixes started; finance page redesign pending |
| 6 | Academics, staff, library, operations, communication, account flows | Review the audit and route inventory before selecting each page | Not started |

For each next page, inspect its controller, model, view, permission handlers,
linked routes, services, and shared components before writing its specification.
Use Page 01's sections as the checklist: purpose/users; reference/pattern;
navigation; hierarchy; components/actions; states; responsive behavior;
accessibility; backend/frontend contracts; changed files; verification; issues.
Keep specifications separate from claims of test completion.

Shared invariants: one shell, one token system, server-side paging on large
datasets, query-preserving navigation, actual API permission checks, record-level
scope, semantic accessible forms/tables, and ordinary usable GET forms without
JavaScript. Consolidate controls into partials only when they have a shared
contract. Review duplicate pages for redirects or consolidation after inspecting
their authorization and purpose; similar titles alone do not establish duplication.

The browser acceptance gate remains open because the browser tool could not
connect. The .NET runner executed successfully on the latest attempt; see the
[verification record](../audit/VERIFICATION.md). Do not report authenticated or
visual acceptance as passed before running those checks.
