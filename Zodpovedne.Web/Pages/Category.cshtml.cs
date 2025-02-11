using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Filters;
using Zodpovedne.Web.Models.Base;

namespace Zodpovedne.Web.Pages;

public class CategoryModel : BasePageModel
{
    public CategoryModel(IHttpClientFactory clientFactory, IConfiguration configuration) : base(clientFactory, configuration)
    {
    }

    [BindProperty(SupportsGet = true)]
    public string CategoryCode { get; set; } = "";

    public string CategoryName { get; set; } = "";
    public string CategoryDescription { get; set; } = "";
    public List<DiscussionListDto> Discussions { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Získání detailu kategorie
        var categoryResponse = await client.GetAsync($"{ApiBaseUrl}/categories/{CategoryCode}");
        if (!categoryResponse.IsSuccessStatusCode)
            return NotFound();

        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryListDto>();
        if (category == null)
            return NotFound();

        CategoryName = category.Name;
        CategoryDescription = category.Description;

        // Získání seznamu diskuzí pro danou kategorii
        var discussionsResponse = await client.GetAsync($"{ApiBaseUrl}/discussions?categoryId={category.Id}");
        if (discussionsResponse.IsSuccessStatusCode)
        {
            Discussions = await discussionsResponse.Content.ReadFromJsonAsync<List<DiscussionListDto>>() ?? new();
        }

        return Page();
    }
}