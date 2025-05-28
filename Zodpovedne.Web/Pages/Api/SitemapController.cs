using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Zodpovedne.Contracts.DTO;

namespace Zodpovedne.Web.Pages.Api;

[Route("sitemap.xml")]
[ApiController]
public class SitemapController : ControllerBase
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;

    public SitemapController(IHttpClientFactory clientFactory, IConfiguration configuration)
    {
        _clientFactory = clientFactory;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> GetSitemap()
    {
        var baseUrl = _configuration["BaseUrl"];
        var urls = new List<SitemapUrl>();

        // Statické stránky
        urls.Add(new SitemapUrl { Loc = $"{baseUrl}/", ChangeFreq = "daily", Priority = 1.0 });
        urls.Add(new SitemapUrl { Loc = $"{baseUrl}/Categories", ChangeFreq = "daily", Priority = 0.9 });
        urls.Add(new SitemapUrl { Loc = $"{baseUrl}/Account/Register", ChangeFreq = "monthly", Priority = 0.6 });
        urls.Add(new SitemapUrl { Loc = $"{baseUrl}/Account/Login", ChangeFreq = "monthly", Priority = 0.5 });

        try
        {
            var client = _clientFactory.CreateClient();

            // Kategorie
            var categoriesResponse = await client.GetAsync($"{_configuration["ApiBaseUrl"]}/Categories");
            if (categoriesResponse.IsSuccessStatusCode)
            {
                var categories = await categoriesResponse.Content.ReadFromJsonAsync<List<CategoryDto>>();
                foreach (var category in categories ?? new())
                {
                    urls.Add(new SitemapUrl
                    {
                        Loc = $"{baseUrl}/Categories/{category.Code}",
                        ChangeFreq = "daily",
                        Priority = 0.8
                    });
                }
            }

            // Veřejné diskuze - používám endpoint, který už máte
            var discussionsResponse = await client.GetAsync($"{_configuration["ApiBaseUrl"]}/discussions?pageSize=1000");
            if (discussionsResponse.IsSuccessStatusCode)
            {
                var result = await discussionsResponse.Content.ReadFromJsonAsync<PagedResultDto<DiscussionListDto>>();
                foreach (var discussion in result?.Items ?? new())
                {
                    urls.Add(new SitemapUrl
                    {
                        Loc = $"{baseUrl}/Categories/{discussion.CategoryCode}/{discussion.Code}",
                        LastMod = discussion.UpdatedAt,
                        ChangeFreq = "weekly",
                        Priority = 0.7
                    });
                }
            }
        }
        catch (Exception)
        {
            // V případě chyby API vratíme alespoň základní sitemap
        }

        var sitemap = GenerateSitemap(urls);
        return Content(sitemap, "application/xml");
    }

    private string GenerateSitemap(List<SitemapUrl> urls)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        foreach (var url in urls)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{System.Web.HttpUtility.HtmlEncode(url.Loc)}</loc>");
            if (url.LastMod.HasValue)
            {
                sb.AppendLine($"    <lastmod>{url.LastMod.Value:yyyy-MM-dd}</lastmod>");
            }
            sb.AppendLine($"    <changefreq>{url.ChangeFreq}</changefreq>");
            sb.AppendLine($"    <priority>{url.Priority.ToString("F1", CultureInfo.InvariantCulture)}</priority>");
            sb.AppendLine("  </url>");
        }

        sb.AppendLine("</urlset>");
        return sb.ToString();
    }
}

public class SitemapUrl
{
    public string Loc { get; set; } = "";
    public DateTime? LastMod { get; set; }
    public string ChangeFreq { get; set; } = "weekly";
    public double Priority { get; set; } = 0.5;
}