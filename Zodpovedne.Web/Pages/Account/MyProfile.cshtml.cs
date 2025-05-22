using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;
using System.Text.Json;
using Zodpovedne.Contracts.Enums;
using Ganss.Xss;
using Zodpovedne.Logging.Services;

namespace Zodpovedne.Web.Pages.Account;

public class MyProfileModel : BasePageModel
{
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

    /// <summary>
    /// P�ezd�vka u�ivatele pro vyhled�v�n�
    /// </summary>
    [BindProperty]
    public string? SearchNickname { get; set; }

    /// <summary>
    /// Chybov� zpr�va p�i vyhled�v�n� u�ivatele
    /// </summary>
    public string? SearchErrorMessage { get; set; }

    /// <summary>
    /// Seznam p��telstv� p�ihl�en�ho u�ivatele
    /// </summary>
    public List<FriendshipItem> Friendships { get; private set; } = new();

    /// <summary>
    /// Indikuje, zda m� u�ivatel n�jak� ��dosti o p��telstv�
    /// </summary>
    public bool HasFriendshipRequests => Friendships.Any(f =>
        f.Status == FriendshipStatus.Requested && !f.IsRequester);

    [BindProperty]
    public string? NewNickname { get; set; }

    [BindProperty]
    public string? NewEmail { get; set; }

    [BindProperty]
    public string? CurrentPassword { get; set; }

    [BindProperty]
    public string? NewPassword { get; set; }

    // Seznam diskuz� s nov�mi aktivitami pro p�ihl�en�ho u�ivatele (nov� odpov�di na jeho koment��e nebo nov� koment��e v jeho diskuz�ch)
    public List<DiscussionWithNewActivitiesDto> NewRepliesNotifications { get; set; } = new();

    public UserProfileDto? UserProfile { get; set; }
    public string? NicknameErrorMessage { get; set; }
    public string? EmailErrorMessage { get; set; }
    public string? PasswordErrorMessage { get; set; }

    // Seznam diskuz� u�ivatele
    public List<BasicDiscussionInfoDto> UserDiscussions { get; set; } = new();

    // V�sledek �i�t�n� datab�ze
    public CleanupResultDto? CleanupResult { get; set; }

