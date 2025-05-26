using System.ComponentModel.DataAnnotations;

namespace Zodpovedne.Contracts.DTO;

public class LoginModelDto
{
    [Required(ErrorMessage = "Email je povinný")]
    [EmailAddress(ErrorMessage = "Neplatný formát emailu")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Heslo je povinné")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Heslo může mít maximálně 100 znaků")]
    public string Password { get; set; } = "";
}

public class TimeRecordLoginDto
{
    public int Id { get; set; }
    public DateTime LoginTime { get; set; }
    public string IpAddress { get; set; } = "";
}

public class RecordLoginDto
{
    public string UserId { get; set; } = "";
    public string IpAddress { get; set; } = "";
}