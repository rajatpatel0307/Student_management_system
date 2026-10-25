# Production readiness and release runbook

25 October 2026. **NO-GO**. Release licensing, browser E2E/visual accessibility,
full module acceptance and infrastructure recovery checks are incomplete. This
runbook is preparation, not authorization to deploy. No deployment was performed.

## Quality gates

Run from the repository root in a trusted Windows environment with .NET 8 SDK,
Node and Git:

```powershell
powershell -NoProfile -File scripts/Verify-Quality.ps1 -Integration -Publish
```

The script records commands/exit codes in `.build/quality/results.json` and local
logs. It continues independent checks after failure and returns nonzero if any
check fails. Vulnerability findings and invalid/unavailable scan JSON fail the
gate, even when the NuGet listing command itself returns zero. Release artifact
creation is blocked after any failure. No publication/deployment occurs.

`-Integration` needs the existing Windows-integrated local SQL Express instance;
the runner creates and removes only its guarded `EduFlowStaffTest_*` database.
It never accepts a production database argument. Without that switch, integration
is explicitly NOT RUN and must be completed separately before release.

`.github/workflows/quality.yml` runs the script on PR/manual events with read-only
repository permissions and no persisted checkout credentials. Its hosted runner
does not claim SQL Express/browser acceptance. No deployment or license secrets
were added. The hosted workflow itself has not been executed in this session.
Current action versions follow the official
[checkout](https://github.com/actions/checkout),
[setup-dotnet](https://github.com/actions/setup-dotnet) and
[setup-node](https://github.com/actions/setup-node) documentation. Review immutable
action pins under the organization's dependency-update process before rollout.

## Required external configuration

| Area | Existing setting / required operational action |
| --- | --- |
| Environment | Explicit Production environment; no development bootstrap/fallback switches |
| Database | Protected ConnectionStrings:DefaultConnection; Encrypt enabled, valid trusted SQL certificate, TrustServerCertificate disabled outside local Development |
| Runtime database identity | Least privilege for application DML; separate controlled migration identity; connection pooling/retry/command timeout already configured |
| Public URL | ActivationDelivery:PublicOrigin is a trusted HTTPS application URL, optionally with base path, without credentials/query/fragment; now validated at startup outside Development |
| SMTP | Existing Smtp settings or SMTP_* environment mapping; protected credentials; configured STARTTLS/port/timeout; verify a disposable recipient's real delivery before release |
| Data Protection | Absolute durable shared DataProtection:KeyDirectory; existing certificate-thumbprint protection and service-account private-key access; retain old keys during rotations/rollback |
| Private files | Durable PrivateUploads root outside public static files, restricted filesystem identity, adequate storage/quota and backup |
| Malware scan | Production requires UploadMalwareScanner:Enabled; install approved scanner, executable/arguments and timeouts; test clean/rejected files without real student documents |
| Identity | Existing first-admin bootstrap uses protected inputs; privileged MFA enrollment and account recovery must work; never ship a predictable credential |
| Proxy/HTTPS | Valid public TLS, application HTTPS/HSTS; existing ForwardedHeaders trusts known proxies/networks only (default loopback); align the real reverse proxy without clearing trust restrictions |
| License | Supply an appropriate valid Six Labors key/file through approved protected build configuration; do not commit it or expose it to untrusted PR jobs |

No organization eligibility or license purchase was assumed. ImageSharp 4.1.2
currently blocks Release when no license exists. The owner must obtain an
appropriate license (including the application-based Community option if
eligible); see [Six Labors](https://sixlabors.com/pricing/). Image processing and
licensing enforcement remain intact. No library migration was attempted.

## Configuration and security review

Source review confirms global MVC CSRF/no-store filters, secure Identity cookies,
authentication/recovery rate limits, explicit permission overrides, active-account
middleware, password rotation and privileged MFA middleware. Dynamic private
responses stay outside the service worker cache. CSP/nonces, nosniff, framing and
referrer controls remain. Tests verify selected real HTTPS authentication,
CSRF/cookie/rate-limit behavior; they do not certify all controllers/uploads.

Production startup validates DB connectivity/pending migration state instead of
silently migrating or seeding. Controlled `--bootstrap` and development bootstrap
paths can mutate schema/Identity/files and require a separately reviewed run.
Do not run them speculatively against existing data. The web runtime must not
receive migration permissions just to make startup succeed.

Health: `/health` is a minimal public aggregate; `/health/configuration` is
SuperAdmin-authorized. SMTP configuration health is not a delivery test and a
degraded check may still return a successful HTTP health response. Define liveness,
readiness and alert rules accordingly. Test proxy redirects, certificate trust and
health behavior in the deployment environment before considering them accepted.

## Invitations, workers and monitoring

Student/admission/guardian creation now persists AccountActivationRequested in
the same transaction as the new account/profile/enrollment. A restarted worker
can create the protected invitation after the HTTP request is lost. Duplicate
requests preserve an already queued token. Token and encrypted delivery job are
atomic, with existing expiry, cooldown, backoff, leases and dead-letter handling.
Delivery is at least once; a crash after SMTP acceptance can resend the same link.
Existing old passwordless accounts without a recovery request still need an
authorized resend; no bulk token generation or data backfill was performed.

Retain a shared key ring for every instance/worker. Ensure the application host
keeps hosted workers running; do not rely on an idle/suspended web process for
timely mail. Monitor queue oldest age, retries, dead letters, SMTP failures,
activation expiry, DB latency/pool exhaustion, HTTP 5xx/403/429, authentication
failure/lockout, disk capacity and scanner errors. Redact credentials, tokens,
recipient content and student data in operational exports. Monitoring/alert
integration is a required external deployment task, not an implemented service.

## Backup and migration checklist

- Establish owner-approved RPO/RTO, SQL full/differential/log backup schedule and
  encrypted off-host retention. No recovery targets were invented here.
- Back up private uploads, Data Protection keys and certificate recovery material
  together with the DB. Do not put private keys in this repository.
- Prove restore into a separate isolated environment; verify linked file access,
  Identity login/MFA and invitation decryption. This restore drill is NOT RUN.
- Generate/review the pending migration SQL with the installed EF tooling and
  test it against a restored production-like copy. Preserve records and measure
  locking/runtime. No new Phase 4 schema migration is needed.
- Record schema version, reviewed artifact checksum, environment/configuration
  versions and previous compatible release before any approved maintenance.

## Release and rollback sequence

1. Resolve all FAIL/BLOCKED mandatory gates in the final acceptance matrix;
   complete real browser journeys at 375/768/1366/1920 and production-like load.
2. Produce a valid licensed Release artifact in a trusted build environment.
   The current artifact gate is blocked; no deployable artifact is claimed.
3. Verify backups/restore and migration compatibility before scheduling release.
   Approve DB and application changes separately under the owner's process.
4. Stage the artifact with protected configuration; run real HTTPS/auth/CSRF,
   least-privilege, enrollment, marks, invoice/refund and delivery smoke checks on
   disposable accounts. Verify key access, worker recovery and observability.
5. Obtain explicit deployment authorization. Deploy with the reviewed rollback
   window and monitor error rates/queue/SQL/health. This step was not executed.
6. On rollback, drain traffic/workers, preserve diagnostics and restore the prior
   compatible application/configuration. Do not reset the database or delete
   outbox/audit/uploads. Preserve shared keys so queued messages remain decryptable.
   A Phase 3 worker does not recognize AccountActivationRequested. Keep such jobs
   intact and workers paused until a compatible forward repair/worker is deployed;
   do not let an incompatible rollback dead-letter the new recovery intents.
7. If schema compatibility is lost, pause writes and use a separately approved,
   tested forward repair or backup-restore plan. Database restoration affects
   post-backup records and requires owner authorization; never automate it as an
   application rollback. Revalidate pending jobs before restarting workers.

## Outstanding release requirements

See [FINAL_ACCEPTANCE_MATRIX.md](../audit/FINAL_ACCEPTANCE_MATRIX.md) and
[IMPLEMENTATION_PHASE4.md](../audit/IMPLEMENTATION_PHASE4.md). Full browser E2E,
viewport/focus/contrast/zoom, complete record-scoped authorization, broader exam
lifecycle races, untested modules, real SMTP, production-like concurrent load,
backup restore and hosted CI acceptance remain outstanding. A successful Debug
build and thousands of targeted assertions do not satisfy those release gates.
