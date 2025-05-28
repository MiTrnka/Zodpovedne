using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Logging;
using Ganss.Xss;
using Zodpovedne.Logging.Services;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Pages.Models;

namespace Zodpovedne.Web.Pages.Account;

/// <summary>
/// Model pro pøihlašovací stránku
/// Zajišuje autentizaci pomocí JWT tokenù pro API volání a cookies pro Razor Pages
/// </summary>
public class LoginModel : BasePageModel
{

    public LoginModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator) : base(clientFactory, configuration, logger, sanitizer, translator)
    {
    }

    /// <summary>
    /// URL stránky, ze které uivatel pøišel na pøihlášení
    /// Po úspìšném pøihlášení bude na tuto stránku pøesmìrován
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// Model obsahující pøihlašovací údaje z formuláøe
    /// </summary>
    [BindProperty]
    public LoginModelDto Input { get; set; } = new ();

    public string? ErrorMessageWrongUser { get; set; } = null;

    public void OnGet(string? statusMessage = null)
    {
        if (!string.IsNullOrEmpty(statusMessage))
            this.StatusMessage = statusMessage;
    }

    /// Zpracovává POST poadavek s pøihlašovacími údaji z formuláøe.
    /// Provádí kompletní proces pøihlášení vèetnì:
    /// - Validace vstupních dat (email, heslo)
    /// - Komunikace s REST API pro ovìøení pøihlašovacích údajù
    /// - Získání JWT tokenu z API odpovìdi
    /// - Vytvoøení autentizaèních cookies pro Razor Pages
    /// - Uloení JWT tokenu do session pro další API volání
    /// - Zaznamenání pøihlášení do historie (IP adresa, èas)
    /// - Pøesmìrování na pùvodní stránku nebo vıchozí umístìní
    /// Implementuje hybridní autentizaèní systém kombinující JWT pro API a cookies pro web.
    /// </summary>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Omlouváme se, momentálnì se nelze pøihlásit.";
            return Page();
        }

        // Pøihlášení se, získání JWT tokenu z API
        var client = _clientFactory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"{ApiBaseUrl}/users/login",
            new { Email = Input.Email, Password = Input.Password }
        );

        if (!response.IsSuccessStatusCode)
        {
            // uivatel zadal špatné pøihlašovací údaje
            ErrorMessage = "Neplatné pøihlašovací údaje";
            return Page();
        }

        var result = await response.Content.ReadFromJsonAsync<TokenResponseDto>();
        if (result == null)
        {
            ErrorMessage = "Omlouváme se, momentálnì se nelze pøihlásit.";
            logger.Log($"Nepodaøilo se naèíst pro {Input.Email} token z API");
            return Page();
        }

        // Pøihlášení uivatele pomocí cookie
        await Login(result.Token, result.Nickname);

        // Získání JWT tokenu z obsahu odpovìdi
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(result.Token);

        // Získání userId z JWT tokenu
        var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.NameId)?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            try
            {
                // Získání IP adresy klienta
                string clientIp = Zodpovedne.Web.Extensions.HttpClientFactoryExtensions.GetClientIpAddress(HttpContext);

                // Vytvoøení klienta s autorizaèním tokenem
                var authClient = _clientFactory.CreateBearerClient(HttpContext);

                // Zaznamenání pøihlášení uivatele
                await authClient.PostAsJsonAsync(
                    $"{ApiBaseUrl}/users/record-login",
                    new RecordLoginDto
                    {
                        UserId = userId,
                        IpAddress = clientIp
                    }
                );
            }
            catch (Exception ex)
            {
                // Logování chyby, ale pokraèujeme, i kdy se nepodaøí zaznamenat pøihlášení
                logger.Log("Chyba pøi zaznamenávání pøihlášení", ex);
            }
        }

        // Pøesmìrování na pùvodní stránku nebo na hlavní stránku
        if ((string.IsNullOrEmpty(ReturnUrl)) || (ReturnUrl == "/Account/Logout") || (ReturnUrl == "/Account/Login") || (ReturnUrl == "/Account/Register") || (ReturnUrl == "/Categories") || (ReturnUrl == "/Account/ForgotPassword") || (ReturnUrl == "/Account/ResetPassword"))
            return RedirectToPage("/Index");

        return LocalRedirect(ReturnUrl);
    }
}