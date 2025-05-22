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

    /// <summary>
    /// Pøezdívka uživatele pro vyhledávání
    /// </summary>
    [BindProperty]
    public string? SearchNickname { get; set; }

    /// <summary>
    /// Chybová zpráva pøi vyhledávání uživatele
    /// </summary>
    public string? SearchErrorMessage { get; set; }

    /// <summary>
    /// Seznam pøátelství pøihlášeného uživatele
    /// </summary>
    public List<FriendshipItem> Friendships { get; private set; } = new();

    /// <summary>
    /// Indikuje, zda má uživatel nìjaké žádosti o pøátelství
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

    // Seznam diskuzí s novými aktivitami pro pøihlášeného uživatele (nové odpovìdi na jeho komentáøe nebo nové komentáøe v jeho diskuzích)
    public List<DiscussionWithNewActivitiesDto> NewRepliesNotifications { get; set; } = new();

    public UserProfileDto? UserProfile { get; set; }
    public string? NicknameErrorMessage { get; set; }
    public string? EmailErrorMessage { get; set; }
    public string? PasswordErrorMessage { get; set; }

    // Seznam diskuzí uživatele
    public List<BasicDiscussionInfoDto> UserDiscussions { get; set; } = new();

    // Výsledek èištìní databáze
    public CleanupResultDto? CleanupResult { get; set; }

    public MyProfileModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator)
        : base(clientFactory, configuration, logger, sanitizer, translator)
    {
    }

    /// <summary>
    /// Pøi naètení stránky se naètou data pøihlášeného uživatele
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
            logger.Log("Nepodaøilo se naèíst data pøihlášeného uživatele z response.");
            ErrorMessage = "Omlouváme se, nepodaøilo se naèíst Váš profil.";
        }

        return Page();
    }

    /// <summary>
    /// Handler pro vyhledání uživatele podle pøezdívky
    /// </summary>
    public async Task<IActionResult> OnPostSearchUserAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchNickname))
        {
            return RedirectToPage(new { searchErrorMessage = "Zadejte pøezdívku uživatele" });
        }

        try
        {
            // Ovìøení, zda uživatel s danou pøezdívkou existuje
            var client = _clientFactory.CreateBearerClient(HttpContext);
            var response = await client.GetAsync($"{ApiBaseUrl}/users/{SearchNickname}");

            if (response.IsSuccessStatusCode)
            {
                // Uživatel existuje, pøesmìrujeme na jeho profil
                return RedirectToPage("/Account/Profile", new { nickname = SearchNickname });
            }
            else
            {
                // Uživatel nebyl nalezen
                return RedirectToPage(new { searchErrorMessage = "Uživatel s touto pøezdívkou neexistuje" });
            }
        }
        catch (Exception ex)
        {
            logger.Log("Chyba pøi vyhledávání uživatele", ex);
            return RedirectToPage(new { searchErrorMessage = "Pøi vyhledávání uživatele došlo k chybì" });
        }
    }

    /// <summary>
    /// Handler pro tlaèítko èištìní databáze (pouze pro adminy)
    /// </summary>
    public async Task<IActionResult> OnPostCleanupDatabaseAsync()
    {
        if (!IsAdmin)
        {
            ErrorMessage = "Pro èištìní databáze musíte být administrátor.";
            return Page();
        }

        var client = _clientFactory.CreateBearerClient(HttpContext);

        try
        {
            var response = await client.PostAsync($"{ApiBaseUrl}/users/cleanup-deleted", null);

            if (response.IsSuccessStatusCode)
            {
                CleanupResult = await response.Content.ReadFromJsonAsync<CleanupResultDto>();
                StatusMessage = $"Databáze byla úspìšnì vyèištìna. Celkem smazáno {CleanupResult?.TotalDeleted} záznamù.";
            }
            else
            {
                ErrorMessage = "Pøi èištìní databáze došlo k chybì.";
                logger.Log($"Chyba pøi volání API pro èištìní databáze. Status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Pøi èištìní databáze došlo k chybì.";
            logger.Log("Chyba pøi èištìní databáze", ex);
        }

        //return RedirectToPage(); // Zpùsobí znovunaètení stránky (znovu se novì naplní model)
        return Page(); // Zpùsobí naètení stránky ale s pùvodním modelem
    }

    /// <summary>
    /// Kliknutí na tlaèítko pro zmìnu emailu
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> OnPostUpdateEmailAsync()
    {
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);
        if (client == null)
        {
            logger.Log("Nepodaøilo se vytvoøit HTTP klienta");
            ErrorMessage = "Omlouváme se, nìco se pokazilo.";
            await LoadUserDataAsync();
            return Page();
        }

        var response = await client.PutAsJsonAsync(
            $"{ApiBaseUrl}/users/authenticated-user/email",
            new { Email = NewEmail }
        );

        if (response.IsSuccessStatusCode)
        {
            // Email se zmìnil úspìšnì, uživatel bude odhlášen a bude se muset znovu nalogovat, aby se mu vygeneroval nový JWT token
            if (await SignedOutIsOK())
                return RedirectToPage("/Account/Login", new { statusMessage = "Váš email byl úspìšnì zmìnìn, nyní se jím mùžete novì pøihlásit" });
            else
            {
                ErrorMessage = "Nastala chyba pøi odhlašování";
                await LoadUserDataAsync();
                return Page();
            }
        }

        EmailErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nastala chyba pøi zmìnì emailu.");
        await LoadUserDataAsync();
        return Page();
    }

    /// <summary>
    /// Kliknutí na tlaèítko pro zmìnu nickname
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> OnPostUpdateNicknameAsync()
    {
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);
        if (client == null)
        {
            logger.Log("Nepodaøilo se vytvoøit HTTP klienta");
            ErrorMessage = "Omlouváme se, nìco se pokazilo.";
            await LoadUserDataAsync();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(NewNickname))
        {
            NicknameErrorMessage = "Pøezdívka nesmí být prázdná.";
            await LoadUserDataAsync();
            return Page();
        }

        var response = await client.PutAsJsonAsync(
            $"{ApiBaseUrl}/users/authenticated-user/nickname",
            new { Nickname = NewNickname }
        );

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("MyProfile", new { statusMessage = "Pøezdívka byla úspìšnì zmìnìna. Zmìna se všude projeví po novém pøihlášení." });
        }

        NicknameErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nastala chyba pøi zmìnì pøezdívky.");
        await LoadUserDataAsync();
        return Page();
    }

    /// <summary>
    /// Kliknutí na tlaèítko pro zmìnu hesla
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> OnPostChangePasswordAsync()
    {
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);
        if (client == null)
        {
            logger.Log("Nepodaøilo se vytvoøit HTTP klienta");
            ErrorMessage = "Omlouváme se, nìco se pokazilo.";
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
            return RedirectToPage("MyProfile", new { statusMessage = "Heslo bylo úspìšnì zmìnìno." });
        }

        PasswordErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nastala chyba pøi zmìnì hesla.");
        await LoadUserDataAsync();
        return Page();
    }

    /// <summary>
    /// Handler pro schválení žádosti o pøátelství
    /// </summary>
    /// <param name="friendshipId">ID pøátelství</param>
    public async Task<IActionResult> OnPostApproveFriendshipAsync(int friendshipId)
    {
        try
        {
            var client = _clientFactory.CreateBearerClient(HttpContext);
            var response = await client.PostAsync($"{ApiBaseUrl}/users/friendships/{friendshipId}/approve", null);

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "Žádost o pøátelství byla schválena.";
            }
            else
            {
                ErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nepodaøilo se schválit žádost o pøátelství.");
            }
        }
        catch (Exception ex)
        {
            logger.Log("Chyba pøi schvalování žádosti o pøátelství", ex);
            ErrorMessage = "Pøi schvalování žádosti došlo k chybì.";
        }

        return RedirectToPage(new { statusMessage = StatusMessage });
    }

    /// <summary>
    /// Handler pro zamítnutí žádosti o pøátelství
    /// </summary>
    /// <param name="friendshipId">ID pøátelství</param>
    public async Task<IActionResult> OnPostDenyFriendshipAsync(int friendshipId)
    {
        try
        {
            var client = _clientFactory.CreateBearerClient(HttpContext);
            var response = await client.PostAsync($"{ApiBaseUrl}/users/friendships/{friendshipId}/deny", null);

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "Žádost o pøátelství byla zamítnuta.";
            }
            else
            {
                ErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nepodaøilo se zamítnout žádost o pøátelství.");
            }
        }
        catch (Exception ex)
        {
            logger.Log("Chyba pøi zamítání žádosti o pøátelství", ex);
            ErrorMessage = "Pøi zamítání žádosti došlo k chybì.";
        }

        return RedirectToPage(new { statusMessage = StatusMessage });
    }

    /// <summary>
    /// Handler pro odebrání pøátelství
    /// </summary>
    /// <param name="friendshipId">ID pøátelství</param>
    public async Task<IActionResult> OnPostRemoveFriendshipAsync(int friendshipId)
    {
        try
        {
            var client = _clientFactory.CreateBearerClient(HttpContext);
            var response = await client.DeleteAsync($"{ApiBaseUrl}/users/friendships/{friendshipId}");

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "Pøátelství bylo odebráno.";
            }
            else
            {
                ErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nepodaøilo se odebrat pøátelství.");
            }
        }
        catch (Exception ex)
        {
            logger.Log("Chyba pøi odebírání pøátelství", ex);
            ErrorMessage = "Pøi odebírání pøátelství došlo k chybì.";
        }

        return RedirectToPage(new { statusMessage = StatusMessage });
    }

    /// <summary>
    /// Pomocná metoda pro naètení dat uživatele
    /// </summary>
    private async Task LoadUserDataAsync()
    {
        if (!IsUserLoggedIn)
            return;

        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Naètení profilu uživatele
        var response = await client.GetAsync($"{ApiBaseUrl}/users/authenticated-user");
        if (response.IsSuccessStatusCode)
        {
            UserProfile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        }

        // Naètení diskuzí uživatele
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
            logger.Log("Nepodaøilo se naèíst diskuze uživatele", ex);
        }

        // Naètení seznamu diskuzí s novými aktivitami pro pøihlášeného uživatele (nové odpovìdi nebo komentáøe)
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
            logger.Log("Nepodaøilo se naèíst notifikace o nových aktivitách", ex);
        }

        // Naètení pøátelství
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
            logger.Log("Nepodaøilo se naèíst seznam pøátel", ex);
        }
    }
}