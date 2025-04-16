using Microsoft.AspNetCore.Mvc;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Filters;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;
using Ganss.Xss;
using Zodpovedne.Logging.Services;
using Zodpovedne.Contracts.Enums;

namespace Zodpovedne.Web.Pages;

[AuthenticationFilter]
public class CreateDiscussionModel : BasePageModel
{
    public CreateDiscussionModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator) : base(clientFactory, configuration, logger, sanitizer, translator)
    {
    }

    [BindProperty]
    public CreateDiscussionDto Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string CategoryCode { get; set; } = "";

    public string CategoryName { get; set; } = "";

    // Pøidáme novou vlastnost pro doèasný kód diskuze
    public string TempDiscussionCode { get; set; } = "";

    // Pøidáme vlastnost, která urèuje, zda uživatel mùže nahrávat obrázky
    public bool CanUploadFiles { get; private set; } = false;

    // Pøidáme vlastnost pro checkbox "Diskuze jen pro kamarády"
    [BindProperty]
    public bool IsPrivate { get; set; } = false;

    public async Task<IActionResult> OnGetAsync()
    {
        // Generování doèasného kódu pro diskuzi
        TempDiscussionCode = $"temp_{Guid.NewGuid().ToString("N")}";

        // Získání detailù kategorie
        var client = _clientFactory.CreateBearerClient(HttpContext);
        var response = await client.GetAsync($"{ApiBaseUrl}/Categories/{CategoryCode}");

        if (!response.IsSuccessStatusCode)
        {
            _logger.Log($"Kategorie {CategoryCode} nenalezena");
            ErrorMessage = "Omlouváme se, ale diskuzi nejde momentálnì založit.";
            return Page();
        }

        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        if (category == null)
        {
            _logger.Log($"Kategorie {CategoryCode} nelze naèíst");
            ErrorMessage = "Omlouváme se, ale diskuzi nejde momentálnì založit.";
            return Page();
        }

        CategoryName = category.Name;
        Input.CategoryId = category.Id;

        // Uložíme doèasný kód do session, aby byl k dispozici i po odeslání formuláøe
        HttpContext.Session.SetString("TempDiscussionCode", TempDiscussionCode);

        // Zjištìní typu pøihlášeného uživatele a nastavení oprávnìní pro nahrávání souborù
        if (IsUserLoggedIn)
        {
            var userResponse = await client.GetAsync($"{ApiBaseUrl}/users/authenticated-user");
            if (userResponse.IsSuccessStatusCode)
            {
                var user = await userResponse.Content.ReadFromJsonAsync<UserProfileDto>();

                // Pouze uživatelé typu Normal mohou nahrávat soubory
                if (user != null && user.UserType == UserType.Normal)
                {
                    CanUploadFiles = true;
                }
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Kontrola validaèního stavu
        if (!ModelState.IsValid)
        {
            // Znovu naèteme kategorii pro zobrazení, ale zachováme vyplnìná data
            var client = _clientFactory.CreateBearerClient(HttpContext);
            var response = await client.GetAsync($"{ApiBaseUrl}/Categories/{CategoryCode}");

            if (response.IsSuccessStatusCode)
            {
                var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
                if (category != null)
                {
                    CategoryName = category.Name;
                }
            }

            // Obnovíme doèasný kód ze session
            TempDiscussionCode = HttpContext.Session.GetString("TempDiscussionCode") ?? $"temp_{Guid.NewGuid().ToString("N")}";

            // Zjištìní typu pøihlášeného uživatele a nastavení oprávnìní pro nahrávání souborù
            if (IsUserLoggedIn)
            {
                var userResponse = await client.GetAsync($"{ApiBaseUrl}/users/authenticated-user");
                if (userResponse.IsSuccessStatusCode)
                {
                    var user = await userResponse.Content.ReadFromJsonAsync<UserProfileDto>();
                    if (user != null && user.UserType == UserType.Normal)
                    {
                        CanUploadFiles = true;
                    }
                }
            }

            // Vrátíme stránku s chybami validace
            return Page();
        }

        try
        {
            // Získáme doèasný kód ze session
            var tempCode = HttpContext.Session.GetString("TempDiscussionCode");
            if (string.IsNullOrEmpty(tempCode))
            {
                // Pokud nìjakým zpùsobem chybí, vygenerujeme nový
                tempCode = $"temp_{Guid.NewGuid().ToString("N")}";
            }

            // Sanitizace vstupního HTML
            Input.Title = _sanitizer.Sanitize(Input.Title);
            Input.Content = _sanitizer.Sanitize(Input.Content);

            // Nastavíme typ diskuze na Private, pokud byl zaškrtnut checkbox
            Input.Type = IsPrivate ? DiscussionType.Private : DiscussionType.Normal;

            var client = _clientFactory.CreateBearerClient(HttpContext);

            // Vytvoøení diskuze
            var response = await client.PostAsJsonAsync($"{ApiBaseUrl}/discussions", Input);

            if (response.IsSuccessStatusCode && response.Headers.Location != null)
            {
                // Diskuze byla úspìšnì vytvoøena, obdržíme URL s ID vytvoøené diskuze
                var locationUrl = response.Headers.Location.ToString();
                var discussionId = int.Parse(locationUrl.Split('/').Last());

                // Získáme informace o vytvoøené diskuzi
                var discussionResponse = await client.GetAsync($"{ApiBaseUrl}/discussions/{discussionId}/basic-info");
                if (discussionResponse.IsSuccessStatusCode)
                {
                    var discussionInfo = await discussionResponse.Content.ReadFromJsonAsync<BasicDiscussionInfoDto>();
                    if (discussionInfo != null)
                    {
                        // Zavoláme API pro pøejmenování odkazù na obrázky v obsahu diskuze
                        await client.PostAsJsonAsync($"{ApiBaseUrl}/discussions/update-image-paths",
                            new
                            {
                                DiscussionId = discussionId,
                                OldPrefix = tempCode,
                                NewPrefix = discussionInfo.DiscussionCode
                            });

                        // Pøejmenování adresáøe s obrázky
                        await client.PostAsJsonAsync($"{BaseUrl}upload/rename-directory",
                            new
                            {
                                OldCode = tempCode,
                                NewCode = discussionInfo.DiscussionCode
                            });
                    }
                }

                return RedirectToPage("/Category", new { categoryCode = CategoryCode });
            }

            ModelState.AddModelError("", "Nepodaøilo se vytvoøit diskuzi.");
        }
        catch (Exception ex)
        {
            _logger.Log("Došlo k chybì pøi vytváøení diskuze", ex);
            ErrorMessage = "Omlouváme se, ale došlo k chybì pøi vytváøení diskuze.";
            return Page();
        }

        // Znovu naèteme kategorii pro zobrazení
        await OnGetAsync();
        return Page();
    }
}