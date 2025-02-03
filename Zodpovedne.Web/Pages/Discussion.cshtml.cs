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

namespace Zodpovedne.Web.Pages;

/// <summary>
/// Model pro stránku zobrazující detail diskuze vèetnì komentáøù
/// </summary>
public class DiscussionModel : PageModel
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;

    public DiscussionModel(IHttpClientFactory clientFactory, IConfiguration configuration)
    {
        _clientFactory = clientFactory;
        _configuration = configuration;
    }

    /// <summary>
    /// Code kategorie z URL
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string CategoryCode { get; set; } = "";

    /// <summary>
    /// Code diskuze z URL
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string DiscussionCode { get; set; } = "";

    /// <summary>
    /// Detail diskuze vèetnì komentáøù a informací o like
    /// </summary>
    public DiscussionDetailDto? Discussion { get; set; }

    /// <summary>
    /// Base URL pro API endpointy
    /// </summary>
    public string ApiBaseUrl => _configuration["ApiBaseUrl"] ?? "";

    /// <summary>
    /// JWT token aktuálnì pøihlášeného uživatele
    /// </summary>
    public string? JwtToken => HttpContext.Session.GetString("JWTToken");

    /// <summary>
    /// ID aktuálnì pøihlášeného uživatele
    /// </summary>
    public string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Indikuje, zda je pøihlášený uživatel admin
    /// </summary>
    public bool IsAdmin => User.IsInRole("Admin");

    /// <summary>
    /// Indikuje, zda je uživatel pøihlášen
    /// </summary>
    public bool IsUserLoggedIn => !string.IsNullOrEmpty(JwtToken);

    /// <summary>
    /// Získá detail diskuze z API
    /// </summary>
    public async Task<IActionResult> OnGetAsync()
    {
        var client = _clientFactory.CreateClient();

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

        return Page();
    }

    /// <summary>
    /// Urèuje, zda aktuální uživatel mùže editovat diskuzi
    /// </summary>
    public bool CanEditDiscussion =>
        Discussion != null && IsUserLoggedIn &&
        (IsAdmin || Discussion.AuthorId == CurrentUserId);

    /// <summary>
    /// Urèuje, zda je komentáø root (není reakcí na jiný komentáø)
    /// </summary>
    public bool IsRootComment(CommentDto comment) =>
        comment.ParentCommentId == null;

    /// <summary>
    /// Urèuje, zda mùže aktuální uživatel dát like diskuzi
    /// </summary>
    public bool CanLikeDiscussion =>
        Discussion != null && IsUserLoggedIn &&
        Discussion.AuthorId != CurrentUserId &&
        Discussion.Likes.CanUserLike;

    /// <summary>
    /// Urèuje, zda mùže aktuální uživatel dát like komentáøi
    /// </summary>
    public bool CanLikeComment(CommentDto comment) =>
        IsUserLoggedIn && comment.AuthorNickname != User.Identity?.Name &&
        comment.Likes.CanUserLike;

    /// <summary>
    /// Vrací tøídu pro tlaèítko like podle stavu
    /// </summary>
    public string GetLikeButtonClass(bool canLike) =>
        canLike ? "btn-outline-primary" : "btn-outline-secondary";
}