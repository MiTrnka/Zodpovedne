using Microsoft.EntityFrameworkCore;
using Zodpovedne.Data.Data;
using Zodpovedne.Data.Models;


namespace Zodpovedne.GraphQL;

/// <summary>
/// Třída Mutation seskupuje všechny operace, které mění data v aplikaci (vytváření, úpravy, mazání).
/// </summary>
public class Mutation
{
    // Deklarace soukromého pole pro uložení továrny na DbContext.
    // Klíčové slovo 'readonly' zajišťuje, že továrnu lze nastavit pouze jednou, a to v konstruktoru.
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    /// <summary>
    /// Konstruktor třídy, který se volá při jejím vytváření.
    /// Systém Dependency Injection sem automaticky "vstříkne" dříve registrovanou továrnu.
    /// </summary>
    /// <param name="contextFactory">Továrna pro vytváření instancí ApplicationDbContext.</param>
    public Mutation(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        // Uložíme si předanou továrnu do soukromého pole pro pozdější použití v metodách této třídy.
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// 'record' je moderní, neměnný typ pro přenos dat.
    /// Definuje strukturu vstupních dat, která klient musí poslat pro vytvoření nové zprávy.
    /// </summary>
    public record AddFreeMessageInput(string Nickname, string Text);

    /// <summary>
    /// Asynchronní metoda pro přidání nové zprávy do databáze.
    /// V GraphQL schématu bude dostupná jako pole 'addFreeMessage'.
    /// </summary>
    /// <param name="input">Vstupní objekt obsahující data pro novou zprávu.</param>
    /// <returns>Kompletní objekt právě vytvořené zprávy.</returns>
    public async Task<FreeMessage> AddFreeMessageAsync(AddFreeMessageInput input)
    {
        // Blok 'await using' vytvoří novou instanci DbContext a zaručí, že se po dokončení
        // operací bezpečně a automaticky "uklidí" (zavolá se metoda DisposeAsync).
        // To je naprosto klíčové pro správu prostředků a zabránění únikům paměti.
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Vytvoříme novou instanci C# entity 'FreeMessage' z dat, která poslal klient.
        var newMessage = new FreeMessage
        {
            // Převezmeme přezdívku ze vstupních dat.
            Nickname = input.Nickname,
            // Převezmeme text zprávy ze vstupních dat.
            Text = input.Text,
            // Nastavíme čas vytvoření na aktuální světový čas (UTC) přímo na serveru,
            // což je spolehlivější než spoléhat na čas nastavený u klienta.
            CreatedUtc = DateTime.UtcNow
        };

        // Přidáme nově vytvořený objekt 'newMessage' do kolekce FreeMessages.
        // Entity Framework Core nyní začne tuto entitu sledovat ve stavu "přidaná".
        context.FreeMessages.Add(newMessage);

        // Asynchronně uložíme všechny sledované změny do databáze.
        // V tomto okamžiku EF Core vygeneruje a spustí patřičný SQL příkaz (zde INSERT).
        await context.SaveChangesAsync();

        // Vrátíme klientovi kompletní objekt nové zprávy. To je dobrá praxe, protože
        // objekt může obsahovat serverem vygenerovaná data (jako je ID nebo čas vytvoření).
        return newMessage;
    }
}




// Níže je využití hotchocolate atributů, pro použítí DBContextu z Factory.
/*
using HotChocolate.Data;
using HotChocolate.Types;
using Zodpovedne.Data;
using Zodpovedne.Data.Data;
using Zodpovedne.Data.Models;

namespace Zodpovedne.GraphQL;

public class Mutation
{
    // 'record' je moderní, zjednodušená forma třídy, ideální pro přenos dat.
    // Použití specializovaného vstupního typu (Input Type) je best practice:
    // 1. Zvyšuje bezpečnost - klient nemůže poslat víc dat, než povolíme.
    // 2. Zlepšuje čitelnost schématu - je jasné, co metoda očekává.
    public record AddFreeMessageInput(string Nickname, string Text);

    // Atribut [UseDbContext] zde funguje stejně jako v Query - stará se o DbContext.
    [UseDbContext(typeof(ApplicationDbContext))]

    // Asynchronní metoda pro přidání nové zprávy. Bude v GraphQL schématu
    // dostupná jako pole 'addFreeMessage'.
    public async Task<FreeMessage> AddFreeMessageAsync(
        // Vstupní argumenty metody, které klient posílá v dotazu.
        AddFreeMessageInput input,
        [Service] ApplicationDbContext context)
    {
        // Vytvoříme novou instanci naší C# entity.
        var newMessage = new FreeMessage
        {
            Nickname = input.Nickname,
            Text = input.Text,
            CreatedUtc = DateTime.UtcNow // Čas je nejlepší nastavovat na serveru.
        };

        // Řekneme Entity Frameworku, aby začal sledovat tuto novou entitu
        // a připravil si ji pro vložení do databáze.
        context.FreeMessages.Add(newMessage);

        // Asynchronně uložíme všechny sledované změny (v našem případě jednu novou
        // zprávu) do databáze. Zde se provede reálný SQL INSERT.
        await context.SaveChangesAsync();

        // Podle GraphQL konvence je dobré vrátit data, která byla právě změněna.
        return newMessage;
    }
}
*/