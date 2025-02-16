using Ganss.Xss;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Zodpovedne.Logging;
using Microsoft.AspNetCore.Mvc;

namespace Zodpovedne.Web.Models.Base;

/// <summary>
/// Předek pro všechny PageModely, obsahuje společné vlastnosti a metody
/// </summary>
public abstract class BasePageModel : PageModel
{
    protected readonly IHttpClientFactory _clientFactory;
    protected readonly IConfiguration _configuration;
    protected readonly FileLogger _logger;

    // HtmlSanitizer pro bezpečné čištění HTML vstupu
    protected readonly HtmlSanitizer _sanitizer;

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
    /// Výchozí velikost stránky - kolik položek se načítá v jednom požadavku
    /// </summary>
    protected const int DEFAULT_PAGE_SIZE = 10;

    /// <summary>
    /// Číslo aktuální stránky (číslováno od 1)
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Velikost stránky - kolik položek načítat najednou
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = DEFAULT_PAGE_SIZE;

    /// <summary>
    /// Indikátor, zda existují další stránky k načtení
    /// </summary>
    public bool HasNextPage { get; protected set; }







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

    protected BasePageModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger)
    {
        try
        {
            _clientFactory = clientFactory;
            _configuration = configuration;
            _logger = logger;

            // Inicializace a konfigurace HTML sanitizeru pro bezpečné čištění HTML vstupu
            _sanitizer = new HtmlSanitizer();

            // Povolené HTML tagy
            _sanitizer.AllowedTags.Clear();
            _sanitizer.AllowedTags.Add("p");
            _sanitizer.AllowedTags.Add("br");
            _sanitizer.AllowedTags.Add("b");
            _sanitizer.AllowedTags.Add("strong");
            _sanitizer.AllowedTags.Add("i");
            _sanitizer.AllowedTags.Add("em");
            _sanitizer.AllowedTags.Add("ul");
            _sanitizer.AllowedTags.Add("ol");
            _sanitizer.AllowedTags.Add("li");
            _sanitizer.AllowedTags.Add("h2");
            _sanitizer.AllowedTags.Add("h3");
            _sanitizer.AllowedTags.Add("h4");
            _sanitizer.AllowedTags.Add("a");
            _sanitizer.AllowedTags.Add("img");

            // Povolené HTML atributy
            _sanitizer.AllowedAttributes.Clear();
            _sanitizer.AllowedAttributes.Add("href");
            _sanitizer.AllowedAttributes.Add("src");
            _sanitizer.AllowedAttributes.Add("alt");

            // Povolené CSS styly (žádné)
            _sanitizer.AllowedCssProperties.Clear();
        }
        catch (Exception e)
        {
            _logger.Log("Nastala chyba při inicializaci BasePageModelu.", e);
        }
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
}
