using Ganss.Xss;
using Microsoft.AspNetCore.Mvc;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Logging;
using Zodpovedne.Web.Models.Base;

namespace Zodpovedne.Web.Pages.Account
{
    public class ForgotPasswordModel : BasePageModel
    {
        [BindProperty]
        public ForgotPasswordDto Input { get; set; } = new();

        public bool EmailSent { get; set; } = false;

        public ForgotPasswordModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer)
            : base(clientFactory, configuration, logger, sanitizer)
        {
        }

        public void OnGet()
        {
            // Základní zobrazení formuláøe
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Nastavení URL pro stránku s resetem hesla
            Input.ResetPageUrl = $"{BaseUrl}Account/ResetPassword";

            // Musíme manuálnì znovu validovat model
            ModelState.Clear();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                // Volání API
                var client = _clientFactory.CreateClient();
                var response = await client.PostAsJsonAsync($"{ApiBaseUrl}/users/forgot-password", Input);

                // Bez ohledu na to, zda e-mail existuje, zobrazíme úspìšnou zprávu (z bezpeènostních dùvodù)
                EmailSent = true;

                return Page();
            }
            catch (Exception ex)
            {
                _logger.Log("Chyba pøi odesílání žádosti o obnovení hesla", ex);
                ErrorMessage = "Došlo k chybì pøi zpracování žádosti. Zkuste to prosím pozdìji.";
                return Page();
            }
        }
    }
}