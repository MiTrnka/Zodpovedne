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

    // P�id�me novou vlastnost pro do�asn� k�d diskuze
    public string TempDiscussionCode { get; set; } = "";

    // P�id�me vlastnost, kter� ur�uje, zda u�ivatel m��e nahr�vat obr�zky
    public bool CanUploadFiles { get; private set; } = false;

    // P�id�me vlastnost pro checkbox "Diskuze jen pro kamar�dy"
    [BindProperty]
    public bool IsPrivate { get; set; } = false;

    public async Task<IActionResult> OnGetAsync()
    {
        // Generov�n� do�asn�ho k�du pro diskuzi
        TempDiscussionCode = $"temp_{Guid.NewGuid().ToString("N")}";

        // Z�sk�n� detail� kategorie
        var client = _clientFactory.CreateBearerClient(HttpContext);
        var response = await client.GetAsync($"{ApiBaseUrl}/Categories/{CategoryCode}");

        if (!response.IsSuccessStatusCode)
        {
            _logger.Log($"Kategorie {CategoryCode} nenalezena");
            ErrorMessage = "Omlouv�me se, ale diskuzi nejde moment�ln� zalo�it.";
            return Page();
        }

        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        if (category == null)
        {
            _logger.Log($"Kategorie {CategoryCode} nelze na��st");
            ErrorMessage = "Omlouv�me se, ale diskuzi nejde moment�ln� zalo�it.";
            return Page();
        }

        CategoryName = category.Name;
        Input.CategoryId = category.Id;

        // Ulo��me do�asn� k�d do session, aby byl k dispozici i po odesl�n� formul��e
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
        // Kontrola valida�n�ho stavu
        if (!ModelState.IsValid)
        {
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
            Input.Title = _sanitizer.Sanitize(Input.Title);
            Input.Content = _sanitizer.Sanitize(Input.Content);

            // Nastav�me typ diskuze na Private, pokud byl za�krtnut checkbox
            Input.Type = IsPrivate ? DiscussionType.Private : DiscussionType.Normal;

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
                    }
                }

                return RedirectToPage("/Category", new { categoryCode = CategoryCode });
            }

            ModelState.AddModelError("", "Nepoda�ilo se vytvo�it diskuzi.");
        }
        catch (Exception ex)
        {
            _logger.Log("Do�lo k chyb� p�i vytv��en� diskuze", ex);
            ErrorMessage = "Omlouv�me se, ale do�lo k chyb� p�i vytv��en� diskuze.";
            return Page();
        }

        // Znovu na�teme kategorii pro zobrazen�
        await OnGetAsync();
        return Page();
    }
}