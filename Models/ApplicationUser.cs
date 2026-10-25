using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;


public class ApplicationUser : IdentityUser
{
    [Required]
    public string FullName { get; set; } = "";
    [MaxLength(40)]
    public string? PublicUserId { get; set; } = "USR-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
    [MaxLength(16)]
    public string? NormalizedMobileNumber { get; private set; }
    public bool MustChangePassword { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginUtc { get; set; }
    public DateTime? MfaCodeLastSentUtc { get; set; }
    [MaxLength(500)]
    public string? ProfileImageStorageKey { get; set; }
}
