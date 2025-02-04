// CreateDiscussion.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Filters;

namespace Zodpovedne.Web.Pages;

[AuthenticationFilter]
public class CreateDiscussionModel : PageModel
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;

    public CreateDiscussionModel(IHttpClientFactory clientFactory, IConfiguration configuration)
    {
        _clientFactory = clientFactory;
        _configuration = configuration;
    }

    [BindProperty]
    public CreateDiscussionDto Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string CategoryCode { get; set; } = "";

    public string CategoryName { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        var client = _clientFactory.CreateClient();
        var response = await client.GetAsync($"{_configuration["ApiBaseUrl"]}/api/categories/{CategoryCode}");

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

        var client = _clientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", HttpContext.Session.GetString("JWTToken"));

        var response = await client.PostAsJsonAsync($"{_configuration["ApiBaseUrl"]}/api/discussions", Input);

        if (response.IsSuccessStatusCode)
            return RedirectToPage("/Category", new { categoryCode = CategoryCode });

        ModelState.AddModelError("", "Nepodaøilo se vytvoøit diskuzi.");
        return Page();
    }
}