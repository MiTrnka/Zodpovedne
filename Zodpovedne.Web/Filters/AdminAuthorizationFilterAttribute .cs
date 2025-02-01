//  NuGet System.IdentityModel.Tokens.Jwt
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;

namespace Zodpovedne.Web.Filters;

/// <summary>
/// Filtr pro ověření, zda je uživatel přihlášen a má roli Admin.
/// Kontroluje existenci JWT tokenu, jeho platnost a přítomnost role Admin.
/// Pokud některá z podmínek není splněna, je uživatel přesměrován na přihlašovací stránku.
/// </summary>
public class AdminAuthorizationFilterAttribute : PageModelAttribute, IPageFilter
{
    public void OnPageHandlerExecuted(PageHandlerExecutedContext context)
    {
    }

    public void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
        // Získáme JWT token ze session
        var token = context.HttpContext.Session.GetString("JWTToken");

        // Pokud token neexistuje
        if (string.IsNullOrEmpty(token))
        {
            // Přesměrujeme na přihlašovací stránku
            context.Result = new RedirectToPageResult("/Account/Login");
            return;
        }
        try
        {
            // Dekódujeme JWT token
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

            if (jsonToken == null)
            {
                context.Result = new RedirectToPageResult("/Account/Login");
                return;
            }

            // Kontrola expirace tokenu
            if (jsonToken.ValidTo < DateTime.UtcNow)
            {
                // Token vypršel
                context.HttpContext.Session.Remove("JWTToken");
                context.Result = new RedirectToPageResult("/Account/Login");
                return;
            }

            // Kontrola role Admin
            var roleClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
            if (roleClaim == null || roleClaim.Value != "Admin")
            {
                // Uživatel nemá roli Admin
                context.Result = new RedirectToPageResult("/Account/Login");
                return;
            }
        }
        catch
        {
            // Při jakékoliv chybě zpracování tokenu přesměrujeme na login
            context.Result = new RedirectToPageResult("/Account/Login");
            return;
        }
    }

    public void OnPageHandlerSelected(PageHandlerSelectedContext context)
    {
    }
}