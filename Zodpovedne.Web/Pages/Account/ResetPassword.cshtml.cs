using Ganss.Xss;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Logging;
using Zodpovedne.Web.Models.Base;

namespace Zodpovedne.Web.Pages.Account
{
    public class ResetPasswordModel : BasePageModel
    {
        public class ResetPasswordInput
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = "";

            [Required]
            public string Token { get; set; } = "";

            [Required(ErrorMessage = "Nov� heslo je povinn�")]
            [DataType(DataType.Password)]
            public string NewPassword { get; set; } = "";

            [DataType(DataType.Password)]
            [Compare("NewPassword", ErrorMessage = "Hesla se neshoduj�.")]
            public string ConfirmPassword { get; set; } = "";
        }

        [BindProperty]
        public ResetPasswordInput Input { get; set; } = new();

        public bool Success { get; set; } = false;

        public ResetPasswordModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer)
            : base(clientFactory, configuration, logger, sanitizer)
        {
        }

        public void OnGet(string email, string token)
        {
            // Napln�n� modelu z query parametr�
            Input.Email = email;
            Input.Token = token;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                // Vytvo�en� DTO objektu pro API
                var resetDto = new ResetPasswordDto
                {
                    Email = Input.Email,
                    Token = Input.Token,
                    NewPassword = Input.NewPassword
                };

                // Vol�n� API
                var client = _clientFactory.CreateClient();
                var response = await client.PostAsJsonAsync($"{ApiBaseUrl}/users/reset-password", resetDto);

                if (response.IsSuccessStatusCode)
                {
                    Success = true;
                    return Page();
                }

                // Pokud nastala chyba
                ErrorMessage = "Neplatn� nebo vypr�en� odkaz pro obnoven� hesla. Zkuste znovu za��dat o obnoven� hesla.";
                return Page();
            }
            catch (Exception ex)
            {
                _logger.Log("Chyba p�i obnoven� hesla", ex);
                ErrorMessage = "Do�lo k chyb� p�i obnoven� hesla. Zkuste to pros�m pozd�ji.";
                return Page();
            }
        }
    }
}