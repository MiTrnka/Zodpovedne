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
            SuccessMessage = "Admin byl �sp�n� vytvo�en.";
            Input = new RegisterModelDto();  // Vy�i�t�n� formul��e
            return Page();
        }

        ErrorMessage = "Registrace se nezda�ila. U�ivatel s danou p�ezd�vkou nebo emailem ji� existuje";
        return Page();
    }
}