using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;

namespace Zodpovedne.Web.Pages;

/// <summary>
/// Model pro str�nku zobrazuj�c� detail konkr�tn� kategorie v�etn� seznamu jej�ch diskuz�.
/// Podporuje str�nkovan� na��t�n� diskuz� - prvn� str�nka se na�te p�i na�ten� str�nky,
/// dal�� str�nky se na��taj� pomoc� AJAX po�adavk�.
/// </summary>
[IgnoreAntiforgeryToken]
public class CategoryModel : BasePageModel
{
    public CategoryModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger)
        : base(clientFactory, configuration, logger)
    {
    }

    /// <summary>
    /// K�d kategorie z�skan� z URL
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string CategoryCode { get; set; } = "";

    /// <summary>
    /// N�zev kategorie pro zobrazen�
    /// </summary>
    public string CategoryName { get; set; } = "";

    /// <summary>
    /// Popis kategorie pro zobrazen�
    /// </summary>
    public string CategoryDescription { get; set; } = "";

    /// <summary>
    /// ID kategorie z datab�ze - pou��v� se pro API vol�n�
    /// </summary>
    public int CategoryId { get; private set; }

    /// <summary>
    /// Seznam diskuz� v aktu�ln� kategorii
    /// </summary>
    public List<DiscussionListDto> Discussions { get; private set; } = new();

    /// <summary>
    /// Handler pro GET po�adavek - na�te detail kategorie a prvn� str�nku diskuz�
    /// </summary>
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

        // Ulo�en� z�kladn�ch informac� o kategorii
        CategoryName = category.Name;
        CategoryDescription = category.Description;
        CategoryId = category.Id;

        // Na�ten� prvn� str�nky diskuz�
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

        // Ulo�en� seznamu diskuz� a informace o dal�� str�nce
        Discussions = result.Items;
        HasNextPage = result.HasNextPage;

        return Page();
    }

    /// <summary>
    /// Handler pro AJAX po�adavek na na�ten� dal�� str�nky diskuz�
    /// </summary>
    /// <param name="categoryId">ID kategorie</param>
    /// <param name="currentPage">Aktu�ln� ��slo str�nky</param>
    public async Task<IActionResult> OnGetNextPageAsync(int categoryId, int currentPage)
    {
        try
        {
            // V�po�et ��sla n�sleduj�c� str�nky
            var nextPage = currentPage + 1;
            _logger.Log($"Na��t�n� dal�� str�nky. CategoryId: {categoryId}, NextPage: {nextPage}");

            // Na�ten� dal�� str�nky diskuz� z API
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

            // Vr�cen� dat pro JavaScript
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
            _logger.Log("Chyba p�i na��t�n� dal�� str�nky diskuz�", ex);
            return BadRequest("Do�lo k chyb� p�i na��t�n� diskuz�.");
        }
    }

    /// <summary>
    /// Handler pro AJAX po�adavek na vykreslen� partial view pro jednu diskuzi
    /// </summary>
    /// <param name="discussion">Data diskuze</param>
    /// <param name="categoryCode">K�d kategorie pro vytvo�en� URL</param>
    public async Task<IActionResult> OnPostDiscussionPartialAsync([FromBody] DiscussionListDto discussion, string categoryCode)
    {
        ViewData["CategoryCode"] = categoryCode;
        return Partial("_DiscussionItem", discussion);
    }
}