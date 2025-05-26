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
            // Nep�ihl�en� u�ivatel je p�esm�rov�n na �eznam kategori�
            if (!IsUserLoggedIn)
            {
                return RedirectToPage("/Categories");
            }

            try
            {
                // SEO meta data
                ViewData["Title"] = "Nov� diskuze od p��tel";
                ViewData["Description"] = "Sledujte nejnov�j�� diskuze od va�ich p��tel na Discussion.cz - �esk� diskuzn� soci�ln� s�ti bez reklam";
                ViewData["Keywords"] = "nov� diskuze, p��tel�, feed, �esk� soci�ln� s�, discussion";
                ViewData["OGTitle"] = "Nov� diskuze od p��tel - Discussion.cz";
                ViewData["OGDescription"] = "Objevte nejnov�j�� diskuze od va�ich p��tel na Discussion.cz";

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

            return Page();
        }
    }
}
