using System.ComponentModel.DataAnnotations;

namespace Student_Management_System.Models;

public class AuditLog
{
    public long Id { get; set; }
    [MaxLength(450)] public string? UserId { get; set; }
    [Required, MaxLength(100)] public string Action { get; set; } = "";
    [Required, MaxLength(100)] public string EntityName { get; set; } = "";
    [MaxLength(100)] public string? EntityId { get; set; }
    [MaxLength(64)] public string? IpAddress { get; set; }
    [MaxLength(36)] public string? CorrelationId { get; set; }
    [MaxLength(10)] public string? HttpMethod { get; set; }
    [MaxLength(500)] public string? RequestPath { get; set; }
    [MaxLength(500)] public string? UserAgent { get; set; }
    [MaxLength(40)] public string Outcome { get; set; } = "Succeeded";
    public string? DetailsJson { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
