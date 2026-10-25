# Executed role-boundary matrix

A = production authorization policy allowed; D = real HTTP role denial verified. Allowed CRUD is not executed by this matrix. Separate integration tests verify selected successful workflows and row scope.

| Route | SuperAdmin | Admin | HOD | Professor | Student | Guardian | Accountant | Librarian | AdmissionOfficer | ExaminationOfficer | TransportManager | HostelManager | Registrar | PlacementOfficer | Employee | Anonymous |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| GET /SuperAdmin | A | D | D | D | D | D | D | D | D | D | D | D | D | D | D | 401 |
| GET /Staff | A | A | D | D | D | D | D | D | D | D | D | D | D | D | D | 401 |
| GET /api/staff | A | A | D | D | D | D | D | D | D | D | D | D | D | D | D | 401 |
| GET /Students | A | A | A | D | D | D | D | D | D | D | D | D | D | D | D | 401 |
| POST /Students/Delete/1 | A | A | D | D | D | D | D | D | D | D | D | D | D | D | D | 401 |
| GET /Guardians | A | A | D | D | D | D | D | D | D | D | D | D | D | D | D | 401 |
| GET /Admissions | A | A | D | D | D | D | D | D | A | D | D | D | A | D | D | 401 |
| GET /Finance | A | A | D | D | D | D | A | D | D | D | D | D | D | D | D | 401 |
| GET /Mark/Create | D | D | A | A | D | D | D | D | D | D | D | D | D | D | D | 401 |
| GET /StudentDashboard | D | D | D | D | A | D | D | D | D | D | D | D | D | D | D | 401 |
| GET /ProfessorDashboard | D | D | D | A | D | D | D | D | D | D | D | D | D | D | D | 401 |
| GET /HodDashboard | D | D | A | D | D | D | D | D | D | D | D | D | D | D | D | 401 |
| GET /GuardianDashboard | D | D | D | D | D | A | D | D | D | D | D | D | D | D | D | 401 |