    public MyProfileModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator)
        : base(clientFactory, configuration, logger, sanitizer, translator)
    {
    }

    /// <summary>
    /// P�i na�ten� str�nky se na�tou data p�ihl�en�ho u�ivatele
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> OnGetAsync(string? statusMessage = null, string? searchErrorMessage = null)
    {
        if (!string.IsNullOrEmpty(statusMessage))
            this.StatusMessage = statusMessage;

        if (!string.IsNullOrEmpty(searchErrorMessage))
            this.SearchErrorMessage = searchErrorMessage;

        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        await LoadUserDataAsync();

        if (UserProfile == null)
        {
            logger.Log("Nepoda�ilo se na��st data p�ihl�en�ho u�ivatele z response.");
            ErrorMessage = "Omlouv�me se, nepoda�ilo se na��st V� profil.";
        }

        return Page();
    }

    /// <summary>
    /// Handler pro vyhled�n� u�ivatele podle p�ezd�vky
    /// </summary>
    public async Task<IActionResult> OnPostSearchUserAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchNickname))
        {
            return RedirectToPage(new { searchErrorMessage = "Zadejte p�ezd�vku u�ivatele" });
        }

        try
        {
            // Ov��en�, zda u�ivatel s danou p�ezd�vkou existuje
            var client = _clientFactory.CreateBearerClient(HttpContext);
            var response = await client.GetAsync($"{ApiBaseUrl}/users/{SearchNickname}");

            if (response.IsSuccessStatusCode)
            {
                // U�ivatel existuje, p�esm�rujeme na jeho profil
                return RedirectToPage("/Account/Profile", new { nickname = SearchNickname });
            }
            else
            {
                // U�ivatel nebyl nalezen
                return RedirectToPage(new { searchErrorMessage = "U�ivatel s touto p�ezd�vkou neexistuje" });
            }
        }
        catch (Exception ex)
        {
            logger.Log("Chyba p�i vyhled�v�n� u�ivatele", ex);
            return RedirectToPage(new { searchErrorMessage = "P�i vyhled�v�n� u�ivatele do�lo k chyb�" });
        }
    }

    /// <summary>
    /// Handler pro tla��tko �i�t�n� datab�ze (pouze pro adminy)
    /// </summary>
    public async Task<IActionResult> OnPostCleanupDatabaseAsync()
    {
        if (!IsAdmin)
        {
            ErrorMessage = "Pro �i�t�n� datab�ze mus�te b�t administr�tor.";
            return Page();
        }

        var client = _clientFactory.CreateBearerClient(HttpContext);

        try
        {
            var response = await client.PostAsync($"{ApiBaseUrl}/users/cleanup-deleted", null);

            if (response.IsSuccessStatusCode)
            {
                CleanupResult = await response.Content.ReadFromJsonAsync<CleanupResultDto>();
                StatusMessage = $"Datab�ze byla �sp�n� vy�i�t�na. Celkem smaz�no {CleanupResult?.TotalDeleted} z�znam�.";
            }
            else
            {
                ErrorMessage = "P�i �i�t�n� datab�ze do�lo k chyb�.";
                logger.Log($"Chyba p�i vol�n� API pro �i�t�n� datab�ze. Status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "P�i �i�t�n� datab�ze do�lo k chyb�.";
            logger.Log("Chyba p�i �i�t�n� datab�ze", ex);
        }

        //return RedirectToPage(); // Zp�sob� znovuna�ten� str�nky (znovu se nov� napln� model)
        return Page(); // Zp�sob� na�ten� str�nky ale s p�vodn�m modelem
    }

    /// <summary>
    /// Kliknut� na tla��tko pro zm�nu emailu
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> OnPostUpdateEmailAsync()
    {
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);
        if (client == null)
        {
            logger.Log("Nepoda�ilo se vytvo�it HTTP klienta");
            ErrorMessage = "Omlouv�me se, n�co se pokazilo.";
            await LoadUserDataAsync();
            return Page();
        }

        var response = await client.PutAsJsonAsync(
            $"{ApiBaseUrl}/users/authenticated-user/email",
            new { Email = NewEmail }
        );

        if (response.IsSuccessStatusCode)
        {
            // Email se zm�nil �sp�n�, u�ivatel bude odhl�en a bude se muset znovu nalogovat, aby se mu vygeneroval nov� JWT token
            if (await SignedOutIsOK())
                return RedirectToPage("/Account/Login", new { statusMessage = "V� email byl �sp�n� zm�n�n, nyn� se j�m m��ete nov� p�ihl�sit" });
            else
            {
                ErrorMessage = "Nastala chyba p�i odhla�ov�n�";
                await LoadUserDataAsync();
                return Page();
            }
        }

        EmailErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nastala chyba p�i zm�n� emailu.");
        await LoadUserDataAsync();
        return Page();
    }

    /// <summary>
    /// Kliknut� na tla��tko pro zm�nu nickname
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> OnPostUpdateNicknameAsync()
    {
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);
        if (client == null)
        {
            logger.Log("Nepoda�ilo se vytvo�it HTTP klienta");
            ErrorMessage = "Omlouv�me se, n�co se pokazilo.";
            await LoadUserDataAsync();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(NewNickname))
        {
            NicknameErrorMessage = "P�ezd�vka nesm� b�t pr�zdn�.";
            await LoadUserDataAsync();
            return Page();
        }

        var response = await client.PutAsJsonAsync(
            $"{ApiBaseUrl}/users/authenticated-user/nickname",
            new { Nickname = NewNickname }
        );

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("MyProfile", new { statusMessage = "P�ezd�vka byla �sp�n� zm�n�na. Zm�na se v�ude projev� po nov�m p�ihl�en�." });
        }

        NicknameErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nastala chyba p�i zm�n� p�ezd�vky.");
        await LoadUserDataAsync();
        return Page();
    }

    /// <summary>
    /// Kliknut� na tla��tko pro zm�nu hesla
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> OnPostChangePasswordAsync()
    {
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);
        if (client == null)
        {
            logger.Log("Nepoda�ilo se vytvo�it HTTP klienta");
            ErrorMessage = "Omlouv�me se, n�co se pokazilo.";
            await LoadUserDataAsync();
            return Page();
        }

        var response = await client.PutAsJsonAsync(
            $"{ApiBaseUrl}/users/authenticated-user/password",
            new
            {
                CurrentPassword,
                NewPassword
            }
        );

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("MyProfile", new { statusMessage = "Heslo bylo �sp�n� zm�n�no." });
        }

        PasswordErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nastala chyba p�i zm�n� hesla.");
        await LoadUserDataAsync();
        return Page();
    }

    /// <summary>
    /// Handler pro schv�len� ��dosti o p��telstv�
    /// </summary>
    /// <param name="friendshipId">ID p��telstv�</param>
    public async Task<IActionResult> OnPostApproveFriendshipAsync(int friendshipId)
    {
        try
        {
            var client = _clientFactory.CreateBearerClient(HttpContext);
            var response = await client.PostAsync($"{ApiBaseUrl}/users/friendships/{friendshipId}/approve", null);

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "��dost o p��telstv� byla schv�lena.";
            }
            else
            {
                ErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nepoda�ilo se schv�lit ��dost o p��telstv�.");
            }
        }
        catch (Exception ex)
        {
            logger.Log("Chyba p�i schvalov�n� ��dosti o p��telstv�", ex);
            ErrorMessage = "P�i schvalov�n� ��dosti do�lo k chyb�.";
        }

        return RedirectToPage(new { statusMessage = StatusMessage });
    }

    /// <summary>
    /// Handler pro zam�tnut� ��dosti o p��telstv�
    /// </summary>
    /// <param name="friendshipId">ID p��telstv�</param>
    public async Task<IActionResult> OnPostDenyFriendshipAsync(int friendshipId)
    {
        try
        {
            var client = _clientFactory.CreateBearerClient(HttpContext);
            var response = await client.PostAsync($"{ApiBaseUrl}/users/friendships/{friendshipId}/deny", null);

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "��dost o p��telstv� byla zam�tnuta.";
            }
            else
            {
                ErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nepoda�ilo se zam�tnout ��dost o p��telstv�.");
            }
        }
        catch (Exception ex)
        {
            logger.Log("Chyba p�i zam�t�n� ��dosti o p��telstv�", ex);
            ErrorMessage = "P�i zam�t�n� ��dosti do�lo k chyb�.";
        }

        return RedirectToPage(new { statusMessage = StatusMessage });
    }

    /// <summary>
    /// Handler pro odebr�n� p��telstv�
    /// </summary>
    /// <param name="friendshipId">ID p��telstv�</param>
    public async Task<IActionResult> OnPostRemoveFriendshipAsync(int friendshipId)
    {
        try
        {
            var client = _clientFactory.CreateBearerClient(HttpContext);
            var response = await client.DeleteAsync($"{ApiBaseUrl}/users/friendships/{friendshipId}");

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "P��telstv� bylo odebr�no.";
            }
            else
            {
                ErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nepoda�ilo se odebrat p��telstv�.");
            }
        }
        catch (Exception ex)
        {
            logger.Log("Chyba p�i odeb�r�n� p��telstv�", ex);
            ErrorMessage = "P�i odeb�r�n� p��telstv� do�lo k chyb�.";
        }

        return RedirectToPage(new { statusMessage = StatusMessage });
    }

    /// <summary>
    /// Pomocn� metoda pro na�ten� dat u�ivatele
    /// </summary>
    private async Task LoadUserDataAsync()
    {
        if (!IsUserLoggedIn)
            return;

        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Na�ten� profilu u�ivatele
        var response = await client.GetAsync($"{ApiBaseUrl}/users/authenticated-user");
        if (response.IsSuccessStatusCode)
        {
            UserProfile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        }

        // Na�ten� diskuz� u�ivatele
        try
        {
            var discussionsResponse = await client.GetAsync($"{ApiBaseUrl}/discussions/user-discussions");
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
            logger.Log("Nepoda�ilo se na��st diskuze u�ivatele", ex);
        }

        // Na�ten� seznamu diskuz� s nov�mi aktivitami pro p�ihl�en�ho u�ivatele (nov� odpov�di nebo koment��e)
        try
        {
            var notificationsResponse = await client.GetAsync($"{ApiBaseUrl}/users/discussions-with-new-activities");
            if (notificationsResponse.IsSuccessStatusCode)
            {
                NewRepliesNotifications = await notificationsResponse.Content
                    .ReadFromJsonAsync<List<DiscussionWithNewActivitiesDto>>() ?? new();
            }
        }
        catch (Exception ex)
        {
            logger.Log("Nepoda�ilo se na��st notifikace o nov�ch aktivit�ch", ex);
        }

        // Na�ten� p��telstv�
        try
        {
            var friendshipsResponse = await client.GetAsync($"{ApiBaseUrl}/users/friendships");
            if (friendshipsResponse.IsSuccessStatusCode)
            {
                Friendships = await friendshipsResponse.Content.ReadFromJsonAsync<List<FriendshipItem>>() ?? new();
            }
        }
        catch (Exception ex)
        {
            logger.Log("Nepoda�ilo se na��st seznam p��tel", ex);
        }
    }
}