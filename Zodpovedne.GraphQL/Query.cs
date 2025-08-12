using Microsoft.EntityFrameworkCore;
using Zodpovedne.Data.Data;
using Zodpovedne.Data.Models;

namespace Zodpovedne.GraphQL;

// Řešeno pomocí rozepsaného zápisu, jak získám DbContext pro GraphQL dotazy z továrny.
public class Query
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    // Necháme si vstříknout továrnu od v Program.cs zaregistrované služby
    public Query(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    [UseProjection] // Umožní klientovi optimalizovat dotazy a získat jen potřebná data
    [UseFiltering] // Umožní klientovi filtrovat výsledky podle zadaných podmínek
    [UseSorting] // Umožní klientovi seřadit výsledky podle zadaných sloupců
    public IQueryable<FreeMessage> GetFreeMessages()
    {
        // Explicitně vytvoříme DbContext z továrny
        // DŮLEŽITÉ: DbContext se zde vrací jako IQueryable, Hot Chocolate se postará
        // o jeho "disposal" (uklizení) po dokončení dotazu.
        // Pro čtení (IQueryable) není `using` blok nutný, pro zápis (mutace) ano.
        ApplicationDbContext context = _contextFactory.CreateDbContext();
        return context.FreeMessages;
    }
}