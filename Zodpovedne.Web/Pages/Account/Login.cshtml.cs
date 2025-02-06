using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Models.Base;

namespace Zodpovedne.Web.Pages.Account;

/// <summary>
/// Model pro p�ihla�ovac� str�nku
/// Zaji��uje autentizaci pomoc� JWT token� pro API vol�n� a cookies pro Razor Pages
/// </summary>
public class LoginModel : BasePageModel
{

    public LoginModel(IHttpClientFactory clientFactory, IConfiguration configuration) : base(clientFactory, configuration)
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

    /// <summary>
    /// Chybov� zpr�va v p��pad� ne�sp�n�ho p�ihl�en�
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Zpracov�n� POST po�adavku p�i odesl�n� p�ihla�ovac�ho formul��e
    /// </summary>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        // Z�sk�n� JWT tokenu z API
        var client = _clientFactory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"{_configuration["ApiBaseUrl"]}/api/users/token",
            new { Email = Input.Email, Password = Input.Password }
        );

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<TokenResponseDto>();
            if (result != null)
            {
                // Ulo�en� JWT do session pro pozd�j�� API vol�n�
                HttpContext.Session.SetString("JWTToken", result.Token);
                HttpContext.Session.SetString("UserNickname", result.Nickname);

                // Vytvo�en� cookie autentizace z JWT tokenu
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(result.Token);

                // Extrakce claim� z JWT tokenu pro cookie autentizaci
                var claims = new List<Claim>();
                claims.AddRange(jwtToken.Claims);

                // Vytvo�en� identity pro cookie autentizaci
                var claimsIdentity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme
                );

                // Nastaven� vlastnost� cookie
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true, // Cookie p�e�ije zav�en� prohl�e�e
                    ExpiresUtc = DateTime.UtcNow.AddHours(12) // Stejn� doba jako u JWT
                };

                // P�ihl�en� u�ivatele pomoc� cookie
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties
                );

                // P�esm�rov�n� na p�vodn� str�nku nebo na hlavn� str�nku
                if (!string.IsNullOrEmpty(ReturnUrl))
                    return LocalRedirect(ReturnUrl);

                return RedirectToPage("/Index");
            }
        }

        ErrorMessage = "Neplatn� p�ihla�ovac� �daje";
        return Page();
    }
}