namespace Zodpovedne.Web.Models;

public class TokenResponseDto
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public string Email { get; set; } = "";
}
