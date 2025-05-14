using Ganss.Xss;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Contracts.Enums;
using Zodpovedne.Logging;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging.Services;
using static Zodpovedne.Web.Pages.Account.MyProfileModel;

namespace Zodpovedne.Web.Pages.Account;

public class ProfileModel : BasePageModel
{
    public UserProfileDto? UserProfile { get; set; }

    /// <summary>
    /// Seznam diskuzí uživatele
    /// </summary>
    public List<BasicDiscussionInfoDto> UserDiscussions { get; set; } = new();

    /// <summary>
    /// Seznam diskuzí, které uživatel oznaèil jako "Líbí se mi"
    /// </summary>
    public List<BasicDiscussionInfoDto> LikedDiscussions { get; set; } = new();

    /// <summary>
    /// Informace o stavu pøátelství mezi pøihlášeným uživatelem a zobrazeným uživatelem.
    /// Null znamená, že žádný vztah zatím neexistuje.
    /// </summary>
    public FriendshipStatus? FriendshipStatus { get; private set; }

    /// <summary>
    /// Seznam pøátel zobrazeného uživatele - viditelný pouze pro jeho pøátele
    /// </summary>
    public List<FriendshipItem> UserFriends { get; private set; } = new();

    /// <summary>
    /// Urèuje, zda má pøihlášený uživatel právo vidìt seznam pøátel zobrazeného uživatele
    /// (buï je sám tímto uživatelem, nebo je jeho pøítelem)
    /// </summary>
    public bool CanViewFriendInfo =>
        (IsUserLoggedIn && UserProfile?.Id == CurrentUserId) || // Je to jeho vlastní profil
        (FriendshipStatus == Zodpovedne.Contracts.Enums.FriendshipStatus.Approved) ||   // Nebo jsou pøátelé
        (IsAdmin); // Nebo je admin

    /// <summary>
    /// Indikuje, zda pøihlášený uživatel mùže odeslat žádost o pøátelství tomuto uživateli.
    /// </summary>
    public bool CanRequestFriendship =>
        // Mùže požádat pokud:
        IsUserLoggedIn &&                               // Je pøihlášený
        UserProfile?.Id != CurrentUserId &&             // Není to jeho vlastní profil
        FriendshipStatus == null;                       // A žádný vztah zatím neexistuje

    /// <summary>
    /// Indikuje, zda již byla odeslána žádost o pøátelství, na kterou èekáme odpovìï.
    /// </summary>
    public bool HasPendingFriendshipRequest =>
        FriendshipStatus == Zodpovedne.Contracts.Enums.FriendshipStatus.Requested;

    public ProfileModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator) : base(clientFactory, configuration, logger, sanitizer, translator)
    {
    }

    public async Task<IActionResult> OnGetAsync(string nickname)
    {
        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Naètení profilu uživatele
        var response = await client.GetAsync($"{ApiBaseUrl}/users/{nickname}");

        if (!response.IsSuccessStatusCode)
        {
            _logger.Log("Nepodaøilo se naèíst data uživatele.");
            ErrorMessage = "Omlouváme se, nepodaøilo se naèíst profil uživatele.";
            return Page();
        }

        UserProfile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        if (UserProfile == null)
        {
            _logger.Log("Nepodaøilo se naèíst data uživatele z response.");
            ErrorMessage = "Omlouváme se, nepodaøilo se naèíst profil uživatele.";
            return Page();
        }

        // Naètení diskuzí uživatele
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
            _logger.Log("Nepodaøilo se naèíst diskuze uživatele", ex);
            // Nebudeme zobrazovat chybu, pokud se nepodaøí naèíst diskuze
        }

        // Naètení lajkovaných diskuzí uživatele
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
            _logger.Log("Nepodaøilo se naèíst lajkované diskuze uživatele", ex);
        }

        // Zjištìní stavu pøátelství - pouze pokud je uživatel pøihlášen a není to jeho vlastní profil
        if (IsUserLoggedIn && UserProfile?.Id != CurrentUserId)
        {
            try
            {
                // Volání API pro zjištìní stavu pøátelství
                var friendshipResponse = await client.GetAsync($"{ApiBaseUrl}/users/friendship-status/{UserProfile?.Id}");
                if (friendshipResponse.IsSuccessStatusCode)
                {
                    // Naètení stavu pøátelství z odpovìdi
                    var responseContent = await friendshipResponse.Content.ReadAsStringAsync();

                    // Kontrola, zda response není prázdný
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
                _logger.Log("Nepodaøilo se naèíst stav pøátelství", ex);
                // Nebudeme zobrazovat chybu, FriendshipStatus zùstane null
            }
        }

        // Naètení pøátel uživatele - pouze pokud jsou pøátelé nebo je to vlastní profil
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
                _logger.Log("Nepodaøilo se naèíst seznam pøátel uživatele", ex);
                // Nebudeme zobrazovat chybu, pokud se nepodaøí naèíst pøátele
            }
        }

        return Page();
    }

    /// <summary>
    /// Handler pro odeslání žádosti o pøátelství
    /// </summary>
    /// <param name="targetUserId">ID uživatele, kterému se odesílá žádost</param>
    /// <param name="nickname">Pøezdívka uživatele, na jehož profilu se nacházíme</param>
    public async Task<IActionResult> OnPostRequestFriendshipAsync(string targetUserId, string nickname)
    {
        try
        {
            // Vytvoøení klienta s autorizaèním tokenem
            var client = _clientFactory.CreateBearerClient(HttpContext);

            // Odeslání POST požadavku na vytvoøení žádosti o pøátelství
            var response = await client.PostAsync($"{ApiBaseUrl}/users/request-friendship/{targetUserId}", null);

            if (response.IsSuccessStatusCode)
            {
                // Nastavení informace o úspìchu pro uživatele
                StatusMessage = "Žádost o pøátelství byla úspìšnì odeslána.";
            }
            else
            {
                // Zpracování chybové odpovìdi z API
                ErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nepodaøilo se odeslat žádost o pøátelství.");
            }
        }
        catch (Exception ex)
        {
            // Logování chyby a zobrazení hlášky uživateli
            _logger.Log("Chyba pøi odesílání žádosti o pøátelství", ex);
            ErrorMessage = "Pøi odesílání žádosti došlo k chybì. Zkuste to prosím pozdìji.";
        }

        // Pøesmìrování zpìt na profil uživatele, aby vidìl zmìnu stavu tlaèítka
        return RedirectToPage(new { nickname = nickname });
    }
}

/// <summary>
/// Tøída reprezentující pøátelství s jiným uživatelem
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