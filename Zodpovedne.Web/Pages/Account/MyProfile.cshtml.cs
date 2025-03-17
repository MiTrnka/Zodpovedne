using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;
using System.Text.Json;

namespace Zodpovedne.Web.Pages.Account;

public class MyProfileModel : BasePageModel
{
    [BindProperty]
    public string? NewNickname { get; set; }

    [BindProperty]
    public string? NewEmail { get; set; }

    [BindProperty]
    public string? CurrentPassword { get; set; }

    [BindProperty]
    public string? NewPassword { get; set; }

    // Seznam diskuzí, ve kterých dostal pøihlášený uživatel nové odpovìdi na své komentáøe
    public List<DiscussionWithNewRepliesDto> NewRepliesNotifications { get; set; } = new();

    public UserProfileDto? UserProfile { get; set; }
    public string? NicknameErrorMessage { get; set; }
    public string? EmailErrorMessage { get; set; }
    public string? PasswordErrorMessage { get; set; }

    // Seznam diskuzí uživatele
    public List<BasicDiscussionInfoDto> UserDiscussions { get; set; } = new();

    // Výsledek èištìní databáze
    public CleanupResultDto? CleanupResult { get; set; }

    public MyProfileModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger) : base(clientFactory, configuration, logger)
    {
    }

    /// <summary>
    /// Pøi naètení stránky se naètou data pøihlášeného uživatele
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> OnGetAsync(string? statusMessage = null)
    {
        if (!string.IsNullOrEmpty(statusMessage))
            this.StatusMessage = statusMessage;

        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Naètení profilu uživatele
        var response = await client.GetAsync($"{ApiBaseUrl}/users/authenticated-user");

        if (!response.IsSuccessStatusCode)
        {
            _logger.Log("Nepodaøilo se naèíst data pøihlášeného uživatele.");
            ErrorMessage = "Omlouváme se, nepodaøilo se naèíst Váš profil.";
            return Page();
        }

        UserProfile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        if (UserProfile == null)
        {
            _logger.Log("Nepodaøilo se naèíst data pøihlášeného uživatele z response.");
            ErrorMessage = "Omlouváme se, nepodaøilo se naèíst Váš profil.";
            return Page();
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
            _logger.Log("Nepodaøilo se naèíst diskuze uživatele", ex);
            // Nebudeme zobrazovat chybu, pokud se nepodaøí naèíst diskuze
        }

        // Naètení seznamu diskuzí, kde pøihlášený uživatel dostal nové odpovìdi ke svým komentáøùm
        try
        {
            var notificationsResponse = await client.GetAsync($"{ApiBaseUrl}/users/discussions-with-new-replies");
            if (notificationsResponse.IsSuccessStatusCode)
            {
                NewRepliesNotifications = await notificationsResponse.Content
                    .ReadFromJsonAsync<List<DiscussionWithNewRepliesDto>>() ?? new();
            }
        }
        catch (Exception ex)
        {
            _logger.Log("Nepodaøilo se naèíst notifikace o nových odpovìdích", ex);
            // Nebudeme zobrazovat chybu, pokud se nepodaøí naèíst notifikace
        }

        return Page(); // Zpùsobí naètení stránky ale s pùvodním modelem
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
                _logger.Log($"Chyba pøi volání API pro èištìní databáze. Status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Pøi èištìní databáze došlo k chybì.";
            _logger.Log("Chyba pøi èištìní databáze", ex);
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
            _logger.Log("Nepodaøilo se vytvoøit HTTP klienta");
            ErrorMessage = "Omlouváme se, nìco se pokazilo.";
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
                return RedirectToPage("/Account/Login", new { statusMessage = "Váš email byl úspìšnì zmìnìn, nyní se jím mùžete novì pøihlásit"}); // Zpùsobí naètení nové stránky (znovu se novì naplní model)
            else
            {
                ErrorMessage = "Nastala chyba pøi odhlašování";
                return Page();
            }
        }

        EmailErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nastala chyba pøi zmìnì emailu.");

        return Page(); // Zpùsobí naètení stránky ale s pùvodním modelem
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
            _logger.Log("Nepodaøilo se vytvoøit HTTP klienta");
            ErrorMessage = "Omlouváme se, nìco se pokazilo.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(NewNickname))
        {
            NicknameErrorMessage = "Pøezdívka nesmí být prázdná.";
            return Page();
        }

        var response = await client.PutAsJsonAsync(
            $"{ApiBaseUrl}/users/authenticated-user/nickname",
            new { Nickname = NewNickname }
        );

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("MyProfile", new { statusMessage = "Pøezdívka byla úspìšnì zmìnìna." }); // Zpùsobí znovunaètení stránky (znovu se novì naplní model)
        }

        NicknameErrorMessage = await GetErrorFromHttpResponseMessage(response,"Nastala chyba pøi zmìnì pøezdívky.");

        return Page(); // Zpùsobí naètení stránky ale s pùvodním modelem
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
            _logger.Log("Nepodaøilo se vytvoøit HTTP klienta");
            ErrorMessage = "Omlouváme se, nìco se pokazilo.";
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
            return RedirectToPage("MyProfile", new { statusMessage = "Heslo bylo úspìšnì zmìnìno." }); // Zpùsobí znovunaètení stránky (znovu se novì naplní model)
        }

        PasswordErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nastala chyba pøi zmìnì hesla.");

        return Page(); // Zpùsobí naètení stránky ale s pùvodním modelem
    }
}