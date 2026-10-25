# Finance and report continuity

Existing routes: `/Finance`, `/Finance/Invoices`, `/Finance/Details/{id}`,
existing collection/adjustment/refund/reversal/issuance commands; `/Reports`,
existing report export actions and `/Reports/StudentsExcel` alias.

Dependencies: FinanceController, ReportingService, ReportsController,
FinancialRecordScope, StudentRecordScope, ledger/invoice services, existing
StudentInvoice/FinancialTransaction/GuardianStudentLink and SavedReport models.
Existing permissions and role restrictions are retained. SuperAdmin can read
global history; administrative/accountant finance access requires active campus
membership; students see their own records, guardians require a currently linked
student and finance flag. Commands independently enforce the record boundary.

Use the existing EduFlow header, metrics, filter card, responsive table and pager.
Invoice columns have semantic headers/caption. Empty filters offer recovery.
Invoice issuance, collection and exports appear only with the respective
permission. Loading/success/error feedback uses existing shell/form behavior.
CSRF and backend authorization remain required regardless of button visibility.
Desktop keeps dense financial tables; tablet/mobile use contained table scrolling
and stacked forms. Do not introduce page-wide horizontal scrolling.

Reports show only role-supported types; an omitted type chooses an allowed
default. Saved filters reload owned/shared definitions, then recalculate against
current permissions. Invalid date ranges/definitions return controlled errors.
Result and attendance reports reuse shared record scopes; low-attendance totals
reuse configured Late/Excused weights. Financial reports use the ledger scope.
Three legacy nonempty registers referenced a nonexistent pagination partial;
finance invoices, communications and promotions now use the actual shared pager.

Acceptance: SQL tests exercise cross-campus denied finance commands without
ledger writes, revoked guardian links, student ownership, report scopes and saved
filter ownership. Native Razor tests cover populated report/invoice/session pages.
No successful new charge/refund is issued against production data. Communications
and promotion page runtime acceptance remains pending. Four viewport widths,
keyboard/focus/contrast/zoom and visual dark-mode acceptance are not run because
the browser connector fails before execution.

Remaining: full finance command-success coverage, additional report tenancy
review, large lookup option limits, and browser acceptance.
