using System.ComponentModel.DataAnnotations;

namespace Student_Management_System.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Enter your email or username.")]
    [StringLength(256)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Enter your password.")]
    [StringLength(1024)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}
