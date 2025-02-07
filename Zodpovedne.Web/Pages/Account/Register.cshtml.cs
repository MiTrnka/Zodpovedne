using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Models.Base;

namespace Zodpovedne.Web.Pages.Account;

public class RegisterModel : BasePageModel
{
    [BindProperty]
    public RegisterModelDto Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public RegisterModel(IHttpClientFactory clientFactory, IConfiguration configuration)
        : base(clientFactory, configuration)
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var client = _clientFactory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"{ApiBaseUrl}/api/users/member",
            Input
        );

        if (response.IsSuccessStatusCode)
        {
            // Pøihlášení nového uživatele
            var loginResponse = await client.PostAsJsonAsync(
                $"{ApiBaseUrl}/api/users/token",
                new { Email = Input.Email, Password = Input.Password }
            );

            if (loginResponse.IsSuccessStatusCode)
            {
                var result = await loginResponse.Content.ReadFromJsonAsync<TokenResponseDto>();
                if (result != null)
                {
                    HttpContext.Session.SetString("JWTToken", result.Token);
                    HttpContext.Session.SetString("UserNickname", result.Nickname);

                    if (!string.IsNullOrEmpty(ReturnUrl))
                        return LocalRedirect(ReturnUrl);

                    return RedirectToPage("/Index");
                }
            }
        }

        ErrorMessage = "Registrace se nezdaøila. Uživatel s danou pøezdívkou nebo emailem již existuje";
        return Page();
    }
}