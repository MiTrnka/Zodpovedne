/*
Zobrazení detailu diskuze s informacemi o autorovi a poètu zobrazení
Tlaèítka like pro diskuzi a komentáøe
Správné zobrazení poètu likù
Deaktivaci tlaèítek like podle oprávnìní
Hierarchické zobrazení komentáøù a odpovìdí
Formuláø pro pøidání nového komentáøe
Tlaèítka pro odpovìdi na komentáøe
JavaScript pro asynchronní zpracování like operací
*/
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Security.Claims;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Contracts.Enums;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;

namespace Zodpovedne.Web.Pages;

/// <summary>
/// Model pro stránku zobrazující detail diskuze vèetnì komentáøù.
/// Zajišuje funkcionalitu pro zobrazení diskuze, pøidávání komentáøù a jejich správu.
/// </summary>
[IgnoreAntiforgeryToken]
public class DiscussionModel : BasePageModel
{
    public DiscussionModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger) : base(clientFactory, configuration, logger)
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
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Code kategorie získanı z URL
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string CategoryCode { get; set; } = "";

    /// <summary>
    /// Jméno kategorie diskuze
    /// </summary>
    public string CategoryName { get; set; } = "";

    /// <summary>
    /// Code diskuze získanı z URL
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string DiscussionCode { get; set; } = "";

    /// <summary>
    /// Detail diskuze vèetnì komentáøù a informací o like
    /// </summary>
    public DiscussionDetailDto? Discussion { get; set; }

    /// <summary>
    /// Všechny kategorie
    /// </summary>
    public List<CategoryDto> AllCategories { get; private set; } = new();

    /// <summary>
    /// ID vybrané kategorie pøi pøesouvání diskuze pod jinou kategorii
    /// </summary>
    [BindProperty]
    public int? SelectedCategoryId { get; set; }

    /// <summary>
    /// Model pro vytvoøení nového komentáøe
    /// Pouije se jak pro root komentáøe, tak pro odpovìdi
    /// </summary>
    [BindProperty]
    public CreateCommentDto NewComment { get; set; } = new();

    /// <summary>
    /// ID komentáøe, na kterı uivatel odpovídá
    /// Null znamená, e se jedná o novı root komentáø
    /// </summary>
    [BindProperty]
    public int? ReplyToCommentId { get; set; }

    /// <summary>
    /// Urèuje, zda pøihlášenı uivatel mùe editovat diskuzi
    /// (musí bıt buï admin nebo autor diskuze)
    /// </summary>
    public bool CanEditDiscussion =>
        Discussion != null && IsUserLoggedIn &&
        (IsAdmin || Discussion.AuthorId == CurrentUserId);

    /// <summary>
    /// Urèuje, zda je komentáø root (není reakcí na jinı komentáø)
    /// </summary>
    public bool IsRootComment(CommentDto comment) =>
        comment.ParentCommentId == null;

    /// <summary>
    /// Urèuje, zda mùe aktuální uivatel dát like diskuzi
    /// </summary>
    public bool CanLikeDiscussion => IsAdmin ||
        Discussion != null && IsUserLoggedIn &&
        Discussion.AuthorId != CurrentUserId &&
        Discussion.Likes.CanUserLike;

    /// <summary>
    /// Urèuje, zda mùe aktuální uivatel dát like komentáøi
    /// Admin mùe dát like jakémukoliv komentáøi, ostatní nemohou lajkovat své vlastní komentáøe
    /// </summary>
    public bool CanLikeComment(CommentDto comment) =>
        IsUserLoggedIn &&
        (IsAdmin || comment.AuthorNickname != User.Identity?.Name) &&
        comment.Likes.CanUserLike;

    /// <summary>
    /// Vrací CSS tøídu pro tlaèítko like podle stavu
    /// </summary>
    public string GetLikeButtonClass(bool canLike) =>
        canLike ? "btn-outline-primary" : "btn-outline-secondary";

    /// <summary>
    /// Generuje unikátní ID pro formuláø s odpovìdí na komentáø
    /// </summary>
    public string GetReplyFormId(int commentId) => $"reply-form-{commentId}";

    /// <summary>
    /// Handler pro získání detailu diskuze z API
    /// Volá se pøi naètení stránky (HTTP GET)
    /// </summary>
    public async Task<IActionResult> OnGetAsync()
    {
        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Získání detailu diskuze (1. stránka)
        var response = await client.GetAsync($"{ApiBaseUrl}/discussions/byCode/{DiscussionCode}?page=1&pageSize={PageSize}");

        if (!response.IsSuccessStatusCode)
        {
            _logger.Log($"Detail diskuze nenalezen: {DiscussionCode}");
            ErrorMessage = "Omlouváme se, ale poadovanou diskuzi se nepodaøilo naèíst.";
            return Page();
        }
        Discussion = await response.Content.ReadFromJsonAsync<DiscussionDetailDto>();
        if (Discussion == null)
        {
            _logger.Log($"Detail diskuze nenalezen: {DiscussionCode}");
            ErrorMessage = "Omlouváme se, ale poadovanou diskuzi se nepodaøilo naèíst.";
            return Page();
        }

        // Získání kategorie, protoe potøebujeme zobrazit název kategorie
        var categoryResponse = await client.GetAsync($"{ApiBaseUrl}/categories/{CategoryCode}");
        if (!categoryResponse.IsSuccessStatusCode)
        {
            _logger.Log($"Nenalezena kategorie {CategoryCode}");
            CategoryName = "Kategorie diskuze";
        }
        else
        {
            var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>();
            if (category == null)
            {
                _logger.Log("Nenalezena kategorie");
                CategoryName = "Kategorie diskuze";
            }
            else
                CategoryName = category.Name;
        }

        // Pokud je uivatel admin, naèteme seznam všech kategorií
        if (IsAdmin)
        {
            var categoriesResponse = await client.GetAsync($"{ApiBaseUrl}/categories");
            if (categoriesResponse.IsSuccessStatusCode)
            {
                AllCategories = await categoriesResponse.Content.ReadFromJsonAsync<List<CategoryDto>>() ?? new();
            }
        }

        // Inkrementujeme poèítadlo zhlédnutí dané diskuze
        await client.PostAsync($"{ApiBaseUrl}/discussions/{Discussion.Id}/increment-view-count", null);

        return Page();
    }

    /// <summary>
    /// Handler pro pøidání nového komentáøe nebo odpovìdi
    /// Volá se pøi odeslání formuláøe pro novı komentáø (HTTP POST)
    /// </summary>
    public async Task<IActionResult> OnPostAddCommentAsync()
    {
        if (!ModelState.IsValid)
        {
            _logger.Log("Neplatnı model pøi pøidávání komentáøe");
            ErrorMessage = "Omlouváme se, ale komentáø se nepodaøilo vloit.";
            return Page();
        }

        // Pokud není uivatel pøihlášen, pøesmìrujeme na login
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Naèteme znovu detail diskuze (1. stránka) pro pøípad, e se mezitím zmìnil
        var discussionResponse = await client.GetAsync($"{ApiBaseUrl}/discussions/byCode/{DiscussionCode}?page=1&pageSize={PageSize}");
        if (!discussionResponse.IsSuccessStatusCode)
        {
            _logger.Log($"Nepodaøilo se naèíst detail diskuze podlé kódu {DiscussionCode} pøi pøidávání komentáøe");
            ErrorMessage = "Omlouváme se, ale komentáø se nepodaøilo vloit.";
            return Page();
        }

        Discussion = await discussionResponse.Content.ReadFromJsonAsync<DiscussionDetailDto>();
        if (Discussion == null)
        {
            _logger.Log($"Nepodaøilo se naèíst detail diskuze podlé kódu {DiscussionCode} pøi pøidávání komentáøe");
            ErrorMessage = "Omlouváme se, ale komentáø se nepodaøilo vloit.";
            return Page();
        }

        // Sestavíme URL podle toho, zda jde o odpovìï na komentáø nebo novı root komentáø
        var url = ReplyToCommentId.HasValue
            ? $"{ApiBaseUrl}/discussions/{Discussion.Id}/comments/{ReplyToCommentId}/replies"
            : $"{ApiBaseUrl}/discussions/{Discussion.Id}/comments";

        // Odešleme poadavek na vytvoøení komentáøe
        var response = await client.PostAsJsonAsync(url, NewComment);

        // V pøípadì úspìchu se provede refresh
        if (!response.IsSuccessStatusCode)
        {
            _logger.Log($"Nepodaøilo se odeslat poadavek na pøidávání komentáøe do diskuze {DiscussionCode}");
            ErrorMessage = "Omlouváme se, ale komentáø se nepodaøilo vloit.";
            return Page();
        }

        // Pøesmìrujeme zpìt na stejnou stránku pro zobrazení nového komentáøe, provede se vlastnì refresh
        return RedirectToPage();
    }

    /// <summary>
    /// Handler pro smazání diskuze
    /// Volá se pøi odeslání formuláøe pro smazání diskuze (HTTP POST)
    /// Po úspìšném smazání pøesmìruje na kategorii
    /// </summary>
    public async Task<IActionResult> OnPostDeleteDiscussionAsync()
    {
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Naèteme detail diskuze (1. stránka) pro ovìøení existence
        var discussionResponse = await client.GetAsync($"{ApiBaseUrl}/discussions/byCode/{DiscussionCode}?page=1&pageSize={PageSize}");
        if (!discussionResponse.IsSuccessStatusCode)
        {
            _logger.Log($"Nepodaøilo se odeslat poadavek na naètení detailu diskuze {DiscussionCode}");
            ErrorMessage = "Omlouváme se, ale nepodaøilo se smazat diskuzi.";
            return Page();
        }

        Discussion = await discussionResponse.Content.ReadFromJsonAsync<DiscussionDetailDto>();
        if (Discussion == null)
            return NotFound();

        // Kontrola oprávnìní
        if (!CanEditDiscussion)
            return Forbid();

        // Zavolání endpointu pro smazání
        var response = await client.DeleteAsync($"{ApiBaseUrl}/discussions/{Discussion.Id}");

        if (!response.IsSuccessStatusCode)
        {
            // V pøípadì chyby pøidáme chybovou zprávu
            _logger.Log($"Nepodaøilo se odeslat poadavek na smazání diskuze dle Id {Discussion.Id}");
            ErrorMessage = "Omlouváme se, ale nepodaøilo se smazat diskuzi.";
            return Page();
        }

        // Po úspìšném smazání pøesmìrujeme na kategorii
        return RedirectToPage("/Category", new { categoryCode = CategoryCode });
    }

    /// <summary>
    /// Handler pro AJAX poadavek na naètení pomocí API další stránky komentáøù, vrátí JSON s novımi komentáøi a informacemi o stránkování
    /// </summary>
    /// <param name="discussionId"></param>
    /// <param name="currentPage"></param>
    /// <returns></returns>
    public async Task<IActionResult> OnGetNextPageAsync(int discussionId, int currentPage)
    {
        try
        {
            // Vıpoèet èísla následující stránky
            var nextPage = currentPage + 1;
            var client = _clientFactory.CreateBearerClient(HttpContext);

            // Naètení další stránky komentáøù z API
            var response = await client.GetAsync($"{ApiBaseUrl}/discussions/{discussionId}?page={nextPage}&pageSize={PageSize}");

            if (!response.IsSuccessStatusCode)
            {
                return BadRequest("Nepodaøilo se naèíst další komentáøe.");
            }

            var result = await response.Content.ReadFromJsonAsync<DiscussionDetailDto>();
            if (result == null)
            {
                return BadRequest("Nepodaøilo se naèíst další komentáøe.");
            }

            // Vrácení dat (komentáøe pro jednu stránku plus info pro stránkování) pro JavaScript, kterı dále bude zpracovávat
            return new JsonResult(new
            {
                comments = result.Comments,
                hasMoreComments = result.HasMoreComments,
                currentPage = nextPage
            });
        }
        catch (Exception ex)
        {
            _logger.Log("Chyba pøi naèítání dalších komentáøù", ex);
            return BadRequest("Došlo k chybì pøi naèítání komentáøù.");
        }
    }

    /// <summary>
    /// Handler pro AJAX poadavek na vykreslení partial view pro jeden komentáø
    /// </summary>
    /// <param name="comment">Data komentáøe</param>
    public IActionResult OnPostCommentPartialAsync([FromBody] CommentDto comment, int discussionId)
    {
        return Partial("_CommentPartial", comment);
    }

    /// <summary>
    /// Handler pro AJAX poadavek na zmìnu kategorie diskuze ze souèasné na tu v property SelectedCategoryId
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> OnPostChangeCategoryAsync()
    {
        if (!IsAdmin || SelectedCategoryId is null)
        {
            return RedirectToPage();
        }

        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Nejprve naèteme detail diskuze, abychom získali ID
        var discussionResponse = await client.GetAsync($"{ApiBaseUrl}/discussions/basic-info/by-code/{DiscussionCode}");
        if (!discussionResponse.IsSuccessStatusCode)
        {
            ErrorMessage = "Diskuzi se nepodaøilo naèíst.";
            return Page();
        }
        var basicDiscussionInfoDto = await discussionResponse.Content.ReadFromJsonAsync<BasicDiscussionInfoDto>();
        if (basicDiscussionInfoDto == null)
        {
            ErrorMessage = "Diskuzi se nepodaøilo naèíst.";
            return Page();
        }

        // Nyní máme ID diskuze, mùeme zavolat API pro zmìnu kategorie
        var response = await client.PutAsync(
            $"{ApiBaseUrl}/discussions/{basicDiscussionInfoDto.Id}/change-category/{SelectedCategoryId}",
            null
        );
        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Nepodaøilo se zmìnit kategorii diskuze.";
            return Page();
        }

        return RedirectToPage();
    }
}