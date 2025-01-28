using Zodpovedne.Models;
using Microsoft.AspNetCore.Identity;
using Zodpovedne.Data.Interfaces;
using Microsoft.Extensions.Logging;

namespace Zodpovedne.Data.Services;

// Třída je součástí servisní vrstvy aplikace a stará se o správu identit (role a uživatelé).
// V současné implementaci zajišťuje především inicializaci základních rolí a admin účtu při startu aplikace.
public class IdentityDataSeeder : IIdentityDataSeeder
{
    private readonly RoleManager<IdentityRole> roleManager;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly ILogger<IIdentityDataSeeder> logger;

    public IdentityDataSeeder(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ILogger<IIdentityDataSeeder> logger)
    {
        this.roleManager = roleManager;
        this.userManager = userManager;
        this.logger = logger;
    }

    // Metoda pro inicializaci základních rolí a admin účtu
    public async Task InitializeRolesAndAdminAsync()
    {
        if (roleManager == null || userManager == null)
        {
            logger.LogError("RoleManager nebo UserManager nebyly inicializovány.");
            return;
        }

        // Vytvoření základních rolí, pokud ještě neexistují
        string[] roles = { "Admin", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Vytvoření admin účtu pokud neexistuje
        var adminEmail = "admin@mamouzodpovedne.cz";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Admin",
                LastName = "Admin",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, "admin.");
            if (result.Succeeded)
            {
                //Přidání role Admin k admin účtu
                await userManager.AddToRoleAsync(admin, "Admin");
                logger.LogInformation("Založen administrátorský účet.");
            }
        }
    }
}