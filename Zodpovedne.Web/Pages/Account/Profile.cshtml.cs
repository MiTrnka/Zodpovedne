using Ganss.Xss;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Contracts.Enums;
using Zodpovedne.Logging;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Logging.Services;
using static Zodpovedne.Web.Pages.Account.MyAccountModel;
using Zodpovedne.Web.Pages.Models;

namespace Zodpovedne.Web.Pages.Account;

public class ProfileModel : BasePageModel
{
    public UserProfileDto? UserProfile { get; set; }

    /// <summary>
    /// Seznam diskuzí uivatele
    /// </summary>
    public List<BasicDiscussionInfoDto> UserDiscussions { get; set; } = new();

    /// <summary>
    /// Seznam diskuzí, které uivatel oznaèil jako "Líbí se mi"
    /// </summary>
    public List<BasicDiscussionInfoDto> LikedDiscussions { get; set; } = new();

    /// <summary>
    /// Informace o stavu pøátelství mezi pøihlášenım uivatelem a zobrazenım uivatelem.
    /// Null znamená, e ádnı vztah zatím neexistuje.
    /// </summary>
    public FriendshipStatus? FriendshipStatus { get; private set; }

    /// <summary>
    /// Seznam pøátel zobrazeného uivatele - viditelnı pouze pro jeho pøátele
    /// </summary>
    public List<FriendshipItem> UserFriends { get; private set; } = new();

    /// <summary>
    /// Urèuje, zda má pøihlášenı uivatel právo vidìt seznam pøátel zobrazeného uivatele
    /// (buï je sám tímto uivatelem, nebo je jeho pøítelem)
    /// </summary>
    public bool CanViewFriendInfo =>
        (IsUserLoggedIn && UserProfile?.Id == UserId) || // Je to jeho vlastní profil
        (FriendshipStatus == Zodpovedne.Contracts.Enums.FriendshipStatus.Approved) ||   // Nebo jsou pøátelé
        (IsAdmin); // Nebo je admin

    /// <summary>
    /// Indikuje, zda pøihlášenı uivatel mùe odeslat ádost o pøátelství tomuto uivateli.
    /// </summary>
    public bool CanRequestFriendship =>
        // Mùe poádat pokud:
        IsUserLoggedIn &&                               // Je pøihlášenı
        UserProfile?.Id != UserId &&             // Není to jeho vlastní profil
        FriendshipStatus == null;                       // A ádnı vztah zatím neexistuje

    // Pøidejte tuto vlastnost do tøídy ProfileModel
    /// <summary>
    /// Historie pøihlášení uivatele - dostupná pouze pro adminy
    /// </summary>
    public List<TimeRecordLoginDto> LoginHistory { get; private set; } = new();

    /// <summary>
    /// Indikuje, zda ji byla odeslána ádost o pøátelství, na kterou èekáme odpovìï.
    /// </summary>
    public bool HasPendingFriendshipRequest =>
        FriendshipStatus == Zodpovedne.Contracts.Enums.FriendshipStatus.Requested;

