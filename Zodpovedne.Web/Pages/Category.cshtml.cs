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
/// Model pro str�nku zobrazuj�c� detail konkr�tn� kategorie v�etn� seznamu jej�ch diskuz�.
/// Podporuje str�nkovan� na��t�n� diskuz� - prvn� str�nka se na�te p�i na�ten� str�nky,
/// dal�� str�nky se na��taj� pomoc� AJAX po�adavk�.
/// </summary>
[IgnoreAntiforgeryToken]
public class CategoryModel : BasePageModel
{
    public CategoryModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator)
        : base(clientFactory, configuration, logger, sanitizer, translator)
    {
    }

    /// <summary>
    /// ��slo aktu�ln� str�nky (��slov�no od 1)
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Velikost str�nky - kolik polo�ek na��tat najednou
    /// </summary>
    public int PageSize { get; set; } = 20;

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
        var categoryResponse = await client.GetAsync($"{ApiBaseUrl}/Categories/{CategoryCode}");
        if (!categoryResponse.IsSuccessStatusCode)
        {
            logger.Log($"Nenalezena kategorie {CategoryCode}");
            ErrorMessage = "Omlouv�me se, ale po�adovanou kategorii diskuze se nepoda�ilo na��st.";
            return Page();
        }

        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>();
        if (category == null)
        {
            logger.Log($"Kategorie {CategoryCode} nejde na��st");
            ErrorMessage = "Omlouv�me se, ale po�adovanou kategorii diskuze se nepoda�ilo na��st.";
            return Page();
        }

        // Ulo�en� z�kladn�ch informac� o kategorii
        CategoryName = category.Name;
        CategoryCode = category.Code;
        CategoryDescription = category.Description;
        CategoryId = category.Id;

        // Po na�ten� kategorie nastavit SEO data
        ViewData["Title"] = $"{CategoryName} - Diskuze";
        ViewData["Description"] = $"Diskuze v kategorii {CategoryName} na Discussion.cz. {(!string.IsNullOrEmpty(CategoryDescription) ? CategoryDescription : "P�ipojte se k diskuz�m v t�to kategorii.")} �esk� diskuzn� komunita bez reklam.";
        ViewData["Keywords"] = $"{CategoryName.ToLower()}, diskuze {CategoryName.ToLower()}, diskuzi {CategoryName.ToLower()}, {CategoryCode}, informace o {CategoryName.ToLower()},informace, rada, poradit, jak na, pokec, �esk� komunita, ceska komunita, discussion, f�rum, forum";

        // Pro Open Graph (bez diakritiky)
        ViewData["OGTitle"] = $"Kategorie {CategoryName} - Discussion.cz";
        ViewData["OGDescription"] = $"Diskuze v kategorii {CategoryName} na Discussion.cz. {(!string.IsNullOrEmpty(CategoryDescription) ? CategoryDescription?.Replace("�", "r").Replace("�", "s").Replace("�", "c").Replace("�", "z").Replace("�", "y").Replace("�", "a").Replace("�", "i").Replace("�", "e").Replace("�", "u").Replace("�", "u").Replace("�", "t").Replace("�", "d").Replace("�", "n") : "Pripojte se k diskuzim v teto kategorii.")}";


        // Na�ten� prvn� str�nky diskuz�
        var discussionsResponse = await client.GetAsync(
            $"{ApiBaseUrl}/discussions?categoryId={CategoryId}&page={CurrentPage}&pageSize={PageSize}");

        if (!discussionsResponse.IsSuccessStatusCode)
        {
            logger.Log($"Pro kategorii Code: {CategoryCode}, Id: {CategoryId} nejde na��st jej� seznam diskuz�.");
            ErrorMessage = "Omlouv�me se, ale po�adovanou kategorii diskuze se nepoda�ilo na��st.";
            return Page();
        }

        var result = await discussionsResponse.Content.ReadFromJsonAsync<PagedResultDto<DiscussionListDto>>();
        if (result == null)
        {
            logger.Log($"Pro kategorii Code: {CategoryCode}, Id: {CategoryId} nejde na��st jej� seznam diskuz� z response.");
            ErrorMessage = "Omlouv�me se, ale po�adovanou kategorii diskuze se nepoda�ilo na��st.";
            return Page();
        }

        // Ulo�en� seznamu diskuz� a informace o dal�� str�nce
        Discussions = result.Items;
        HasNextPage = result.HasNextPage;


        // SEO pro str�nkov�n�
        ViewData["IsFirstPage"] = CurrentPage == 1;

        if (CurrentPage > 1)
        {
            ViewData["PrevPageUrl"] = CurrentPage == 2
                ? $"{_configuration["BaseUrl"]}/Categories/{CategoryCode}"
                : $"{_configuration["BaseUrl"]}/Categories/{CategoryCode}?page={CurrentPage - 1}";

            // Pro str�nky 2+ - neindexovat
            ViewData["Robots"] = "noindex, follow";
            ViewData["Title"] = $"{CategoryName} - Diskuze (str�nka {CurrentPage})";
        }

        if (result.HasNextPage)
        {
            ViewData["NextPageUrl"] = $"{_configuration["BaseUrl"]}/Categories/{CategoryCode}?page={CurrentPage + 1}";
        }

        // Kanonick� URL - pouze pro prvn� str�nku
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
    /// Handler pro AJAX po�adavek na na�ten� pomoc� API dal�� str�nky diskuz�, vr�t� JSON s nov�mi diskuzemi a informacemi o str�nkov�n�
    /// </summary>
    /// <param name="categoryId">ID kategorie</param>
    /// <param name="currentPage">Aktu�ln� ��slo str�nky</param>
    public async Task<IActionResult> OnGetNextPageAsync(int categoryId, int currentPage)
    {
        try
        {
            // V�po�et ��sla n�sleduj�c� str�nky
            var nextPage = currentPage + 1;

            // Na�ten� dal�� str�nky diskuz� z API
            var client = _clientFactory.CreateBearerClient(HttpContext);
            var response = await client.GetAsync(
                $"{ApiBaseUrl}/discussions?categoryId={categoryId}&page={nextPage}&pageSize={PageSize}");

            if (!response.IsSuccessStatusCode)
            {
                logger.Log($"Nepoda�ilo se na��st dal�� str�nku diskuz�. StatusCode: {response.StatusCode}");
                return BadRequest("Nepoda�ilo se na��st dal�� diskuze.");
            }

            var result = await response.Content.ReadFromJsonAsync<PagedResultDto<DiscussionListDto>>();
            if (result == null)
            {
                logger.Log("Nepoda�ilo se deserializovat odpov�� z API");
                return BadRequest("Nepoda�ilo se na��st dal�� diskuze.");
            }

            // Vr�cen� dat (diskuze pro jednu str�nku plus info pro str�nkov�n�) pro JavaScript, kter� d�le bude zpracov�vat
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
            logger.Log("Chyba p�i na��t�n� dal�� str�nky diskuz�", ex);
            return BadRequest("Do�lo k chyb� p�i na��t�n� diskuz�.");
        }
    }

    /// <summary>
    /// Handler pro AJAX po�adavek na vykreslen� partial view pro jednu diskuzi
    /// </summary>
    /// <param name="discussion">Data diskuze</param>
    /// <param name="categoryCode">K�d kategorie pro vytvo�en� URL</param>
    public IActionResult OnPostDiscussionPartial([FromBody] DiscussionListDto discussion, string categoryCode)
    {
        return Partial("_DiscussionPartial", discussion);
    }
}