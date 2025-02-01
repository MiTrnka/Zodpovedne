using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;

namespace Zodpovedne.Web.Filters;

/// <summary>
/// Filtr pro ověření, zda je uživatel přihlášen.
/// Používá se jako atribut na Razor Pages, kde chceme vyžadovat přihlášení.
/// Pokud není uživatel přihlášen (nemá platný JWT token v session), je přesměrován na přihlašovací stránku.
/// Příklad použití: [AuthenticationFilter] nad třídou PageModel
/// </summary>
public class AuthenticationFilterAttribute : PageModelAttribute, IPageFilter
{
    /// <summary>
    /// Metoda volaná po zpracování handleru stránky.
    /// V našem případě nepotřebujeme žádnou funkcionalitu po zpracování, proto je prázdná.
    /// </summary>
    public void OnPageHandlerExecuted(PageHandlerExecutedContext context)
    {
        // Zde by mohl být kód, který se má provést po zpracování požadavku
        // Například logování, čištění zdrojů atd.
    }

    /// <summary>
    /// Hlavní metoda pro ověření přihlášení.
    /// Je volána před zpracováním handleru stránky (před OnGet, OnPost atd.).
    /// Kontroluje přítomnost JWT tokenu v session a případně přesměruje na přihlašovací stránku.
    /// </summary>
    public void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
        // Získáme JWT token ze session
        var token = context.HttpContext.Session.GetString("JWTToken");

        // Pokud token není nalezen (uživatel není přihlášen)
        if (string.IsNullOrEmpty(token))
        {
            // Přesměrujeme na přihlašovací stránku
            context.Result = new RedirectToPageResult("/Account/Login");
            return;
        }

        // Pokud je token nalezen, pokračuje se normálně ve zpracování požadavku
    }

    /// <summary>
    /// Metoda volaná při výběru handleru stránky.
    /// V našem případě nepotřebujeme žádnou funkcionalitu při výběru handleru, proto je prázdná.
    /// Handler je metoda jako OnGet, OnPost atd.
    /// </summary>
    public void OnPageHandlerSelected(PageHandlerSelectedContext context)
    {
        // Zde by mohl být kód, který se má provést při výběru handleru
        // Například logování, příprava dat atd.
    }
}