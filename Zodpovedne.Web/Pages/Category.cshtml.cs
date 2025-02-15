using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Filters;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;

namespace Zodpovedne.Web.Pages;

/// <summary>
/// Model pro stránku zobrazující detail konkrétní kategorie vèetnì seznamu jejích diskuzí
/// </summary>
public class CategoryModel : BasePageModel
{
    public CategoryModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger) : base(clientFactory, configuration, logger)
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
        {
            _logger.Log($"Nenalezena kategorie {CategoryCode}");
            ErrorMessage = "Omlouváme se, ale požadovanou kategorii diskuze se nepodaøilo naèíst.";
            return Page();
        }

        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>();
        if (category == null)
        {
            _logger.Log($"Kategorie {CategoryCode} nezle z categoryResponse naèíst.");
            ErrorMessage = "Omlouváme se, ale požadovanou kategorii diskuze se nepodaøilo naèíst.";
            return Page();
        }

        CategoryName = category.Name;
        CategoryDescription = category.Description;

        // Získání seznamu diskuzí pro danou kategorii
        var discussionsResponse = await client.GetAsync($"{ApiBaseUrl}/discussions?categoryId={category.Id}");
        if (discussionsResponse.IsSuccessStatusCode)
        {
            Discussions = await discussionsResponse.Content.ReadFromJsonAsync<List<DiscussionListDto>>() ?? new();
        }
        else
        {
            _logger.Log($"Pro kategori Code: {CategoryCode}, Id: {category.Id} nezle naèíst její seznam diskuzí.");
            ErrorMessage = "Omlouváme se, ale požadovanou kategorii diskuze se nepodaøilo naèíst.";
            return Page();
        }

        return Page();
    }
}