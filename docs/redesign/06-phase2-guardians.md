# Guardian directory and relationship management

Route `/Guardians/Index`; existing Admin/SuperAdmin access is retained. Purpose:
create real guardian accounts, find existing guardians, and manage student links.
Dependencies: GuardiansController, Identity activation service, GuardianProfiles,
GuardianStudentLinks and link history, active Students and the shared EduFlow shell.

Layout: page header; responsive two-column account form and directory on desktop;
single column on tablet/mobile; labeled search and page-size controls; paginated
semantic guardian table; expandable relationship controls and explicit unlink
reason; existing-guardian link form. No new route or mocked records.

States: empty list offers search reset; native required/email/length validation;
server errors remain real; successful commands use shared feedback; submit loading
and confirmations use site.js. Read-only/denied users remain rejected by the
controller's existing role boundary. Login/activation remain required for guardians.

Security: unchecked finance access must bind false. Initial links retain the
provided relationship. Relinking reuses the historical unique relationship row
and records a new history event; revoked links do not appear as active. Input
lengths match existing database constraints. No schema change is needed.

Acceptance: SQL creation/link/unlink/relink and guardian flags; stable directory
paging/search; real Razor rendering. Browser viewport, focus, zoom and contrast
acceptance remain pending until browser tooling is available.
