using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Web.Filters;
using Zodpovedne.Web.Models.Base;
using Zodpovedne.Logging;

namespace Zodpovedne.Web.Pages;

/// <summary>
/// Model pro str�nku zobrazuj�c� seznam kategori�
/// </summary>
public class CategoriesModel : BasePageModel
{
    public CategoriesModel(IHttpClientFactory clientFactory, IConfiguration configuration, FileLogger logger) : base(clientFactory, configuration, logger)
    {
    }

    public List<CategoryDto> Categories { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var client = _clientFactory.CreateClient();
        var response = await client.GetAsync($"{ApiBaseUrl}/categories");

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Omlouv�me se, ale seznam kategori� se nepoda�ilo na��st.";
            _logger.Log("Nepoda�ilo se na��st seznam v�ech kategori�");
            return Page();
        }
        Categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>() ?? new();
        if (Categories == null)
        {
            ErrorMessage = "Omlouv�me se, ale seznam kategori� se nepoda�ilo na��st.";
            _logger.Log("Nepoda�ilo se na��st seznam v�ech kategori� z response");
            return Page();
        }

        return Page();
    }
}