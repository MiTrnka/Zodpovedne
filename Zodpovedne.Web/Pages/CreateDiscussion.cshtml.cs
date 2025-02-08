using Microsoft.AspNetCore.Mvc;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Extensions;
using Zodpovedne.Web.Filters;
using Zodpovedne.Web.Models.Base;

namespace Zodpovedne.Web.Pages;

[AuthenticationFilter]
public class CreateDiscussionModel : BasePageModel
{

    public CreateDiscussionModel(IHttpClientFactory clientFactory, IConfiguration configuration)
        : base(clientFactory, configuration)
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
        var response = await client.GetAsync($"{ApiBaseUrl}/api/categories/{CategoryCode}");

        if (!response.IsSuccessStatusCode)
            return NotFound();

        var category = await response.Content.ReadFromJsonAsync<CategoryListDto>();
        if (category == null)
            return NotFound();

        CategoryName = category.Name;
        Input.CategoryId = category.Id;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        try
        {
            // Sanitizace vstupního HTML
            Input.Title = _sanitizer.Sanitize(Input.Title);
            Input.Content = _sanitizer.Sanitize(Input.Content);

            var client = _clientFactory.CreateBearerClient(HttpContext);
            var response = await client.PostAsJsonAsync($"{ApiBaseUrl}/api/discussions", Input);

            if (response.IsSuccessStatusCode)
                return RedirectToPage("/Category", new { categoryCode = CategoryCode });

            ModelState.AddModelError("", "Nepodaøilo se vytvoøit diskuzi.");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Došlo k chybì pøi vytváøení diskuze: {ex.Message}");
        }

        // Znovu naèteme kategorii pro zobrazení
        await OnGetAsync();
        return Page();
    }
}