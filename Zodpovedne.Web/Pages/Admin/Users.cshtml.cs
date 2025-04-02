using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Logging;
using Ganss.Xss;

namespace Zodpovedne.Web.Pages.Admin;

// Pages/Admin/Users.cshtml.cs
public class UsersModel : BasePageModel
{
    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public PagedResultDto<UserListDto>? PagedUsers { get; set; }

    public UsersModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer) : base(clientFactory, configuration, logger, sanitizer)
    {
    }

    /// <summary>
    /// Akce na trvalé smazání uživatele
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<IActionResult> OnPostDeleteUserPermanentlyAsync(string userId)
    {
        var client = _clientFactory.CreateBearerClient(HttpContext);
        var response = await client.DeleteAsync($"{ApiBaseUrl}/users/permanently/{userId}");

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Trvalé smazání uživatele se nezdaøilo.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsAdmin)
            return RedirectToPage("/Index");

        var client = _clientFactory.CreateBearerClient(HttpContext);
        var response = await client.GetAsync($"{ApiBaseUrl}/users/paged?page={CurrentPage}");

        if (response.IsSuccessStatusCode)
        {
            PagedUsers = await response.Content.ReadFromJsonAsync<PagedResultDto<UserListDto>>();
            return Page();
        }

        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostToggleVisibilityAsync(string userId)
    {
        var client = _clientFactory.CreateBearerClient(HttpContext);
        await client.PutAsync($"{ApiBaseUrl}/users/{userId}/toggle-visibility", null);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteUserAsync(string userId)
    {
        var client = _clientFactory.CreateBearerClient(HttpContext);
        await client.DeleteAsync($"{ApiBaseUrl}/users/user/{userId}");
        return RedirectToPage();
    }
}