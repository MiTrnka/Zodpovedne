// DTO - objekty slouží pro transfer dat mezi klientem (PostMan, aplikace...) a API
using System.ComponentModel.DataAnnotations;

namespace Zodpovedne.Contracts.DTO;

public class RegisterModelDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    [StringLength(100)]
    public string Password { get; set; } = "";

    [Required]
    public string Nickname { get; set; } = "";
}

public class UserProfileDto
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string Nickname { get; set; } = "";
    public DateTime Created { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class LoginModelDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}

public class UpdateNicknameDto
{
    [Required]
    public string Nickname { get; set; } = "";
}

public class ChangePasswordModelDto
{
    [Required]
    public string CurrentPassword { get; set; } = "";

    [Required]
    [StringLength(100)]
    public string NewPassword { get; set; } = "";
}

public class TokenResponseDto
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public string Email { get; set; } = "";
    public string Nickname { get; set; } = "";
}