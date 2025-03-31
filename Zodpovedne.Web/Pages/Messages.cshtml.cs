using Microsoft.AspNetCore.Mvc;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Filters;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;
using Zodpovedne.Contracts.Enums;

namespace Zodpovedne.Web.Pages;

/// <summary>
/// Model pro stránku se zprávami
/// Umožòuje zobrazit seznam konverzací a detailní zobrazení konkrétní konverzace
/// </summary>
[AuthenticationFilter] // Pouze pro pøihlášené uživatele
public class MessagesModel : BasePageModel
{
    /// <summary>
    /// Pomocná tøída pro seznam pøátel
    /// </summary>
    public class FriendItem
    {
        public string UserId { get; set; } = "";
        public string Nickname { get; set; } = "";
    }

    /// <summary>
    /// Seznam pøátel pøihlášeného uživatele pro zahájení nové konverzace
    /// </summary>
    public List<FriendItem> Friends { get; private set; } = new();

    public MessagesModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger)
        : base(clientFactory, configuration, logger)
    {
    }

    /// <summary>
    /// Handler pro naètení stránky (HTTP GET)
    /// Naèítá seznam konverzací a pøátel
    /// </summary>
    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);

        try
        {
            // Naètení seznamu pøátel pro zahájení nových konverzací
            var friendshipsResponse = await client.GetAsync($"{ApiBaseUrl}/users/friendships");

            if (friendshipsResponse.IsSuccessStatusCode)
            {
                // Zpracování seznamu pøátelství
                var friendships = await friendshipsResponse.Content
                .ReadFromJsonAsync<List<FriendshipDto>>();

                if (friendships != null)
                {
                    // Filtrování pouze na schválená pøátelství
                    Friends = friendships
                        .Where(f => f.Status == FriendshipStatus.Approved)
                        .Select(f => new FriendItem
                        {
                            UserId = f.OtherUserId,
                            Nickname = f.OtherUserNickname
                        })
                        .OrderBy(f => f.Nickname)
                        .ToList();
                }
            }
            else
            {
                _logger.Log($"Nepodaøilo se naèíst seznam pøátel, status: {friendshipsResponse.StatusCode}");
                // Nezobrazujeme chybu pøi naèítání pøátel, aby uživatel mohl alespoò pracovat s existujícími konverzacemi
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.Log("Chyba pøi naèítání stránky se zprávami", ex);
            ErrorMessage = "Pøi naèítání zpráv došlo k chybì. Zkuste to prosím pozdìji.";
            return Page();
        }
    }
}