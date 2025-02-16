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
    public int CategoryId { get; private set; }

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
            _logger.Log($"Kategorie {CategoryCode} nejde naèíst");
            ErrorMessage = "Omlouváme se, ale požadovanou kategorii diskuze se nepodaøilo naèíst.";
            return Page();
        }

        CategoryName = category.Name;
        CategoryDescription = category.Description;
        CategoryId = category.Id;

        // Naètení seznamu diskuzí s podporou stránkování
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

        Discussions = result.Items;
        HasNextPage = result.HasNextPage;

        return Page();
    }

    /// <summary>
    /// Handler pro naètení další stránky diskuzí
    /// </summary>
    public async Task<IActionResult> OnGetNextPageAsync(int categoryId, int currentPage)
    {
        try
        {
            var nextPage = currentPage + 1;
            _logger.Log($"Naèítání další stránky. CategoryId: {categoryId}, NextPage: {nextPage}");

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

            return new JsonResult(new
            {
                discussions = result.Items,
                hasNextPage = result.HasNextPage,
                currentPage = nextPage,
                categoryCode = CategoryCode  // Pøidáme categoryCode do response
            });
        }
        catch (Exception ex)
        {
            _logger.Log("Chyba pøi naèítání další stránky diskuzí", ex);
            return BadRequest("Došlo k chybì pøi naèítání diskuzí.");
        }
    }
}