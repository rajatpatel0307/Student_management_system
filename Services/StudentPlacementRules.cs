using Student_Management_System.Models;

namespace Student_Management_System.Services;

public static class StudentPlacementRules
{
    public static IReadOnlyList<(string Field, string Message)> Validate(
        Student student, Class? placement, Section? section, Semester? semester)
    {
        var errors = new List<(string, string)>();
        if (placement == null || placement.IsArchived)
            errors.Add((nameof(Student.ClassId), "Select an active class."));
        if (section == null || section.IsArchived || section.ClassId != student.ClassId)
            errors.Add((nameof(Student.SectionId), "Select an active section from the selected class."));
        if (student.DepartmentId.HasValue && placement?.DepartmentId != student.DepartmentId)
            errors.Add((nameof(Student.DepartmentId), "The selected department must match the class department."));
        var departmentId = student.DepartmentId ?? placement?.DepartmentId;
        if (student.SemesterId.HasValue && (semester == null || semester.IsArchived ||
            (departmentId.HasValue && semester.DepartmentId != departmentId)))
            errors.Add((nameof(Student.SemesterId), "Select an active semester from the student's department."));
        return errors;
    }
}
