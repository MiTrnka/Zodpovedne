using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;
using Ganss.Xss;
using Zodpovedne.Logging.Services;

namespace Zodpovedne.Web.Pages;

/// <summary>
/// PageModel pro stránku vyhledávání diskuzí
/// Umožòuje uživatelùm vyhledávat diskuze podle klíèových slov
/// s využitím fulltextového vyhledávání s podporou èeského jazyka
/// </summary>
public class SearchModel : BasePageModel
{
    /// <summary>
    /// Vyhledávací dotaz zadaný uživatelem
    /// Získáván z URL parametru (napø. /Search?query=slovo1+slovo2)
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string Query { get; set; } = "";

    /// <summary>
    /// Seznam výsledkù vyhledávání - diskuze odpovídající vyhledávacímu dotazu
    /// </summary>
    public List<BasicDiscussionInfoDto> SearchResults { get; private set; } = new();

    /// <summary>
    /// Konstruktor - pøedává závislosti do nadøazené tøídy BasePageModel
    /// </summary>
    public SearchModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator)
        : base(clientFactory, configuration, logger, sanitizer, translator)
    {
    }

    /// <summary>
    /// Handler pro GET požadavek na stránku vyhledávání
    /// Pokud byl zadán vyhledávací dotaz, volá API pro získání výsledkù
    /// </summary>
    public async Task OnGetAsync()
    {
        // Pokud nebyl zadán vyhledávací dotaz, nezpracovávat nic
        if (string.IsNullOrWhiteSpace(Query))
            return;

        try
        {
            // Vytvoøení HTTP klienta s automatickým pøidáním JWT tokenu
            var client = _clientFactory.CreateBearerClient(HttpContext);

            // Volání API pro vyhledávání diskuzí
            // Použití URI.EscapeDataString zajistí správné zakódování speciálních znakù v dotazu
            var response = await client.GetAsync($"{ApiBaseUrl}/discussions/search?query={Uri.EscapeDataString(Query)}&limit=20");

            // Zpracování odpovìdi z API
            if (response.IsSuccessStatusCode)
            {
                // Deserializace výsledkù vyhledávání
                SearchResults = await response.Content.ReadFromJsonAsync<List<BasicDiscussionInfoDto>>() ?? new();
            }
            else
            {
                // Logování chyby pøi neúspìšném volání API
                _logger.Log($"Chyba pøi vyhledávání: {response.StatusCode}");
                ErrorMessage = "Pøi vyhledávání došlo k chybì.";
            }
        }
        catch (Exception ex)
        {
            // Logování a zobrazení chyby pøi výjimce
            _logger.Log("Chyba pøi vyhledávání", ex);
            ErrorMessage = "Pøi vyhledávání došlo k chybì.";
        }
    }

    /// <summary>
    /// Metoda pro zvýraznìní hledaných slov v textu obsahu diskuze
    /// Nalezená slova budou obalena HTML tagem span s CSS tøídou pro zvýraznìní
    /// </summary>
    /// <param name="content">Pùvodní text obsahu diskuze</param>
    /// <returns>HTML s zvýraznìnými slovy nebo pùvodní text, pokud nelze zvýraznit</returns>
    public string GetHighlightedContent(string content)
    {
        // Kontrola vstupních parametrù
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(Query))
            return content;

        // Nejprve odstraníme všechny HTML tagy
        var plainText = Regex.Replace(content, "<.*?>", string.Empty);

        // Omezení délky obsahu pro pøehlednìjší zobrazení
        if (plainText.Length > 300)
        {
            plainText = plainText.Substring(0, 300) + "...";
        }

        // Sanitizace je stále vhodná pro pøípadné zbývající HTML-like obsah
        plainText = _sanitizer.Sanitize(plainText);

        // Rozdìlení vyhledávacího dotazu na jednotlivá slova
        // Ignorujeme slova kratší než 2 znaky (pøíliš obecná)
        var queryWords = Query.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1)
            .Select(w => Regex.Escape(w))  // Escapování speciálních znakù pro bezpeèné použití v regex
            .ToList();

        // Pokud nezùstala žádná platná slova, vrátíme text bez HTML tagù
        if (!queryWords.Any())
            return plainText;

        // Vytvoøení regex vzoru pro zvýraznìní - case insensitive
        // Vzor bude hledat všechna zadaná slova oddìlená operátorem |
        var pattern = $"({string.Join("|", queryWords)})";

        // Nahrazení nalezených slov zvýraznìným HTML
        // Parametr RegexOptions.IgnoreCase zajistí, že nezáleží na velikosti písmen
        var highlighted = Regex.Replace(
            plainText,
            pattern,
            "<span class=\"bg-warning\">$1</span>",  // Použití Bootstrap tøídy bg-warning pro žluté zvýraznìní
            RegexOptions.IgnoreCase);

        return highlighted;
    }
}