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
/// PageModel pro str�nku vyhled�v�n� diskuz�
/// Umo��uje u�ivatel�m vyhled�vat diskuze podle kl��ov�ch slov
/// s vyu�it�m fulltextov�ho vyhled�v�n� s podporou �esk�ho jazyka
/// </summary>
public class SearchModel : BasePageModel
{
    /// <summary>
    /// Vyhled�vac� dotaz zadan� u�ivatelem
    /// Z�sk�v�n z URL parametru (nap�. /Search?query=slovo1+slovo2)
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string Query { get; set; } = "";

    /// <summary>
    /// Seznam v�sledk� vyhled�v�n� - diskuze odpov�daj�c� vyhled�vac�mu dotazu
    /// </summary>
    public List<BasicDiscussionInfoDto> SearchResults { get; private set; } = new();

    /// <summary>
    /// Konstruktor - p�ed�v� z�vislosti do nad�azen� t��dy BasePageModel
    /// </summary>
    public SearchModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator)
        : base(clientFactory, configuration, logger, sanitizer, translator)
    {
    }

    /// <summary>
    /// Handler pro GET po�adavek na str�nku vyhled�v�n�
    /// Pokud byl zad�n vyhled�vac� dotaz, vol� API pro z�sk�n� v�sledk�
    /// </summary>
    public async Task OnGetAsync()
    {
        // Pokud nebyl zad�n vyhled�vac� dotaz, nezpracov�vat nic
        if (string.IsNullOrWhiteSpace(Query))
            return;

        try
        {
            // Vytvo�en� HTTP klienta s automatick�m p�id�n�m JWT tokenu
            var client = _clientFactory.CreateBearerClient(HttpContext);

            // Vol�n� API pro vyhled�v�n� diskuz�
            // Pou�it� URI.EscapeDataString zajist� spr�vn� zak�dov�n� speci�ln�ch znak� v dotazu
            var response = await client.GetAsync($"{ApiBaseUrl}/discussions/search?query={Uri.EscapeDataString(Query)}&limit=20");

            // Zpracov�n� odpov�di z API
            if (response.IsSuccessStatusCode)
            {
                // Deserializace v�sledk� vyhled�v�n�
                SearchResults = await response.Content.ReadFromJsonAsync<List<BasicDiscussionInfoDto>>() ?? new();
            }
            else
            {
                // Logov�n� chyby p�i ne�sp�n�m vol�n� API
                _logger.Log($"Chyba p�i vyhled�v�n�: {response.StatusCode}");
                ErrorMessage = "P�i vyhled�v�n� do�lo k chyb�.";
            }
        }
        catch (Exception ex)
        {
            // Logov�n� a zobrazen� chyby p�i v�jimce
            _logger.Log("Chyba p�i vyhled�v�n�", ex);
            ErrorMessage = "P�i vyhled�v�n� do�lo k chyb�.";
        }
    }

    /// <summary>
    /// Metoda pro zv�razn�n� hledan�ch slov v textu obsahu diskuze
    /// Nalezen� slova budou obalena HTML tagem span s CSS t��dou pro zv�razn�n�
    /// </summary>
    /// <param name="content">P�vodn� text obsahu diskuze</param>
    /// <returns>HTML s zv�razn�n�mi slovy nebo p�vodn� text, pokud nelze zv�raznit</returns>
    public string GetHighlightedContent(string content)
    {
        // Kontrola vstupn�ch parametr�
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(Query))
            return content;

        // Nejprve odstran�me v�echny HTML tagy
        var plainText = Regex.Replace(content, "<.*?>", string.Empty);

        // Omezen� d�lky obsahu pro p�ehledn�j�� zobrazen�
        if (plainText.Length > 300)
        {
            plainText = plainText.Substring(0, 300) + "...";
        }

        // Sanitizace je st�le vhodn� pro p��padn� zb�vaj�c� HTML-like obsah
        plainText = _sanitizer.Sanitize(plainText);

        // Rozd�len� vyhled�vac�ho dotazu na jednotliv� slova
        // Ignorujeme slova krat�� ne� 2 znaky (p��li� obecn�)
        var queryWords = Query.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1)
            .Select(w => Regex.Escape(w))  // Escapov�n� speci�ln�ch znak� pro bezpe�n� pou�it� v regex
            .ToList();

        // Pokud nez�stala ��dn� platn� slova, vr�t�me text bez HTML tag�
        if (!queryWords.Any())
            return plainText;

        // Vytvo�en� regex vzoru pro zv�razn�n� - case insensitive
        // Vzor bude hledat v�echna zadan� slova odd�len� oper�torem |
        var pattern = $"({string.Join("|", queryWords)})";

        // Nahrazen� nalezen�ch slov zv�razn�n�m HTML
        // Parametr RegexOptions.IgnoreCase zajist�, �e nez�le�� na velikosti p�smen
        var highlighted = Regex.Replace(
            plainText,
            pattern,
            "<span class=\"bg-warning\">$1</span>",  // Pou�it� Bootstrap t��dy bg-warning pro �lut� zv�razn�n�
            RegexOptions.IgnoreCase);

        return highlighted;
    }
}