using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Filters;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;
using Ganss.Xss;
using Zodpovedne.Web.Services;

namespace Zodpovedne.Web.Pages;

/// <summary>
/// Model pro stránku zobrazující seznam kategorií
/// </summary>
public class CategoriesModel : BasePageModel
{
    public CategoriesModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator) : base(clientFactory, configuration, logger, sanitizer, translator)
    {
    }

    public List<CategoryDto> Categories { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var client = _clientFactory.CreateClient();
        var response = await client.GetAsync($"{ApiBaseUrl}/Categories");

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Omlouváme se, ale seznam kategorií se nepodaøilo naèíst.";
            _logger.Log("Nepodaøilo se naèíst seznam všech kategorií");
            return Page();
        }
        Categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>() ?? new();
        if (Categories == null)
        {
            ErrorMessage = "Omlouváme se, ale seznam kategorií se nepodaøilo naèíst.";
            _logger.Log("Nepodaøilo se naèíst seznam všech kategorií z response");
            return Page();
        }

        return Page();
    }
}