using Microsoft.AspNetCore.Mvc;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Filters;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;

namespace Zodpovedne.Web.Pages;

/// <summary>
/// Model pro str�nku vytv��en� nov� diskuze
/// </summary>
[AuthenticationFilter]
public class CreateDiscussionModel : BasePageModel
{

    public CreateDiscussionModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger) : base(clientFactory, configuration, logger)
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
        var response = await client.GetAsync($"{ApiBaseUrl}/categories/{CategoryCode}");

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
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();

            if (errors.Any())
            {
                // Zobraz�me prvn� chybovou hl�ku, typicky to bude hl�ka ohledn� p�ekro�en� maxim�ln� d�lky
                ErrorMessage = errors.First();
            }
            else
            {
                ErrorMessage = "Omlouv�me se, ale diskuzi se nepoda�ilo zalo�it.";
            }

            // Znovu na�teme kategorii pro zobrazen�
            await OnGetAsync();
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