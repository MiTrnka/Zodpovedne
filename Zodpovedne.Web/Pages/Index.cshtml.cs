using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Logging;
using Ganss.Xss;

namespace Zodpovedne.Web.Pages
{
    public class IndexModel : BasePageModel
    {
        /// <summary>
        /// Seznam diskuzí pøátel a TOP diskuzí
        /// </summary>
        public List<DiscussionListDto> CombinedFeed { get; private set; } = new();

        public IndexModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer)
        : base(clientFactory, configuration, logger, sanitizer)
        {
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Nepøihlášený uživatel je pøesmìrován na šeznam kategorií
            if (!IsUserLoggedIn)
            {
                return RedirectToPage("/Categories");
            }

            try
            {
                // Naètení kombinovaného seznamu diskuzí
                var client = _clientFactory.CreateBearerClient(HttpContext);
                var response = await client.GetAsync($"{ApiBaseUrl}/discussions/combined-feed");

                if (response.IsSuccessStatusCode)
                {
                    CombinedFeed = await response.Content.ReadFromJsonAsync<List<DiscussionListDto>>() ?? new();
                }
                else
                {
                    _logger.Log($"Nepodaøilo se naèíst kombinovaný feed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.Log("Chyba pøi naèítání kombinovaného feedu", ex);
                // Zde nechceme zobrazovat chybu uživateli, staèí logování
            }

            return Page();
        }
    }
}
