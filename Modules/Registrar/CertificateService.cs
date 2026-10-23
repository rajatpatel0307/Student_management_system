using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;

namespace Student_Management_System.Modules.Registrar;

public sealed class CertificateService(ApplicationDbContext db, AuditService audit)
{
    public async Task ReviewAsync(RegistrarCertificate certificate, ReviewWorkInput input, string actor)
    {
        if (string.IsNullOrWhiteSpace(input.Comment) || input.Comment.Length > 1000) throw new StaffValidationException("A review comment of up to 1,000 characters is required.");
        var valid = (certificate.Status, input.Status) switch
        {
            ("Draft", "Verified" or "Rejected") => true,
            ("Verified", "Approved" or "Rejected") => true,
            ("Approved", "Issued") => true,
            _ => false
        };
        if (!valid) throw new StaffValidationException("Certificates must be verified, approved and then issued.");
        if (!Convert.FromBase64String(input.RowVersion).SequenceEqual(certificate.RowVersion)) throw new DbUpdateConcurrencyException();
        if (input.Status == "Approved" && (certificate.CreatedByUserId == actor || certificate.ReviewedByUserId == actor))
            throw new StaffValidationException("A different authorized registrar or administrator must approve this certificate.");
        if (input.Status == "Verified")
        {
            if (string.IsNullOrWhiteSpace(certificate.EvidenceReference)) throw new StaffValidationException("Add an evidence reference before verifying this certificate.");
            if (await db.AdmissionDocuments.AnyAsync(d => d.AdmissionApplication.EnrolledStudentId == certificate.StudentId && d.Status != DocumentVerificationStatus.Verified))
                throw new StaffValidationException("Verify all admission documents for this student before verifying the certificate.");
        }
        certificate.Status = input.Status; certificate.ReviewComment = input.Comment.Trim(); certificate.ReviewedByUserId = actor; certificate.UpdatedUtc = DateTime.UtcNow;
        if (input.Status == "Issued")
        {
            certificate.IssuedUtc = DateTime.UtcNow;
            certificate.IssuedByUserId = actor;
            certificate.CertificateNumber = $"CERT-{DateTime.UtcNow:yyyy}-{certificate.Id:D8}";
        }
        audit.Add($"{input.Status} registrar certificate", nameof(RegistrarCertificate), certificate.Id,
            detailsJson: System.Text.Json.JsonSerializer.Serialize(new { input.Comment, certificate.CertificateType }));
        await db.SaveChangesAsync();
    }
}
