using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Logging;
using Ganss.Xss;
using Zodpovedne.Logging.Services;
using Zodpovedne.Web.Pages.Models;

namespace Zodpovedne.Web.Pages.Account;

public class RegisterAdminModel : BasePageModel
{
    [BindProperty]
    public RegisterModelDto Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? SuccessMessage { get; set; }

    public RegisterAdminModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator) : base(clientFactory, configuration, logger, sanitizer, translator)
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var client = _clientFactory.CreateBearerClient(HttpContext);
        var response = await client.PostAsJsonAsync(
            $"{ApiBaseUrl}/users/admin",
            Input
        );

        if (response.IsSuccessStatusCode)
        {
            SuccessMessage = "Admin byl úspìšnì vytvoøen.";
            Input = new RegisterModelDto();  // Vyèištìní formuláøe
            return Page();
        }

        ErrorMessage = "Registrace se nezdaøila. Uživatel s danou pøezdívkou nebo emailem již existuje";
        return Page();
    }
}