# Authentication, shared shell and assessment increment

24 October 2026. These are implemented changes with static/automated verification
limits. No page is certified as fully accepted without runtime checks.

## Page specifications and current acceptance

| Page / route | Users, purpose and dependencies | Implementation / states | Acceptance remaining |
| --- | --- | --- | --- |
| Sign in, GET/POST `/Account/Login` | Anonymous users; Identity/UserManager/SignInManager and LoginHistories; real account data | Existing two-column EduFlow layout retained; one-column responsive rules retained; bounded username/password input, native validation, labeled error descriptions, validation summary, empty initial form, preserved identifier/destination on failure; password is never rendered back into its field; global submit feedback retained | Actual successful/failed login, CSRF, throttling, lockout, local-return redirect, database logging and keyboard/mobile acceptance |
| Authenticator challenge, GET/POST `/Account/VerifyTwoFactor` | Pending authenticated challenge; active Identity account, audit and login history | Existing compact labeled challenge form retained; code errors preserve destination; verified challenge uses a session cookie and does not silently remember the browser | New production-handler tests compile; execution blocked by Application Control; real authenticator/recovery flow and responsive acceptance pending |
| Shared shell, all views using `_Layout` | Existing role-specific navigation and APIs remain authoritative | Existing sidebar/header/breadcrumb/empty/error/loading system retained; shared accessible theme toggle; saved light/dark palette; storage failure cannot stop shared navigation; dark table/filter surfaces follow tokens | New preference tests pass; role navigation and focus handling need browser acceptance |
| Marks entry, GET `/Mark/Create`, POST `/Mark/SaveBulk`, legacy POST `/Mark/Create` | HOD/Professor; existing subject permission checks, Students, Subjects, Enrollments, Exams, Marks, Teachers and AuditLogs | Existing bulk register, exam/subject filters, approved read-only rows, empty/locked states and save confirmation retained; archived students excluded from entry; duplicate/empty bulk rosters rejected; single-entry input restricted to entry fields; approved marks return conflict instead of being overwritten; validation/forbidden/not-found/locked responses remain real server responses | SQL CRUD, approvals/correction workflow, grade previews, concurrent edits and keyboard/mobile acceptance pending; validation tests compile but cannot run |

The visual reference is the existing EduFlow compact register and shared form
system. No external design dependency or fabricated production data is added.
Desktop retains sidebar + header + content; tablet retains collapse behavior;
mobile retains drawer + single-column controls + horizontally scrollable tables.
Contrast has been considered in token choices; WCAG compliance is not claimed
without browser, keyboard, zoom and assistive-technology checks.

Loading/success/error UI uses the existing shared form feedback and TempData
system. Login has no empty-record state; the empty state is the blank credential
form. Authorization failures remain server-enforced. The legacy marks command is
not a newly invented page: the existing POST retains the same writable field names
and response pattern while ignoring server-owned approval/lifecycle input.

## Backend and cache contracts

Global MVC responses now default to `no-store`/no-cache, covering student pages,
login forms, MFA keys and recovery codes. Static file middleware remains separate.
The service worker only adds successful, nonredirected public asset-directory
responses to its own versioned cache and respects server cache directives.
Activation removes obsolete EduFlow caches without deleting unrelated caches.
Existing explicit private-download exclusions remain in place.

No schema, database, upload directory, secret, dependency version, role, permission
or production record was changed. New dedicated input models preserve existing
HTTP form field names. Read-only approval metadata remains on existing entities
and page view models; it is absent from the single-entry request model.
