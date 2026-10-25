# Phase 4 — validation, reliability and measured optimization

25 October 2026. **Production acceptance: NO-GO.** Source fixes and executable
checks below are complete for this increment. Full browser E2E, complete module
acceptance and operational release gates remain incomplete. Earlier reports do
not establish production readiness and have not been overwritten.

## Verified architecture and workspace

Existing ASP.NET Core 8 MVC/Razor, EF Core SQL Server, ASP.NET Core Identity,
Bootstrap/EduFlow, JavaScript/Chart.js, SMTP, Data Protection and SQL outbox are
preserved. Recounted: **49 controllers, 167 Razor files, 116 DbSets**, 15 configured
roles and 353 distinct controller action methods in the executable policy matrix.
Controllers can expose multiple routes per method; these numbers are not a count
of independently tested workflows.

Reviewed prior reports, route/page inventories, redesign specifications, Git
changes, startup/security/delivery/query implementations and existing tests.
HEAD remains `37975c1`; pre-existing uncommitted Phase 1–3 changes remain. Selected
Phase 4 source checkpoints are in ignored `.build/checkpoints/phase4`. No reset,
commit, push, deployment, production data mutation or destructive migration ran.
No new frontend framework, database schema or package version was introduced.

## Issues fixed directly in source

| Priority | Confirmed cause | Change | Executed regression evidence |
| --- | --- | --- | --- |
| High reliability | Account creation committed before token/outbox creation, leaving a crash window with no retry job | Persist AccountActivationRequested in the account/profile/enrollment transaction for student, admission and guardian creation; worker creates protected invitation in its own unit of work | Real SQL rollback removes account and request together; interruption recovery, duplicate recovery jobs, one token and actual background delivery pass |
| High workflow | After successful enrollment/account commit, immediate invitation setup exceptions could surface as failed creation; worker-first queuing can also trigger the existing cooldown | Students/admissions preserve committed success and report pending retry after InvalidOperationException, matching existing guardian behavior | Approved admission commits profile/history/campus/account despite a controlled setup failure; retry does not duplicate student |
| Medium integrity | Admissions copied supplied phone to Student but omitted Identity.PhoneNumber, conflicting with the deployed-style Identity phone constraint | Synchronize supplied phone when creating the admission account; do not invent a placeholder or new mandatory-phone rule | Actual enrollment test with required Identity trigger and supplied phone passes |
| Medium reliability | A recovery request waiting before token creation looked like “Not requested” in status UI | Batch-read scoped recovery request metadata and expose queued/retry/attention before a token exists | Real SQL status-query test before worker token creation passes |
| Medium configuration | Invalid/missing public activation URL was discovered only when email delivery ran | Startup options validation outside Development; share the same HTTPS/base-path/credential/query/fragment checks with sender | Ten configuration assertions; safe production URL and development loopback preserved |
| Performance | Shared layout made 13 separate permission decisions, totaling 15 SQL commands for a fresh ordinary-user service | Presentation-only ReadForDisplayAsync batches requested role grants and overrides into two queries; layout consumes one snapshot | 195 role/key parity checks plus explicit SuperAdmin denial, refreshed override and anonymous rejection; real Razor renders |

Server-side HasAsync/explicit denial checks were retained independently of the
display snapshot; no cross-request permission cache was added. New activation
recovery intents contain only an internal account ID. Actual invitation delivery
payloads remain protected by Data Protection; raw tokens/passwords are not added
to request events. Existing cooldown, token expiry, one-time consumption,
active-account checks, leases, bounded retries and safe activation logging remain.

Recovery requests are duplicate-safe and preserve an already queued invitation,
including its expiry or dead-letter state; they do not endlessly rotate tokens.
Delivery remains at least once, not exactly once. Old passwordless accounts with
no recovery event are not bulk-modified and still require authorized resend.

## Functional and security validation

Added real SQL/controller/service coverage for:

- Admission approval prerequisite, account/profile placement and supplied phone,
  campus/class-history persistence, queued recovery, setup failure and duplicate
  enrollment rejection.
- Invoice creation, authoritative fee/enrollment relationship, payment allocation,
  matching request replay, mismatched reused key, partial refund, replayed refund,
  refund ceiling, subsequent payment, reversal, repeated reversal rejection and
  immutable original payment/allocation preservation.
- Recovery request atomic rollback, request loss before token creation, duplicate
  request completion, resumed mail dispatch and correct pre-token status.
- Shared navigation decisions equivalent to independent authoritative decisions
  across all configured roles, with explicit denial and unauthenticated claims.

The final **9,242 assertions** include existing real HTTPS login/logout, MFA
recovery/reset, CSRF, cookies/rate limits, archive rollback, private histories,
assessment edit/approval races, targeted campus/guardian/student boundaries,
operational staff workflows, real selected Razor rendering and policy evaluation.
Many assertions evaluate the policy matrix; they are not 9,242 browser journeys.

The policy matrix covers all 49 controllers, 353 action methods and 15 roles.
The representative HTTP matrix executes denied roles and anonymous requests.
Neither replaces exhaustive successful CRUD or record-scoped authorization for
all endpoints. Full XSS/injection/upload and privilege-escalation matrices remain
NOT RUN. Existing protection was inspected and preserved; no blanket security
clearance is claimed.

