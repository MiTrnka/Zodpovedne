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
        ViewData["Description"] = "Prozkoumejte všechny kategorie diskuzí na Discussion.cz - od aktuálních témat po hobby a zájmy. Najdìte svou komunitu v èeské diskuzní síti bez reklam.";
        ViewData["Keywords"] = "kategorie diskuzí, kategorie diskuzi, diskuzní témata, diskuzni temata, fóra, fora, èeská komunita, ceska komunita, témata diskuzí, temata diskuzi, discussion, discussion kategorie, bez reklamy, bez reklam, zdarma, sí, sit, sociální sí, socialni sit, diskuzni socialni sit, diskuzní sociální sí";

        // Pro Open Graph (bez diakritiky)
        ViewData["OGTitle"] = "Diskuze pod kategoriemi - Discussion.cz";
        ViewData["OGDescription"] = "Prozkoumejte vsechny kategorie diskuzi na Discussion.cz - od aktualnich temat po hobby a zajmy. Najdete svou komunitu!";

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