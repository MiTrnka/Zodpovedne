using Microsoft.AspNetCore.Mvc;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Filters;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;

namespace Zodpovedne.Web.Pages;

/// <summary>
/// Model pro stránku vytváøení nové diskuze
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
        // Získání detailù kategorie
        var client = _clientFactory.CreateBearerClient(HttpContext);
        var response = await client.GetAsync($"{ApiBaseUrl}/categories/{CategoryCode}");

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
                // Zobrazíme první chybovou hlášku, typicky to bude hláška ohlednì pøekroèení maximální délky
                ErrorMessage = errors.First();
            }
            else
            {
                ErrorMessage = "Omlouváme se, ale diskuzi se nepodaøilo založit.";
            }

            // Znovu naèteme kategorii pro zobrazení
            await OnGetAsync();
            return Page();
        }

        try
        {
            // Sanitizace vstupního HTML
            Input.Title = _sanitizer.Sanitize(Input.Title);
            Input.Content = _sanitizer.Sanitize(Input.Content);

            var client = _clientFactory.CreateBearerClient(HttpContext);
            var response = await client.PostAsJsonAsync($"{ApiBaseUrl}/discussions", Input);

            if (response.IsSuccessStatusCode)
                return RedirectToPage("/Category", new { categoryCode = CategoryCode });

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