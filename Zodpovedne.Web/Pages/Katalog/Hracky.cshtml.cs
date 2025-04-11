using Ganss.Xss;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Logging;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Web.Services;

namespace Zodpovedne.Web.Pages.Katalog
{
    public class HrackyModel : BasePageModel
    {
        public HrackyModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator) : base(clientFactory, configuration, logger, sanitizer, translator)
        {
        }
        public void OnGet()
        {
        }
    }
}
