using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Logging;
using Ganss.Xss;
using Zodpovedne.Logging.Services;
using Zodpovedne.Web.Pages.Models;

namespace Zodpovedne.Web.Pages
{
    public class IndexModel : BasePageModel
    {
        /// <summary>
        /// Seznam diskuzí pøátel a TOP diskuzí
        /// </summary>
        public List<DiscussionListDto> CombinedFeed { get; private set; } = new();

        public IndexModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator)
        : base(clientFactory, configuration, logger, sanitizer, translator)
        {
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Nepøihlášenı uivatel je pøesmìrován na šeznam kategorií
            if (!IsUserLoggedIn)
            {
                return RedirectToPage("/Categories");
            }

            try
            {
                // SEO meta data
                ViewData["Title"] = "Nové diskuze od pøátel";
                ViewData["Description"] = "Sledujte nejnovìjší diskuze od vašich pøátel na Discussion.cz - èeské diskuzní sociální síti bez reklam";
                ViewData["Keywords"] = "nové diskuze, pøátelé, feed, èeská sociální sí, discussion";
                ViewData["OGTitle"] = "Nové diskuze od pøátel - Discussion.cz";
                ViewData["OGDescription"] = "Objevte nejnovìjší diskuze od vašich pøátel na Discussion.cz";

                // Naètení kombinovaného seznamu diskuzí
                var client = _clientFactory.CreateBearerClient(HttpContext);
                var response = await client.GetAsync($"{ApiBaseUrl}/discussions/combined-feed");

                if (response.IsSuccessStatusCode)
                {
                    CombinedFeed = await response.Content.ReadFromJsonAsync<List<DiscussionListDto>>() ?? new();
                }
                else
                {
                    logger.Log($"Nepodaøilo se naèíst kombinovanı feed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                logger.Log("Chyba pøi naèítání kombinovaného feedu", ex);
                // Zde nechceme zobrazovat chybu uivateli, staèí logování
            }

            return Page();
        }
    }
}