Real test hosts started on loopback against only disposable SQL databases.
AuthenticationHttpVerification runs the actual application entry point over HTTPS
with real cookies/CSRF/middleware; NativePageVerification is a separate test-only
authenticated Razor host. Its fake identity is never registered in production.
These are HTTP/SQL tests, not executed browser E2E.

## Measured performance before and after

Reproducible commands:

```powershell
dotnet run --project StaffTests/StaffTests.csproj --no-restore -- --integration --benchmark-baseline
dotnet run --project StaffTests/StaffTests.csproj --no-restore -- --integration --benchmark-after
```

Baseline was executed before replacing the display lookup implementation. The
same machine, .NET 8.0.31, local SQL Express provider and workload were used:
three warm-ups excluded, then 20 sequential fresh DbContexts resolving the same
13 Admin navigation permission keys. The `--benchmark-baseline` flag labels output;
it does not restore old source. Reproduce old performance using the saved source
checkpoint in a separate review checkout, not by overwriting this workspace.

| Measure | Before | Latest after |
| --- | ---: | ---: |
| SQL reader commands, 20 iterations | 300 | 40 |
| Median permission-resolution time | 10.6327 ms | 3.6157 ms |
| P95 (19th sorted observation of 20) | 12.9690 ms | 6.0362 ms |
| Sum of provider command execution duration | 129.2732 ms | 51.9903 ms |
| Total timed workload | 214.4237 ms | 89.1381 ms |
| Process CPU delta | 187.5 ms | 62.5 ms |
| Process working-set snapshot | 273,149,952 bytes | 269,475,840 bytes |
| Workload errors | 0 | 0 |

SQL command count fell **86.7%**. Median time fell approximately **66%** in the
latest sample; an earlier after-run measured 2.9145 ms median / 3.6817 ms P95.
Local scheduling/caching noise affects timing; no production SLA is inferred.
Provider duration excludes complete materialization; working-set observations
are process snapshots, not peak memory or proof of reduced memory consumption.
No full-page/browser load, concurrent throughput, connection saturation or soak
benchmark ran. No production load or index migration was performed.

Raw local measurements: `.build/performance-phase4-before.json` and `-after.json`.
The harness also records CPU/working-set samples and errors without recording SQL
values, passwords or connection strings. The same permission data/workload is
used even as unrelated functional fixtures grow during the wider suite.

## Quality gates and exact final results

Created `scripts/Verify-Quality.ps1` and `.github/workflows/quality.yml`. The script
records command exit codes, continues independent checks after failure, parses
NuGet advisory JSON, fails on vulnerability findings or scan parsing failure,
and guards optional local artifact creation. It never deploys. Hosted workflow
execution is NOT RUN; local script execution is verified.

Final invocation:

```powershell
powershell -NoProfile -File scripts/Verify-Quality.ps1 -Integration -Publish
```

Overall exit **1**, correctly reflecting failed Release licensing. Exact child
commands and outcomes are recorded in `.build/quality/results.json` and logs:

| Exact command / check | Exit | Result |
| --- | ---: | --- |
| `dotnet restore Student_Management_System.sln` | 0 | PASS; packages unchanged/up to date |
| `dotnet build Student_Management_System.sln --no-restore` | 0 | PASS; 0 errors, 1 ImageSharp Debug licensing warning |
| `dotnet build Student_Management_System.sln --no-restore -c Release` | 1 | FAIL; 1 missing-license error, 0 warnings |
| `dotnet run --project StaffTests/StaffTests.csproj --no-restore` | 0 | PASS; 131 non-SQL assertions |
| `dotnet run --project StaffTests/StaffTests.csproj --no-restore -- --integration` | 0 | PASS; 9,242 total assertions |
| `node --test Tests/*.test.mjs` | 0 | PASS; 35 tests |
| `node --check wwwroot/js/dashboard-charts.js` | 0 | PASS |
| `node --check wwwroot/js/preferences.js` | 0 | PASS |
| `node --check wwwroot/js/private-drafts.js` | 0 | PASS |
| `node --check wwwroot/js/site.js` | 0 | PASS |
| `node --check wwwroot/js/staff-native.js` | 0 | PASS |
| `node --check wwwroot/js/staff-resource-schemas.js` | 0 | PASS |
| `node --check wwwroot/service-worker.js` | 0 | PASS |
| Rendered marks/attendance inline `node --check`, driven by integration runner | 0 | PASS |
| `dotnet list Student_Management_System.csproj package --vulnerable --include-transitive --no-restore --format json` | 0 | PASS query; no vulnerable packages reported by configured nuget.org/local sources |
| `git diff --check` | 0 | PASS; line-ending notices do not fail it |
| `dotnet publish Student_Management_System.csproj --no-build --no-restore -c Release -o .build/artifacts` | — | BLOCKED by failed gate; command not executed |
| In-app browser setup | — | BLOCKED before execution: missing sandboxPolicy metadata |
| Hosted GitHub Actions execution | — | NOT RUN |

