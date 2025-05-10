using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;
using Ganss.Xss;
using Zodpovedne.Logging.Services;

namespace Zodpovedne.Web.Pages.Account;

public class RegisterModel : BasePageModel
{
    [BindProperty]
    public RegisterModelDto Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public RegisterModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator) : base(clientFactory, configuration, logger, sanitizer, translator)
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Omlouv�me se, nastala chyba";
            _logger.Log("Chyba p�i registraci nov�ho u�ivatele");
            return Page();
        }

        var client = _clientFactory.CreateClient();
        var response = await client.PostAsJsonAsync($"{ApiBaseUrl}/users/member", Input);
        if (!response.IsSuccessStatusCode)
        {
            string responseError = await response.Content.ReadAsStringAsync();
            ErrorMessage = "Registrace se nezda�ila. "+ responseError;
            return Page();
        }

        // P�ihl�en� nov�ho u�ivatele
        var loginResponse = await client.PostAsJsonAsync(
            $"{ApiBaseUrl}/users/login",
            new { Email = Input.Email, Password = Input.Password }
        );
        if (!loginResponse.IsSuccessStatusCode)
        {
            ErrorMessage = "Registrace prob�hla �sp�n�, ale nepoda�ilo se p�ihl�sit";
            _logger.Log($"Registrace prob�hla �sp�n�, ale nepoda�ilo se p�ihl�sit pro {Input.Email}");
            return Page();
        }

        var result = await loginResponse.Content.ReadFromJsonAsync<TokenResponseDto>();
        if (result == null)
        {
            ErrorMessage = "Registrace prob�hla �sp�n�, ale nepoda�ilo se p�ihl�sit";
            _logger.Log($"Registrace prob�hla �sp�n�, ale nepoda�ilo se p�ihl�sit pro {Input.Email}, chyba se z�sk�n�m response");
            return Page();
        }

        // P�ihl�en� u�ivatele pomoc� cookie
        await Login(result.Token, result.Nickname);

        // P�esm�rov�n� na p�vodn� str�nku nebo na hlavn� str�nku
        if ((string.IsNullOrEmpty(ReturnUrl)) || (ReturnUrl == "/Account/Logout") || (ReturnUrl == "/Account/Login") || (ReturnUrl == "/Account/ForgotPassword"))
            return RedirectToPage("/Categories");

        return LocalRedirect(ReturnUrl);

    }
}