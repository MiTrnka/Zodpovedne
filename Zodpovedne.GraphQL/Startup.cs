// Zodpovedne.GraphQL/Startup.cs
// Tato třída je mozkem konfigurace vaší aplikace. Má dvě hlavní metody: ConfigureServices pro nastavení služeb 
// (jako je připojení k DB) a Configure pro nastavení toho, jak aplikace zpracovává příchozí HTTP požadavky.

using Microsoft.EntityFrameworkCore;
using Zodpovedne.Data;
using Zodpovedne.Data.Data;

namespace Zodpovedne.GraphQL
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        //
        // Metoda pro konfiguraci služeb (tzv. Dependency Injection kontejner).
        // Cokoliv zde zaregistrujeme, můžeme si později "vyžádat" kdekoliv v aplikaci.
        //
        public void ConfigureServices(IServiceCollection services)
        {
            // Načteme connection string z konfiguračního souboru (appsettings.json).
            var connectionString = Configuration.GetConnectionString("DefaultConnection");

            // Zaregistrujeme "továrnu" na výrobu DbContextu.
            // Proč továrnu (Factory) a ne jen DbContext? Protože GraphQL může zpracovávat
            // více částí jednoho dotazu paralelně. Továrna zajistí, že každý takový
            // úkol dostane vlastní, izolovanou instanci DbContextu, což zabraňuje
            // konfliktům a chybám při práci ve více vláknech.
            services.AddDbContextFactory<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Zaregistrujeme a nakonfigurujeme GraphQL server.
            services
                .AddGraphQLServer()
                // Říkáme serveru, že všechny operace pro čtení jsou definovány ve třídě 'Query'.
                .AddQueryType<Query>()
                // Říkáme serveru, že všechny operace pro zápis jsou definovány ve třídě 'Mutation'.
                .AddMutationType<Mutation>()
                // Zapne podporu pro "projekce". Umožní klientovi specifikovat v dotazu,
                // která pole chce vrátit, a EF Core se postará o efektivní SQL dotaz (SELECT Id, Text...).
                .AddProjections()
                // Zapne podporu pro filtrování. Umožní klientovi posílat v dotazu
                // podmínky (where: { nickname: { eq: "Michal" } }), které se přeloží na SQL WHERE.
                .AddFiltering()
                // Zapne podporu pro řazení. Umožní klientovi posílat v dotazu
                // pravidla pro řazení (order: { createdUtc: DESC }), která se přeloží na SQL ORDER BY.
                .AddSorting();
        }

        //
        // Metoda pro konfiguraci HTTP request pipeline.
        // Definuje, jak bude server reagovat na příchozí požadavky.
        //
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Zapne "routing", což je mechanismus, který rozhoduje, která část kódu
            // se má spustit na základě URL adresy požadavku.
            app.UseRouting();

            // Definuje koncové body (endpoints) naší aplikace.
            app.UseEndpoints(endpoints =>
            {
                // Vytvoří jeden koncový bod na adrese '/graphql'.
                // Všechny GraphQL požadavky budou směřovat sem.
                // Tento endpoint také hostuje testovací prostředí Banana Cake Pop.
                endpoints.MapGraphQL();
            });
        }
    }
}