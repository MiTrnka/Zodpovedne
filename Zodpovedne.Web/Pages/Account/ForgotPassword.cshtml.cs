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
            // Z�kladn� zobrazen� formul��e
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Nastaven� URL pro str�nku s resetem hesla
            Input.ResetPageUrl = $"{BaseUrl}Account/ResetPassword";

            // Mus�me manu�ln� znovu validovat model
            ModelState.Clear();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                // Vol�n� API
                var client = _clientFactory.CreateClient();
                var response = await client.PostAsJsonAsync($"{ApiBaseUrl}/users/forgot-password", Input);

                // Bez ohledu na to, zda e-mail existuje, zobraz�me �sp�nou zpr�vu (z bezpe�nostn�ch d�vod�)
                EmailSent = true;

                return Page();
            }
            catch (Exception ex)
            {
                _logger.Log("Chyba p�i odes�l�n� ��dosti o obnoven� hesla", ex);
                ErrorMessage = "Do�lo k chyb� p�i zpracov�n� ��dosti. Zkuste to pros�m pozd�ji.";
                return Page();
            }
        }
    }
}