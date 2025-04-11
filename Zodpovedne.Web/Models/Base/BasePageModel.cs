using Ganss.Xss;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Zodpovedne.Logging;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using Zodpovedne.Contracts.DTO;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Zodpovedne.Web.Services;

namespace Zodpovedne.Web.Models.Base;

/// <summary>
/// Předek pro všechny PageModely, obsahuje společné vlastnosti a metody
/// </summary>
public abstract class BasePageModel : PageModel
{
    protected readonly IHttpClientFactory _clientFactory;
    protected readonly IConfiguration _configuration;
    protected readonly FileLogger _logger;
    public Translator Translator { get; }  // Translator pro překlady textů na stránkách

    // HtmlSanitizer pro bezpečné čištění HTML vstupu
    protected readonly IHtmlSanitizer _sanitizer;

    /// <summary>
    /// Chybová zpráva pro zobrazení uživateli, pokud je vyplněna, zobrazí se na stránce a nic dalšího se na stránce nezobrazí
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Stavová zpráva pro zobrazení uživateli
    /// </summary>
    public string? StatusMessage { get; set; }

    /// <summary>
    /// Base URL pro web z konfigurace
    /// </summary>
    public string ApiBaseUrl => _configuration["ApiBaseUrl"] ?? "";

    /// <summary>
    /// Base URL pro API endpointy z konfigurace - pro veřejné použití v JavaScriptu
    /// </summary>
    public string PublicApiBaseUrl => _configuration["PublicApiBaseUrl"] ?? "";

    /// <summary>
    /// Base URL pro API endpointy z konfigurace
    /// </summary>
    public string BaseUrl => _configuration["BaseUrl"] ?? "";

    /// <summary>
    /// JWT token přihlášeného uživatele
    /// </summary>
    public string? JwtToken => HttpContext.Session.GetString("JWTToken");

    /// <summary>
    /// Přezdívka přihlášeného uživatele
    /// </summary>
    public string? UserNickname => IsUserLoggedIn ? HttpContext.Session.GetString("UserNickname") : null;

    /// <summary>
    /// ID přihlášeného uživatele
    /// </summary>
    public string? CurrentUserId => IsUserLoggedIn ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    /// <summary>
    /// Indikuje, zda je uživatel přihlášen
    /// </summary>
    public bool IsUserLoggedIn => !string.IsNullOrEmpty(JwtToken);

    /// <summary>
    /// Indikuje, zda je přihlášený uživatel admin
    /// </summary>
    public bool IsAdmin => IsUserLoggedIn && User.IsInRole("Admin");

    /// <summary>
    /// Indikátor, zda existují další stránky k načtení
    /// </summary>
    public bool HasNextPage { get; protected set; }

    /// <summary>
    /// Provede session přihlášení uživatele pomocí JWT tokenu a přezdívky
    /// </summary>
    /// <param name="token"></param>
    /// <param name="nickname"></param>
    /// <returns></returns>
    public async Task Login(string token, string nickname)
    {

        // Uložení JWT do session pro pozdější API volání
        HttpContext.Session.SetString("JWTToken", token);
        HttpContext.Session.SetString("UserNickname", nickname);

        // Vytvoření cookie autentizace z JWT tokenu
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // Extrakce claimů z JWT tokenu pro cookie autentizaci
        var claims = new List<Claim>();
        claims.AddRange(jwtToken.Claims);

        // Vytvoření identity pro cookie autentizaci
        var claimsIdentity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme
        );

        // Nastavení vlastností cookie
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true, // Cookie přežije zavření prohlížeče
            ExpiresUtc = DateTime.UtcNow.AddHours(12) // Stejná doba jako u JWT
        };

        // Přihlášení uživatele pomocí cookie
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties
        );

    }

    /// <summary>
    /// Handler se volá těsně před zpracováním požadavku na stránku (třeba OnGet, OnPost...) a umožňuje provést akce před zpracováním požadavku
    /// </summary>
    /// <param name="context"></param>
    /// <param name="next"></param>
    /// <returns></returns>
    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        try
        {
            // Kontrola nekonzistentního stavu autentizace (má autentizační cookie, ale ne token, například když se naposledy neodhlásil a autentizační cookie ještě platí)
            if (!IsUserLoggedIn && User?.Identity?.IsAuthenticated == true)
            {
                // Smazání JWT ze session pro jistotu
                HttpContext.Session.Clear();
                // Uživatel má cookie ale ne token - odhlásíme ho
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        }
        catch (Exception e)
        {
            _logger.Log("Nastala chyba při kontrole stavu autentizace.", e);
        }

        await next();
    }

    protected BasePageModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sanitizer = sanitizer;
        Translator = translator ?? throw new ArgumentNullException(nameof(translator));
    }

    /// <summary>
    /// Metoda pro odhlášení uživatele, vrátí true pokud odhlášení proběhlo úspěšně
    /// </summary>
    /// <returns></returns>
    protected async Task<bool> SignedOutIsOK()
    {
        try
        {
            // Smazání JWT ze session
            HttpContext.Session.Clear();
            // Smazání autentizační cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            StatusMessage = "Byl jste úspěšně odhlášen.";
            return true;
        }
        catch (Exception e)
        {
            _logger.Log("Nastala chyba při odhlašování.", e);
            ErrorMessage = "Omlouváme se, nastala chyba při odhlašování.";
            return false;
        }
    }

    /// <summary>
    /// Najde v objektu HttpResponseMessage popis chyby, nebo vrátí text chyby
    /// </summary>
    /// <param name="response"></param>
    /// <param name="defaultMessage"></param>
    /// <returns></returns>
    public async Task<string> GetErrorFromHttpResponseMessage(HttpResponseMessage response, string defaultMessage = "Nastala chyba")
    {
        string errorMessage = "";
        string errorContent = "";
        if ((response == null) || (response.Content == null))
            return defaultMessage;
        try
        {
            errorContent = await response.Content.ReadAsStringAsync();
            if (errorContent.Trim() == "")
                return defaultMessage;

            var problemDetails = JsonSerializer.Deserialize<ValidationProblemDetails>(errorContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (problemDetails?.Errors != null)
            {
                foreach (var kvp in problemDetails.Errors)
                {
                    foreach (var error in kvp.Value)
                    {
                        errorMessage += error + " ";
                    }
                }
            }
        }
        catch
        {
            // Pokud API vrací jen textovou chybu místo JSON, vypíše ji přímo
            errorMessage = errorContent;
        }
        if (errorMessage.Trim()=="")
            return defaultMessage;
        else
            return errorMessage;
    }
}
