using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;

namespace Student_Management_System.Services;

public interface IApprovalTargetValidator
{
    Task<bool> ExistsAsync(string entityType, string entityId, CancellationToken cancellationToken = default);
}

public sealed class ApprovalTargetValidator : IApprovalTargetValidator
{
    private readonly ApplicationDbContext _db;
    public ApprovalTargetValidator(ApplicationDbContext db) => _db = db;

    public async Task<bool> ExistsAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(entityId, out var id) || id <= 0) return false;
        return entityType switch
        {
            nameof(Student) => id <= int.MaxValue && await _db.Students.AnyAsync(x => x.Id == (int)id && !x.IsArchived, cancellationToken),
            nameof(FinancialTransaction) => await _db.FinancialTransactions.AnyAsync(x => x.Id == id, cancellationToken),
            nameof(StudentAidAward) => await _db.StudentAidAwards.AnyAsync(x => x.Id == id, cancellationToken),
            nameof(Exam) => id <= int.MaxValue && await _db.Exams.AnyAsync(x => x.Id == (int)id, cancellationToken),
            nameof(Mark) => id <= int.MaxValue && await _db.Marks.AnyAsync(x => x.Id == (int)id, cancellationToken),
            nameof(Attendance) => id <= int.MaxValue && await _db.Attendances.AnyAsync(x => x.Id == (int)id, cancellationToken),
            nameof(PromotionBatch) => await _db.PromotionBatches.AnyAsync(x => x.Id == id, cancellationToken),
            nameof(LeaveRequest) => await _db.LeaveRequests.AnyAsync(x => x.Id == id, cancellationToken),
            nameof(AdmissionDocument) => await _db.AdmissionDocuments.AnyAsync(x => x.Id == id, cancellationToken),
            nameof(AdmissionApplication) => await _db.AdmissionApplications.AnyAsync(x => x.Id == id && !x.IsArchived, cancellationToken),
            _ => false
        };
    }
}
