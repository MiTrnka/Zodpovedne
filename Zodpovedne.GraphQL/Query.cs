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
    public async Task<IEnumerable<FreeMessage>> GetFreeMessages()
    {
        // Použijeme stejný bezpečný vzor jako v mutacích.
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Zde dotaz rovnou vykonáme a načteme všechny zprávy do paměti.
        // DbContext se hned poté bezpečně uklidí.
        return await context.FreeMessages.ToListAsync();
    }
    public string Ping() => "Pong";
}