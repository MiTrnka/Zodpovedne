using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Logging;
using Ganss.Xss;
using Zodpovedne.Logging.Services;
using Zodpovedne.Web.Pages.Models;

namespace Zodpovedne.Web.Pages;

/// <summary>
/// Model pro stránku zobrazující detail konkrétní kategorie vèetnì seznamu jejích diskuzí.
/// Podporuje stránkované naèítání diskuzí - první stránka se naète pøi naètení stránky,
/// další stránky se naèítají pomocí AJAX poadavkù.
/// </summary>
[IgnoreAntiforgeryToken]
public class CategoryModel : BasePageModel
{
    public CategoryModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator)
        : base(clientFactory, configuration, logger, sanitizer, translator)
    {
    }

    /// <summary>
    /// Èíslo aktuální stránky (èíslováno od 1)
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Velikost stránky - kolik poloek naèítat najednou
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Kód kategorie získanı z URL
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string CategoryCode { get; set; } = "";

    /// <summary>
    /// Název kategorie pro zobrazení
    /// </summary>
    public string CategoryName { get; set; } = "";

    /// <summary>
    /// Popis kategorie pro zobrazení
    /// </summary>
    public string CategoryDescription { get; set; } = "";

    /// <summary>
    /// ID kategorie z databáze - pouívá se pro API volání
    /// </summary>
    public int CategoryId { get; private set; }

    /// <summary>
    /// Seznam diskuzí v aktuální kategorii
    /// </summary>
    public List<DiscussionListDto> Discussions { get; private set; } = new();

    /// <summary>
    /// Handler pro GET poadavek - naète detail kategorie a první stránku diskuzí
    /// </summary>
    public async Task<IActionResult> OnGetAsync()
    {
        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Získání detailu kategorie
        var categoryResponse = await client.GetAsync($"{ApiBaseUrl}/Categories/{CategoryCode}");
        if (!categoryResponse.IsSuccessStatusCode)
        {
            logger.Log($"Nenalezena kategorie {CategoryCode}");
            ErrorMessage = "Omlouváme se, ale poadovanou kategorii diskuze se nepodaøilo naèíst.";
            return Page();
        }

        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>();
        if (category == null)
        {
            logger.Log($"Kategorie {CategoryCode} nejde naèíst");
            ErrorMessage = "Omlouváme se, ale poadovanou kategorii diskuze se nepodaøilo naèíst.";
            return Page();
        }

        // Uloení základních informací o kategorii
        CategoryName = category.Name;
        CategoryCode = category.Code;
        CategoryDescription = category.Description;
        CategoryId = category.Id;

        // Po naètení kategorie nastavit SEO data
        ViewData["Title"] = $"{CategoryName} - Diskuze";
        ViewData["Description"] = $"Diskuze v kategorii {CategoryName} na Discussion.cz. {(!string.IsNullOrEmpty(CategoryDescription) ? CategoryDescription : "Pøipojte se k diskuzím v této kategorii.")} Èeská diskuzní komunita bez reklam.";
        ViewData["Keywords"] = $"{CategoryName.ToLower()}, diskuze {CategoryName.ToLower()}, diskuzi {CategoryName.ToLower()}, {CategoryCode}, informace o {CategoryName.ToLower()},informace, rada, poradit, jak na, pokec, èeská komunita, ceska komunita, discussion, fórum, forum";

        // Pro Open Graph (bez diakritiky)
        ViewData["OGTitle"] = $"Kategorie {CategoryName} - Discussion.cz";
        ViewData["OGDescription"] = $"Diskuze v kategorii {CategoryName} na Discussion.cz. {(!string.IsNullOrEmpty(CategoryDescription) ? CategoryDescription?.Replace("ø", "r").Replace("š", "s").Replace("è", "c").Replace("", "z").Replace("ı", "y").Replace("á", "a").Replace("í", "i").Replace("é", "e").Replace("ù", "u").Replace("ú", "u").Replace("", "t").Replace("ï", "d").Replace("ò", "n") : "Pripojte se k diskuzim v teto kategorii.")}";


        // Naètení první stránky diskuzí
        var discussionsResponse = await client.GetAsync(
            $"{ApiBaseUrl}/discussions?categoryId={CategoryId}&page={CurrentPage}&pageSize={PageSize}");

        if (!discussionsResponse.IsSuccessStatusCode)
        {
            logger.Log($"Pro kategorii Code: {CategoryCode}, Id: {CategoryId} nejde naèíst její seznam diskuzí.");
            ErrorMessage = "Omlouváme se, ale poadovanou kategorii diskuze se nepodaøilo naèíst.";
            return Page();
        }

        var result = await discussionsResponse.Content.ReadFromJsonAsync<PagedResultDto<DiscussionListDto>>();
        if (result == null)
        {
            logger.Log($"Pro kategorii Code: {CategoryCode}, Id: {CategoryId} nejde naèíst její seznam diskuzí z response.");
            ErrorMessage = "Omlouváme se, ale poadovanou kategorii diskuze se nepodaøilo naèíst.";
            return Page();
        }

        // Uloení seznamu diskuzí a informace o další stránce
        Discussions = result.Items;
        HasNextPage = result.HasNextPage;


        // SEO pro stránkování
        ViewData["IsFirstPage"] = CurrentPage == 1;

        if (CurrentPage > 1)
        {
            ViewData["PrevPageUrl"] = CurrentPage == 2
                ? $"{_configuration["BaseUrl"]}/Categories/{CategoryCode}"
                : $"{_configuration["BaseUrl"]}/Categories/{CategoryCode}?page={CurrentPage - 1}";

            // Pro stránky 2+ - neindexovat
            ViewData["Robots"] = "noindex, follow";
            ViewData["Title"] = $"{CategoryName} - Diskuze (stránka {CurrentPage})";
        }

        if (result.HasNextPage)
        {
            ViewData["NextPageUrl"] = $"{_configuration["BaseUrl"]}/Categories/{CategoryCode}?page={CurrentPage + 1}";
        }

        // Kanonická URL - pouze pro první stránku
        if (CurrentPage == 1)
        {
            ViewData["CanonicalUrl"] = $"{_configuration["BaseUrl"]}/Categories/{CategoryCode}";
        }
        else
        {
            ViewData["CanonicalUrl"] = $"{_configuration["BaseUrl"]}/Categories/{CategoryCode}?page={CurrentPage}";
        }


        return Page();
    }

    /// <summary>
    /// Handler pro AJAX poadavek na naètení pomocí API další stránky diskuzí, vrátí JSON s novımi diskuzemi a informacemi o stránkování
    /// </summary>
    /// <param name="categoryId">ID kategorie</param>
    /// <param name="currentPage">Aktuální èíslo stránky</param>
    public async Task<IActionResult> OnGetNextPageAsync(int categoryId, int currentPage)
    {
        try
        {
            // Vıpoèet èísla následující stránky
            var nextPage = currentPage + 1;

            // Naètení další stránky diskuzí z API
            var client = _clientFactory.CreateBearerClient(HttpContext);
            var response = await client.GetAsync(
                $"{ApiBaseUrl}/discussions?categoryId={categoryId}&page={nextPage}&pageSize={PageSize}");

            if (!response.IsSuccessStatusCode)
            {
                logger.Log($"Nepodaøilo se naèíst další stránku diskuzí. StatusCode: {response.StatusCode}");
                return BadRequest("Nepodaøilo se naèíst další diskuze.");
            }

            var result = await response.Content.ReadFromJsonAsync<PagedResultDto<DiscussionListDto>>();
            if (result == null)
            {
                logger.Log("Nepodaøilo se deserializovat odpovìï z API");
                return BadRequest("Nepodaøilo se naèíst další diskuze.");
            }

            // Vrácení dat (diskuze pro jednu stránku plus info pro stránkování) pro JavaScript, kterı dále bude zpracovávat
            return new JsonResult(new
            {
                discussions = result.Items,
                hasNextPage = result.HasNextPage,
                currentPage = nextPage,
                categoryCode = CategoryCode
            });
        }
        catch (Exception ex)
        {
            logger.Log("Chyba pøi naèítání další stránky diskuzí", ex);
            return BadRequest("Došlo k chybì pøi naèítání diskuzí.");
        }
    }

    /// <summary>
    /// Handler pro AJAX poadavek na vykreslení partial view pro jednu diskuzi
    /// </summary>
    /// <param name="discussion">Data diskuze</param>
    /// <param name="categoryCode">Kód kategorie pro vytvoøení URL</param>
    public IActionResult OnPostDiscussionPartial([FromBody] DiscussionListDto discussion, string categoryCode)
    {
        return Partial("_DiscussionPartial", discussion);
    }
}