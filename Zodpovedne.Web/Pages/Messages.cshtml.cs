using Microsoft.AspNetCore.Mvc;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Filters;
using Zodpovedne.Logging;
using Zodpovedne.Contracts.Enums;
using Ganss.Xss;
using Zodpovedne.Logging.Services;
using Zodpovedne.Web.Pages.Models;

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
        public int UnreadCount { get; set; } = 0;
    }

    /// <summary>
    /// Seznam pøátel pøihlášeného uživatele pro zahájení nové konverzace
    /// </summary>
    public List<FriendItem> Friends { get; private set; } = new();

    public MessagesModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator)
        : base(clientFactory, configuration, logger, sanitizer, translator)
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
            // Pøidání uživatelských dat do ViewData pro odlišení zpráv v konverzacích
            ViewData["CurrentUserId"] = UserId;

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
                    var approvedFriendships = friendships
                        .Where(f => f.Status == FriendshipStatus.Approved)
                        .Select(f => new FriendItem
                        {
                            UserId = f.OtherUserId,
                            Nickname = f.OtherUserNickname
                        })
                        .OrderBy(f => f.Nickname)
                        .ToList();

                    // Získání poètu nepøeètených zpráv od každého pøítele
                    var unreadByUserResponse = await client.GetAsync($"{ApiBaseUrl}/messages/unread-counts-by-user");
                    if (unreadByUserResponse.IsSuccessStatusCode)
                    {
                        var unreadCounts = await unreadByUserResponse.Content.ReadFromJsonAsync<Dictionary<string, int>>();
                        if (unreadCounts != null)
                        {
                            // Pøiøazení poètu nepøeètených zpráv k jednotlivým pøátelùm
                            foreach (var friend in approvedFriendships)
                            {
                                if (unreadCounts.TryGetValue(friend.UserId, out int count))
                                {
                                    friend.UnreadCount = count;
                                }
                            }
                        }
                    }

                    // Seøazení pøátel - nejprve ti s nepøeètenými zprávami, potom ostatní
                    Friends = approvedFriendships
                        .OrderByDescending(f => f.UnreadCount > 0)
                        .ThenBy(f => f.Nickname)
                        .ToList();
                }
            }
            else
            {
                logger.Log($"Nepodaøilo se naèíst seznam pøátel, status: {friendshipsResponse.StatusCode}");
                // Nezobrazujeme chybu pøi naèítání pøátel, aby uživatel mohl alespoò pracovat s existujícími konverzacemi
            }

            return Page();
        }
        catch (Exception ex)
        {
            logger.Log("Chyba pøi naèítání stránky se zprávami", ex);
            ErrorMessage = "Pøi naèítání zpráv došlo k chybì. Zkuste to prosím pozdìji.";
            return Page();
        }
    }
}