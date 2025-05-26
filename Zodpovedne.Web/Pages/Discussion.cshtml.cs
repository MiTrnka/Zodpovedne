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
using Zodpovedne.Logging;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Ganss.Xss;
using Zodpovedne.Logging.Services;
using Zodpovedne.Web.Pages.Models;

namespace Zodpovedne.Web.Pages;

/// <summary>
/// Model pro str�nku zobrazuj�c� detail diskuze v�etn� koment���.
/// Zaji��uje funkcionalitu pro zobrazen� diskuze, p�id�v�n� koment��� a jejich spr�vu.
/// </summary>
[IgnoreAntiforgeryToken]
public class DiscussionModel : BasePageModel
{
    public DiscussionModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator) : base(clientFactory, configuration, logger, sanitizer, translator)
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
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Code kategorie z�skan� z URL
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string CategoryCode { get; set; } = "";

    /// <summary>
    /// Jm�no kategorie diskuze
    /// </summary>
    public string CategoryName { get; set; } = "";

    /// <summary>
    /// Code diskuze z�skan� z URL
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string DiscussionCode { get; set; } = "";

    /// <summary>
    /// Detail diskuze v�etn� koment��� a informac� o like
    /// </summary>
    public DiscussionDetailDto Discussion { get; set; } = new();

    /// <summary>
    /// V�echny kategorie
    /// </summary>
    public List<CategoryDto> AllCategories { get; private set; } = new();

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
        (IsAdmin || Discussion.AuthorId == UserId);

    /// <summary>
    /// Ur�uje, zda je koment�� root (nen� reakc� na jin� koment��)
    /// </summary>
    public bool IsRootComment(CommentDto comment) =>
        comment.ParentCommentId == null;

    /// <summary>
    /// Ur�uje, zda m��e aktu�ln� u�ivatel d�t nebo odebrat like diskuzi
    /// </summary>
    public bool CanLikeDiscussion =>
        Discussion != null && IsUserLoggedIn &&
        Discussion.AuthorId != UserId; // U�ivatel nem��e lajkovat sv� vlastn� diskuze

    /// <summary>
    /// Ur�uje, zda m��e aktu�ln� u�ivatel d�t nebo odebrat like koment��i
    /// </summary>
    public bool CanLikeComment(CommentDto comment) =>
        IsUserLoggedIn &&
        comment.AuthorNickname != User.Identity?.Name; // U�ivatel nem��e lajkovat sv� vlastn� koment��e

    /// <summary>
    /// Vrac� CSS t��du pro tla��tko like podle typu
    /// </summary>
    public string GetLikeButtonClass(bool isLikeButton) =>
        isLikeButton ? "like-btn" : "like-btn-disable";

    /// <summary>
    /// Generuje unik�tn� ID pro formul�� s odpov�d� na koment��
    /// </summary>
    public string GetReplyFormId(int commentId) => $"reply-form-{commentId}";

    /// <summary>
    /// Ur�uje, zda m��e aktu�ln� u�ivatel nahr�vat soubory do editoru
    /// </summary>
    public bool CanUploadFiles { get; private set; } = false;

    /// <summary>
    /// Handler pro z�sk�n� detailu diskuze z API
    /// Vol� se p�i na�ten� str�nky (HTTP GET)
    /// </summary>
    public async Task<IActionResult> OnGetAsync()
    {
        var client = _clientFactory.CreateBearerClient(HttpContext);
        // Z�sk�n� detailu diskuze (1. str�nka)
        var response = await client.GetAsync($"{ApiBaseUrl}/discussions/byCode/{DiscussionCode}?page=1&pageSize={PageSize}");

        if (!response.IsSuccessStatusCode)
        {
            logger.Log($"Detail diskuze nenalezen: {DiscussionCode}");
            ErrorMessage = "Omlouv�me se, ale po�adovanou diskuzi se nepoda�ilo na��st.";
            return Page();
        }
        var d = await response.Content.ReadFromJsonAsync<DiscussionDetailDto>();
        if (d == null)
        {
            logger.Log($"Detail diskuze nenalezen: {DiscussionCode}");
            ErrorMessage = "Omlouv�me se, ale po�adovanou diskuzi se nepoda�ilo na��st.";
            return Page();
        }
        Discussion = d;

        // P�ed�v�me ID diskuze do JavaScriptu pro pou�it� v hlasovac�m skriptu
        ViewData["DiscussionId"] = Discussion.Id;

        // Zji�t�n� typu p�ihl�en�ho u�ivatele a nastaven� opr�vn�n� pro nahr�v�n� soubor�
        if (IsUserLoggedIn)
        {
            var userResponse = await client.GetAsync($"{ApiBaseUrl}/users/authenticated-user");
            if (userResponse.IsSuccessStatusCode)
            {
                var user = await userResponse.Content.ReadFromJsonAsync<UserProfileDto>();

                // Pouze u�ivatel� typu Normal mohou nahr�vat soubory
                if (user != null && user.UserType == UserType.Normal)
                {
                    CanUploadFiles = true;
                }
            }
        }

        // Z�sk�n� kategorie, proto�e pot�ebujeme zobrazit n�zev kategorie
        var categoryResponse = await client.GetAsync($"{ApiBaseUrl}/Categories/{CategoryCode}");
        if (!categoryResponse.IsSuccessStatusCode)
        {
            logger.Log($"Nenalezena kategorie {CategoryCode}");
            CategoryName = "Kategorie diskuze";
        }
        else
        {
            var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>();
            if (category == null)
            {
                logger.Log("Nenalezena kategorie");
                CategoryName = "Kategorie diskuze";
            }
            else
                CategoryName = category.Name;
        }

        // Po na�ten� diskuze nastavit SEO data
        if (Discussion != null)
        {
            ViewData["Title"] = Discussion.Title;

            // Vytvo��me description z prvn�ch 160 znak� obsahu (bez HTML tag�)
            var plainTextContent = System.Text.RegularExpressions.Regex.Replace(Discussion.Content ?? "", "<.*?>", string.Empty);
            var description = plainTextContent.Length > 120
                ? plainTextContent.Substring(0, 117) + "..."
                : plainTextContent;

            ViewData["Description"] = $"{description} | Diskuze od {Discussion.AuthorNickname} v kategorii {CategoryName} na Discussion.cz - �esk� diskuzn� s�ti bez reklam.";
            ViewData["Keywords"] = $"{Discussion.Title}, {CategoryName}, {Discussion.AuthorNickname}, diskuze, diskuzi, koment��e, komentare, �esk� komunita, ceska komunita, discussion";

            // Pro Open Graph (bez diakritiky)
            var titleWithoutDiacritics = Discussion.Title
                .Replace("�", "r").Replace("�", "s").Replace("�", "c").Replace("�", "z").Replace("�", "y")
                .Replace("�", "a").Replace("�", "i").Replace("�", "e").Replace("�", "u").Replace("�", "u")
                .Replace("�", "t").Replace("�", "d").Replace("�", "n").Replace("�", "R").Replace("�", "S")
                .Replace("�", "C").Replace("�", "Z").Replace("�", "Y").Replace("�", "A").Replace("�", "I")
                .Replace("�", "E").Replace("�", "U").Replace("�", "U").Replace("�", "T").Replace("�", "D").Replace("�", "N");

            var descriptionWithoutDiacritics = description
                .Replace("�", "r").Replace("�", "s").Replace("�", "c").Replace("�", "z").Replace("�", "y")
                .Replace("�", "a").Replace("�", "i").Replace("�", "e").Replace("�", "u").Replace("�", "u")
                .Replace("�", "t").Replace("�", "d").Replace("�", "n").Replace("�", "R").Replace("�", "S")
                .Replace("�", "C").Replace("�", "Z").Replace("�", "Y").Replace("�", "A").Replace("�", "I")
                .Replace("�", "E").Replace("�", "U").Replace("�", "U").Replace("�", "T").Replace("�", "D").Replace("�", "N");

            ViewData["OGTitle"] = titleWithoutDiacritics;
            ViewData["OGDescription"] = $"{descriptionWithoutDiacritics} | Diskuze od {Discussion.AuthorNickname} na Discussion.cz";
            ViewData["OGType"] = "article";

            // Pro diskuze p�id�me dal�� meta tagy
            ViewData["ArticleAuthor"] = Discussion.AuthorNickname;
            ViewData["ArticlePublishedTime"] = Discussion.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ");
            ViewData["ArticleModifiedTime"] = Discussion.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        // Pokud je u�ivatel admin, na�teme seznam v�ech kategori�, aby mohl admin mezi nimi p�esouvat diskuze
        if (IsAdmin)
        {
            var categoriesResponse = await client.GetAsync($"{ApiBaseUrl}/Categories");
            if (categoriesResponse.IsSuccessStatusCode)
            {
                AllCategories = await categoriesResponse.Content.ReadFromJsonAsync<List<CategoryDto>>() ?? new();
            }
        }

        // Inkrementujeme po��tadlo zhl�dnut� dan� diskuze
        await client.PostAsync($"{ApiBaseUrl}/discussions/{Discussion!.Id}/increment-view-count", null);

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
            logger.Log("Nepoda�ilo se vlo�it koment��, p�ekro�ila se maxim�ln� velikost");
            ErrorMessage = "Omlouv�me se, ale koment�� se nepoda�ilo vlo�it, p�ekro�ili jste maxim�ln� velikost 1000 znak�.";
            return Page();
        }

        // Pokud nen� u�ivatel p�ihl�en, p�esm�rujeme na login
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Nejprve na�teme detail diskuze, abychom z�skali jej� ID
        var discussionResponse = await client.GetAsync($"{ApiBaseUrl}/discussions/basic-info/by-code/{DiscussionCode}");
        if (!discussionResponse.IsSuccessStatusCode)
        {
            ErrorMessage = "Diskuzi se nepoda�ilo na��st.";
            return Page();
        }
        var basicDiscussionInfoDto = await discussionResponse.Content.ReadFromJsonAsync<BasicDiscussionInfoDto>();
        if (basicDiscussionInfoDto == null)
        {
            ErrorMessage = "Diskuzi se nepoda�ilo na��st.";
            return Page();
        }

        // Sestav�me URL podle toho, zda jde o odpov�� na koment�� nebo nov� root koment��
        var url = ReplyToCommentId.HasValue
            ? $"{ApiBaseUrl}/discussions/{basicDiscussionInfoDto.Id}/comments/{ReplyToCommentId}/replies"
            : $"{ApiBaseUrl}/discussions/{basicDiscussionInfoDto.Id}/comments";

        // Ode�leme po�adavek na vytvo�en� koment��e
        var response = await client.PostAsJsonAsync(url, NewComment);

        // V p��pad� �sp�chu se provede refresh
        if (!response.IsSuccessStatusCode)
        {
            logger.Log($"Nepoda�ilo se odeslat po�adavek na p�id�v�n� koment��e do diskuze {DiscussionCode}");
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

        // Nejprve na�teme detail diskuze, abychom z�skali jej� ID
        var discussionResponse = await client.GetAsync($"{ApiBaseUrl}/discussions/basic-info/by-code/{DiscussionCode}");
        if (!discussionResponse.IsSuccessStatusCode)
        {
            ErrorMessage = "Diskuzi se nepoda�ilo na��st.";
            return Page();
        }
        var basicDiscussionInfoDto = await discussionResponse.Content.ReadFromJsonAsync<BasicDiscussionInfoDto>();
        if (basicDiscussionInfoDto == null)
        {
            ErrorMessage = "Diskuzi se nepoda�ilo na��st.";
            return Page();
        }

        // Kontrola opr�vn�n� na smaz�n� diskuze
        if (!(IsAdmin || basicDiscussionInfoDto.AuthorId == UserId))
        {
            logger.Log($"U�ivatel {UserId} nem� opr�vn�n� na smaz�n� diskuze {basicDiscussionInfoDto.Id}: {basicDiscussionInfoDto.Title}");
            ErrorMessage = "Nem�te opr�vn�n� na smaz�n� t�to diskuze.";
            return Page();
        }

        // Zavol�n� endpointu pro smaz�n�
        var response = await client.DeleteAsync($"{ApiBaseUrl}/discussions/{basicDiscussionInfoDto.Id}");

        if (!response.IsSuccessStatusCode)
        {
            // V p��pad� chyby p�id�me chybovou zpr�vu
            logger.Log($"Nepoda�ilo se odeslat po�adavek na smaz�n� diskuze dle Id {basicDiscussionInfoDto.Id}");
            ErrorMessage = "Omlouv�me se, ale nepoda�ilo se smazat diskuzi.";
            return Page();
        }

        // Po �sp�n�m smaz�n� p�esm�rujeme na kategorii
        return RedirectToPage("/Category", new { categoryCode = CategoryCode });
    }

    /// <summary>
    /// Handler pro AJAX po�adavek na na�ten� pomoc� API dal�� str�nky koment���, vr�t� JSON s nov�mi koment��i a informacemi o str�nkov�n�
    /// </summary>
    /// <param name="discussionId"></param>
    /// <param name="currentPage"></param>
    /// <returns></returns>
    public async Task<IActionResult> OnGetNextPageAsync(int discussionId, int currentPage)
    {
        try
        {
            // V�po�et ��sla n�sleduj�c� str�nky
            var nextPage = currentPage + 1;
            var client = _clientFactory.CreateBearerClient(HttpContext);

            // Na�ten� dal�� str�nky koment��� z API
            var response = await client.GetAsync($"{ApiBaseUrl}/discussions/{discussionId}?page={nextPage}&pageSize={PageSize}");

            if (!response.IsSuccessStatusCode)
            {
                return BadRequest("Nepoda�ilo se na��st dal�� koment��e.");
            }

            var result = await response.Content.ReadFromJsonAsync<DiscussionDetailDto>();
            if (result == null)
            {
                return BadRequest("Nepoda�ilo se na��st dal�� koment��e.");
            }

            // Vr�cen� dat (koment��e pro jednu str�nku plus info pro str�nkov�n�) pro JavaScript, kter� d�le bude zpracov�vat
            return new JsonResult(new
            {
                comments = result.Comments,
                hasMoreComments = result.HasMoreComments,
                currentPage = nextPage
            });
        }
        catch (Exception ex)
        {
            logger.Log("Chyba p�i na��t�n� dal��ch koment���", ex);
            return BadRequest("Do�lo k chyb� p�i na��t�n� koment���.");
        }
    }

    /// <summary>
    /// Handler pro AJAX po�adavek na vykreslen� partial view pro jeden koment��
    /// </summary>
    /// <param name="comment">Data koment��e</param>
    public IActionResult OnPostCommentPartialAsync([FromBody] CommentDto comment)
    {
        return Partial("_CommentPartial", comment);
    }

    /// <summary>
    /// Handler pro AJAX po�adavek na zm�nu kategorie diskuze ze sou�asn� na tu v property SelectedCategoryId
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> OnPostChangeCategoryAsync(int newCategoryId)
    {
        if (!IsAdmin)
        {
            return RedirectToPage();
        }

        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Nejprve na�teme detail diskuze, abychom z�skali jej� ID
        var discussionResponse = await client.GetAsync($"{ApiBaseUrl}/discussions/basic-info/by-code/{DiscussionCode}");
        if (!discussionResponse.IsSuccessStatusCode)
        {
            ErrorMessage = "Diskuzi se nepoda�ilo na��st.";
            return Page();
        }
        var basicDiscussionInfoDto = await discussionResponse.Content.ReadFromJsonAsync<BasicDiscussionInfoDto>();
        if (basicDiscussionInfoDto == null)
        {
            ErrorMessage = "Diskuzi se nepoda�ilo na��st.";
            return Page();
        }

        // Nyn� m�me ID diskuze, m��eme zavolat API pro zm�nu kategorie
        var response = await client.PutAsync(
            $"{ApiBaseUrl}/discussions/{basicDiscussionInfoDto.Id}/change-category/{newCategoryId}",
            null
        );
        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Nepoda�ilo se zm�nit kategorii diskuze.";
            return Page();
        }

        return RedirectToPage();
    }

    /// <summary>
    /// Vrac� CSS t��du pro zobrazen� informace o stavu hlasov�n�
    /// </summary>
    public string GetVotingStatusBadgeClass(VoteType voteType) =>
        voteType switch
        {
            VoteType.Visible => "bg-success",
            VoteType.Closed => "bg-secondary",
            VoteType.Hidden => "bg-warning",
            _ => "bg-light"
        };

    /// <summary>
    /// Vrac� textov� popis stavu hlasov�n�
    /// </summary>
    public string GetVotingStatusText(VoteType voteType) =>
        voteType switch
        {
            VoteType.Visible => "V tomto hlasov�n� m��ete vyj�d�it sv�j n�zor ke ka�d� ot�zce. Zvolte Ano, Ne, nebo Nehlasuji.",
            VoteType.Closed => "Hlasov�n� je uzav�eno. M��ete vid�t v�sledky, ale ji� nelze hlasovat.",
            VoteType.Hidden => "Toto hlasov�n� je nyn� skryt�. Vid�te ho, proto�e jste autor diskuze.",
            _ => "Neplatn� stav hlasov�n�"
        };
}