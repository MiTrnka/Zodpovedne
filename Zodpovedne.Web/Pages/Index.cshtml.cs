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
        /// Seznam diskuz� p��tel a TOP diskuz�
        /// </summary>
        public List<DiscussionListDto> CombinedFeed { get; private set; } = new();

        public IndexModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer)
        : base(clientFactory, configuration, logger, sanitizer)
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
                // Na�ten� kombinovan�ho seznamu diskuz�
                var client = _clientFactory.CreateBearerClient(HttpContext);
                var response = await client.GetAsync($"{ApiBaseUrl}/discussions/combined-feed");

                if (response.IsSuccessStatusCode)
                {
                    CombinedFeed = await response.Content.ReadFromJsonAsync<List<DiscussionListDto>>() ?? new();
                }
                else
                {
                    _logger.Log($"Nepoda�ilo se na��st kombinovan� feed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.Log("Chyba p�i na��t�n� kombinovan�ho feedu", ex);
                // Zde nechceme zobrazovat chybu u�ivateli, sta�� logov�n�
            }

            return Page();
        }
    }
}
