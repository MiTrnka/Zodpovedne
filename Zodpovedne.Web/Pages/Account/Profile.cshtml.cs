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
    /// Seznam diskuz� u�ivatele
    /// </summary>
    public List<BasicDiscussionInfoDto> UserDiscussions { get; set; } = new();

    /// <summary>
    /// Seznam diskuz�, kter� u�ivatel ozna�il jako "L�b� se mi"
    /// </summary>
    public List<BasicDiscussionInfoDto> LikedDiscussions { get; set; } = new();

    /// <summary>
    /// Informace o stavu p��telstv� mezi p�ihl�en�m u�ivatelem a zobrazen�m u�ivatelem.
    /// Null znamen�, �e ��dn� vztah zat�m neexistuje.
    /// </summary>
    public FriendshipStatus? FriendshipStatus { get; private set; }

    /// <summary>
    /// Seznam p��tel zobrazen�ho u�ivatele - viditeln� pouze pro jeho p��tele
    /// </summary>
    public List<FriendshipItem> UserFriends { get; private set; } = new();

    /// <summary>
    /// Ur�uje, zda m� p�ihl�en� u�ivatel pr�vo vid�t seznam p��tel zobrazen�ho u�ivatele
    /// (bu� je s�m t�mto u�ivatelem, nebo je jeho p��telem)
    /// </summary>
    public bool CanViewFriendInfo =>
        (IsUserLoggedIn && UserProfile?.Id == CurrentUserId) || // Je to jeho vlastn� profil
        (FriendshipStatus == Zodpovedne.Contracts.Enums.FriendshipStatus.Approved) ||   // Nebo jsou p��tel�
        (IsAdmin); // Nebo je admin

    /// <summary>
    /// Indikuje, zda p�ihl�en� u�ivatel m��e odeslat ��dost o p��telstv� tomuto u�ivateli.
    /// </summary>
    public bool CanRequestFriendship =>
        // M��e po��dat pokud:
        IsUserLoggedIn &&                               // Je p�ihl�en�
        UserProfile?.Id != CurrentUserId &&             // Nen� to jeho vlastn� profil
        FriendshipStatus == null;                       // A ��dn� vztah zat�m neexistuje

    /// <summary>
    /// Indikuje, zda ji� byla odesl�na ��dost o p��telstv�, na kterou �ek�me odpov��.
    /// </summary>
    public bool HasPendingFriendshipRequest =>
        FriendshipStatus == Zodpovedne.Contracts.Enums.FriendshipStatus.Requested;

    public ProfileModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator) : base(clientFactory, configuration, logger, sanitizer, translator)
    {
    }

    public async Task<IActionResult> OnGetAsync(string nickname)
    {
        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Na�ten� profilu u�ivatele
        var response = await client.GetAsync($"{ApiBaseUrl}/users/{nickname}");

        if (!response.IsSuccessStatusCode)
        {
            _logger.Log("Nepoda�ilo se na��st data u�ivatele.");
            ErrorMessage = "Omlouv�me se, nepoda�ilo se na��st profil u�ivatele.";
            return Page();
        }

        UserProfile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        if (UserProfile == null)
        {
            _logger.Log("Nepoda�ilo se na��st data u�ivatele z response.");
            ErrorMessage = "Omlouv�me se, nepoda�ilo se na��st profil u�ivatele.";
            return Page();
        }

        // Na�ten� diskuz� u�ivatele
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
            _logger.Log("Nepoda�ilo se na��st diskuze u�ivatele", ex);
            // Nebudeme zobrazovat chybu, pokud se nepoda�� na��st diskuze
        }

        // Na�ten� lajkovan�ch diskuz� u�ivatele
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
            _logger.Log("Nepoda�ilo se na��st lajkovan� diskuze u�ivatele", ex);
        }

        // Zji�t�n� stavu p��telstv� - pouze pokud je u�ivatel p�ihl�en a nen� to jeho vlastn� profil
        if (IsUserLoggedIn && UserProfile?.Id != CurrentUserId)
        {
            try
            {
                // Vol�n� API pro zji�t�n� stavu p��telstv�
                var friendshipResponse = await client.GetAsync($"{ApiBaseUrl}/users/friendship-status/{UserProfile?.Id}");
                if (friendshipResponse.IsSuccessStatusCode)
                {
                    // Na�ten� stavu p��telstv� z odpov�di
                    var responseContent = await friendshipResponse.Content.ReadAsStringAsync();

                    // Kontrola, zda response nen� pr�zdn�
                    if (!string.IsNullOrWhiteSpace(responseContent))
                    {
                        // Deserializace pouze nepr�zdn�ho obsahu
                        FriendshipStatus = await friendshipResponse.Content.ReadFromJsonAsync<FriendshipStatus?>();
                    }
                    else
                    {
                        // Pr�zdn� odpov�� znamen� null
                        FriendshipStatus = null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log("Nepoda�ilo se na��st stav p��telstv�", ex);
                // Nebudeme zobrazovat chybu, FriendshipStatus z�stane null
            }
        }

        // Na�ten� p��tel u�ivatele - pouze pokud jsou p��tel� nebo je to vlastn� profil
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
                _logger.Log("Nepoda�ilo se na��st seznam p��tel u�ivatele", ex);
                // Nebudeme zobrazovat chybu, pokud se nepoda�� na��st p��tele
            }
        }

        return Page();
    }

    /// <summary>
    /// Handler pro odesl�n� ��dosti o p��telstv�
    /// </summary>
    /// <param name="targetUserId">ID u�ivatele, kter�mu se odes�l� ��dost</param>
    /// <param name="nickname">P�ezd�vka u�ivatele, na jeho� profilu se nach�z�me</param>
    public async Task<IActionResult> OnPostRequestFriendshipAsync(string targetUserId, string nickname)
    {
        try
        {
            // Vytvo�en� klienta s autoriza�n�m tokenem
            var client = _clientFactory.CreateBearerClient(HttpContext);

            // Odesl�n� POST po�adavku na vytvo�en� ��dosti o p��telstv�
            var response = await client.PostAsync($"{ApiBaseUrl}/users/request-friendship/{targetUserId}", null);

            if (response.IsSuccessStatusCode)
            {
                // Nastaven� informace o �sp�chu pro u�ivatele
                StatusMessage = "��dost o p��telstv� byla �sp�n� odesl�na.";
            }
            else
            {
                // Zpracov�n� chybov� odpov�di z API
                ErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nepoda�ilo se odeslat ��dost o p��telstv�.");
            }
        }
        catch (Exception ex)
        {
            // Logov�n� chyby a zobrazen� hl�ky u�ivateli
            _logger.Log("Chyba p�i odes�l�n� ��dosti o p��telstv�", ex);
            ErrorMessage = "P�i odes�l�n� ��dosti do�lo k chyb�. Zkuste to pros�m pozd�ji.";
        }

        // P�esm�rov�n� zp�t na profil u�ivatele, aby vid�l zm�nu stavu tla��tka
        return RedirectToPage(new { nickname = nickname });
    }
}

/// <summary>
/// T��da reprezentuj�c� p��telstv� s jin�m u�ivatelem
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