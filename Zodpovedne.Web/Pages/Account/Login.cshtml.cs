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

    public void OnGet(string? statusMessage = null)
    {
        if (!string.IsNullOrEmpty(statusMessage))
            this.StatusMessage = statusMessage;
    }

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
            _logger.Log($"Nepodaøilo se naèíst pro {Input.Email} token z API");
            return Page();
        }

        // Pøihlášení uivatele pomocí cookie
        await Login(result.Token, result.Nickname);

        // Pøesmìrování na pùvodní stránku nebo na hlavní stránku
        if ((string.IsNullOrEmpty(ReturnUrl)) || (ReturnUrl == "/Account/Logout") || (ReturnUrl == "/Account/Login") || (ReturnUrl == "/Account/Register") || (ReturnUrl == "/Account/ForgotPassword"))
            return RedirectToPage("/Index");

        return LocalRedirect(ReturnUrl);
    }
}