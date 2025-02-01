using System.ComponentModel.DataAnnotations;

namespace Zodpovedne.Web.Models;

public class LoginModel
{
    [Required(ErrorMessage = "Email je povinný")]
    [EmailAddress(ErrorMessage = "Neplatný formát emailu")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Heslo je povinné")]
    public string Password { get; set; } = "";
}