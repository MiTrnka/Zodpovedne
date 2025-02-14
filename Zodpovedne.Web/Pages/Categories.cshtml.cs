using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Filters;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;

namespace Zodpovedne.Web.Pages;

//[AuthenticationFilter]
//[AdminAuthorizationFilter]
public class CategoriesModel : BasePageModel
{
    public CategoriesModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger) : base(clientFactory, configuration, logger)
    {
    }

    public List<CategoryDto> Categories { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var client = _clientFactory.CreateClient();
        var response = await client.GetAsync($"{ApiBaseUrl}/categories");

        if (response.IsSuccessStatusCode)
        {
            Categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>() ?? new();
        }
        return Page();
    }
}