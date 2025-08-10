// Zodpovedne.GraphQL/Program.cs

/*
 * Tento soubor je vstupním bodem vaší aplikace. Jeho jediným úkolem je nastartovat a spustit webový server, který bude naslouchat požadavkùm. Konkrétní nastavení serveru a služeb deleguje na tøídu Startup
 * =================================================================================
 * NEŽ ZAÈNETE, UJISTÌTE SE, ŽE MÁTE NAINSTALOVANÉ TYTO NUGET BALÍÈKY:
 * =================================================================================
 * * 1. HotChocolate.AspNetCore
 * - Základní balíèek pro hostování GraphQL serveru v prostøedí ASP.NET Core.
 * * 2. HotChocolate.Data.EntityFramework
 * - Propojuje Hot Chocolate s Entity Framework Core. Umožòuje kouzla jako
 * automatické filtrování a øazení pøímo v GraphQL dotazech. Poskytuje
 * také atribut [UseDbContext].
 * * 3. Npgsql.EntityFrameworkCore.PostgreSQL
 * - Ovladaè (provider), který umožòuje Entity Framework Core komunikovat
 * s vaší PostgreSQL databází.
 * * =================================================================================
*/

// Definice jmenného prostoru, do kterého tato hlavní tøída aplikace patøí.
namespace Zodpovedne.GraphQL;

// Import potøebných balíèkù a knihoven pro bìh aplikace.
using Microsoft.EntityFrameworkCore;
using Zodpovedne.Data.Data;

/// <summary>
/// Hlavní tøída aplikace, která obsahuje vstupní bod 'Main'.
/// </summary>
public class Program
{
    /// <summary>
    /// Vstupní bod aplikace. Kód v této metodì se spustí jako úplnì první.
    /// </summary>
    public static void Main(string[] args)
    {
        // === 1. Fáze: Vytvoøení a konfigurace "Builderu" ===
        // Builder slouží jako "stavebnice" pro naši webovou aplikaci. Postupnì do nìj
        // pøidáváme a konfigurujeme všechny potøebné souèásti.
        var builder = WebApplication.CreateBuilder(args);

        // Zkontrolujeme, zda aplikace bìží v produkèním prostøedí.
        // Toto prostøedí se nastavuje na serveru pomocí promìnné ASPNETCORE_ENVIRONMENT=Production.
        if (builder.Environment.IsProduction())
        {
            // Pokud ano, nakonfigurujeme Kestrel, aby naslouchal na portu 5002.
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(5002);
            });
        }

        // --- Konfigurace služeb (døíve metoda 'ConfigureServices' v Startup.cs) ---
        // V této sekci registrujeme všechny "služby", které bude naše aplikace potøebovat.

        // Naèteme connection string z konfiguraèního souboru (appsettings.json).
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

        // Zaregistrujeme "továrnu" na výrobu DbContextu. Továrna zajistí,
        // že každý paralelní úkol v rámci jednoho GraphQL dotazu dostane vlastní,
        // izolovanou instanci DbContextu, což zabraòuje konfliktùm.
        builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Zaregistrujeme a nakonfigurujeme samotný GraphQL server (Hot Chocolate).
        builder.Services
            .AddGraphQLServer()
            // Definuje tøídu, která obsahuje všechny operace pro ètení dat (dotazy).
            .AddQueryType<Query>()
            // Definuje tøídu, která obsahuje všechny operace pro zápis dat (mutace).
            .AddMutationType<Mutation>()
            // Zapne podporu pro "projekce". Umožní klientovi specifikovat v dotazu,
            // která pole chce vrátit, a EF Core se postará o efektivní SQL dotaz.
            .AddProjections()
            // Zapne podporu pro filtrování, která se pøeloží na SQL WHERE.
            .AddFiltering()
            // Zapne podporu pro øazení, která se pøeloží na SQL ORDER BY.
            .AddSorting();


        // === 2. Fáze: Sestavení aplikace ===
        // Z nakonfigurovaného builderu nyní vytvoøíme finální instanci aplikace.
        var app = builder.Build();


        // --- Konfigurace HTTP Pipeline (døíve metoda 'Configure' v Startup.cs) ---
        // Zde definujeme, jak bude server postupnì zpracovávat každý pøíchozí HTTP požadavek.

        // Pokud aplikace bìží ve vývojovém prostøedí, použije se stránka s detailními chybami.
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Zapne "routing", což je mechanismus, který rozhoduje, která èást kódu
        // se má spustit na základì URL adresy požadavku.
        app.UseRouting();

        // Definuje koncový bod (endpoint) naší aplikace na adrese '/graphql'.
        // Všechny GraphQL požadavky budou smìøovat sem.
        // Tento endpoint také hostuje testovací prostøedí Banana Cake Pop.
        app.MapGraphQL();


        // === 4. Fáze: Spuštìní aplikace ===
        // Tento pøíkaz spustí aplikaci a ta zaène naslouchat na pøíchozí požadavky.
        app.Run();
    }
}

/*
Zapsání zprávy:
mutation CreateMessage
{
addFreeMessage(input: { nickname: "Michal", text: "První zpráva z testovacího prostøedí!"}) {
id
nickname
text
createdUtc
}
}

Pøeètìte všechna data
query NactiVsechnyZpravy
{
freeMessages {
id
nickname
text
createdUtc
}
}

Filtrování: Naèti zprávy pouze od "Michala"
query NactiMichalovyZpravy
{
freeMessages(where: { nickname: { eq: "Michal" } }) {
id
text
}
}

Øazení: Seøaï zprávy od nejnovìjší po nejstarší
query SeradZpravy
{
freeMessages(order: { createdUtc: DESC }) {
id
nickname
createdUtc
}
}

*/