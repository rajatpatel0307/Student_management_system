# Page 02: Student creation and editing

Status: existing design retained with a connected safety/interaction pass.
This is not a claim of completed visual or authenticated acceptance.

## Purpose, users, and reference pattern

Create an Identity-linked student with a valid academic placement and activation
invitation; edit existing profile/placement while synchronizing identity and
enrollment history. Admin/SuperAdmin use the existing creation/edit forms. HOD
may create in its department; its narrower authoritative profile editor remains
HodManagement.EditStudent with the existing Profile.Student.Edit policy.

Use a sectioned administration form: personal information, academic placement,
and account/photo guidance, with a clear submit/cancel action pair. Reuse EduFlow
form sections, validation, buttons, shared shell and existing edit sticky actions.
Use dependent selection rather than a new enrollment wizard or duplicate page.

## Navigation, hierarchy, and components

Routes: GET/POST /Students/Create; GET/POST /Students/Edit/{id}; existing HOD route
unchanged. Shared role header/sidebar and breadcrumbs remain. Create cancels to
the directory; Edit cancels to the canonical profile. No new APIs or pages.

One page heading -> validation summary -> personal fields (name, birth date,
email, phone, address) -> department/semester/class/section selects -> optional
validated private photo -> activation guidance (Create) -> submit/cancel actions.
Keep field labels and inline errors. Creation now loads client validation and
uses the same dependent lookup attributes as editing. Initial selector values
still come from server-rendered SelectLists, retaining ordinary form use without
JavaScript; the server validates every submitted relationship.

Department changes reset semester/class; class changes reset section. The shared
handler immediately clears descendants while fetching, disables pending fields,
and prevents an obsolete response from replacing newer options or busy state.
Errors disable the invalid choices and give retry guidance. Submit remains
available on a rerendered Create form even when its values are unchanged.

## State, responsive, and accessibility requirements

| State | Required behavior |
| --- | --- |
| Loading | Existing submit feedback; dependent selects disabled with aria-busy |
| No academic options | Existing placeholders; server error for invalid class/section; do not invent options |
| Missing campus | Explain assignment requirement before storing identity or a photo |
| Validation/upload error | Preserve submitted values; display summary and field messages |
| Successful create | Redirect to directory; show invitation success or visible pending-delivery warning |
| Successful edit | Redirect to profile; existing success feedback |
| Permission denied | Existing role/policy/permission filters; HOD cannot submit another department |

Desktop: existing centered form card/edit shell, paired fields, section headings.
Tablet: two-column fields within available space and wrapping actions.
Mobile: Bootstrap col-md-6 fields stack; edit sticky actions remain; labels/errors
stay with controls. Use native labeled controls, autocomplete/input types,
role=alert summaries, required cues, keyboard focus, accessible upload help and
44px targets. Check light/dark, 200% zoom and mobile sticky action overlap in
browser acceptance; these checks have not been run.

## Backend contracts and data integrity

- Existing authenticated /api/lookups/classes?departmentId,
  /semesters?departmentId and /sections?classId supply dependent options.
- StudentPlacementRules and the controller's EF validation are shared by direct
  Create/Edit; admissions enrollment also reuses the placement rule.
- Reject absent/archived classes and sections, cross-class sections,
  department/class mismatches, and invalid/cross-department semesters before
  identity or private-file writes. Optional department remains supported for
  existing unassigned records; a supplied semester is checked against the class
  department even if DepartmentId was omitted.
- Direct Create obtains an authorized active default campus through
  ICampusScopeService and writes the new membership inside the existing identity,
  Student, and enrollment transaction. It assigns no arbitrary submitted campus.
- Subject enrollment no longer silently depends on a current academic year.
  Optional class-year history keeps its existing setup semantics.
- Edit binds only fields that the existing form/command supports; no silent
  acceptance of extra admission/program fields that were never persisted.
- Existing CSRF, upload limits/re-encoding, file compensation, identity checks,
  transaction boundaries, row-version handling, and audit flow remain.

## Files, checks, and remaining work

This pass changes Controllers/StudentsController.cs,
Services/StudentPlacementRules.cs, Views/Students/Create.cshtml,
wwwroot/js/site.js, and Views/Shared/_Layout.cshtml (visible Warning feedback).
Related profile account status and archive validation changes are recorded in
the audit. Regression checks live in StaffTests/StudentDirectoryVerification.cs
and Tests/DependentSelects.test.mjs.

See [actual verification](../audit/VERIFICATION.md). Live identity/enrollment
transactions, invitation delivery, selector navigation in a real browser, and
viewport/accessibility acceptance are pending. Historical student campus data
is not automatically backfilled. Broad service extraction, invitation outbox
delivery, profile-history pagination, and the archive transaction remain explicit
follow-up findings in the audit.
