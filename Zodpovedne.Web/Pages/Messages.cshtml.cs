using Microsoft.AspNetCore.Mvc;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Filters;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;

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
    }

    /// <summary>
    /// Seznam konverzac� p�ihl�en�ho u�ivatele
    /// </summary>
    public List<ConversationDto> Conversations { get; private set; } = new();

    /// <summary>
    /// Seznam p��tel p�ihl�en�ho u�ivatele pro zah�jen� nov� konverzace
    /// </summary>
    public List<FriendItem> Friends { get; private set; } = new();

    public MessagesModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger)
        : base(clientFactory, configuration, logger)
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
            // Na�ten� seznamu konverzac�
            var conversationsResponse = await client.GetAsync($"{ApiBaseUrl}/messages/conversations");

            if (conversationsResponse.IsSuccessStatusCode)
            {
                Conversations = await conversationsResponse.Content.ReadFromJsonAsync<List<ConversationDto>>() ?? new();
            }
            else
            {
                _logger.Log($"Nepoda�ilo se na��st konverzace, status: {conversationsResponse.StatusCode}");
                ErrorMessage = "Nepoda�ilo se na��st seznam konverzac�.";
                return Page();
            }

            // Na�ten� seznamu p��tel pro zah�jen� nov�ch konverzac�
            var friendshipsResponse = await client.GetAsync($"{ApiBaseUrl}/users/friendships");

            if (friendshipsResponse.IsSuccessStatusCode)
            {
                // Zpracov�n� seznamu p��telstv�
                var friendships = await friendshipsResponse.Content.ReadFromJsonAsync<List<dynamic>>();

                if (friendships != null)
                {
                    // Filtrov�n� pouze na schv�len� p��telstv�
                    Friends = friendships
                        .Where(f => ((int)f.Status) == 1) // Status 1 = Approved
                        .Select(f => new FriendItem
                        {
                            UserId = (string)f.OtherUserId,
                            Nickname = (string)f.OtherUserNickname
                        })
                        .OrderBy(f => f.Nickname)
                        .ToList();
                }
            }
            else
            {
                _logger.Log($"Nepoda�ilo se na��st seznam p��tel, status: {friendshipsResponse.StatusCode}");
                // Nezobrazujeme chybu p�i na��t�n� p��tel, aby u�ivatel mohl alespo� pracovat s existuj�c�mi konverzacemi
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.Log("Chyba p�i na��t�n� str�nky se zpr�vami", ex);
            ErrorMessage = "P�i na��t�n� zpr�v do�lo k chyb�. Zkuste to pros�m pozd�ji.";
            return Page();
        }
    }
}