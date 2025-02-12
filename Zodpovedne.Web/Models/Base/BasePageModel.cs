using Ganss.Xss;
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
    public string? UserNickname => HttpContext.Session.GetString("UserNickname");

    /// <summary>
    /// ID přihlášeného uživatele
    /// </summary>
    public string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Indikuje, zda je uživatel přihlášen
    /// </summary>
    public bool IsUserLoggedIn => !string.IsNullOrEmpty(JwtToken);

    /// <summary>
    /// Indikuje, zda je přihlášený uživatel admin
    /// </summary>
    public bool IsAdmin => User.IsInRole("Admin");
}
