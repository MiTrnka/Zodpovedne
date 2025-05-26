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

    // Do�asn� k�d diskuze pro pr�ci s obr�zky a n�sledn�mi referencemi v obsahu
    public string TempDiscussionCode { get; set; } = "";

    // Vlastnost ur�uj�c�, zda u�ivatel m��e nahr�vat obr�zky
    public bool CanUploadFiles { get; private set; } = false;

    // Nov� vlastnost pro checkbox "Vytvo�it hlasov�n�"
    [BindProperty]
    public bool HasVoting { get; set; } = false;

    // Vlastnost pro ulo�en� hlasovac�ch ot�zek
    [BindProperty]
    public string VotingQuestions { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        // Generov�n� do�asn�ho k�du pro diskuzi
        TempDiscussionCode = $"temp_{Guid.NewGuid().ToString("N")}";

        // Z�sk�n� detail� kategorie
        var client = _clientFactory.CreateBearerClient(HttpContext);
        var response = await client.GetAsync($"{ApiBaseUrl}/Categories/{CategoryCode}");

        if (!response.IsSuccessStatusCode)
        {
            logger.Log($"Kategorie {CategoryCode} nenalezena");
            ErrorMessage = "Omlouv�me se, ale diskuzi nejde moment�ln� zalo�it.";
            return Page();
        }

        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        if (category == null)
        {
            logger.Log($"Kategorie {CategoryCode} nelze na��st");
            ErrorMessage = "Omlouv�me se, ale diskuzi nejde moment�ln� zalo�it.";
            return Page();
        }

        CategoryName = category.Name;
        Input.CategoryId = category.Id;

        // Nastaven� v�choz� hodnoty VoteType na None (��dn� hlasov�n�)
        Input.VoteType = VoteType.None;

        // Ulo�en� do�asn�ho k�du do session pro pozd�j�� pou�it�
        HttpContext.Session.SetString("TempDiscussionCode", TempDiscussionCode);

        // Zji�t�n� typu p�ihl�en�ho u�ivatele a nastaven� opr�vn�n� pro nahr�v�n� soubor�
        if (IsUserLoggedIn)
        {
            var userResponse = await client.GetAsync($"{ApiBaseUrl}/users/authenticated-user");
            if (userResponse.IsSuccessStatusCode)
            {
                var user = await userResponse.Content.ReadFromJsonAsync<UserProfileDto>();

                // Pouze u�ivatel� typu Normal mohou nahr�vat soubory
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
        // Pokud se nejedn� o hlasov�n�, manu�ln� o�ist�me ModelState od chyb spojen�ch s hlasov�n�m
        if (!HasVoting)
        {
            // Resetujeme VoteType na None (��dn� hlasov�n�) bez ohledu na to, co p�i�lo z formul��e
            Input.VoteType = VoteType.None;

            // Vy�ist�me hodnotu VotingQuestions
            VotingQuestions = "";

            // Odstran�me v�echny chyby validace spojen� s hlasov�n�m
            foreach (var key in ModelState.Keys.ToList())
            {
                if (key.StartsWith("VotingQuestions") || key.StartsWith("Input.VoteType"))
                {
                    ModelState.Remove(key);
                }
            }
        }

        // Kontrola, jestli po�adavek jde z platn�ho odkazu
        var referer = Request.Headers.Referer.ToString();
        if (!referer.Contains("/discussion/create/"))
        {
            return Forbid("Neopr�vn�n� p��stup");
        }

        // Kontrola valida�n�ho stavu
        if (!ModelState.IsValid)
        {
            // Debug informace - vyp�eme si chyby v ModelState, abychom vid�li, co konkr�tn� selh�v�
            foreach (var state in ModelState)
            {
                if (state.Value.Errors.Any())
                {
                    logger.Log($"Valida�n� chyba pro {state.Key}: {string.Join(", ", state.Value.Errors.Select(e => e.ErrorMessage))}");
                }
            }

            // Znovu na�teme kategorii pro zobrazen�, ale zachov�me vypln�n� data
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

            // Obnov�me do�asn� k�d ze session
            TempDiscussionCode = HttpContext.Session.GetString("TempDiscussionCode") ?? $"temp_{Guid.NewGuid().ToString("N")}";

            // Zji�t�n� typu p�ihl�en�ho u�ivatele a nastaven� opr�vn�n� pro nahr�v�n� soubor�
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

            // Vr�t�me str�nku s chybami validace
            return Page();
        }

        try
        {
            // Z�sk�me do�asn� k�d ze session
            var tempCode = HttpContext.Session.GetString("TempDiscussionCode");
            if (string.IsNullOrEmpty(tempCode))
            {
                // Pokud n�jak�m zp�sobem chyb�, vygenerujeme nov�
                tempCode = $"temp_{Guid.NewGuid().ToString("N")}";
            }

            // Sanitizace vstupn�ho HTML
            Input.Title = sanitizer.Sanitize(Input.Title);
            Input.Content = sanitizer.Sanitize(Input.Content);


            // Nastaven� typu hlasov�n� podle hodnoty v checkboxu a vybran�hotypu
            if (HasVoting)
            {
                // Pokud je hlasov�n� povoleno, zkontrolujeme, zda je typ nastaven na n�co jin�ho ne� None
                // Pokud by typ z�stal None, nastav�me v�choz� hodnotu Visible
                if (Input.VoteType == VoteType.None)
                {
                    Input.VoteType = VoteType.Visible;
                }
                // Jinak ponech�me vybran� typ (z formul��e p�ijde v Input.VoteType)
            }
            else
            {
                // Pokud nen� hlasov�n� povoleno, nastav�me typ na None bez ohledu na to, co p�i�lo z formul��e
                Input.VoteType = VoteType.None;
                // Vy�ist�me p��padn� ot�zky, kter� p�i�ly z formul��e
                VotingQuestions = "";
            }

            var client = _clientFactory.CreateBearerClient(HttpContext);

            // Vytvo�en� diskuze
            var response = await client.PostAsJsonAsync($"{ApiBaseUrl}/discussions", Input);

            if (response.IsSuccessStatusCode && response.Headers.Location != null)
            {
                // Diskuze byla �sp�n� vytvo�ena, obdr��me URL s ID vytvo�en� diskuze
                var locationUrl = response.Headers.Location.ToString();
                var discussionId = int.Parse(locationUrl.Split('/').Last());

                // Z�sk�me informace o vytvo�en� diskuzi
                var discussionResponse = await client.GetAsync($"{ApiBaseUrl}/discussions/{discussionId}/basic-info");
                if (discussionResponse.IsSuccessStatusCode)
                {
                    var discussionInfo = await discussionResponse.Content.ReadFromJsonAsync<BasicDiscussionInfoDto>();
                    if (discussionInfo != null)
                    {
                        // Zavol�me API pro p�ejmenov�n� odkaz� na obr�zky v obsahu diskuze
                        await client.PostAsJsonAsync($"{ApiBaseUrl}/discussions/update-image-paths",
                            new
                            {
                                DiscussionId = discussionId,
                                OldPrefix = tempCode,
                                NewPrefix = discussionInfo.DiscussionCode
                            });

                        // P�ejmenov�n� adres��e s obr�zky
                        await client.PostAsJsonAsync($"{BaseUrl}upload/rename-directory",
                            new
                            {
                                OldCode = tempCode,
                                NewCode = discussionInfo.DiscussionCode
                            });

                        // Vytvo�en� hlasov�n�, pokud je povoleno
                        if (HasVoting && discussionInfo.VoteType != VoteType.None && !string.IsNullOrEmpty(VotingQuestions))
                        {
                            try
                            {
                                // Deserializace ot�zek s ignorov�n�m velikosti p�smen
                                var options = new JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                };
                                var votingQuestionsData = JsonSerializer.Deserialize<List<VotingQuestionDto>>(VotingQuestions, options);

                                if (votingQuestionsData != null && votingQuestionsData.Count > 0)
                                {
                                    // Vytvo�en� hlasov�n� pomoc� API
                                    var createVotingRequest = new CreateOrUpdateVotingDto
                                    {
                                        DiscussionId = discussionId,
                                        VoteType = discussionInfo.VoteType,
                                        Questions = votingQuestionsData
                                    };

                                    var votingResponse = await client.PostAsJsonAsync($"{ApiBaseUrl}/votings", createVotingRequest);

                                    if (!votingResponse.IsSuccessStatusCode)
                                    {
                                        logger.Log($"Nepoda�ilo se vytvo�it hlasov�n� pro diskuzi ID {discussionId}. Stavov� k�d: {votingResponse.StatusCode}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Log($"Chyba p�i vytv��en� hlasov�n� pro diskuzi ID {discussionId}", ex);
                                // Pokra�ujeme i p�i chyb� vytv��en� hlasov�n� (diskuze byla �sp�n� vytvo�ena)
                            }
                        }
                    }
                }

                return RedirectToPage("/Category", new { categoryCode = CategoryCode });
            }

            ModelState.AddModelError("", "Nepoda�ilo se vytvo�it diskuzi.");
        }
        catch (Exception ex)
        {
            logger.Log("Do�lo k chyb� p�i vytv��en� diskuze", ex);
            ErrorMessage = "Omlouv�me se, ale do�lo k chyb� p�i vytv��en� diskuze.";
            return Page();
        }

        // Znovu na�teme kategorii pro zobrazen�
        await OnGetAsync();
        return Page();
    }
}