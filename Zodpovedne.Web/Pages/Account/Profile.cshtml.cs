using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;

namespace Zodpovedne.Web.Pages.Account;

public class ProfileModel : BasePageModel
{
    [BindProperty]
    public string? NewNickname { get; set; }

    [BindProperty]
    public string? NewEmail { get; set; }

    public string? EmailErrorMessage { get; set; }
    [BindProperty]
    public string? CurrentPassword { get; set; }
    [BindProperty]
    public string? NewPassword { get; set; }

    public UserProfileDto? UserProfile { get; set; }
    public string? NicknameErrorMessage { get; set; }
    public string? PasswordErrorMessage { get; set; }

    public ProfileModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger) : base(clientFactory, configuration, logger)
    {
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);
        var response = await client.GetAsync($"{ApiBaseUrl}/users/authenticated-user");

        if (response.IsSuccessStatusCode)
        {
            UserProfile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
            return Page();
        }

        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostUpdateNicknameAsync()
    {
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);
        var response = await client.PutAsJsonAsync(
            $"{ApiBaseUrl}/users/authenticated-user/nickname",
            new { Nickname = NewNickname }
        );

        if (response.IsSuccessStatusCode)
        {
            HttpContext.Session.SetString("UserNickname", NewNickname);
            return RedirectToPage();
        }

        NicknameErrorMessage = "P�ezd�vka je ji� pou��v�na.";

        await OnGetAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateEmailAsync()
    {
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);
        var response = await client.PutAsJsonAsync(
            $"{ApiBaseUrl}/users/authenticated-user/email",
            new { Email = NewEmail }
        );

        if (response.IsSuccessStatusCode)
        {
            // Email se zm�nil �sp�n�, u�ivatel bude odhl�en a bude se muset znovu nalogovat, aby se mu vygeneroval nov� JWT token
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Account/Login");
        }

        EmailErrorMessage = "Zm�na emailu se nezda�ila. Email je ji� pou��v�n.";
        await OnGetAsync(); // Znovu na�teme data profilu
        return Page();
    }

    public async Task<IActionResult> OnPostChangePasswordAsync()
    {
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);
        var response = await client.PutAsJsonAsync(
            $"{ApiBaseUrl}/users/authenticated-user/password",
            new
            {
                CurrentPassword,
                NewPassword
            }
        );

        if (response.IsSuccessStatusCode)
            return RedirectToPage();

        PasswordErrorMessage = "Nespr�vn� sou�asn� heslo nebo nastala chyba.";
        await OnGetAsync();
        return Page();
    }
}
