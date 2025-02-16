using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;

namespace Zodpovedne.Web.Pages;

/// <summary>
/// Model pro stránku zobrazující detail konkrétní kategorie vèetnì seznamu jejích diskuzí.
/// Podporuje stránkované naèítání diskuzí - první stránka se naète pøi naètení stránky,
/// další stránky se naèítají pomocí AJAX požadavkù.
/// </summary>
[IgnoreAntiforgeryToken]
public class CategoryModel : BasePageModel
{
    public CategoryModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger)
        : base(clientFactory, configuration, logger)
    {
    }

    /// <summary>
    /// Kód kategorie získaný z URL
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
    /// ID kategorie z databáze - používá se pro API volání
    /// </summary>
    public int CategoryId { get; private set; }

    /// <summary>
    /// Seznam diskuzí v aktuální kategorii
    /// </summary>
    public List<DiscussionListDto> Discussions { get; private set; } = new();

    /// <summary>
    /// Handler pro GET požadavek - naète detail kategorie a první stránku diskuzí
    /// </summary>
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
            _logger.Log($"Kategorie {CategoryCode} nejde naèíst");
            ErrorMessage = "Omlouváme se, ale požadovanou kategorii diskuze se nepodaøilo naèíst.";
            return Page();
        }

        // Uložení základních informací o kategorii
        CategoryName = category.Name;
        CategoryDescription = category.Description;
        CategoryId = category.Id;

        // Naètení první stránky diskuzí
        var discussionsResponse = await client.GetAsync(
            $"{ApiBaseUrl}/discussions?categoryId={CategoryId}&page={CurrentPage}&pageSize={PageSize}");

        if (!discussionsResponse.IsSuccessStatusCode)
        {
            _logger.Log($"Pro kategorii Code: {CategoryCode}, Id: {CategoryId} nejde naèíst její seznam diskuzí.");
            ErrorMessage = "Omlouváme se, ale požadovanou kategorii diskuze se nepodaøilo naèíst.";
            return Page();
        }

        var result = await discussionsResponse.Content.ReadFromJsonAsync<PagedResultDto<DiscussionListDto>>();
        if (result == null)
        {
            _logger.Log($"Pro kategorii Code: {CategoryCode}, Id: {CategoryId} nejde naèíst její seznam diskuzí z response.");
            ErrorMessage = "Omlouváme se, ale požadovanou kategorii diskuze se nepodaøilo naèíst.";
            return Page();
        }

        // Uložení seznamu diskuzí a informace o další stránce
        Discussions = result.Items;
        HasNextPage = result.HasNextPage;

        return Page();
    }

    /// <summary>
    /// Handler pro AJAX požadavek na naètení další stránky diskuzí
    /// </summary>
    /// <param name="categoryId">ID kategorie</param>
    /// <param name="currentPage">Aktuální èíslo stránky</param>
    public async Task<IActionResult> OnGetNextPageAsync(int categoryId, int currentPage)
    {
        try
        {
            // Výpoèet èísla následující stránky
            var nextPage = currentPage + 1;
            _logger.Log($"Naèítání další stránky. CategoryId: {categoryId}, NextPage: {nextPage}");

            // Naètení další stránky diskuzí z API
            var client = _clientFactory.CreateBearerClient(HttpContext);
            var response = await client.GetAsync(
                $"{ApiBaseUrl}/discussions?categoryId={categoryId}&page={nextPage}&pageSize={PageSize}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.Log($"Nepodaøilo se naèíst další stránku diskuzí. StatusCode: {response.StatusCode}");
                return BadRequest("Nepodaøilo se naèíst další diskuze.");
            }

            var result = await response.Content.ReadFromJsonAsync<PagedResultDto<DiscussionListDto>>();
            if (result == null)
            {
                _logger.Log("Nepodaøilo se deserializovat odpovìï z API");
                return BadRequest("Nepodaøilo se naèíst další diskuze.");
            }

            // Vrácení dat pro JavaScript
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
            _logger.Log("Chyba pøi naèítání další stránky diskuzí", ex);
            return BadRequest("Došlo k chybì pøi naèítání diskuzí.");
        }
    }

    /// <summary>
    /// Handler pro AJAX požadavek na vykreslení partial view pro jednu diskuzi
    /// </summary>
    /// <param name="discussion">Data diskuze</param>
    /// <param name="categoryCode">Kód kategorie pro vytvoøení URL</param>
    public async Task<IActionResult> OnPostDiscussionPartialAsync([FromBody] DiscussionListDto discussion, string categoryCode)
    {
        ViewData["CategoryCode"] = categoryCode;
        return Partial("_DiscussionItem", discussion);
    }
}