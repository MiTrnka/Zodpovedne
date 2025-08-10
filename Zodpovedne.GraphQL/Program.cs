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

// Definice jmenn�ho prostoru, do kter�ho tato hlavn� t��da aplikace pat��.
namespace Zodpovedne.GraphQL;

// Import pot�ebn�ch bal��k� a knihoven pro b�h aplikace.
using Microsoft.EntityFrameworkCore;
using Zodpovedne.Data.Data;

/// <summary>
/// Hlavn� t��da aplikace, kter� obsahuje vstupn� bod 'Main'.
/// </summary>
public class Program
{
    /// <summary>
    /// Vstupn� bod aplikace. K�d v t�to metod� se spust� jako �pln� prvn�.
    /// </summary>
    public static void Main(string[] args)
    {
        // === 1. F�ze: Vytvo�en� a konfigurace "Builderu" ===
        // Builder slou�� jako "stavebnice" pro na�i webovou aplikaci. Postupn� do n�j
        // p�id�v�me a konfigurujeme v�echny pot�ebn� sou��sti.
        var builder = WebApplication.CreateBuilder(args);

        // Zkontrolujeme, zda aplikace b�� v produk�n�m prost�ed�.
        // Toto prost�ed� se nastavuje na serveru pomoc� prom�nn� ASPNETCORE_ENVIRONMENT=Production.
        if (builder.Environment.IsProduction())
        {
            // Pokud ano, nakonfigurujeme Kestrel, aby naslouchal na portu 5002.
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(5002);
            });
        }

        // --- Konfigurace slu�eb (d��ve metoda 'ConfigureServices' v Startup.cs) ---
        // V t�to sekci registrujeme v�echny "slu�by", kter� bude na�e aplikace pot�ebovat.

        // Na�teme connection string z konfigura�n�ho souboru (appsettings.json).
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

        // Zaregistrujeme "tov�rnu" na v�robu DbContextu. Tov�rna zajist�,
        // �e ka�d� paraleln� �kol v r�mci jednoho GraphQL dotazu dostane vlastn�,
        // izolovanou instanci DbContextu, co� zabra�uje konflikt�m.
        builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Zaregistrujeme a nakonfigurujeme samotn� GraphQL server (Hot Chocolate).
        builder.Services
            .AddGraphQLServer()
            // Definuje t��du, kter� obsahuje v�echny operace pro �ten� dat (dotazy).
            .AddQueryType<Query>()
            // Definuje t��du, kter� obsahuje v�echny operace pro z�pis dat (mutace).
            .AddMutationType<Mutation>()
            // Zapne podporu pro "projekce". Umo�n� klientovi specifikovat v dotazu,
            // kter� pole chce vr�tit, a EF Core se postar� o efektivn� SQL dotaz.
            .AddProjections()
            // Zapne podporu pro filtrov�n�, kter� se p�elo�� na SQL WHERE.
            .AddFiltering()
            // Zapne podporu pro �azen�, kter� se p�elo�� na SQL ORDER BY.
            .AddSorting();


        // === 2. F�ze: Sestaven� aplikace ===
        // Z nakonfigurovan�ho builderu nyn� vytvo��me fin�ln� instanci aplikace.
        var app = builder.Build();


        // --- Konfigurace HTTP Pipeline (d��ve metoda 'Configure' v Startup.cs) ---
        // Zde definujeme, jak bude server postupn� zpracov�vat ka�d� p��choz� HTTP po�adavek.

        // Pokud aplikace b�� ve v�vojov�m prost�ed�, pou�ije se str�nka s detailn�mi chybami.
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Zapne "routing", co� je mechanismus, kter� rozhoduje, kter� ��st k�du
        // se m� spustit na z�klad� URL adresy po�adavku.
        app.UseRouting();

        // Definuje koncov� bod (endpoint) na�� aplikace na adrese '/graphql'.
        // V�echny GraphQL po�adavky budou sm��ovat sem.
        // Tento endpoint tak� hostuje testovac� prost�ed� Banana Cake Pop.
        app.MapGraphQL();


        // === 4. F�ze: Spu�t�n� aplikace ===
        // Tento p��kaz spust� aplikaci a ta za�ne naslouchat na p��choz� po�adavky.
        app.Run();
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