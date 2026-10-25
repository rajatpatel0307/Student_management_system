# Shared permission enforcement

Production role names, default grants and named authorization policies now have
shared source definitions consumed by Program and the test host. Values and
fallback authentication rules are preserved. No new administrative grant is
introduced. Per-user explicit denials apply to legacy role-authorized actions,
including SuperAdmin, and action matching is case-insensitive.

Admission create uses admission.create independently of review; published-result
correction uses results.publish; export aliases enforce reports.export themselves.
Attendance creation/correction/approval and report exports use their respective
permissions. A hidden button never substitutes for controller/record checks.

The generated policy matrix evaluates all discovered action methods for every
configured role. PASS means authorization may reach subsequent record/service
guards; it does not certify ownership, MFA or successful CRUD. A second matrix
executes representative denied roles over real local HTTP. Targeted SQL tests
verify specific campus, guardian and student boundaries. Full record-level
acceptance of every controller remains outstanding.
