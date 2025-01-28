using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Zodpovedne.Data.Data;

/* slouží pro Entity Framework Core pouze při design-time operacích (zejména při vytváření migrací). Když spustíte příkaz jako Add-Migration,
EF Core potřebuje vytvořit instanci DbContextu, aby mohl analyzovat model a vytvořit migraci.
Tato tovární třída mu říká, jak DbContext vytvořit a nakonfigurovat.
EF Core hledá implementaci IDesignTimeDbContextFactory<TContext> v projektu. Pokud ji najde, použije ji k vytvoření DbContextu.
Aby to fungovalo, třída musí být public, být ve stejném projektu jako DbContext a musí implementovat IDesignTimeDbContextFactory<TContext>´, EF Core ji pak sám najde
U ASP.NET Core se tato třída deklarovat nemusí, protože ASP.NET Core má vlastní mechanismus pro vytváření DbContextu z DI.
*/
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Vytvoření konfigurace a načtení appsettings.json
        // Možné vylepšení: Přidat podporu pro různá prostředí (Development, Production)
        var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json")
        .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Nenalezen connection string s názvem 'DefaultConnection'.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}