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
/// Model pro str�nku zobrazuj�c� seznam kategori�
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
        ViewData["Title"] = "Kategorie diskuz�";
        ViewData["Description"] = "Prozkoumejte v�echny kategorie diskuz� na Discussion.cz - od aktu�ln�ch t�mat po hobby a z�jmy. Najd�te svou komunitu v �esk� diskuzn� s�ti bez reklam.";
        ViewData["Keywords"] = "kategorie diskuz�, kategorie diskuzi, diskuzn� t�mata, diskuzni temata, f�ra, fora, �esk� komunita, ceska komunita, t�mata diskuz�, temata diskuzi, discussion, discussion kategorie, bez reklamy, bez reklam, zdarma, s�, sit, soci�ln� s�, socialni sit, diskuzni socialni sit, diskuzn� soci�ln� s�";

        // Pro Open Graph (bez diakritiky)
        ViewData["OGTitle"] = "Diskuze pod kategoriemi - Discussion.cz";
        ViewData["OGDescription"] = "Prozkoumejte vsechny kategorie diskuzi na Discussion.cz - od aktualnich temat po hobby a zajmy. Najdete svou komunitu!";

        var client = _clientFactory.CreateClient();
        var response = await client.GetAsync($"{ApiBaseUrl}/Categories");

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Omlouv�me se, ale seznam kategori� se nepoda�ilo na��st.";
            logger.Log("Nepoda�ilo se na��st seznam v�ech kategori�");
            return Page();
        }
        Categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>() ?? new();
        if (Categories == null)
        {
            ErrorMessage = "Omlouv�me se, ale seznam kategori� se nepoda�ilo na��st.";
            logger.Log("Nepoda�ilo se na��st seznam v�ech kategori� z response");
            return Page();
        }

        return Page();
    }
}