    public ProfileModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator) : base(clientFactory, configuration, logger, sanitizer, translator)
    {
    }

    public async Task<IActionResult> OnGetAsync(string nickname)
    {
        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Naètení profilu uivatele
        var response = await client.GetAsync($"{ApiBaseUrl}/users/{nickname}");

        if (!response.IsSuccessStatusCode)
        {
            logger.Log("Nepodaøilo se naèíst data uivatele.");
            ErrorMessage = "Omlouváme se, nepodaøilo se naèíst profil uivatele.";
            return Page();
        }

        UserProfile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        if (UserProfile == null)
        {
            logger.Log("Nepodaøilo se naèíst data uivatele z response.");
            ErrorMessage = "Omlouváme se, nepodaøilo se naèíst profil uivatele.";
            return Page();
        }

        // Naètení diskuzí uivatele
        try
        {
            var discussionsResponse = await client.GetAsync($"{ApiBaseUrl}/discussions/user-discussions/{nickname}");
            if (discussionsResponse.IsSuccessStatusCode)
            {
                var userDiscussions = await discussionsResponse.Content.ReadFromJsonAsync<List<BasicDiscussionInfoDto>>();
                if (userDiscussions != null)
                {
                    UserDiscussions = userDiscussions;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Log("Nepodaøilo se naèíst diskuze uivatele", ex);
            // Nebudeme zobrazovat chybu, pokud se nepodaøí naèíst diskuze
        }

        // Naètení lajkovanıch diskuzí uivatele
        try
        {
            if (UserProfile != null)
            {
                var likedDiscussionsResponse = await client.GetAsync($"{ApiBaseUrl}/discussions/user-liked/{UserProfile.Id}?limit=3");
                if (likedDiscussionsResponse.IsSuccessStatusCode)
                {
                    var likedDiscussions = await likedDiscussionsResponse.Content
                        .ReadFromJsonAsync<List<BasicDiscussionInfoDto>>();
                    if (likedDiscussions != null)
                    {
                        LikedDiscussions = likedDiscussions;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Log("Nepodaøilo se naèíst lajkované diskuze uivatele", ex);
        }

        // Zjištìní stavu pøátelství - pouze pokud je uivatel pøihlášen a není to jeho vlastní profil
        if (IsUserLoggedIn && UserProfile?.Id != UserId)
        {
            try
            {
                // Volání API pro zjištìní stavu pøátelství
                var friendshipResponse = await client.GetAsync($"{ApiBaseUrl}/users/friendship-status/{UserProfile?.Id}");
                if (friendshipResponse.IsSuccessStatusCode)
                {
                    // Naètení stavu pøátelství z odpovìdi
                    var responseContent = await friendshipResponse.Content.ReadAsStringAsync();

                    // Kontrola, zda response není prázdnı
                    if (!string.IsNullOrWhiteSpace(responseContent))
                    {
                        // Deserializace pouze neprázdného obsahu
                        FriendshipStatus = await friendshipResponse.Content.ReadFromJsonAsync<FriendshipStatus?>();
                    }
                    else
                    {
                        // Prázdná odpovìï znamená null
                        FriendshipStatus = null;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Log("Nepodaøilo se naèíst stav pøátelství", ex);
                // Nebudeme zobrazovat chybu, FriendshipStatus zùstane null
            }
        }

        // Naètení pøátel uivatele - pouze pokud jsou pøátelé nebo je to vlastní profil
        if (CanViewFriendInfo)
        {
            try
            {
                var friendsResponse = await client.GetAsync($"{ApiBaseUrl}/users/user-friends/{UserProfile?.Id}");
                if (friendsResponse.IsSuccessStatusCode)
                {
                    UserFriends = await friendsResponse.Content.ReadFromJsonAsync<List<FriendshipItem>>() ?? new();
                }
            }
            catch (Exception ex)
            {
                logger.Log("Nepodaøilo se naèíst seznam pøátel uivatele", ex);
                // Nebudeme zobrazovat chybu, pokud se nepodaøí naèíst pøátele
            }
        }

        // Naètení historie pøihlášení - pouze pro adminy
        if (IsAdmin && UserProfile != null)
        {
            try
            {
                var loginHistoryResponse = await client.GetAsync($"{ApiBaseUrl}/users/login-history/{UserProfile.Id}?limit=20");
                if (loginHistoryResponse.IsSuccessStatusCode)
                {
                    LoginHistory = await loginHistoryResponse.Content.ReadFromJsonAsync<List<TimeRecordLoginDto>>() ?? new();
                }
            }
            catch (Exception ex)
            {
                logger.Log("Nepodaøilo se naèíst historii pøihlášení uivatele", ex);
                // Nebudeme zobrazovat chybu, pokud se nepodaøí naèíst historii
            }
        }

        // Po naètení profilu nastavit SEO data
        if (UserProfile != null)
        {
            ViewData["Title"] = $"Profil {UserProfile.Nickname}";
            ViewData["Description"] = $"Profil uivatele {UserProfile.Nickname} na Discussion.cz - èeské diskuzní sociální síti bez reklam. Zobrazit diskuze a aktivitu uivatele.";
            ViewData["Keywords"] = $"profil, uivatel, uzivatel, {UserProfile.Nickname}, diskuze, diskuzi, komunita, discussion.cz, èeská sí, ceska sit";

            // Pro Open Graph (bez diakritiky)
            ViewData["OGTitle"] = $"Profil {UserProfile.Nickname} - Discussion.cz";
            ViewData["OGDescription"] = $"Prohlednete si profil a diskuze uzivatele {UserProfile.Nickname} na Discussion.cz - ceske diskuzni socialni siti.";

            // Soukromé profily neindexovat
            ViewData["Robots"] = CanViewFriendInfo ? "index, follow" : "noindex, nofollow";
        }

        return Page();
    }

    /// <summary>
    /// Handler pro odeslání ádosti o pøátelství
    /// </summary>
    /// <param name="targetUserId">ID uivatele, kterému se odesílá ádost</param>
    /// <param name="nickname">Pøezdívka uivatele, na jeho profilu se nacházíme</param>
    public async Task<IActionResult> OnPostRequestFriendshipAsync(string targetUserId, string nickname)
    {
        try
        {
            // Vytvoøení klienta s autorizaèním tokenem
            var client = _clientFactory.CreateBearerClient(HttpContext);

            // Odeslání POST poadavku na vytvoøení ádosti o pøátelství
            var response = await client.PostAsync($"{ApiBaseUrl}/users/request-friendship/{targetUserId}", null);

            if (response.IsSuccessStatusCode)
            {
                // Nastavení informace o úspìchu pro uivatele
                StatusMessage = "ádost o pøátelství byla úspìšnì odeslána.";
            }
            else
            {
                // Zpracování chybové odpovìdi z API
                ErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nepodaøilo se odeslat ádost o pøátelství.");
            }
        }
        catch (Exception ex)
        {
            // Logování chyby a zobrazení hlášky uivateli
            logger.Log("Chyba pøi odesílání ádosti o pøátelství", ex);
            ErrorMessage = "Pøi odesílání ádosti došlo k chybì. Zkuste to prosím pozdìji.";
        }

        // Pøesmìrování zpìt na profil uivatele, aby vidìl zmìnu stavu tlaèítka
        return RedirectToPage(new { nickname = nickname });
    }
}

/// <summary>
/// Tøída reprezentující pøátelství s jinım uivatelem
/// </summary>
public class FriendshipItem
{
    public int FriendshipId { get; set; }
    public string OtherUserId { get; set; } = "";
    public string OtherUserNickname { get; set; } = "";
    public FriendshipStatus Status { get; set; }
    public bool IsRequester { get; set; }
    public DateTime CreatedAt { get; set; }
}