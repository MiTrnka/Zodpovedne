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

using Zodpovedne.GraphQL;

namespace Zodpovedne.GraphQL
{
    public class Program
    {
        // Standardní 'Main' metoda, která se spustí jako první pøi startu aplikace.
        public static void Main(string[] args)
        {
            // Vytvoøíme a spustíme hostitele aplikace.
            CreateHostBuilder(args).Build().Run();
        }

        // Vytvoøí a nakonfiguruje "hostitele" (prostøedí, ve kterém aplikace bìží).
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    // Øíkáme hostiteli, aby pro veškerou konfiguraci služeb
                    // a HTTP pipeline použil naši tøídu 'Startup'.
                    webBuilder.UseStartup<Startup>();
                });
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