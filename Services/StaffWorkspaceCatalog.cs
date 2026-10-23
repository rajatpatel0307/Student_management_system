namespace Student_Management_System.Services;

public sealed record StaffPage(string Slug, string Title, string? Href = null);
public sealed record StaffWorkspace(string Key, string Role, string Title, string Description, string Icon, StaffPage[] Pages);

public static class StaffWorkspaceCatalog
{
    public static readonly StaffWorkspace[] All =
    [
        new("librarian", "Librarian", "Librarian", "Keep knowledge moving. Manage your collection, circulation and members.", "LibraryBig", [
            new("books", "Book Management", "/Library"), new("add-book", "Add Book", "/Library/Create"), new("edit-book", "Edit Book", "/Library"),
            new("categories", "Book Categories", "/Library"), new("authors", "Authors"), new("publishers", "Publishers"),
            new("inventory", "Book Inventory", "/Library"), new("issue", "Issue Book", "/Library"), new("return", "Return Book", "/Library"),
            new("renew", "Renew Book", "/Library"), new("overdue", "Overdue Books", "/Library"), new("members", "Student/Member Management"),
            new("transactions", "Library Transactions", "/Library"), new("fines", "Fines & Penalties", "/Library"), new("reports", "Library Reports", "/Reports?ReportType=Library")]),
        new("transport", "TransportManager", "Transport Manager", "Safe journeys, every day. Coordinate your fleet and campus transport.", "Bus", [
            new("vehicles", "Vehicle Management", "/Operations"), new("add-vehicle", "Add Vehicle", "/Operations"), new("edit-vehicle", "Edit Vehicle", "/Operations"),
            new("drivers", "Driver Management"), new("routes", "Route Management", "/Operations"), new("stops", "Stops Management"),
            new("allocations", "Student Transport Allocation", "/Operations"), new("tracking", "Vehicle Tracking"), new("trips", "Trip Management"),
            new("attendance", "Attendance"), new("fuel", "Fuel Management"), new("maintenance", "Maintenance"), new("expenses", "Transport Expenses"),
            new("complaints", "Complaints"), new("reports", "Transport Reports")]),
        new("hostel", "HostelManager", "Hostel Warden", "A better place to belong. Oversee accommodation and daily resident care.", "Building2", [
            new("hostels", "Hostel Management", "/Operations"), new("buildings", "Building Management", "/Operations"), new("rooms", "Room Management", "/Operations"),
            new("beds", "Bed Management"), new("allocations", "Student Allocation", "/Operations"), new("transfers", "Room Transfer"),
            new("attendance", "Hostel Attendance"), new("leave", "Leave Management"), new("visitors", "Visitor Management"),
            new("complaints", "Hostel Complaints"), new("maintenance", "Maintenance Requests"), new("mess", "Mess Management"),
            new("fees", "Hostel Fees"), new("notices", "Hostel Notices"), new("reports", "Hostel Reports")]),
        new("registrar", "Registrar", "Registrar", "Trusted records, seamless journeys. Manage academic requests and verification.", "GraduationCap", [
            new("students", "Student Records"), new("registration", "Student Registration", "/Admissions/Create"), new("admissions", "Admission Management", "/Admissions"),
            new("courses", "Course Management"), new("departments", "Department Management"), new("enrollment", "Enrollment Management"),
            new("documents", "Student Documents", "/Admissions"), new("certificates", "Certificates"), new("transfer-certificates", "Transfer Certificates"),
            new("bonafide", "Bonafide Certificates"), new("id-cards", "ID Card Management"), new("academic-records", "Academic Records"),
            new("attendance", "Attendance Records"), new("promotions", "Promotion Management"), new("requests", "Requests & Applications"),
            new("notices", "Notices"), new("reports", "Reports")]),
        new("accountant", "Accountant", "Accountant", "Clarity in every transaction. Manage collections and financial operations.", "Wallet", [
            new("fees", "Fee Management", "/Finance/Invoices"), new("student-fees", "Student Fees", "/Finance"), new("collection", "Fee Collection", "/Finance/Collect"),
            new("pending", "Pending Fees", "/Finance"), new("receipts", "Fee Receipts", "/Finance/Ledger"), new("payments", "Payment History", "/Finance/Ledger"),
            new("expenses", "Expenses"), new("expense-categories", "Expense Categories"), new("salary", "Salary Management"), new("payroll", "Payroll"),
            new("vendors", "Vendor Payments"), new("refunds", "Refunds", "/Finance"), new("transactions", "Transactions", "/Finance/Ledger"),
            new("accounts", "Accounts"), new("income", "Income"), new("reports", "Reports", "/Reports?ReportType=Finance"),
            new("financial-reports", "Financial Reports", "/Reports?ReportType=Finance"), new("tax", "Tax/Financial Records")])
    ];

    public static StaffWorkspace? Find(string key) => All.SingleOrDefault(w => w.Key == key);
}
