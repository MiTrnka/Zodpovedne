using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;


namespace Zodpovedne.Web.Pages.Account;

public class LoginModel : PageModel
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;

    public LoginModel(IHttpClientFactory clientFactory, IConfiguration configuration)
    {
        _clientFactory = clientFactory;
        _configuration = configuration;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public Models.LoginModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

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
                HttpContext.Session.SetString("JWTToken", result.Token);
                HttpContext.Session.SetString("UserNickname", result.Nickname);

                if (!string.IsNullOrEmpty(ReturnUrl))
                    return LocalRedirect(ReturnUrl);

                return RedirectToPage("/Index");
            }
        }

        ErrorMessage = "Neplatné pøihlašovací údaje";
        return Page();
    }
}