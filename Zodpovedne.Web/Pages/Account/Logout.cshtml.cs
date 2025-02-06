using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Zodpovedne.Web.Pages.Account;

public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        // Smazání JWT ze session
        HttpContext.Session.Clear();

        // Smazání autentizaèní cookie
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToPage("/Index");
    }
}
