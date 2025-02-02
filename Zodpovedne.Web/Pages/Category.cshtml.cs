using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Filters;

namespace Zodpovedne.Web.Pages;

public class CategoryModel : PageModel
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;

    public CategoryModel(IHttpClientFactory clientFactory, IConfiguration configuration)
    {
        _clientFactory = clientFactory;
        _configuration = configuration;
    }

    [BindProperty(SupportsGet = true)]
    public string CategoryCode { get; set; } = "";

    public string CategoryName { get; set; } = "";
    public string CategoryDescription { get; set; } = "";
    public List<DiscussionListDto> Discussions { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        // Z�sk�me kategorii podle Code
        var client = _clientFactory.CreateClient();

        // Z�sk�n� seznamu diskuz� pro danou kategorii
        var response = await client.GetAsync($"{_configuration["ApiBaseUrl"]}/api/discussions?categoryCode={CategoryCode}");
        if (!response.IsSuccessStatusCode)
            return NotFound();

        Discussions = await response.Content.ReadFromJsonAsync<List<DiscussionListDto>>() ?? new();

        // TODO: Z�skat n�zev a popis kategorie z API a� bude endpoint hotov�
        // Prozat�m mockujeme podle code
        CategoryName = CategoryCode switch
        {
            "tehotenstvi" => "T�hotenstv�",
            "porod" => "Porod",
            "kojeni" => "Kojen�",
            "vychova" => "V�chova",
            "skolky-a-skoly" => "�kolky a �koly",
            _ => CategoryCode
        };

        return Page();
    }
}