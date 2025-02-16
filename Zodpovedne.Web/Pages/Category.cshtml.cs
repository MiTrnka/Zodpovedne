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
/// Model pro str�nku zobrazuj�c� detail konkr�tn� kategorie v�etn� seznamu jej�ch diskuz�
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

        // Z�sk�n� detailu kategorie
        var categoryResponse = await client.GetAsync($"{ApiBaseUrl}/categories/{CategoryCode}");
        if (!categoryResponse.IsSuccessStatusCode)
        {
            _logger.Log($"Nenalezena kategorie {CategoryCode}");
            ErrorMessage = "Omlouv�me se, ale po�adovanou kategorii diskuze se nepoda�ilo na��st.";
            return Page();
        }

        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>();
        if (category == null)
        {
            _logger.Log($"Kategorie {CategoryCode} nejde na��st");
            ErrorMessage = "Omlouv�me se, ale po�adovanou kategorii diskuze se nepoda�ilo na��st.";
            return Page();
        }

        CategoryName = category.Name;
        CategoryDescription = category.Description;
        CategoryId = category.Id;

        // Na�ten� seznamu diskuz� s podporou str�nkov�n�
        var discussionsResponse = await client.GetAsync(
            $"{ApiBaseUrl}/discussions?categoryId={CategoryId}&page={CurrentPage}&pageSize={PageSize}");

        if (!discussionsResponse.IsSuccessStatusCode)
        {
            _logger.Log($"Pro kategorii Code: {CategoryCode}, Id: {CategoryId} nejde na��st jej� seznam diskuz�.");
            ErrorMessage = "Omlouv�me se, ale po�adovanou kategorii diskuze se nepoda�ilo na��st.";
            return Page();
        }

        var result = await discussionsResponse.Content.ReadFromJsonAsync<PagedResultDto<DiscussionListDto>>();
        if (result == null)
        {
            _logger.Log($"Pro kategorii Code: {CategoryCode}, Id: {CategoryId} nejde na��st jej� seznam diskuz� z response.");
            ErrorMessage = "Omlouv�me se, ale po�adovanou kategorii diskuze se nepoda�ilo na��st.";
            return Page();
        }

        Discussions = result.Items;
        HasNextPage = result.HasNextPage;

        return Page();
    }

    /// <summary>
    /// Handler pro na�ten� dal�� str�nky diskuz�
    /// </summary>
    public async Task<IActionResult> OnGetNextPageAsync(int categoryId, int currentPage)
    {
        try
        {
            var nextPage = currentPage + 1;
            _logger.Log($"Na��t�n� dal�� str�nky. CategoryId: {categoryId}, NextPage: {nextPage}");

            var client = _clientFactory.CreateBearerClient(HttpContext);
            var response = await client.GetAsync(
                $"{ApiBaseUrl}/discussions?categoryId={categoryId}&page={nextPage}&pageSize={PageSize}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.Log($"Nepoda�ilo se na��st dal�� str�nku diskuz�. StatusCode: {response.StatusCode}");
                return BadRequest("Nepoda�ilo se na��st dal�� diskuze.");
            }

            var result = await response.Content.ReadFromJsonAsync<PagedResultDto<DiscussionListDto>>();
            if (result == null)
            {
                _logger.Log("Nepoda�ilo se deserializovat odpov�� z API");
                return BadRequest("Nepoda�ilo se na��st dal�� diskuze.");
            }

            return new JsonResult(new
            {
                discussions = result.Items,
                hasNextPage = result.HasNextPage,
                currentPage = nextPage,
                categoryCode = CategoryCode  // P�id�me categoryCode do response
            });
        }
        catch (Exception ex)
        {
            _logger.Log("Chyba p�i na��t�n� dal�� str�nky diskuz�", ex);
            return BadRequest("Do�lo k chyb� p�i na��t�n� diskuz�.");
        }
    }
}