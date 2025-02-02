using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Filters;

namespace Zodpovedne.Web.Pages;

//[AuthenticationFilter]
//[AdminAuthorizationFilter]
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
        var client = _clientFactory.CreateClient();
        var response = await client.GetAsync($"{_configuration["ApiBaseUrl"]}/api/categories");

        if (response.IsSuccessStatusCode)
        {
            Categories = await response.Content.ReadFromJsonAsync<List<CategoryListDto>>() ?? new();
        }

        return Page();
    }
}