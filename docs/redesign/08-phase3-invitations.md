# Durable invitations and delivery visibility

Phase 4 continuation: student/admission/guardian creation persists an internal
recovery request with the account transaction. The worker can create the protected
invitation after an interrupted request, and duplicate requests preserve one
token. Status UI now shows queued/retry/attention even before token creation.
Old accounts without recovery events still need authorized resend. Startup
validates the trusted public URL outside Development. Real SQL recovery, rollback,
replay and enrollment tests pass; visual acceptance remains blocked.

Existing routes: admission details, guardian directory, Account activation/resend,
and existing account creation commands. Preserve their roles, record scope and
permissions. Use existing outbox/Identity/SMTP/Data Protection; no external service
or new page. Store encrypted payloads and hashes, never raw tokens in logs.

UI: reuse a compact shared delivery-status partial showing queued, retry,
delivered, activated, expiry or attention states with real timestamps. Existing
admission actions remain permission-aware; guardian resend uses the existing
Account endpoint and generic response. Never show a token or protected payload.

Desktop retains the application overview and workflow columns; tablet/mobile
stack the same cards. Labels, announced status, real empty states and shared
submit feedback are required. Forms retain CSRF and backend checks. Browser
viewport/focus/contrast acceptance must be recorded separately.

Delivery is at-least-once: a crash after SMTP acceptance but before recording
success can cause the same link to be resent. Stable one-time tokens and leases
make duplicates safe; no exactly-once delivery claim. Expired, superseded,
activated, disabled or changed-email recipients must not receive queued mail.
Production requires a configured trusted ActivationDelivery:PublicOrigin and a
durable shared Data Protection key ring. SMTP credentials stay external.

Tests cover real SQL queue creation, protected payload, retry/backoff, lease
recovery, canonical origin, expiry, consumption, audit and the real dispatcher.
