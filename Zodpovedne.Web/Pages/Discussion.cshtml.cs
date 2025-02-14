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
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;

namespace Zodpovedne.Web.Pages;

/// <summary>
/// Model pro str�nku zobrazuj�c� detail diskuze v�etn� koment���.
/// Zaji��uje funkcionalitu pro zobrazen� diskuze, p�id�v�n� koment��� a jejich spr�vu.
/// </summary>
public class DiscussionModel : BasePageModel
{
    public DiscussionModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger) : base(clientFactory, configuration, logger)
    {
    }

    /// <summary>
    /// Jm�no kategorie diskuze
    /// </summary>
    public string CategoryName { get; set; } = "";


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
    /// Ur�uje, zda p�ihl�en� u�ivatel m��e editovat diskuzi
    /// (mus� b�t bu� admin nebo autor diskuze)
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

    /// <summary>
    /// Handler pro z�sk�n� detailu diskuze z API
    /// Vol� se p�i na�ten� str�nky (HTTP GET)
    /// </summary>
    public async Task<IActionResult> OnGetAsync()
    {
        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Z�sk�n� detailu diskuze
        var response = await client.GetAsync($"{ApiBaseUrl}/discussions/byCode/{DiscussionCode}");

        if (!response.IsSuccessStatusCode)
        {
            _logger.Log($"Detail diskuze nenalezen: {DiscussionCode}");
            ErrorMessage = "Omlouv�me se, ale po�adovanou diskuzi se nepoda�ilo na��st.";
            return Page();
        }
        Discussion = await response.Content.ReadFromJsonAsync<DiscussionDetailDto>();
        if (Discussion == null)
        {
            _logger.Log($"Detail diskuze nenalezen: {DiscussionCode}");
            ErrorMessage = "Omlouv�me se, ale po�adovanou diskuzi se nepoda�ilo na��st.";
            return Page();
        }

        // Z�sk�n� kategorie, proto�e pot�ebujeme zobrazit n�zev kategorie
        var categoryResponse = await client.GetAsync($"{ApiBaseUrl}/categories/{CategoryCode}");
        if (!categoryResponse.IsSuccessStatusCode)
        {
            _logger.Log("Nenalezena kategorie");
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

        // Inkrementujeme po��tadlo zhl�dnut� dan� diskuze
        await client.PostAsync($"{ApiBaseUrl}/discussions/{Discussion.Id}/increment-view-count", null);

        return Page();
    }

    /// <summary>
    /// Handler pro p�id�n� nov�ho koment��e nebo odpov�di
    /// Vol� se p�i odesl�n� formul��e pro nov� koment�� (HTTP POST)
    /// </summary>
    public async Task<IActionResult> OnPostAddCommentAsync()
    {
        if (!ModelState.IsValid)
        {
            _logger.Log("Neplatn� model p�i p�id�v�n� koment��e");
            ErrorMessage = "Omlouv�me se, ale koment�� se nepoda�ilo vlo�it.";
            return Page();
        }

        // Pokud nen� u�ivatel p�ihl�en, p�esm�rujeme na login
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Na�teme znovu detail diskuze pro p��pad, �e se mezit�m zm�nil
        var discussionResponse = await client.GetAsync($"{ApiBaseUrl}/discussions/byCode/{DiscussionCode}");
        if (!discussionResponse.IsSuccessStatusCode)
        {
            _logger.Log($"Nepoda�ilo se na��st detail diskuze podl� k�du {DiscussionCode} p�i p�id�v�n� koment��e");
            ErrorMessage = "Omlouv�me se, ale koment�� se nepoda�ilo vlo�it.";
            return Page();
        }

        Discussion = await discussionResponse.Content.ReadFromJsonAsync<DiscussionDetailDto>();
        if (Discussion == null)
        {
            _logger.Log($"Nepoda�ilo se na��st detail diskuze podl� k�du {DiscussionCode} p�i p�id�v�n� koment��e");
            ErrorMessage = "Omlouv�me se, ale koment�� se nepoda�ilo vlo�it.";
            return Page();
        }

        // Sestav�me URL podle toho, zda jde o odpov�� na koment�� nebo nov� root koment��
        var url = ReplyToCommentId.HasValue
            ? $"{ApiBaseUrl}/discussions/{Discussion.Id}/comments/{ReplyToCommentId}/replies"
            : $"{ApiBaseUrl}/discussions/{Discussion.Id}/comments";

        // Ode�leme po�adavek na vytvo�en� koment��e
        var response = await client.PostAsJsonAsync(url, NewComment);

        // V p��pad� �sp�chu se provede refresh
        if (!response.IsSuccessStatusCode)
        {
            _logger.Log($"Nepoda�ilo se odeslat po�adavek na p�id�v�n� koment��e do diskuze {DiscussionCode}");
            ErrorMessage = "Omlouv�me se, ale koment�� se nepoda�ilo vlo�it.";
            return Page();
        }

        // P�esm�rujeme zp�t na stejnou str�nku pro zobrazen� nov�ho koment��e, provede se vlastn� refresh
        return RedirectToPage();
    }

    /// <summary>
    /// Handler pro smaz�n� diskuze
    /// Vol� se p�i odesl�n� formul��e pro smaz�n� diskuze (HTTP POST)
    /// Po �sp�n�m smaz�n� p�esm�ruje na kategorii
    /// </summary>
    public async Task<IActionResult> OnPostDeleteDiscussionAsync()
    {
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Na�teme diskuzi pro ov��en� existence
        var discussionResponse = await client.GetAsync($"{ApiBaseUrl}/discussions/byCode/{DiscussionCode}");
        if (!discussionResponse.IsSuccessStatusCode)
        {
            _logger.Log($"Nepoda�ilo se odeslat po�adavek na na�ten� detailu diskuze {DiscussionCode}");
            ErrorMessage = "Omlouv�me se, ale nepoda�ilo se smazat diskuzi.";
            return Page();
        }

        Discussion = await discussionResponse.Content.ReadFromJsonAsync<DiscussionDetailDto>();
        if (Discussion == null)
            return NotFound();

        // Kontrola opr�vn�n�
        if (!CanEditDiscussion)
            return Forbid();

        // Zavol�n� endpointu pro smaz�n�
        var response = await client.DeleteAsync($"{ApiBaseUrl}/discussions/{Discussion.Id}");

        if (response.IsSuccessStatusCode)
        {
            // V p��pad� chyby p�id�me chybovou zpr�vu
            _logger.Log($"Nepoda�ilo se odeslat po�adavek na smaz�n� diskuze dle Id {Discussion.Id}");
            ErrorMessage = "Omlouv�me se, ale nepoda�ilo se smazat diskuzi.";
            return Page();
        }

        // Po �sp�n�m smaz�n� p�esm�rujeme na kategorii
        return RedirectToPage("/Category", new { categoryCode = CategoryCode });
    }
}