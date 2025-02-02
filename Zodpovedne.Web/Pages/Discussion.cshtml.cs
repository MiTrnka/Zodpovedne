using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Zodpovedne.Web.Pages;

public class DiscussionModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string CategoryCode { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string DiscussionCode { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        return Page();
    }
}
