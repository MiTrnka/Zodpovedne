// Zodpovedne.GraphQL/Program.cs

/*
 * Tento soubor je vstupn�m bodem va�� aplikace. Jeho jedin�m �kolem je nastartovat a spustit webov� server, kter� bude naslouchat po�adavk�m. Konkr�tn� nastaven� serveru a slu�eb deleguje na t��du Startup
 * =================================================================================
 * NE� ZA�NETE, UJIST�TE SE, �E M�TE NAINSTALOVAN� TYTO NUGET BAL��KY:
 * =================================================================================
 * * 1. HotChocolate.AspNetCore
 * - Z�kladn� bal��ek pro hostov�n� GraphQL serveru v prost�ed� ASP.NET Core.
 * * 2. HotChocolate.Data.EntityFramework
 * - Propojuje Hot Chocolate s Entity Framework Core. Umo��uje kouzla jako
 * automatick� filtrov�n� a �azen� p��mo v GraphQL dotazech. Poskytuje
 * tak� atribut [UseDbContext].
 * * 3. Npgsql.EntityFrameworkCore.PostgreSQL
 * - Ovlada� (provider), kter� umo��uje Entity Framework Core komunikovat
 * s va�� PostgreSQL datab�z�.
 * * =================================================================================
*/

using Zodpovedne.GraphQL;

namespace Zodpovedne.GraphQL
{
    public class Program
    {
        // Standardn� 'Main' metoda, kter� se spust� jako prvn� p�i startu aplikace.
        public static void Main(string[] args)
        {
            // Vytvo��me a spust�me hostitele aplikace.
            CreateHostBuilder(args).Build().Run();
        }

        // Vytvo�� a nakonfiguruje "hostitele" (prost�ed�, ve kter�m aplikace b��).
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    // ��k�me hostiteli, aby pro ve�kerou konfiguraci slu�eb
                    // a HTTP pipeline pou�il na�i t��du 'Startup'.
                    webBuilder.UseStartup<Startup>();
                });
    }
}

/*
Zaps�n� zpr�vy:
mutation CreateMessage
{
    addFreeMessage(input: { nickname: "Michal", text: "Prvn� zpr�va z testovac�ho prost�ed�!"}) {
        id
        nickname
      text
    createdUtc
    }
}

P�e�t�te v�echna data
query NactiVsechnyZpravy
{
  freeMessages {
    id
    nickname
    text
    createdUtc
  }
}

Filtrov�n�: Na�ti zpr�vy pouze od "Michala"
query NactiMichalovyZpravy
{
  freeMessages(where: { nickname: { eq: "Michal" } }) {
    id
    text
  }
}

�azen�: Se�a� zpr�vy od nejnov�j�� po nejstar��
query SeradZpravy
{
  freeMessages(order: { createdUtc: DESC }) {
    id
    nickname
    createdUtc
  }
}

*/