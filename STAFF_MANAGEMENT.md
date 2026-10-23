# Staff Management — native site integration

The module now uses the existing **ASP.NET Core MVC, Razor, Bootstrap, Font Awesome,
and EduFlow CSS/JavaScript** stack. There is no frontend package installation,
TypeScript compilation, Tailwind build, or separate application shell.

## Open the pages

The corrected local instance is running at **https://localhost:5292/Staff**.
Sign in with an existing administrator account to open the directory.

- Admin Dashboard and Super Admin Dashboard both have a **Manage staff** card.
- The administration sidebar contains **Staff Management**.
- Directory: `/Staff` (also `/Staff/Index`).
- Add staff: `/Staff/Create`.
- View and manage an account: `/Staff/Details/{id}`.
- Edit staff: `/Staff/Edit/{id}`.
- Role dashboards: `/Staff/Workspace/librarian`, `/Staff/Workspace/transport`,
  `/Staff/Workspace/hostel`, `/Staff/Workspace/registrar`, `/Staff/Workspace/accountant`.
- Operational pages use normal URLs: `/Staff/Workspace/{workspace}/{page}`.
  For example, `/Staff/Workspace/transport/vehicles`.
- Personal tools: `/Staff/Personal/messages`, `/Staff/Personal/activity`,
  `/Staff/Personal/settings`, `/Staff/Personal/help`.

Staff login and the old dashboard URLs redirect to the appropriate workspace.
The shared sidebar shows the role's pages and personal tools. Existing profile,
password, notifications and logout pages retain their normal site behavior.

## Visibility fix and database setup

Previously the module loaded a separate frontend bundle and the new migrations
had not been applied to the local application database. The replacement uses
server-rendered Razor directory/forms and the same shared layout as other pages.
The dashboards and operational tables use small plain-JavaScript requests to the
existing protected staff APIs.

Development startup now honors the existing
`DatabaseBootstrap:ApplyMigrationsOnStartup=true` setting for **only** these two
additive migrations when they are the only pending migrations:

1. `20261023122648_StaffManagementWorkspaces`
2. `20261023124049_StaffOperationalRegisters`

Both were applied successfully during the corrected local launch. The application
reported successful startup and `/health` returned HTTP 200. No existing passwords
were changed. In Production, migrations remain a controlled deployment step:

```powershell
dotnet tool restore
dotnet ef database update --project Student_Management_System.csproj
dotnet build Student_Management_System.csproj
```

Use the installation's protected migration connection. If staff migrations are
still pending, the directory shows a setup explanation rather than a blank shell.
If other migrations are pending, Development startup does not silently apply them.

## Implemented workflows

- Server-rendered staff directory: search, role/status filters, paging and CSV export.
- Full native create/edit form, private profile photos, department, employment,
  contact, demographic, emergency and notes fields.
- Native staff detail page with activation/deactivation, soft deletion, password
  reset, permission controls, login history and activity history.
- One primary Identity role per managed account. `HostelManager` is displayed as
  **Hostel Warden**. Role changes revoke sessions and clear previous overrides.
- Explicit additional workspace grants; explicit denials and action permissions
  remain enforced by the backend. Existing MVC workflows keep their own role and
  permission requirements; a grant does not impersonate another Identity role.
- Five dashboards with eight database-backed metrics each and real page navigation.
- Native Bootstrap record forms/tables for books, categories, authors, publishers,
  fleet/drivers/routes/stops/trips/fuel, hostel buildings/rooms/beds/attendance,
  registrar courses/departments/certificates, and institutional expenses.
- Existing library circulation, transport/hostel allocation, admissions/document
  verification, fee collection, invoices, refunds, receipts and finance reports.
- Certificate verification, independent approval, issuance and printable records.
- Trip compliance checks, unique driver/vehicle departures, calculated fuel costs,
  attendance allocation checks, expense approval and optimistic concurrency.
- Messages, personal activity, accessible labeled forms, Bootstrap modals,
  success/error states, loading states, empty states and pagination.
- Student-linked records, certificates, finance, expenses and requests honor campus
  access. Existing globally shared catalog, fleet and hostel inventory remain shared.
- Secure Identity cookie authentication, CSRF protection, password hashing,
  first-login password rotation and immediate session stamp validation.

Supporting request queues remain for specialized workflows without a full domain
engine: live GPS/provider integration; maintenance and complaints; hostel transfers,
leave, visitors, mess and hostel-specific billing; custom operational reports;
registrar promotion/attendance changes; payroll disbursement, bank/vendor payment
integration, non-fee income and statutory tax filing. Bed inventory does not yet
assign individual beds to residents. Student registration uses admissions/enrollment.
JWT endpoints and cryptographic certificate signatures are not implemented.

## Source and verification

- Views: `Views/Staff` and shared `Views/Shared/_Layout.cshtml`.
- Page routes: `Controllers/StaffController.cs`.
- Native enhancements: `wwwroot/js/staff-native.js` and `staff-resource-schemas.js`.
- Backend: protected `Staff*` APIs, staff services, normalized models and migrations.
- Certificate styling: `wwwroot/css/staff-certificate.css`.

The previous frontend source and generated bundle were moved to the ignored
`.build/retired-staff-react` backup. They are not part of the active application.

```powershell
dotnet build StaffTests/StaffTests.csproj -o .build/staff-native-ready
dotnet .build/staff-native-ready/StaffTests.dll --integration
node --test Tests/StaffCache.test.mjs Tests/StaffNative.test.mjs
```

The integration suite creates and cleans up only its own disposable SQL Express
database. Its checks cover legacy staff migration, Identity/RBAC, operational registers,
authenticated HTTP rendering of the actual Razor directory, staff form, all five
dashboards and role pages, and forbidden cross-role navigation. The updated suite
compiled successfully, but execution was blocked by Windows Application Control
(0x800711C7), so the new authenticated rendering checks have not run.
Fourteen JavaScript tests passed, validating native register schemas and private
response cache exclusion. Live HTTPS checks confirmed the staff routes redirect
unauthenticated requests to login, static assets load, and database health returns 200.
The older sibling release test project is absent from this checkout;
these tests do not replace that missing application-wide suite.

The browser connector returned a sandbox metadata error, so visual browser QA
could not be completed. Existing ImageSharp 4.1.2 now produces a license warning;
configure its required license for the deployment. The secure cookie setting is
preserved, and the local verification server uses the existing validated localhost
HTTPS certificate on port 5292. Restart a running older Visual Studio/IIS application
before expecting its in-memory routes and compiled views to reflect these changes.