During implementation, a missing namespace in the new benchmark initially
prevented compilation; it was corrected before baseline measurement. The first
quality script exposed Windows PowerShell native-stderr and single-object Count
handling differences. Those were fixed and final execution confirms a failed
Release propagates exit 1 while Git notices do not masquerade as command failure.
No Windows security policies or licensing checks were bypassed.

## UI, E2E and responsive acceptance

Browser skill setup was attempted through the approved in-app browser and failed
before JavaScript/browser execution: `codex/sandbox-state-meta: missing field
sandboxPolicy`. No screenshot, browser click, viewport, contrast/focus/zoom,
screen-reader or WCAG acceptance was fabricated. Existing layouts were retained;
the shared navigation lookup changed for measured performance, not visual churn.

All critical user journeys still require browser E2E acceptance, despite selected
backend/HTTP coverage. All four viewport checks are BLOCKED. Do not relabel the
HTTP runner as Playwright or claim a browser framework was installed/executed.

## Production configuration, database and deployment review

Existing production startup validates DB connectivity/migrations and restricts
automatic initialization to explicit bootstrap/development paths. HTTPS/HSTS,
known-proxy forwarding, CSP/nonces, Identity cookies, no-store MVC, service-worker
privacy, durable protected keys and required production malware scanning remain.
The new origin validator prevents a deployment from quietly queuing unusable mail.

No schema change or production migration was needed. All SQL tests migrated a
fresh named database, reproduced deployed Identity compatibility constraints and
cleaned it through the existing guarded finally path. Real SMTP, hosted workers,
TLS/proxy deployment, scanner installation, backup restore, monitoring alerts and
rollback drills are NOT RUN. Missing authoritative campus data is not backfilled.

ImageSharp 4.1.2 still requires an appropriate valid key/file for Release. The
owner must obtain/configure it through trusted build settings; the vendor also
offers application-based Community licensing for eligible users. See
[Six Labors licensing](https://sixlabors.com/pricing/). No eligibility decision,
purchase, downgrade, target override or image-function removal occurred.

[PRODUCTION_READINESS.md](../deployment/PRODUCTION_READINESS.md) gives concrete
configuration, license, backup, migration, monitoring, release and rollback gates.
No deployment credentials were introduced and nothing was pushed/deployed.

## Files changed in Phase 4

Existing Phase 1–3 changes remain separate. No files deleted.

Created:

- `Services/AccountActivationRequests.cs`: durable creation recovery intent/handler.
- `Services/ActivationDeliveryOptionsValidator.cs`: startup/trusted-origin validation.
- `StaffTests/AdmissionWorkflowVerification.cs`, `FinanceWorkflowVerification.cs`,
  `ProductionConfigurationVerification.cs`, `PermissionDisplayVerification.cs`,
  `PermissionPerformanceVerification.cs`: real workflows, config, parity and metrics.
- `scripts/Verify-Quality.ps1`, `.github/workflows/quality.yml`: quality automation.
- This report, `FINAL_ACCEPTANCE_MATRIX.md`,
  `docs/deployment/PRODUCTION_READINESS.md`: current evidence and release runbook.

Modified:

- `Controllers/StudentsController.cs`, `AdmissionsController.cs`,
  `GuardiansController.cs`: transactional recovery request; student/admission
  post-commit failure feedback; admission Identity phone synchronization.
- `Services/OutboxDispatcher.cs`: new recovery request handler with scoped context.
- `Services/ActivationDeliveryService.cs`, `Program.cs`: shared URL validation and
  startup options validation.
- `Services/InvitationStatusQuery.cs`: recovery queue status before token creation.
- `Services/PermissionService.cs`, `Views/Shared/_Layout.cshtml`: batched display
  lookup with independent unchanged backend checks.
- `StaffTests/Program.cs`, `InvitationDeliveryVerification.cs`: coverage integration
  and real worker/rollback/replay checks.
- `README.md`, audit `PAGES.md`, `ROUTES.md`, `VERIFICATION.md`, redesign invitation
  specification: current guidance and limits, preserving historical reports.

No new endpoint, role, permission grant, database table or migration. Existing
API/form contracts remain; the outbox gains one internal event type. Rollback
must account for a prior worker not understanding that new event.

## Acceptance and remaining work

[FINAL_ACCEPTANCE_MATRIX.md](FINAL_ACCEPTANCE_MATRIX.md) lists all 49 controllers,
critical workflow checks, backgrounds and global gates. Named targeted tests PASS;
full untested modules remain NOT RUN. All browser acceptance is BLOCKED.

Remaining mandatory work includes complete record-level security/CRUD acceptance,
the remaining inventoried pages, full student create/edit/photo journeys, full
exam publication/mark-write lifecycle races, guardian primary-link concurrency,
global lookup/other report tenancy review, historical campus reconciliation,
real SMTP/deployment/monitoring, production-like load, restore and rollback drills.
They are not declared safely completed simply because builds or policy tests pass.

Release remains blocked by licensing and browser acceptance; additional NOT RUN
gates also prevent production approval. The safe source/test/automation increment
is applied and verified, with no claim that the whole application is finished.
