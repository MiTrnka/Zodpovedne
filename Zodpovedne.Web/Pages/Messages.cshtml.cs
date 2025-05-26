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
/// Model pro str�nku se zpr�vami
/// Umo��uje zobrazit seznam konverzac� a detailn� zobrazen� konkr�tn� konverzace
/// </summary>
[AuthenticationFilter] // Pouze pro p�ihl�en� u�ivatele
public class MessagesModel : BasePageModel
{
    /// <summary>
    /// Pomocn� t��da pro seznam p��tel
    /// </summary>
    public class FriendItem
    {
        public string UserId { get; set; } = "";
        public string Nickname { get; set; } = "";
        public int UnreadCount { get; set; } = 0;
    }

    /// <summary>
    /// Seznam p��tel p�ihl�en�ho u�ivatele pro zah�jen� nov� konverzace
    /// </summary>
    public List<FriendItem> Friends { get; private set; } = new();

    public MessagesModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator)
        : base(clientFactory, configuration, logger, sanitizer, translator)
    {
    }

    /// <summary>
    /// Handler pro na�ten� str�nky (HTTP GET)
    /// Na��t� seznam konverzac� a p��tel
    /// </summary>
    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsUserLoggedIn)
            return RedirectToPage("/Account/Login");

        var client = _clientFactory.CreateBearerClient(HttpContext);

        try
        {
            // P�id�n� u�ivatelsk�ch dat do ViewData pro odli�en� zpr�v v konverzac�ch
            ViewData["CurrentUserId"] = UserId;

            // Na�ten� seznamu p��tel pro zah�jen� nov�ch konverzac�
            var friendshipsResponse = await client.GetAsync($"{ApiBaseUrl}/users/friendships");

            if (friendshipsResponse.IsSuccessStatusCode)
            {
                // Zpracov�n� seznamu p��telstv�
                var friendships = await friendshipsResponse.Content
                    .ReadFromJsonAsync<List<FriendshipDto>>();

                if (friendships != null)
                {
                    // Filtrov�n� pouze na schv�len� p��telstv�
                    var approvedFriendships = friendships
                        .Where(f => f.Status == FriendshipStatus.Approved)
                        .Select(f => new FriendItem
                        {
                            UserId = f.OtherUserId,
                            Nickname = f.OtherUserNickname
                        })
                        .OrderBy(f => f.Nickname)
                        .ToList();

                    // Z�sk�n� po�tu nep�e�ten�ch zpr�v od ka�d�ho p��tele
                    var unreadByUserResponse = await client.GetAsync($"{ApiBaseUrl}/messages/unread-counts-by-user");
                    if (unreadByUserResponse.IsSuccessStatusCode)
                    {
                        var unreadCounts = await unreadByUserResponse.Content.ReadFromJsonAsync<Dictionary<string, int>>();
                        if (unreadCounts != null)
                        {
                            // P�i�azen� po�tu nep�e�ten�ch zpr�v k jednotliv�m p��tel�m
                            foreach (var friend in approvedFriendships)
                            {
                                if (unreadCounts.TryGetValue(friend.UserId, out int count))
                                {
                                    friend.UnreadCount = count;
                                }
                            }
                        }
                    }

                    // Se�azen� p��tel - nejprve ti s nep�e�ten�mi zpr�vami, potom ostatn�
                    Friends = approvedFriendships
                        .OrderByDescending(f => f.UnreadCount > 0)
                        .ThenBy(f => f.Nickname)
                        .ToList();
                }
            }
            else
            {
                logger.Log($"Nepoda�ilo se na��st seznam p��tel, status: {friendshipsResponse.StatusCode}");
                // Nezobrazujeme chybu p�i na��t�n� p��tel, aby u�ivatel mohl alespo� pracovat s existuj�c�mi konverzacemi
            }

            return Page();
        }
        catch (Exception ex)
        {
            logger.Log("Chyba p�i na��t�n� str�nky se zpr�vami", ex);
            ErrorMessage = "P�i na��t�n� zpr�v do�lo k chyb�. Zkuste to pros�m pozd�ji.";
            return Page();
        }
    }
}