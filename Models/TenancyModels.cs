using System.ComponentModel.DataAnnotations;

namespace Student_Management_System.Models;

public sealed class UserCampusMembership
{
    public long Id { get; set; }
    [Required, MaxLength(450)] public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public int CampusId { get; set; }
    public Campus Campus { get; set; } = null!;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime GrantedUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(450)] public string? GrantedByUserId { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}
