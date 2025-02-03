/*
Zobrazen� detailu diskuze s informacemi o autorovi a po�tu zobrazen�
Tla��tka like pro diskuzi a koment��e
Spr�vn� zobrazen� po�tu lik�
Deaktivaci tla��tek like podle opr�vn�n�
Hierarchick� zobrazen� koment��� a odpov�d�
Formul�� pro p�id�n� nov�ho koment��e
Tla��tka pro odpov�di na koment��e
JavaScript pro asynchronn� zpracov�n� like operac�
*/
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Security.Claims;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Contracts.Enums;

namespace Zodpovedne.Web.Pages;

/// <summary>
/// Model pro str�nku zobrazuj�c� detail diskuze v�etn� koment���
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
    /// Detail diskuze v�etn� koment��� a informac� o like
    /// </summary>
    public DiscussionDetailDto? Discussion { get; set; }

    /// <summary>
    /// Base URL pro API endpointy
    /// </summary>
    public string ApiBaseUrl => _configuration["ApiBaseUrl"] ?? "";

    /// <summary>
    /// JWT token aktu�ln� p�ihl�en�ho u�ivatele
    /// </summary>
    public string? JwtToken => HttpContext.Session.GetString("JWTToken");

    /// <summary>
    /// ID aktu�ln� p�ihl�en�ho u�ivatele
    /// </summary>
    public string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Indikuje, zda je p�ihl�en� u�ivatel admin
    /// </summary>
    public bool IsAdmin => User.IsInRole("Admin");

    /// <summary>
    /// Indikuje, zda je u�ivatel p�ihl�en
    /// </summary>
    public bool IsUserLoggedIn => !string.IsNullOrEmpty(JwtToken);

    /// <summary>
    /// Z�sk� detail diskuze z API
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
    /// Ur�uje, zda aktu�ln� u�ivatel m��e editovat diskuzi
    /// </summary>
    public bool CanEditDiscussion =>
        Discussion != null && IsUserLoggedIn &&
        (IsAdmin || Discussion.AuthorId == CurrentUserId);

    /// <summary>
    /// Ur�uje, zda je koment�� root (nen� reakc� na jin� koment��)
    /// </summary>
    public bool IsRootComment(CommentDto comment) =>
        comment.ParentCommentId == null;

    /// <summary>
    /// Ur�uje, zda m��e aktu�ln� u�ivatel d�t like diskuzi
    /// </summary>
    public bool CanLikeDiscussion =>
        Discussion != null && IsUserLoggedIn &&
        Discussion.AuthorId != CurrentUserId &&
        Discussion.Likes.CanUserLike;

    /// <summary>
    /// Ur�uje, zda m��e aktu�ln� u�ivatel d�t like koment��i
    /// </summary>
    public bool CanLikeComment(CommentDto comment) =>
        IsUserLoggedIn && comment.AuthorNickname != User.Identity?.Name &&
        comment.Likes.CanUserLike;

    /// <summary>
    /// Vrac� t��du pro tla��tko like podle stavu
    /// </summary>
    public string GetLikeButtonClass(bool canLike) =>
        canLike ? "btn-outline-primary" : "btn-outline-secondary";
}