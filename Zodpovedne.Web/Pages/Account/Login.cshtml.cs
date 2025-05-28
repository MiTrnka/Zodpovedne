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
/// Model pro p�ihla�ovac� str�nku
/// Zaji��uje autentizaci pomoc� JWT token� pro API vol�n� a cookies pro Razor Pages
/// </summary>
public class LoginModel : BasePageModel
{

    public LoginModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator) : base(clientFactory, configuration, logger, sanitizer, translator)
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
    public LoginModelDto Input { get; set; } = new ();

    public string? ErrorMessageWrongUser { get; set; } = null;

    public void OnGet(string? statusMessage = null)
    {
        if (!string.IsNullOrEmpty(statusMessage))
            this.StatusMessage = statusMessage;
    }

    /// Zpracov�v� POST po�adavek s p�ihla�ovac�mi �daji z formul��e.
    /// Prov�d� kompletn� proces p�ihl�en� v�etn�:
    /// - Validace vstupn�ch dat (email, heslo)
    /// - Komunikace s REST API pro ov��en� p�ihla�ovac�ch �daj�
    /// - Z�sk�n� JWT tokenu z API odpov�di
    /// - Vytvo�en� autentiza�n�ch cookies pro Razor Pages
    /// - Ulo�en� JWT tokenu do session pro dal�� API vol�n�
    /// - Zaznamen�n� p�ihl�en� do historie (IP adresa, �as)
    /// - P�esm�rov�n� na p�vodn� str�nku nebo v�choz� um�st�n�
    /// Implementuje hybridn� autentiza�n� syst�m kombinuj�c� JWT pro API a cookies pro web.
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
            logger.Log($"Nepoda�ilo se na��st pro {Input.Email} token z API");
            return Page();
        }

        // P�ihl�en� u�ivatele pomoc� cookie
        await Login(result.Token, result.Nickname);

        // Z�sk�n� JWT tokenu z obsahu odpov�di
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(result.Token);

        // Z�sk�n� userId z JWT tokenu
        var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.NameId)?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            try
            {
                // Z�sk�n� IP adresy klienta
                string clientIp = Zodpovedne.Web.Extensions.HttpClientFactoryExtensions.GetClientIpAddress(HttpContext);

                // Vytvo�en� klienta s autoriza�n�m tokenem
                var authClient = _clientFactory.CreateBearerClient(HttpContext);

                // Zaznamen�n� p�ihl�en� u�ivatele
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
                // Logov�n� chyby, ale pokra�ujeme, i kdy� se nepoda�� zaznamenat p�ihl�en�
                logger.Log("Chyba p�i zaznamen�v�n� p�ihl�en�", ex);
            }
        }

        // P�esm�rov�n� na p�vodn� str�nku nebo na hlavn� str�nku
        if ((string.IsNullOrEmpty(ReturnUrl)) || (ReturnUrl == "/Account/Logout") || (ReturnUrl == "/Account/Login") || (ReturnUrl == "/Account/Register") || (ReturnUrl == "/Categories") || (ReturnUrl == "/Account/ForgotPassword") || (ReturnUrl == "/Account/ResetPassword"))
            return RedirectToPage("/Index");

        return LocalRedirect(ReturnUrl);
    }
}