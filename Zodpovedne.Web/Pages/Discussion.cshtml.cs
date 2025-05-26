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
using Zodpovedne.Logging;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Ganss.Xss;
using Zodpovedne.Logging.Services;
using Zodpovedne.Web.Pages.Models;

namespace Zodpovedne.Web.Pages;

/// <summary>
/// Model pro stránku zobrazující detail diskuze vèetnì komentáøù.
/// Zajišuje funkcionalitu pro zobrazení diskuze, pøidávání komentáøù a jejich správu.
/// </summary>
[IgnoreAntiforgeryToken]
public class DiscussionModel : BasePageModel
{
    public DiscussionModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator) : base(clientFactory, configuration, logger, sanitizer, translator)
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
    public DiscussionDetailDto Discussion { get; set; } = new();

    /// <summary>
    /// Všechny kategorie
    /// </summary>
    public List<CategoryDto> AllCategories { get; private set; } = new();

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
        (IsAdmin || Discussion.AuthorId == UserId);

    /// <summary>
    /// Urèuje, zda je komentáø root (není reakcí na jinı komentáø)
    /// </summary>
    public bool IsRootComment(CommentDto comment) =>
        comment.ParentCommentId == null;

    /// <summary>
    /// Urèuje, zda mùe aktuální uivatel dát nebo odebrat like diskuzi
    /// </summary>
    public bool CanLikeDiscussion =>
        Discussion != null && IsUserLoggedIn &&
        Discussion.AuthorId != UserId; // Uivatel nemùe lajkovat své vlastní diskuze

    /// <summary>
    /// Urèuje, zda mùe aktuální uivatel dát nebo odebrat like komentáøi
    /// </summary>
    public bool CanLikeComment(CommentDto comment) =>
        IsUserLoggedIn &&
        comment.AuthorNickname != User.Identity?.Name; // Uivatel nemùe lajkovat své vlastní komentáøe

    /// <summary>
    /// Vrací CSS tøídu pro tlaèítko like podle typu
    /// </summary>
    public string GetLikeButtonClass(bool isLikeButton) =>
        isLikeButton ? "like-btn" : "like-btn-disable";

    /// <summary>
    /// Generuje unikátní ID pro formuláø s odpovìdí na komentáø
    /// </summary>
    public string GetReplyFormId(int commentId) => $"reply-form-{commentId}";

    /// <summary>
    /// Urèuje, zda mùe aktuální uivatel nahrávat soubory do editoru
    /// </summary>
    public bool CanUploadFiles { get; private set; } = false;

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
            logger.Log($"Detail diskuze nenalezen: {DiscussionCode}");
            ErrorMessage = "Omlouváme se, ale poadovanou diskuzi se nepodaøilo naèíst.";
            return Page();
        }
        var d = await response.Content.ReadFromJsonAsync<DiscussionDetailDto>();
        if (d == null)
        {
            logger.Log($"Detail diskuze nenalezen: {DiscussionCode}");
            ErrorMessage = "Omlouváme se, ale poadovanou diskuzi se nepodaøilo naèíst.";
            return Page();
        }
        Discussion = d;

        // Pøedáváme ID diskuze do JavaScriptu pro pouití v hlasovacím skriptu
        ViewData["DiscussionId"] = Discussion.Id;

        // Zjištìní typu pøihlášeného uivatele a nastavení oprávnìní pro nahrávání souborù
        if (IsUserLoggedIn)
        {
            var userResponse = await client.GetAsync($"{ApiBaseUrl}/users/authenticated-user");
            if (userResponse.IsSuccessStatusCode)
            {
                var user = await userResponse.Content.ReadFromJsonAsync<UserProfileDto>();

                // Pouze uivatelé typu Normal mohou nahrávat soubory
                if (user != null && user.UserType == UserType.Normal)
                {
                    CanUploadFiles = true;
                }
            }
        }

        // Získání kategorie, protoe potøebujeme zobrazit název kategorie
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

        // Po naètení diskuze nastavit SEO data
        if (Discussion != null)
        {
            ViewData["Title"] = Discussion.Title;

            // Vytvoøíme description z prvních 160 znakù obsahu (bez HTML tagù)
            var plainTextContent = System.Text.RegularExpressions.Regex.Replace(Discussion.Content ?? "", "<.*?>", string.Empty);
            var description = plainTextContent.Length > 120
                ? plainTextContent.Substring(0, 117) + "..."
                : plainTextContent;

            ViewData["Description"] = $"{description} | Diskuze od {Discussion.AuthorNickname} v kategorii {CategoryName} na Discussion.cz - èeské diskuzní síti bez reklam.";
            ViewData["Keywords"] = $"{Discussion.Title}, {CategoryName}, {Discussion.AuthorNickname}, diskuze, diskuzi, komentáøe, komentare, èeská komunita, ceska komunita, discussion";

            // Pro Open Graph (bez diakritiky)
            var titleWithoutDiacritics = Discussion.Title
                .Replace("ø", "r").Replace("š", "s").Replace("è", "c").Replace("", "z").Replace("ı", "y")
                .Replace("á", "a").Replace("í", "i").Replace("é", "e").Replace("ù", "u").Replace("ú", "u")
                .Replace("", "t").Replace("ï", "d").Replace("ò", "n").Replace("Ø", "R").Replace("Š", "S")
                .Replace("È", "C").Replace("", "Z").Replace("İ", "Y").Replace("Á", "A").Replace("Í", "I")
                .Replace("É", "E").Replace("Ù", "U").Replace("Ú", "U").Replace("", "T").Replace("Ï", "D").Replace("Ò", "N");

            var descriptionWithoutDiacritics = description
                .Replace("ø", "r").Replace("š", "s").Replace("è", "c").Replace("", "z").Replace("ı", "y")
                .Replace("á", "a").Replace("í", "i").Replace("é", "e").Replace("ù", "u").Replace("ú", "u")
                .Replace("", "t").Replace("ï", "d").Replace("ò", "n").Replace("Ø", "R").Replace("Š", "S")
                .Replace("È", "C").Replace("", "Z").Replace("İ", "Y").Replace("Á", "A").Replace("Í", "I")
                .Replace("É", "E").Replace("Ù", "U").Replace("Ú", "U").Replace("", "T").Replace("Ï", "D").Replace("Ò", "N");

            ViewData["OGTitle"] = titleWithoutDiacritics;
            ViewData["OGDescription"] = $"{descriptionWithoutDiacritics} | Diskuze od {Discussion.AuthorNickname} na Discussion.cz";
            ViewData["OGType"] = "article";

            // Pro diskuze pøidáme další meta tagy
            ViewData["ArticleAuthor"] = Discussion.AuthorNickname;
            ViewData["ArticlePublishedTime"] = Discussion.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ");
            ViewData["ArticleModifiedTime"] = Discussion.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        // Pokud je uivatel admin, naèteme seznam všech kategorií, aby mohl admin mezi nimi pøesouvat diskuze
        if (IsAdmin)
        {
            var categoriesResponse = await client.GetAsync($"{ApiBaseUrl}/Categories");
            if (categoriesResponse.IsSuccessStatusCode)
            {
                AllCategories = await categoriesResponse.Content.ReadFromJsonAsync<List<CategoryDto>>() ?? new();
            }
        }

        // Inkrementujeme poèítadlo zhlédnutí dané diskuze
        await client.PostAsync($"{ApiBaseUrl}/discussions/{Discussion!.Id}/increment-view-count", null);

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
            logger.Log("Nepodaøilo se vloit komentáø, pøekroèila se maximální velikost");
            ErrorMessage = "Omlouváme se, ale komentáø se nepodaøilo vloit, pøekroèili jste maximální velikost 1000 znakù.";
            return Page();
        }

        // Pokud není uivatel pøihlášen, pøesmìrujeme na login
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Nejprve naèteme detail diskuze, abychom získali její ID
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

        // Sestavíme URL podle toho, zda jde o odpovìï na komentáø nebo novı root komentáø
        var url = ReplyToCommentId.HasValue
            ? $"{ApiBaseUrl}/discussions/{basicDiscussionInfoDto.Id}/comments/{ReplyToCommentId}/replies"
            : $"{ApiBaseUrl}/discussions/{basicDiscussionInfoDto.Id}/comments";

        // Odešleme poadavek na vytvoøení komentáøe
        var response = await client.PostAsJsonAsync(url, NewComment);

        // V pøípadì úspìchu se provede refresh
        if (!response.IsSuccessStatusCode)
        {
            logger.Log($"Nepodaøilo se odeslat poadavek na pøidávání komentáøe do diskuze {DiscussionCode}");
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

        // Nejprve naèteme detail diskuze, abychom získali její ID
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

        // Kontrola oprávnìní na smazání diskuze
        if (!(IsAdmin || basicDiscussionInfoDto.AuthorId == UserId))
        {
            logger.Log($"Uivatel {UserId} nemá oprávnìní na smazání diskuze {basicDiscussionInfoDto.Id}: {basicDiscussionInfoDto.Title}");
            ErrorMessage = "Nemáte oprávnìní na smazání této diskuze.";
            return Page();
        }

        // Zavolání endpointu pro smazání
        var response = await client.DeleteAsync($"{ApiBaseUrl}/discussions/{basicDiscussionInfoDto.Id}");

        if (!response.IsSuccessStatusCode)
        {
            // V pøípadì chyby pøidáme chybovou zprávu
            logger.Log($"Nepodaøilo se odeslat poadavek na smazání diskuze dle Id {basicDiscussionInfoDto.Id}");
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
            logger.Log("Chyba pøi naèítání dalších komentáøù", ex);
            return BadRequest("Došlo k chybì pøi naèítání komentáøù.");
        }
    }

    /// <summary>
    /// Handler pro AJAX poadavek na vykreslení partial view pro jeden komentáø
    /// </summary>
    /// <param name="comment">Data komentáøe</param>
    public IActionResult OnPostCommentPartialAsync([FromBody] CommentDto comment)
    {
        return Partial("_CommentPartial", comment);
    }

    /// <summary>
    /// Handler pro AJAX poadavek na zmìnu kategorie diskuze ze souèasné na tu v property SelectedCategoryId
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> OnPostChangeCategoryAsync(int newCategoryId)
    {
        if (!IsAdmin)
        {
            return RedirectToPage();
        }

        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Nejprve naèteme detail diskuze, abychom získali její ID
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
            $"{ApiBaseUrl}/discussions/{basicDiscussionInfoDto.Id}/change-category/{newCategoryId}",
            null
        );
        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Nepodaøilo se zmìnit kategorii diskuze.";
            return Page();
        }

        return RedirectToPage();
    }

    /// <summary>
    /// Vrací CSS tøídu pro zobrazení informace o stavu hlasování
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
    /// Vrací textovı popis stavu hlasování
    /// </summary>
    public string GetVotingStatusText(VoteType voteType) =>
        voteType switch
        {
            VoteType.Visible => "V tomto hlasování mùete vyjádøit svùj názor ke kadé otázce. Zvolte Ano, Ne, nebo Nehlasuji.",
            VoteType.Closed => "Hlasování je uzavøeno. Mùete vidìt vısledky, ale ji nelze hlasovat.",
            VoteType.Hidden => "Toto hlasování je nyní skryté. Vidíte ho, protoe jste autor diskuze.",
            _ => "Neplatnı stav hlasování"
        };
}