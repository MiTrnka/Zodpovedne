using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;

namespace Zodpovedne.Web.Pages;

public class SearchModel : BasePageModel
{
    [BindProperty(SupportsGet = true)]
    public string Query { get; set; } = "";

    public List<BasicDiscussionInfoDto> SearchResults { get; private set; } = new();

    public SearchModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger)
        : base(clientFactory, configuration, logger)
    {
    }

    public async Task OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Query))
            return;

        try
        {
            var client = _clientFactory.CreateBearerClient(HttpContext);
            var response = await client.GetAsync($"{ApiBaseUrl}/discussions/search?query={Uri.EscapeDataString(Query)}&limit=20");

            if (response.IsSuccessStatusCode)
            {
                SearchResults = await response.Content.ReadFromJsonAsync<List<BasicDiscussionInfoDto>>() ?? new();
            }
            else
            {
                _logger.Log($"Chyba p�i vyhled�v�n�: {response.StatusCode}");
                ErrorMessage = "P�i vyhled�v�n� do�lo k chyb�.";
            }
        }
        catch (Exception ex)
        {
            _logger.Log("Chyba p�i vyhled�v�n�", ex);
            ErrorMessage = "P�i vyhled�v�n� do�lo k chyb�.";
        }
    }

    public string GetHighlightedContent(string content)
    {
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(Query))
            return content;

        // Omezen� d�lky obsahu
        if (content.Length > 300)
        {
            content = content.Substring(0, 300) + "...";
        }

        // Jednoduch� HTML sanitizer pro zobrazen�
        content = _sanitizer.Sanitize(content);

        // Rozd�len� dotazu na jednotliv� slova
        var queryWords = Query.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1)
            .Select(w => Regex.Escape(w))
            .ToList();

        if (!queryWords.Any())
            return content;

        // Vytvo�en� regex vzoru pro zv�razn�n� - case insensitive
        var pattern = $"({string.Join("|", queryWords)})";
        var highlighted = Regex.Replace(
            content,
            pattern,
            "<span class=\"bg-warning\">$1</span>",
            RegexOptions.IgnoreCase);

        return highlighted;
    }
}