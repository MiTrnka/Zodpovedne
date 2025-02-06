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

namespace Zodpovedne.Web.Pages;

/// <summary>
/// Model pro stránku zobrazující detail diskuze vèetnì komentáøù.
/// Zajišuje funkcionalitu pro zobrazení diskuze, pøidávání komentáøù a jejich správu.
/// </summary>
public class DiscussionModel : BasePageModel
{
    public DiscussionModel(IHttpClientFactory clientFactory, IConfiguration configuration) : base(clientFactory, configuration)
    {
    }

    /// <summary>
    /// Code kategorie získanı z URL
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string CategoryCode { get; set; } = "";

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
    /// Handler pro získání detailu diskuze z API
    /// Volá se pøi naètení stránky (HTTP GET)
    /// </summary>
    public async Task<IActionResult> OnGetAsync()
    {
        var client = _clientFactory.CreateBearerClient(HttpContext);

        if (IsUserLoggedIn)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtToken);
        }

        var response = await client.GetAsync($"{ApiBaseUrl}/api/discussions/byCode/{DiscussionCode}");
        if (!response.IsSuccessStatusCode)
            return NotFound();

        Discussion = await response.Content.ReadFromJsonAsync<DiscussionDetailDto>();
        if (Discussion == null)
            return NotFound();

        // Inkrementujeme poèítadlo zhlédnutí dané diskuze
        await client.PostAsync(
            $"{_configuration["ApiBaseUrl"]}/api/discussions/{Discussion.Id}/increment-view",
            null
        );

        return Page();
    }

    /// <summary>
    /// Handler pro pøidání nového komentáøe nebo odpovìdi
    /// Volá se pøi odeslání formuláøe pro novı komentáø (HTTP POST)
    /// </summary>
    public async Task<IActionResult> OnPostAddCommentAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        // Pokud není uivatel pøihlášen, pøesmìrujeme na login
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtToken);

        // Naèteme znovu detail diskuze pro pøípad, e se mezitím zmìnil
        var discussionResponse = await client.GetAsync($"{ApiBaseUrl}/api/discussions/byCode/{DiscussionCode}");
        if (!discussionResponse.IsSuccessStatusCode)
            return NotFound();

        Discussion = await discussionResponse.Content.ReadFromJsonAsync<DiscussionDetailDto>();
        if (Discussion == null)
            return NotFound();

        // Sestavíme URL podle toho, zda jde o odpovìï na komentáø nebo novı root komentáø
        var url = ReplyToCommentId.HasValue
            ? $"{ApiBaseUrl}/api/discussions/{Discussion.Id}/comments/{ReplyToCommentId}/replies"
            : $"{ApiBaseUrl}/api/discussions/{Discussion.Id}/comments";

        // Odešleme poadavek na vytvoøení komentáøe
        var response = await client.PostAsJsonAsync(url, NewComment);

        if (response.IsSuccessStatusCode)
        {
            // Pøesmìrujeme zpìt na stejnou stránku pro zobrazení nového komentáøe
            return RedirectToPage();
        }

        // V pøípadì chyby pøidáme chybovou zprávu
        ModelState.AddModelError("", "Nepodaøilo se pøidat komentáø.");
        return Page();
    }

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
    public bool CanLikeDiscussion =>
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
}