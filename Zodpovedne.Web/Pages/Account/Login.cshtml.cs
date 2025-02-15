using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;

namespace Zodpovedne.Web.Pages.Account;

/// <summary>
/// Model pro pøihlašovací stránku
/// Zajišuje autentizaci pomocí JWT tokenù pro API volání a cookies pro Razor Pages
/// </summary>
public class LoginModel : BasePageModel
{

    public LoginModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger) : base(clientFactory, configuration, logger)
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
    public Models.LoginModel Input { get; set; } = new();

    public string? ErrorMessageWrongUser { get; set; } = null;

    /// <summary>
    /// Zpracování POST poadavku pøi odeslání pøihlašovacího formuláøe
    /// </summary>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Omlouváme se, momentálnì se nelze pøihlásit.";
            return Page();
        }

        // Získání JWT tokenu z API
        var client = _clientFactory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"{ApiBaseUrl}/users/token",
            new { Email = Input.Email, Password = Input.Password }
        );

        if (!response.IsSuccessStatusCode)
        {
            // uivatel zadal špatné pøihlašovací údaje
            ErrorMessageWrongUser = "Neplatné pøihlašovací údaje";
            return Page();
        }

        var result = await response.Content.ReadFromJsonAsync<TokenResponseDto>();
        if (result == null)
        {
            ErrorMessage = "Omlouváme se, momentálnì se nelze pøihlásit.";
            _logger.Log($"Nepodaøilo se naèíst pro {Input.Email} token z API");
            return Page();

        }
        // Uloení JWT do session pro pozdìjší API volání
        HttpContext.Session.SetString("JWTToken", result.Token);
        HttpContext.Session.SetString("UserNickname", result.Nickname);

        // Vytvoøení cookie autentizace z JWT tokenu
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(result.Token);

        // Extrakce claimù z JWT tokenu pro cookie autentizaci
        var claims = new List<Claim>();
        claims.AddRange(jwtToken.Claims);

        // Vytvoøení identity pro cookie autentizaci
        var claimsIdentity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme
        );

        // Nastavení vlastností cookie
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true, // Cookie pøeije zavøení prohlíeèe
            ExpiresUtc = DateTime.UtcNow.AddHours(12) // Stejná doba jako u JWT
        };

        // Pøihlášení uivatele pomocí cookie
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties
        );

        // Pøesmìrování na pùvodní stránku nebo na hlavní stránku
        if ((string.IsNullOrEmpty(ReturnUrl)) || (ReturnUrl == "/Account/Logout") || (ReturnUrl == "/Account/login"))
            return RedirectToPage("/Index");

        return LocalRedirect(ReturnUrl);
    }
}