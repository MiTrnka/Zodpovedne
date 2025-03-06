using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zodpovedne.Data.Data;
using Zodpovedne.Data.Interfaces;
using Zodpovedne.Data.Models;
using Zodpovedne.Data.Services;

namespace Zodpovedne.Data.Extensions;

// Extension třída pro konfiguraci datové vrstvy
// Centralizuje veškerou konfiguraci spojenou s datovou vrstvou
public static class DataLayerConfiguration
{
    // Extension metoda pro IServiceCollection
    // Umožňuje jednoduše zaregistrovat datovou vrstvu do DI kontejneru
    public static IServiceCollection AddDataLayer(this IServiceCollection services, IConfiguration externalConfiguration = null)
    {
        string connectionString = null;

        // Pokud byla poskytnuta konfigurace z vnějšku (např. z RESTAPI projektu)
        if (externalConfiguration != null)
        {
            connectionString = externalConfiguration.GetConnectionString("DefaultConnection");
        }

        // Pokud externí konfigurace neobsahuje connection string, načteme vlastní
        if (string.IsNullOrEmpty(connectionString))
        {
            IConfiguration configuration = null;

            // Nejprve zkusíme načíst konfiguraci z aktuálního adresáře
            try
            {
                configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true)
                    .Build();

                connectionString = configuration.GetConnectionString("DefaultConnection");
            }
            catch { } // Ignorujeme chyby a zkusíme další způsob

            // Pokud se nepodařilo, zkusíme vývojářskou cestu (relativní cesta k Data projektu)
            if (string.IsNullOrEmpty(connectionString))
            {
                try
                {
                    configuration = new ConfigurationBuilder()
                        .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../Zodpovedne.Data"))
                        .AddJsonFile("appsettings.json", optional: true)
                        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true)
                        .Build();

                    connectionString = configuration.GetConnectionString("DefaultConnection");
                }
                catch { } // Ignorujeme chyby
            }
        }

        // Pokud se ani jeden způsob nepodařil, vyhodíme výjimku
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' není nastaven v konfiguraci Data projektu.");
        }

        // Registrace DbContextu
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Registrace IDataContext
        // Když někdo požádá o IDataContext, dostane instanci ApplicationDbContext
        services.AddScoped<IDataContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        // Registrace služeb pro Identity
        services.AddIdentity<ApplicationUser, IdentityRole>(options => {
            // Původní konfigurace z ServiceCollectionExtensions
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 1;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // Registrace dalších služeb
        services.AddScoped<IIdentityDataSeeder, IdentityDataSeeder>();
        services.AddScoped<ITestDataSeeder, TestDataSeeder>();

        return services;
    }
}