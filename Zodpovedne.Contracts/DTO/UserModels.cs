// DTO - objekty slouží pro transfer dat mezi klientem (PostMan, aplikace...) a API
using System.ComponentModel.DataAnnotations;
using Zodpovedne.Contracts.Enums;

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
public class UserListDto
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string Nickname { get; set; } = "";
    public DateTime? LastLogin { get; set; }
    public UserType Type { get; set; }
}

public class PagedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
