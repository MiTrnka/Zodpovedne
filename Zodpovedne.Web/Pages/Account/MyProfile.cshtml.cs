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

    // Seznam diskuz�, ve kter�ch dostal p�ihl�en� u�ivatel nov� odpov�di na sv� koment��e
    public List<DiscussionWithNewRepliesDto> NewRepliesNotifications { get; set; } = new();

    public UserProfileDto? UserProfile { get; set; }
    public string? NicknameErrorMessage { get; set; }
    public string? EmailErrorMessage { get; set; }
    public string? PasswordErrorMessage { get; set; }

    // Seznam diskuz� u�ivatele
    public List<BasicDiscussionInfoDto> UserDiscussions { get; set; } = new();

    // V�sledek �i�t�n� datab�ze
    public CleanupResultDto? CleanupResult { get; set; }

    public MyProfileModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger) : base(clientFactory, configuration, logger)
    {
    }

    /// <summary>
    /// P�i na�ten� str�nky se na�tou data p�ihl�en�ho u�ivatele
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> OnGetAsync(string? statusMessage = null)
    {
        if (!string.IsNullOrEmpty(statusMessage))
            this.StatusMessage = statusMessage;

        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);

        // Na�ten� profilu u�ivatele
        var response = await client.GetAsync($"{ApiBaseUrl}/users/authenticated-user");

        if (!response.IsSuccessStatusCode)
        {
            _logger.Log("Nepoda�ilo se na��st data p�ihl�en�ho u�ivatele.");
            ErrorMessage = "Omlouv�me se, nepoda�ilo se na��st V� profil.";
            return Page();
        }

        UserProfile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        if (UserProfile == null)
        {
            _logger.Log("Nepoda�ilo se na��st data p�ihl�en�ho u�ivatele z response.");
            ErrorMessage = "Omlouv�me se, nepoda�ilo se na��st V� profil.";
            return Page();
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
            _logger.Log("Nepoda�ilo se na��st diskuze u�ivatele", ex);
            // Nebudeme zobrazovat chybu, pokud se nepoda�� na��st diskuze
        }

        // Na�ten� seznamu diskuz�, kde p�ihl�en� u�ivatel dostal nov� odpov�di ke sv�m koment���m
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
            _logger.Log("Nepoda�ilo se na��st notifikace o nov�ch odpov�d�ch", ex);
            // Nebudeme zobrazovat chybu, pokud se nepoda�� na��st notifikace
        }

        return Page(); // Zp�sob� na�ten� str�nky ale s p�vodn�m modelem
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
                _logger.Log($"Chyba p�i vol�n� API pro �i�t�n� datab�ze. Status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "P�i �i�t�n� datab�ze do�lo k chyb�.";
            _logger.Log("Chyba p�i �i�t�n� datab�ze", ex);
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
            _logger.Log("Nepoda�ilo se vytvo�it HTTP klienta");
            ErrorMessage = "Omlouv�me se, n�co se pokazilo.";
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
                return RedirectToPage("/Account/Login", new { statusMessage = "V� email byl �sp�n� zm�n�n, nyn� se j�m m��ete nov� p�ihl�sit"}); // Zp�sob� na�ten� nov� str�nky (znovu se nov� napln� model)
            else
            {
                ErrorMessage = "Nastala chyba p�i odhla�ov�n�";
                return Page();
            }
        }

        EmailErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nastala chyba p�i zm�n� emailu.");

        return Page(); // Zp�sob� na�ten� str�nky ale s p�vodn�m modelem
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
            _logger.Log("Nepoda�ilo se vytvo�it HTTP klienta");
            ErrorMessage = "Omlouv�me se, n�co se pokazilo.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(NewNickname))
        {
            NicknameErrorMessage = "P�ezd�vka nesm� b�t pr�zdn�.";
            return Page();
        }

        var response = await client.PutAsJsonAsync(
            $"{ApiBaseUrl}/users/authenticated-user/nickname",
            new { Nickname = NewNickname }
        );

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("MyProfile", new { statusMessage = "P�ezd�vka byla �sp�n� zm�n�na." }); // Zp�sob� znovuna�ten� str�nky (znovu se nov� napln� model)
        }

        NicknameErrorMessage = await GetErrorFromHttpResponseMessage(response,"Nastala chyba p�i zm�n� p�ezd�vky.");

        return Page(); // Zp�sob� na�ten� str�nky ale s p�vodn�m modelem
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
            _logger.Log("Nepoda�ilo se vytvo�it HTTP klienta");
            ErrorMessage = "Omlouv�me se, n�co se pokazilo.";
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
            return RedirectToPage("MyProfile", new { statusMessage = "Heslo bylo �sp�n� zm�n�no." }); // Zp�sob� znovuna�ten� str�nky (znovu se nov� napln� model)
        }

        PasswordErrorMessage = await GetErrorFromHttpResponseMessage(response, "Nastala chyba p�i zm�n� hesla.");

        return Page(); // Zp�sob� na�ten� str�nky ale s p�vodn�m modelem
    }
}