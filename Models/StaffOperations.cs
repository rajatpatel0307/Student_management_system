using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Student_Management_System.Models;

public sealed class RegistrarCertificate
{
    public long Id { get; set; }
    public int CampusId { get; set; }
    public Campus Campus { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    [Required, MaxLength(40)] public string CertificateType { get; set; } = "Bonafide";
    [MaxLength(40)] public string? CertificateNumber { get; set; }
    [Required, MaxLength(1000)] public string Purpose { get; set; } = "";
    [MaxLength(160)] public string? EvidenceReference { get; set; }
    [Required, MaxLength(30)] public string Status { get; set; } = "Draft";
    [Required, MaxLength(450)] public string CreatedByUserId { get; set; } = "";
    [MaxLength(450)] public string? ReviewedByUserId { get; set; }
    [MaxLength(1000)] public string? ReviewComment { get; set; }
    [MaxLength(450)] public string? IssuedByUserId { get; set; }
    public DateTime? IssuedUtc { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public sealed class StaffAttendanceRecord
{
    public long Id { get; set; }
    public int CampusId { get; set; }
    public Campus Campus { get; set; } = null!;
    [Required, MaxLength(40)] public string Workspace { get; set; } = "";
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public DateTime AttendanceDate { get; set; }
    [Required, MaxLength(20)] public string Status { get; set; } = "Present";
    [MaxLength(500)] public string? Notes { get; set; }
    [Required, MaxLength(450)] public string RecordedByUserId { get; set; } = "";
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public sealed class TransportTrip
{
    public long Id { get; set; }
    public int TransportVehicleId { get; set; }
    public TransportVehicle TransportVehicle { get; set; } = null!;
    public int TransportDriverId { get; set; }
    public TransportDriver TransportDriver { get; set; } = null!;
    public int TransportRouteId { get; set; }
    public TransportRoute TransportRoute { get; set; } = null!;
    public DateTime ScheduledOn { get; set; }
    [Required, MaxLength(5)] public string DepartureTime { get; set; } = "08:00";
    [Required, MaxLength(20)] public string Status { get; set; } = "Planned";
    [MaxLength(1000)] public string? Notes { get; set; }
    [Required, MaxLength(450)] public string CreatedByUserId { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public sealed class VehicleFuelLog
{
    public long Id { get; set; }
    public int TransportVehicleId { get; set; }
    public TransportVehicle TransportVehicle { get; set; } = null!;
    public DateTime FilledOn { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal Litres { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal UnitPrice { get; set; }
    public int OdometerKm { get; set; }
    [MaxLength(100)] public string? Reference { get; set; }
    [Required, MaxLength(450)] public string RecordedByUserId { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}
