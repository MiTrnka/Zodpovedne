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
            //Inkrementace poètu pøístupù na Index stránku
            try
            {
                var client = _clientFactory.CreateBearerClient(HttpContext);

                // Zavolání API endpointu pro inkrementaci
                var response = await client.PostAsync($"{ApiBaseUrl}/users/increment-parameter/AccessCount", null);

                // Logování pouze v pøípadì chyby (neblokuje naètení stránky)
                if (!response.IsSuccessStatusCode)
                {
                    logger.Log($"Nepodaøilo se inkrementovat AccessCount. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                // Chyba pøi poèítání pøístupù nesmí zablokovat naètení stránky
                logger.Log("Chyba pøi inkrementaci AccessCount", ex);
            }


            // Nepøihlášenı uivatel je pøesmìrován na šeznam kategorií
            if (!IsUserLoggedIn)
            {
                return RedirectToPage("/Categories");
            }

            try
            {
                // SEO meta data pro pøihlášené uivatele
                ViewData["Title"] = "Nové diskuze od pøátel";
                ViewData["Description"] = "Sledujte nejnovìjší diskuze od vašich pøátel na Discussion.cz - èeské diskuzní sociální síti bez reklam. Zùstaòte v kontaktu s komunitou.";
                ViewData["Keywords"] = "nové diskuze, nove diskuze, pøátelé, pratele, feed, aktuality, èeská sociální sí, ceska socialni sit, discussion, komunita, bez reklam";

                // Pro Open Graph (bez diakritiky)
                ViewData["OGTitle"] = "Nove diskuze od pratel - Discussion.cz";
                ViewData["OGDescription"] = "Sledujte nejnovejsi diskuze od vasich pratel na Discussion.cz - ceske diskuzni socialni siti bez reklam.";

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

            ViewData["CanonicalUrl"] = $"{_configuration["BaseUrl"]}/";
            return Page();
        }
    }
}
