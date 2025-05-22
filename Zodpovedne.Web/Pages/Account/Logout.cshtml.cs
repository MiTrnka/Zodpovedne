using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;
using Zodpovedne.Web.Extensions;
using Ganss.Xss;
using Zodpovedne.Logging.Services;

namespace Zodpovedne.Web.Pages.Account;

public class LogoutModel : BasePageModel
{
    public LogoutModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator) : base(clientFactory, configuration, logger, sanitizer, translator)
    {
    }
    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            // Volání API pro aktualizaci LastLogin, pokud je uživatel pøihlášen
            if (IsUserLoggedIn)
            {
                var client = _clientFactory.CreateBearerClient(HttpContext);
                var result = await client.PostAsync($"{ApiBaseUrl}/users/logout", null);
                if (!result.IsSuccessStatusCode)
                {
                    ErrorMessage = $"Pøi odhlašování nastala chyba: {await result.Content.ReadAsStringAsync()}";
                    return Page();
                }
            }

            // Standardní odhlášení
            await SignedOutIsOK();
            return Page();
        }
        catch (Exception ex)
        {
            logger.Log("Chyba pøi odhlašování", ex);
            ErrorMessage = "Pøi odhlašování nastala chyba.";
            return Page();
        }
    }
}
