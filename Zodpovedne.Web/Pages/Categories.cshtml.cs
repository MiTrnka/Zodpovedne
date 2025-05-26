using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Filters;
using Zodpovedne.Logging;
using Ganss.Xss;
using Zodpovedne.Logging.Services;
using Zodpovedne.Web.Pages.Models;

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
        // SEO meta data
        ViewData["Title"] = "Kategorie diskuzí";
        ViewData["Description"] = "Prozkoumejte všechny kategorie diskuzí na Discussion.cz - od aktuálních témat po hobby a zájmy. Najdìte svou komunitu!";
        ViewData["Keywords"] = "kategorie, diskuzní témata, fóra, èeská komunita, discussion témata";
        ViewData["OGTitle"] = "Kategorie diskuzí - Discussion.cz";
        ViewData["OGDescription"] = "Objevte rozmanité kategorie diskuzí na Discussion.cz";

        var client = _clientFactory.CreateClient();
        var response = await client.GetAsync($"{ApiBaseUrl}/Categories");

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Omlouváme se, ale seznam kategorií se nepodaøilo naèíst.";
            logger.Log("Nepodaøilo se naèíst seznam všech kategorií");
            return Page();
        }
        Categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>() ?? new();
        if (Categories == null)
        {
            ErrorMessage = "Omlouváme se, ale seznam kategorií se nepodaøilo naèíst.";
            logger.Log("Nepodaøilo se naèíst seznam všech kategorií z response");
            return Page();
        }

        return Page();
    }
}