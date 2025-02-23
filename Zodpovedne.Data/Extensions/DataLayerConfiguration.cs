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
    public static IServiceCollection AddDataLayer(this IServiceCollection services)
    {
        IConfiguration? configuration = null;
        string? connectionString = null;

        // Zkus načíst z aktuálního adresáře (produkce)
        try
        {
            configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.Data.json", optional: true)
                .Build();

            connectionString = configuration.GetConnectionString("DefaultConnection");
        }
        catch { } // Pokud se nepodaří, zkusíme další variantu

        // Pokud se nepodařilo, zkus vývojářskou cestu
        if (string.IsNullOrEmpty(connectionString))
        {
            try
            {
                configuration = new ConfigurationBuilder()
                    .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../Zodpovedne.Data"))
                    .AddJsonFile("appsettings.Data.json", optional: true)
                    .Build();

                connectionString = configuration.GetConnectionString("DefaultConnection");
            }
            catch { } // Pokud se nepodaří, vyhodíme výjimku níže
        }

        // Pokud se ani jedna cesta nepodařila
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