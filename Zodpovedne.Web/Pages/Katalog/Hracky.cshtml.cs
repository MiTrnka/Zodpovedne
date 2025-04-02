using Ganss.Xss;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Logging;
using Zodpovedne.Web.Models.Base;

namespace Zodpovedne.Web.Pages.Katalog
{
    public class HrackyModel : BasePageModel
    {
        public HrackyModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer) : base(clientFactory, configuration, logger, sanitizer)
        {
        }
        public void OnGet()
        {
        }
    }
}
