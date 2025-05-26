using Microsoft.AspNetCore.Mvc;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Filters;
using Zodpovedne.Logging;
using Ganss.Xss;
using Zodpovedne.Logging.Services;
using Zodpovedne.Contracts.Enums;
using System.Text.Json;
using Zodpovedne.Web.Pages.Models;

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

    // Doèasný kód diskuze pro práci s obrázky a následnými referencemi v obsahu
    public string TempDiscussionCode { get; set; } = "";

    // Vlastnost urèující, zda uživatel mùže nahrávat obrázky
    public bool CanUploadFiles { get; private set; } = false;

    // Nová vlastnost pro checkbox "Vytvoøit hlasování"
    [BindProperty]
    public bool HasVoting { get; set; } = false;

    // Vlastnost pro uložení hlasovacích otázek
    [BindProperty]
    public string VotingQuestions { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        // Generování doèasného kódu pro diskuzi
        TempDiscussionCode = $"temp_{Guid.NewGuid().ToString("N")}";

        // Získání detailù kategorie
        var client = _clientFactory.CreateBearerClient(HttpContext);
        var response = await client.GetAsync($"{ApiBaseUrl}/Categories/{CategoryCode}");

        if (!response.IsSuccessStatusCode)
        {
            logger.Log($"Kategorie {CategoryCode} nenalezena");
            ErrorMessage = "Omlouváme se, ale diskuzi nejde momentálnì založit.";
            return Page();
        }

        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        if (category == null)
        {
            logger.Log($"Kategorie {CategoryCode} nelze naèíst");
            ErrorMessage = "Omlouváme se, ale diskuzi nejde momentálnì založit.";
            return Page();
        }

        CategoryName = category.Name;
        Input.CategoryId = category.Id;

        // Nastavení výchozí hodnoty VoteType na None (žádné hlasování)
        Input.VoteType = VoteType.None;

        // Uložení doèasného kódu do session pro pozdìjší použití
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
        // Pokud se nejedná o hlasování, manuálnì oèistíme ModelState od chyb spojených s hlasováním
        if (!HasVoting)
        {
            // Resetujeme VoteType na None (žádné hlasování) bez ohledu na to, co pøišlo z formuláøe
            Input.VoteType = VoteType.None;

            // Vyèistíme hodnotu VotingQuestions
            VotingQuestions = "";

            // Odstraníme všechny chyby validace spojené s hlasováním
            foreach (var key in ModelState.Keys.ToList())
            {
                if (key.StartsWith("VotingQuestions") || key.StartsWith("Input.VoteType"))
                {
                    ModelState.Remove(key);
                }
            }
        }

        // Kontrola, jestli požadavek jde z platného odkazu
        var referer = Request.Headers.Referer.ToString();
        if (!referer.Contains("/discussion/create/"))
        {
            return Forbid("Neoprávnìný pøístup");
        }

        // Kontrola validaèního stavu
        if (!ModelState.IsValid)
        {
            // Debug informace - vypíšeme si chyby v ModelState, abychom vidìli, co konkrétnì selhává
            foreach (var state in ModelState)
            {
                if (state.Value.Errors.Any())
                {
                    logger.Log($"Validaèní chyba pro {state.Key}: {string.Join(", ", state.Value.Errors.Select(e => e.ErrorMessage))}");
                }
            }

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
            Input.Title = sanitizer.Sanitize(Input.Title);
            Input.Content = sanitizer.Sanitize(Input.Content);


            // Nastavení typu hlasování podle hodnoty v checkboxu a vybranéhotypu
            if (HasVoting)
            {
                // Pokud je hlasování povoleno, zkontrolujeme, zda je typ nastaven na nìco jiného než None
                // Pokud by typ zùstal None, nastavíme výchozí hodnotu Visible
                if (Input.VoteType == VoteType.None)
                {
                    Input.VoteType = VoteType.Visible;
                }
                // Jinak ponecháme vybraný typ (z formuláøe pøijde v Input.VoteType)
            }
            else
            {
                // Pokud není hlasování povoleno, nastavíme typ na None bez ohledu na to, co pøišlo z formuláøe
                Input.VoteType = VoteType.None;
                // Vyèistíme pøípadné otázky, které pøišly z formuláøe
                VotingQuestions = "";
            }

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

                        // Vytvoøení hlasování, pokud je povoleno
                        if (HasVoting && discussionInfo.VoteType != VoteType.None && !string.IsNullOrEmpty(VotingQuestions))
                        {
                            try
                            {
                                // Deserializace otázek s ignorováním velikosti písmen
                                var options = new JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                };
                                var votingQuestionsData = JsonSerializer.Deserialize<List<VotingQuestionDto>>(VotingQuestions, options);

                                if (votingQuestionsData != null && votingQuestionsData.Count > 0)
                                {
                                    // Vytvoøení hlasování pomocí API
                                    var createVotingRequest = new CreateOrUpdateVotingDto
                                    {
                                        DiscussionId = discussionId,
                                        VoteType = discussionInfo.VoteType,
                                        Questions = votingQuestionsData
                                    };

                                    var votingResponse = await client.PostAsJsonAsync($"{ApiBaseUrl}/votings", createVotingRequest);

                                    if (!votingResponse.IsSuccessStatusCode)
                                    {
                                        logger.Log($"Nepodaøilo se vytvoøit hlasování pro diskuzi ID {discussionId}. Stavový kód: {votingResponse.StatusCode}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Log($"Chyba pøi vytváøení hlasování pro diskuzi ID {discussionId}", ex);
                                // Pokraèujeme i pøi chybì vytváøení hlasování (diskuze byla úspìšnì vytvoøena)
                            }
                        }
                    }
                }

                return RedirectToPage("/Category", new { categoryCode = CategoryCode });
            }

            ModelState.AddModelError("", "Nepodaøilo se vytvoøit diskuzi.");
        }
        catch (Exception ex)
        {
            logger.Log("Došlo k chybì pøi vytváøení diskuze", ex);
            ErrorMessage = "Omlouváme se, ale došlo k chybì pøi vytváøení diskuze.";
            return Page();
        }

        // Znovu naèteme kategorii pro zobrazení
        await OnGetAsync();
        return Page();
    }
}