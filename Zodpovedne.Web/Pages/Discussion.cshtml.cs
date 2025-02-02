using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Security.Claims;
using Zodpovedne.Contracts.DTO;

namespace Zodpovedne.Web.Pages;

public class DiscussionModel : PageModel
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;

    public DiscussionModel(IHttpClientFactory clientFactory, IConfiguration configuration)
    {
        _clientFactory = clientFactory;
        _configuration = configuration;
    }

    [BindProperty(SupportsGet = true)]
    public string CategoryCode { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string DiscussionCode { get; set; } = "";

    public DiscussionDetailDto? Discussion { get; set; }
    public bool CanEditDiscussion { get; set; }
    public bool IsAdmin { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = _clientFactory.CreateClient();
        var token = HttpContext.Session.GetString("JWTToken");

        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            IsAdmin = User.IsInRole("Admin");
        }

        // Získání detailu diskuze
        var response = await client.GetAsync($"{_configuration["ApiBaseUrl"]}/api/discussions/byCode/{DiscussionCode}");
        if (!response.IsSuccessStatusCode)
            return NotFound();

        Discussion = await response.Content.ReadFromJsonAsync<DiscussionDetailDto>();
        if (Discussion == null)
            return NotFound();

        // Kontrola oprávnìní k editaci
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        CanEditDiscussion = IsAdmin || (!string.IsNullOrEmpty(userId) && Discussion.AuthorId == userId);

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteDiscussionAsync(int id)
    {
        if (!IsAdmin && !CanEditDiscussion)
            return Forbid();

        var client = _clientFactory.CreateClient();
        var token = HttpContext.Session.GetString("JWTToken");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"{_configuration["ApiBaseUrl"]}/api/discussions/{id}");
        if (!response.IsSuccessStatusCode)
            return NotFound();

        return RedirectToPage("/Category", new { categoryCode = CategoryCode });
    }

    public async Task<IActionResult> OnPostDeleteCommentAsync(int commentId)
    {
        if (!IsAdmin)
            return Forbid();

        var client = _clientFactory.CreateClient();
        var token = HttpContext.Session.GetString("JWTToken");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"{_configuration["ApiBaseUrl"]}/api/discussions/{Discussion!.Id}/comments/{commentId}");
        if (!response.IsSuccessStatusCode)
            return NotFound();

        return RedirectToPage();
    }
}