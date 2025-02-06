using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Filters;
using Zodpovedne.Web.Models.Base;

namespace Zodpovedne.Web.Pages;

//[AuthenticationFilter]
//[AdminAuthorizationFilter]
public class CategoriesModel : BasePageModel
{
    public CategoriesModel(IHttpClientFactory clientFactory, IConfiguration configuration) : base(clientFactory, configuration)
    {
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