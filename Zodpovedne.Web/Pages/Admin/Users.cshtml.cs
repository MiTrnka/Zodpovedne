using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Web.Extensions;

namespace Zodpovedne.Web.Pages.Admin;

// Pages/Admin/Users.cshtml.cs
public class UsersModel : BasePageModel
{
    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public PagedResultDto<UserListDto>? PagedUsers { get; set; }

    public UsersModel(IHttpClientFactory clientFactory, IConfiguration configuration)
        : base(clientFactory, configuration)
    {
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsAdmin)
            return RedirectToPage("/Index");

        var client = _clientFactory.CreateBearerClient(HttpContext);
        var response = await client.GetAsync($"{ApiBaseUrl}/api/users/paged?page={CurrentPage}");

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
        await client.PutAsync($"{ApiBaseUrl}/api/users/{userId}/toggle-visibility", null);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteUserAsync(string userId)
    {
        var client = _clientFactory.CreateBearerClient(HttpContext);
        await client.DeleteAsync($"{ApiBaseUrl}/api/users/user/{userId}");
        return RedirectToPage();
    }
}