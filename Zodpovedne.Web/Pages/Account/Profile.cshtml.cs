using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Logging;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Models.Base;

namespace Zodpovedne.Web.Pages.Account
{
    public class ProfileModel : BasePageModel
    {
        public UserProfileDto? UserProfile { get; set; }
        // Seznam diskuzí uživatele
        public List<BasicDiscussionInfoDto> UserDiscussions { get; set; } = new();

        public ProfileModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger) : base(clientFactory, configuration, logger)
        {
        }

        public async Task<IActionResult> OnGetAsync(string nickname)
        {
            var client = _clientFactory.CreateBearerClient(HttpContext);

            // Naètení profilu uživatele
            var response = await client.GetAsync($"{ApiBaseUrl}/users/{nickname}");

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

            return Page();
        }
    }
}
