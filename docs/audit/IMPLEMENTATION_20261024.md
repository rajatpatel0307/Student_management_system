# Implementation report: authentication, shell and assessment

24 October 2026. This report records this execution session, separately from the
student/admissions changes already present on entry. Those changes were preserved.
This is a partial project implementation, not a completed full-project redesign.

## 1. Actual project architecture

- ASP.NET Core MVC targeting .NET 8, Razor/tag helpers, local Bootstrap,
  Font Awesome, jQuery validation, Chart.js and plain JavaScript. No package.json,
  frontend lint task or TypeScript project is configured.
- EF Core SQL Server, ASP.NET Identity cookies, global anti-forgery, granular
  permission filters, campus/record scope services, staff isolation, password
  rotation, privileged MFA, authentication/recovery rate limiting.
- Confirmed on disk: 49 controllers, 166 Razor files and 116 DbSet declarations.
  Controller/service queries implement record scope; there is no universal tenant
  query filter. Existing relationship indexes, row versions and migrations remain.
- Conventional `/{controller=Account}/{action=Login}/{id?}` routes plus attribute
  APIs, including staff APIs and authenticated academic lookups. Existing route
  and page inventories: [ROUTES.md](ROUTES.md), [PAGES.md](PAGES.md). Their line
  numbers describe the earlier inspection and may move as source is edited.
- Roles verified in Program.cs: SuperAdmin, Admin, HOD, Professor, Student,
  Guardian, Accountant, Librarian, Registrar, AdmissionOfficer, ExaminationOfficer,
  TransportManager, HostelManager, PlacementOfficer, Employee; legacy Teacher
  upgrade handling remains. Roles are combined with permission and record scope.
- Modules include students/academic structure, admissions/guardians, attendance,
  assessments/results, finance, library, staff workspaces, reports, approvals,
  communications, assignments/materials, profile/account and administrative pages.

## 2. Verified issues fixed

| Severity | Root cause | Applied change |
| --- | --- | --- |
| High | Legacy single-mark creation bound an entire Mark entity, including approval/withholding/grace metadata, then added it directly | Dedicated input model and explicit entity construction; only student/subject/exam/score/remarks can enter this workflow |
| High | Legacy single-mark upsert overwrote approved marks while bulk and Edit paths protected them | Approved existing records now return HTTP conflict and require correction workflow |
| Medium | MVC caching policy existed only on selected controllers/downloads | Global dynamic-response no-store default covers sensitive HTML and MFA responses; static assets retain normal caching |
| Medium | Service worker cached any successful image/script/font/style destination without considering server cache policy or redirect status | Public-directory allowlist, server directive and redirect checks, safe asynchronous cache writes |
| Medium | Authenticator verification always passed rememberClient=true without offering a trust-device choice | Explicit false; successful challenges do not silently establish a remembered browser |
| Medium | Unbounded login strings could reach identity lookup/logging; failed posts did not preserve entered identifier | Required/length-validated input model, preserved identifier/destination, field errors, password never echoed |
| Medium | Layout/site.js forced light theme and erased theme choice; storage access could throw and abort shared scripts | Optional storage wrapper, persistent theme toggle and dark semantic tokens across shared components |
| Medium | Solution referenced a sibling test project absent from checkout | Solution now includes the real StaffTests project; external absent tests are not claimed as retained coverage |
| Medium | Marks roster included archived students and accepted duplicate student IDs | Active roster restrictions on reads/writes and empty/duplicate roster validation |
| Low | Worker activation removed every cache on the origin | Cleanup limited to obsolete `eduflow-static-*` caches |

## 3. Pages improved

See [page specifications](../redesign/03-authentication-shell-assessment.md) for
users, dependencies, layouts and states. Login and authenticator challenge were
improved; shared theme/navigation changes apply through `_Layout`; marks entry
received backend safety fixes and roster validation. Existing student/admissions
work is documented separately in earlier audit/redesign files and is not claimed
as newly authored here. Remaining pages have inventories, not complete acceptance.

## 4. Files changed in this execution

| Created files | Reason |
| --- | --- |
| Models/LoginViewModel.cs | Bounded login form contract |
| Models/MarkEntryInputModel.cs | Restricted single-mark entry contract |
| StaffTests/AuthenticationVerification.cs | Login/MFA production-handler and mark-input regression cases |
| Tests/Preferences.test.mjs | Real preference script under normal/denied browser storage |
| wwwroot/js/preferences.js | Safe optional preferences, theme initialization and toggle |
| docs/redesign/03-authentication-shell-assessment.md | Per-page specifications and acceptance gaps |
| docs/audit/IMPLEMENTATION_20261024.md | This implementation and verification record |

