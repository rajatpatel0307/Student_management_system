namespace Student_Management_System.Security;

public static class ApplicationRoles
{
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly<string>(
    [
        "SuperAdmin", "Admin", "HOD", "Professor", "Student", "Guardian",
        "Accountant", "Librarian", "AdmissionOfficer", "ExaminationOfficer",
        "TransportManager", "HostelManager", "Registrar", "PlacementOfficer", "Employee"
    ]);
}
