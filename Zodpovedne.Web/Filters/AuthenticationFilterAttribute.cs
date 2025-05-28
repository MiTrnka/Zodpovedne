using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using System.IdentityModel.Tokens.Jwt;

namespace Zodpovedne.Web.Filters;

/// <summary>
/// Filtr pro ověření, zda je uživatel přihlášen. Pokud token v session není nalezen (není platný), uživatel je přesměrován na přihlašovací stránku
/// Může být použit jako atribut na Razor Pages, kde chceme vyžadovat přihlášení. Díky tomu, že je potomkem PageModelAttribute, je připraven pro použití s Razor Pages.
/// IPageFilter je rozhraní, které umožňuje filtrovat zpracování handlerů stránek. Handler je metoda jako OnGet, OnPost atd.
/// </summary>
public class AuthenticationFilterAttribute : PageModelAttribute, IPageFilter
{
    /// <summary>
    /// Metoda volaná při výběru handleru (metoda jako OnGet, OnPost atd.) stránky.
    /// </summary>
    public void OnPageHandlerSelected(PageHandlerSelectedContext context)
    {
        // Volá se jako první, když je vybrán handler stránky
    }

    /// <summary>
    /// Autentizační filtr pro Razor Pages, který zajišťuje, že pouze přihlášení uživatelé
    /// mohou přistupovat k chráněným stránkám. Filtr kontroluje:
    /// - Existenci JWT tokenu v session storage
    /// - Platnost tokenu (kontrola expirace)
    /// - Integritu tokenu (správnost formátu)
    /// Pokud některá z kontrol selže, uživatel je automaticky přesměrován na přihlašovací stránku.
    /// Filtr se aplikuje na page model třídy pomocí atributu a je vykonán před každým handlerem stránky.
    /// Implementuje IPageFilter rozhraní pro integraci s Razor Pages pipeline.
    /// </summary>
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
        }
        catch
        {
            // Při jakékoliv chybě zpracování tokenu přesměrujeme na login
            context.Result = new RedirectToPageResult("/Account/Login");
            return;
        }
    }

    /// <summary>
    /// Metoda volaná po zpracování handleru stránky.
    /// </summary>
    public void OnPageHandlerExecuted(PageHandlerExecutedContext context)
    {
        // Zde by mohl být kód, který se má provést po zpracování požadavku, po tom, co se na Razor Page zavolal handler OnGet, OnPost atd.
    }
}