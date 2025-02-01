using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using Zodpovedne.Web.Filters;
using Zodpovedne.Contracts.DTO;

namespace Zodpovedne.Web.Pages;

//[AuthenticationFilter]
[AdminAuthorizationFilter]
public class CategoriesModel : PageModel
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;

    public CategoriesModel(IHttpClientFactory clientFactory, IConfiguration configuration)
    {
        _clientFactory = clientFactory;
        _configuration = configuration;
    }

    public List<CategoryListDto> Categories { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var token = HttpContext.Session.GetString("JWTToken");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToPage("/Account/Login");
        }

        var client = _clientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"{_configuration["ApiBaseUrl"]}/api/categories");
        if (response.IsSuccessStatusCode)
        {
            Categories = await response.Content.ReadFromJsonAsync<List<CategoryListDto>>() ?? new List<CategoryListDto>();
        }

        return Page();
    }
}