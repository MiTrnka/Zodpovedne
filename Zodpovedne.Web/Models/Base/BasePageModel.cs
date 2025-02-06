using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace Zodpovedne.Web.Models.Base;

/// <summary>
/// Předek pro všechny PageModely, obsahuje společné vlastnosti a metody
/// </summary>
public abstract class BasePageModel : PageModel
{
    protected readonly IHttpClientFactory _clientFactory;
    protected readonly IConfiguration _configuration;

    protected BasePageModel(IHttpClientFactory clientFactory, IConfiguration configuration)
    {
        _clientFactory = clientFactory;
        _configuration = configuration;
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
