using Ganss.Xss;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Zodpovedne.Logging;

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
    /// Handler se volá těsně před zpracováním požadavku na stránku (třeba OnGet, OnPost...) a umožňuje provést akce před zpracováním požadavku
    /// </summary>
    /// <param name="context"></param>
    /// <param name="next"></param>
    /// <returns></returns>
    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        // Kontrola nekonzistentního stavu autentizace (má autentizační cookie, ale ne token, například když se naposledy neodhlásil a autentizační cookie ještě platí)
        if (!IsUserLoggedIn && User?.Identity?.IsAuthenticated == true)
        {
            // Uživatel má cookie ale ne token - odhlásíme ho
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        await next();
    }

    protected BasePageModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger)
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

    /// <summary>
    /// Chybová zpráva pro zobrazení uživateli
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Base URL pro API endpointy z konfigurace
    /// </summary>
    public string ApiBaseUrl => _configuration["ApiBaseUrl"] ?? "";

    /// <summary>
    /// JWT token přihlášeného uživatele
    /// </summary>
    public string? JwtToken => HttpContext.Session.GetString("JWTToken");

    /// <summary>
    /// Přezdívka přihlášeného uživatele
    /// </summary>
    public string? UserNickname => IsUserLoggedIn ? HttpContext.Session.GetString("UserNickname"): null;

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
}
