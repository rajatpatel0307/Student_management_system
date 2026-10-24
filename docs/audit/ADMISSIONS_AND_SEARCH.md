# Connected admissions and search audit

Recorded 24 October 2026 after the student safety pass, before the additional
changes below. These modules are the first continuation of the project audit;
this document is not a claim of full application completion.

| ID | Severity | Evidence / affected file | Recommended change |
| --- | --- | --- | --- |
| A01 | High | AdmissionsController.Enroll validates section ownership, but does not revalidate active class/department at enrollment; it omits semester, program, and subject enrollments when creating Student | Validate the current academic placement before writing; transfer program/semester and class subjects atomically |
| A02 | Medium | AdmissionsController.Index shows only Take(250) applications while its Total counts all matches | Introduce server-side pagination in the admissions page redesign; do not pretend the returned subset is the complete register |
| A03 | Medium | AdmissionsController.Index reads open AdmissionInquiries without campus criteria; AdmissionInquiry has no CampusId | Determine inquiry ownership/migration before enforcing a new data model. Preserve records; no speculative deletion or reassignment |
| A04 | Medium | AdmissionsController.TransitionTo calls reason.Trim() while the form calls its comment optional | Accept null/blank comments for normal transitions; require a reason for exceptional outcomes |
| A05 | Medium | Application views show edit/review/approval controls without testing action-specific permission overrides | Keep server checks; make the admissions redesign permission-aware per action; include denied states |
| Q01 | High | SearchController searches admission applications without the campus filter used by AdmissionsController.Index | Apply allowed campus IDs before projecting names/emails/stage metadata |
| Q02 | High | SearchController student HOD filter checks department only, while the fixed directory/profile require shared active campuses | Reuse StudentRecordScope.ForHod; missing department or identity must produce no rows |
| Q03 | High | SearchController guardian student and finance queries omit UnlinkedUtc; its finance query already requires CanViewFinance | Exclude unlinked records before rendering search results |
| Q04 | Medium | SearchController student search admits Admin by role without student.view while StudentsController enforces its override | Mirror the existing Admin override boundary in the search result projection |

Admissions routes, policies, workflow transitions, private document reads/writes,
enrollment, activation resend, form validation, and the input model were inspected.
The page needs a separate specification/redesign after these contract fixes.
Do not merge application workflow history into the student directory or remove
the registrar staff workspace, which has a different grant/navigation model.

Planned incremental work: fix A01/A04/Q01-Q04; retain A02/A03/A05 for the admissions
page pass. Student campus membership and enrollment return navigation were fixed
in the preceding connected patch (S03/S09). Test with build, SQL translation,
controller regression checks, and real role/campus acceptance when available.

## Implementation outcome

A01/A04/Q01-Q04 are implemented. Enrollment revalidates placement, preserves
program/semester, initializes class-subject enrollments and campus membership in
the existing transaction, and returns to application details. Search retains
library/subject behavior while narrowing student/admission/guardian projections.
A02/A03/A05 remain open. No inquiry ownership migration or page deletion was made.
Builds and shared scope/SQL regression assertions pass; enrollment concurrency,
role navigation, real SQL data and admissions visual acceptance remain pending.
