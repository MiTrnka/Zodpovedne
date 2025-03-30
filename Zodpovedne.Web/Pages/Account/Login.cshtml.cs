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
/// Model pro p�ihla�ovac� str�nku
/// Zaji��uje autentizaci pomoc� JWT token� pro API vol�n� a cookies pro Razor Pages
/// </summary>
public class LoginModel : BasePageModel
{

    public LoginModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger) : base(clientFactory, configuration, logger)
    {
    }

    /// <summary>
    /// URL str�nky, ze kter� u�ivatel p�i�el na p�ihl�en�
    /// Po �sp�n�m p�ihl�en� bude na tuto str�nku p�esm�rov�n
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// Model obsahuj�c� p�ihla�ovac� �daje z formul��e
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
    /// Zpracov�n� POST po�adavku p�i odesl�n� p�ihla�ovac�ho formul��e
    /// </summary>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Omlouv�me se, moment�ln� se nelze p�ihl�sit.";
            return Page();
        }

        // P�ihl�en� se, z�sk�n� JWT tokenu z API
        var client = _clientFactory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"{ApiBaseUrl}/users/login",
            new { Email = Input.Email, Password = Input.Password }
        );

        if (!response.IsSuccessStatusCode)
        {
            // u�ivatel zadal �patn� p�ihla�ovac� �daje
            ErrorMessage = "Neplatn� p�ihla�ovac� �daje";
            return Page();
        }

        var result = await response.Content.ReadFromJsonAsync<TokenResponseDto>();
        if (result == null)
        {
            ErrorMessage = "Omlouv�me se, moment�ln� se nelze p�ihl�sit.";
            _logger.Log($"Nepoda�ilo se na��st pro {Input.Email} token z API");
            return Page();
        }

        // P�ihl�en� u�ivatele pomoc� cookie
        await Login(result.Token, result.Nickname);

        // P�esm�rov�n� na p�vodn� str�nku nebo na hlavn� str�nku
        if ((string.IsNullOrEmpty(ReturnUrl)) || (ReturnUrl == "/Account/Logout") || (ReturnUrl == "/Account/Login") || (ReturnUrl == "/Account/Register") || (ReturnUrl == "/Account/ForgotPassword"))
            return RedirectToPage("/Index");

        return LocalRedirect(ReturnUrl);
    }
}