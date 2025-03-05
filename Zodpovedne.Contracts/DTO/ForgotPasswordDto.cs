using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodpovedne.Contracts.DTO;

public class ForgotPasswordDto
{
    [Required(ErrorMessage = "Email je povinný")]
    [EmailAddress(ErrorMessage = "Neplatný formát emailu")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "URL pro obnovení hesla je povinná")]
    public string ResetPageUrl { get; set; } = "";
}

public class ResetPasswordDto
{
    [Required(ErrorMessage = "Email je povinný")]
    [EmailAddress(ErrorMessage = "Neplatný formát emailu")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Token je povinný")]
    public string Token { get; set; } = "";

    [Required(ErrorMessage = "Nové heslo je povinné")]
    public string NewPassword { get; set; } = "";
}