| Modified files | Reason |
| --- | --- |
| Controllers/AccountController.cs | Validated login input and nonremembered MFA challenge |
| Controllers/MarkController.cs | Restricted entry, approved-record protection, archived roster exclusion |
| Models/MarksEntryViewModel.cs | Empty/duplicate roster validation |
| Program.cs | Default no-store response policy for MVC |
| StaffTests/Program.cs | Run new authentication/assessment checks |
| Student_Management_System.sln | Reference existing in-repo test runner |
| Tests/StaffCache.test.mjs | Cache directive/redirect/allowlist/cleanup regression coverage |
| Views/Account/Login.cshtml | Accessible bounded form, validation and preserved identifier |
| Views/Shared/_Layout.cshtml | Shared preferences script and theme button |
| wwwroot/css/site.css | Dark tokens and semantic table/filter surfaces |
| wwwroot/js/site.js | Storage-safe sidebar preferences; stop erasing theme |
| wwwroot/service-worker.js | Versioned public-only caching and constrained cleanup |
| Student_Management_System.csproj | Accurate Debug/Release license comment, no version changes |
| README.md | Correct solution/check status and report link |
| docs/audit/VERIFICATION.md | Latest results supersede historical checks where applicable |

No files deleted. Selected existing source files were checkpointed before edits
under `.build/checkpoints/20261024-auth-shell/`; no secrets/data were copied.

## 5. Commands and actual verification

| Exact command / action | Result |
| --- | --- |
| `dotnet build Student_Management_System.csproj --no-restore -o .build/session-baseline` | Passed before edits; ImageSharp license warning |
| `node --test Tests/*.test.mjs` | Passed, 27/27 tests: existing staff/dependent-select coverage plus preference/cache regression cases |
| `node --check wwwroot/js/site.js` | Passed |
| `node --check wwwroot/js/preferences.js` | Passed |
| `node --check wwwroot/service-worker.js` | Passed |
| `dotnet run --project StaffTests/StaffTests.csproj --no-restore` | Initial new-test compilation error fixed (Options name shadowing); next invocation compiled but execution blocked by Windows Application Control, 0x800711C7. New .NET assertions NOT RUN |
| `dotnet build Student_Management_System.sln --no-restore` | Passed after code changes; app/Razor and regression runner compile; one existing license warning |
| `dotnet build Student_Management_System.sln --no-restore -c Release` | Failed: ImageSharp 4.1.2 requires a Six Labors license. Enforcement retained; no bypass |
| `dotnet list Student_Management_System.csproj package --vulnerable --include-transitive --no-restore` | Failed: network/socket access to api.nuget.org denied; dependency security NOT VERIFIED |
| `git diff --check` | Passed; Windows line-ending notices only |
| Browser skill connection | Failed before navigation: missing sandboxPolicy metadata; browser tests NOT RUN |
| SQL integration/CRUD/auth role matrix/375,768,1366,1920px | NOT RUN: runtime/browser unavailable; no database was started, migrated or modified |
| Frontend lint / TypeScript checks | NOT CONFIGURED |

The earlier verification document records successful prior-session .NET assertions.
Those are historical evidence, not execution of the new checks in this session.

## 6. Remaining issues and risks

- Release build requires the appropriate ImageSharp license. No dependency version
  downgrade or licensing bypass was used.
- Application Control must permit normal .NET execution before new .NET assertions
  and authenticated local smoke tests can run. Machine policy was not changed.
- Browser connection needs repair before screenshots, keyboard, WCAG, theme and
  four-viewport acceptance can be established. Token choices alone prove none of these.
- Dependency advisory scan needs network access; no claim of vulnerability clearance.
- Full database workflows, direct-ID permission probes, concurrent result edits,
  email activation/recovery, file handling and complete role matrix remain unverified.
- Current ordinary mark edits still use the existing persistence/concurrency design;
  the added approval check is not a redesign of simultaneous approval/edit transactions.
- Earlier documented gaps remain: durable invitation delivery, transactional
  student archive/Identity updates, historical campus-membership repair, shared
  lookup scoping, attendance formula consistency, large profile-history pagination,
  admissions paging and remaining per-module deep audits/redesigns.
- Browser-local attendance/mark drafts retain the existing behavior and require a
  separate shared-device privacy review; the preferences helper does not change drafts.

## 7. Final status

**Implemented and verified:** source/Razor/test-runner Debug compilation; 27 Node
tests including optional-storage and offline-cache behavior; JavaScript syntax.

**Implemented but not runtime-verified:** login/MFA changes, global MVC cache
policy, single-mark approval/overposting protections, roster validation and theme UI.

**Not implemented:** full redesign/deep audit of every page, complete module CRUD
acceptance, end-to-end role/record-access testing, transactional concurrency redesign.

**Blocked:** Release build by missing license; .NET runner by Application Control;
browser by connection metadata; vulnerability scan by network access.
