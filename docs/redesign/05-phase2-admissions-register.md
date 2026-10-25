# Admissions register continuation specification

Route `/Admissions/Index`, existing SuperAdmin/Admin/AdmissionOfficer/Registrar
roles plus `admission.view`; create/review actions retain separate permissions.
Purpose: navigate the complete campus-scoped application pipeline, replacing the
silent 250-record ceiling with server pagination. Existing applications, stages,
metrics, inquiry capture and detail/edit navigation are preserved.

Design: existing EduFlow header and metrics, labeled search/stage/year filters,
page-size selector, semantic scrollable register, reusable server pager, then
inquiry capture. Desktop uses the existing grid; mobile stacks controls and keeps
table scrolling inside its container. Tables need captions and scoped headers;
actions have applicant-specific accessible labels. Create/Edit controls reflect
their real permissions without changing the backend policy.

States: real SQL metrics across the filtered set; empty results show filter reset
guidance; success/error TempData and shared loading feedback remain; forbidden
access is server enforced. Stable sorting uses update timestamp plus record ID.
Page links/page-size changes preserve search, stage and academic-year filters.

Dependencies: AdmissionsController, AdmissionDashboardViewModel, ListQuery,
PagedResult, existing pagination/page-size partials, campus scope and permissions;
AdmissionApplications, academic years/programs and inquiries. Inquiry rows have
no campus column; non-global users must see only their assigned inquiry rows.
No schema change, fake data or new endpoint is needed.

Acceptance must include real SQL paging beyond 250 records, filter preservation,
permission controls, inquiry isolation and Razor rendering. Browser viewport and
keyboard acceptance must remain pending if the connector cannot run.
