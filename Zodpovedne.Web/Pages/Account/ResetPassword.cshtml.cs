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

            [Required(ErrorMessage = "Nové heslo je povinné")]
            [DataType(DataType.Password)]
            public string NewPassword { get; set; } = "";

            [DataType(DataType.Password)]
            [Compare("NewPassword", ErrorMessage = "Hesla se neshodují.")]
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
            // Naplnìní modelu z query parametrù
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
                // Vytvoøení DTO objektu pro API
                var resetDto = new ResetPasswordDto
                {
                    Email = Input.Email,
                    Token = Input.Token,
                    NewPassword = Input.NewPassword
                };

                // Volání API
                var client = _clientFactory.CreateClient();
                var response = await client.PostAsJsonAsync($"{ApiBaseUrl}/users/reset-password", resetDto);

                if (response.IsSuccessStatusCode)
                {
                    Success = true;
                    return Page();
                }

                // Pokud nastala chyba
                ErrorMessage = "Neplatný nebo vypršený odkaz pro obnovení hesla. Zkuste znovu zažádat o obnovení hesla.";
                return Page();
            }
            catch (Exception ex)
            {
                _logger.Log("Chyba pøi obnovení hesla", ex);
                ErrorMessage = "Došlo k chybì pøi obnovení hesla. Zkuste to prosím pozdìji.";
                return Page();
            }
        }
    }
}