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
            ErrorMessage = "Omlouváme se, nastala chyba";
            _logger.Log("Chyba pøi registraci nového uživatele");
            return Page();
        }

        var client = _clientFactory.CreateClient();
        var response = await client.PostAsJsonAsync($"{ApiBaseUrl}/users/member", Input);
        if (!response.IsSuccessStatusCode)
        {
            string responseError = await response.Content.ReadAsStringAsync();
            ErrorMessage = "Registrace se nezdaøila. "+ responseError;
            return Page();
        }

        // Pøihlášení nového uživatele
        var loginResponse = await client.PostAsJsonAsync(
            $"{ApiBaseUrl}/users/login",
            new { Email = Input.Email, Password = Input.Password }
        );
        if (!loginResponse.IsSuccessStatusCode)
        {
            ErrorMessage = "Registrace probìhla úspìšnì, ale nepodaøilo se pøihlásit";
            _logger.Log($"Registrace probìhla úspìšnì, ale nepodaøilo se pøihlásit pro {Input.Email}");
            return Page();
        }

        var result = await loginResponse.Content.ReadFromJsonAsync<TokenResponseDto>();
        if (result == null)
        {
            ErrorMessage = "Registrace probìhla úspìšnì, ale nepodaøilo se pøihlásit";
            _logger.Log($"Registrace probìhla úspìšnì, ale nepodaøilo se pøihlásit pro {Input.Email}, chyba se získáním response");
            return Page();
        }

        // Pøihlášení uživatele pomocí cookie
        await Login(result.Token, result.Nickname);

        // Pøesmìrování na pùvodní stránku nebo na hlavní stránku
        if ((string.IsNullOrEmpty(ReturnUrl)) || (ReturnUrl == "/Account/Logout") || (ReturnUrl == "/Account/Login") || (ReturnUrl == "/Account/ForgotPassword"))
            return RedirectToPage("/Categories");

        return LocalRedirect(ReturnUrl);

    }
}