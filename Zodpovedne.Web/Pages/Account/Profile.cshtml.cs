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
        // Seznam diskuz� u�ivatele
        public List<BasicDiscussionInfoDto> UserDiscussions { get; set; } = new();

        public ProfileModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger) : base(clientFactory, configuration, logger)
        {
        }

        public async Task<IActionResult> OnGetAsync(string nickname)
        {
            var client = _clientFactory.CreateBearerClient(HttpContext);

            // Na�ten� profilu u�ivatele
            var response = await client.GetAsync($"{ApiBaseUrl}/users/{nickname}");

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

            return Page();
        }
    }
}
