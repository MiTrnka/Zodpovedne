using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Web.Extensions;

namespace Zodpovedne.Web.Pages.Account;

public class RegisterAdminModel : BasePageModel
{
    [BindProperty]
    public RegisterModelDto Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public RegisterAdminModel(IHttpClientFactory clientFactory, IConfiguration configuration)
        : base(clientFactory, configuration)
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