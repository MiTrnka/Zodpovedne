using Microsoft.AspNetCore.Mvc;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Filters;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;
using Ganss.Xss;

namespace Zodpovedne.Web.Pages;

/// <summary>
/// Model pro str�nku vytv��en� nov� diskuze
/// </summary>
[AuthenticationFilter]
public class CreateDiscussionModel : BasePageModel
{

    public CreateDiscussionModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger, IHtmlSanitizer sanitizer) : base(clientFactory, configuration, logger, sanitizer)
    {
    }

    [BindProperty]
    public CreateDiscussionDto Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string CategoryCode { get; set; } = "";

    public string CategoryName { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
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
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Kontrola valida�n�ho stavu - toto je kl��ov�
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
                    // Zachov�v�me ID kategorie, kter� ji� bylo nastaveno v Input objektu
                }
            }

            // Vr�t�me str�nku s chybami validace
            return Page();
        }

        try
        {
            // Sanitizace vstupn�ho HTML
            Input.Title = _sanitizer.Sanitize(Input.Title);
            Input.Content = _sanitizer.Sanitize(Input.Content);

            var client = _clientFactory.CreateBearerClient(HttpContext);
            var response = await client.PostAsJsonAsync($"{ApiBaseUrl}/discussions", Input);

            if (response.IsSuccessStatusCode)
                return RedirectToPage("/Category", new { categoryCode = CategoryCode });

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