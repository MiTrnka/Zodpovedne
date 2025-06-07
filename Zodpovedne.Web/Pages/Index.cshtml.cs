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
        /// Seznam diskuz� p��tel a TOP diskuz�
        /// </summary>
        public List<DiscussionListDto> CombinedFeed { get; private set; } = new();

        public IndexModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator)
        : base(clientFactory, configuration, logger, sanitizer, translator)
        {
        }

        public async Task<IActionResult> OnGetAsync()
        {
            //Inkrementace po�tu p��stup� na Index str�nku
            try
            {
                var client = _clientFactory.CreateBearerClient(HttpContext);

                // Zavol�n� API endpointu pro inkrementaci
                var response = await client.PostAsync($"{ApiBaseUrl}/users/increment-parameter/AccessCount", null);

                // Logov�n� pouze v p��pad� chyby (neblokuje na�ten� str�nky)
                if (!response.IsSuccessStatusCode)
                {
                    logger.Log($"Nepoda�ilo se inkrementovat AccessCount. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                // Chyba p�i po��t�n� p��stup� nesm� zablokovat na�ten� str�nky
                logger.Log("Chyba p�i inkrementaci AccessCount", ex);
            }


            // Nep�ihl�en� u�ivatel je p�esm�rov�n na �eznam kategori�
            if (!IsUserLoggedIn)
            {
                return RedirectToPage("/Categories");
            }

            try
            {
                // SEO meta data pro p�ihl�en� u�ivatele
                ViewData["Title"] = "Nov� diskuze od p��tel";
                ViewData["Description"] = "Sledujte nejnov�j�� diskuze od va�ich p��tel na Discussion.cz - �esk� diskuzn� soci�ln� s�ti bez reklam. Z�sta�te v kontaktu s komunitou.";
                ViewData["Keywords"] = "nov� diskuze, nove diskuze, p��tel�, pratele, feed, aktuality, �esk� soci�ln� s�, ceska socialni sit, discussion, komunita, bez reklam";

                // Pro Open Graph (bez diakritiky)
                ViewData["OGTitle"] = "Nove diskuze od pratel - Discussion.cz";
                ViewData["OGDescription"] = "Sledujte nejnovejsi diskuze od vasich pratel na Discussion.cz - ceske diskuzni socialni siti bez reklam.";

                // Na�ten� kombinovan�ho seznamu diskuz�
                var client = _clientFactory.CreateBearerClient(HttpContext);
                var response = await client.GetAsync($"{ApiBaseUrl}/discussions/combined-feed");

                if (response.IsSuccessStatusCode)
                {
                    CombinedFeed = await response.Content.ReadFromJsonAsync<List<DiscussionListDto>>() ?? new();
                }
                else
                {
                    logger.Log($"Nepoda�ilo se na��st kombinovan� feed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                logger.Log("Chyba p�i na��t�n� kombinovan�ho feedu", ex);
                // Zde nechceme zobrazovat chybu u�ivateli, sta�� logov�n�
            }

            ViewData["CanonicalUrl"] = $"{_configuration["BaseUrl"]}/";
            return Page();
        }
    }
}
