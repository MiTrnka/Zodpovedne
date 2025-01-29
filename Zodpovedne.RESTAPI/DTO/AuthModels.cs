// DTO - objekty slouží pro transfer dat mezi klientem (PostMan, aplikace...) a API
using System.ComponentModel.DataAnnotations;

namespace Zodpovedne.Models;

public class RegisterModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    [StringLength(100)]
    public string Password { get; set; } = "";

    [Required]
    public string FirstName { get; set; } = "";

    [Required]
    public string LastName { get; set; } = "";
}

public class LoginModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}

public class UpdateProfileModel
{
    [Required]
    public string FirstName { get; set; } = "";

    [Required]
    public string LastName { get; set; } = "";
}

public class ChangePasswordModel
{
    [Required]
    public string CurrentPassword { get; set; } = "";

    [Required]
    [StringLength(100)]
    public string NewPassword { get; set; } = "";
}

public class TokenResponse
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}