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
/// Model pro str�nku zobrazuj�c� detail diskuze v�etn� koment���.
/// Zaji��uje funkcionalitu pro zobrazen� diskuze, p�id�v�n� koment��� a jejich spr�vu.
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
    /// Code kategorie z�skan� z URL
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string CategoryCode { get; set; } = "";

    /// <summary>
    /// Code diskuze z�skan� z URL
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string DiscussionCode { get; set; } = "";

    /// <summary>
    /// Detail diskuze v�etn� koment��� a informac� o like
    /// </summary>
    public DiscussionDetailDto? Discussion { get; set; }

    /// <summary>
    /// Model pro vytvo�en� nov�ho koment��e
    /// Pou�ije se jak pro root koment��e, tak pro odpov�di
    /// </summary>
    [BindProperty]
    public CreateCommentDto NewComment { get; set; } = new();

    /// <summary>
    /// ID koment��e, na kter� u�ivatel odpov�d�
    /// Null znamen�, �e se jedn� o nov� root koment��
    /// </summary>
    [BindProperty]
    public int? ReplyToCommentId { get; set; }

    /// <summary>
    /// Base URL pro API endpointy z�skan� z konfigurace
    /// </summary>
    public string ApiBaseUrl => _configuration["ApiBaseUrl"] ?? "";

    /// <summary>
    /// JWT token aktu�ln� p�ihl�en�ho u�ivatele z�skan� ze session
    /// </summary>
    public string? JwtToken => HttpContext.Session.GetString("JWTToken");

    /// <summary>
    /// ID aktu�ln� p�ihl�en�ho u�ivatele z�skan� z claims
    /// </summary>
    public string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Indikuje, zda je p�ihl�en� u�ivatel v roli admin
    /// </summary>
    public bool IsAdmin => User.IsInRole("Admin");

    /// <summary>
    /// Indikuje, zda je u�ivatel p�ihl�en (m� platn� JWT token)
    /// </summary>
    public bool IsUserLoggedIn => !string.IsNullOrEmpty(JwtToken);

    /// <summary>
    /// Handler pro z�sk�n� detailu diskuze z API
    /// Vol� se p�i na�ten� str�nky (HTTP GET)
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
    /// Handler pro p�id�n� nov�ho koment��e nebo odpov�di
    /// Vol� se p�i odesl�n� formul��e pro nov� koment�� (HTTP POST)
    /// </summary>
    public async Task<IActionResult> OnPostAddCommentAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        // Pokud nen� u�ivatel p�ihl�en, p�esm�rujeme na login
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtToken);

        // Na�teme znovu detail diskuze pro p��pad, �e se mezit�m zm�nil
        var discussionResponse = await client.GetAsync($"{ApiBaseUrl}/api/discussions/byCode/{DiscussionCode}");
        if (!discussionResponse.IsSuccessStatusCode)
            return NotFound();

        Discussion = await discussionResponse.Content.ReadFromJsonAsync<DiscussionDetailDto>();
        if (Discussion == null)
            return NotFound();

        // Sestav�me URL podle toho, zda jde o odpov�� na koment�� nebo nov� root koment��
        var url = ReplyToCommentId.HasValue
            ? $"{ApiBaseUrl}/api/discussions/{Discussion.Id}/comments/{ReplyToCommentId}/replies"
            : $"{ApiBaseUrl}/api/discussions/{Discussion.Id}/comments";

        // Ode�leme po�adavek na vytvo�en� koment��e
        var response = await client.PostAsJsonAsync(url, NewComment);

        if (response.IsSuccessStatusCode)
        {
            // P�esm�rujeme zp�t na stejnou str�nku pro zobrazen� nov�ho koment��e
            return RedirectToPage();
        }

        // V p��pad� chyby p�id�me chybovou zpr�vu
        ModelState.AddModelError("", "Nepoda�ilo se p�idat koment��.");
        return Page();
    }

    /// <summary>
    /// Ur�uje, zda aktu�ln� u�ivatel m��e editovat diskuzi
    /// (m��e admin nebo autor diskuze)
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
    /// Admin m��e d�t like jak�mukoliv koment��i, ostatn� nemohou lajkovat sv� vlastn� koment��e
    /// </summary>
    public bool CanLikeComment(CommentDto comment) =>
        IsUserLoggedIn &&
        (IsAdmin || comment.AuthorNickname != User.Identity?.Name) &&
        comment.Likes.CanUserLike;

    /// <summary>
    /// Vrac� CSS t��du pro tla��tko like podle stavu
    /// </summary>
    public string GetLikeButtonClass(bool canLike) =>
        canLike ? "btn-outline-primary" : "btn-outline-secondary";

    /// <summary>
    /// Generuje unik�tn� ID pro formul�� s odpov�d� na koment��
    /// </summary>
    public string GetReplyFormId(int commentId) => $"reply-form-{commentId}";
}