using Zodpovedne.Models;
using Microsoft.AspNetCore.Identity;
using Zodpovedne.Data.Interfaces;

namespace Zodpovedne.Data.Services;

public class IdentityService : IIdentityService
{
    private readonly RoleManager<IdentityRole> roleManager;
    private readonly UserManager<ApplicationUser> userManager;

    public IdentityService(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager)
    {
        this.roleManager = roleManager;
        this.userManager = userManager;
    }

    public async Task InitializeRolesAndAdminAsync()
    {
        // Vytvoření základních rolí
        string[] roles = { "Admin", "Moderator", "User" };
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
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }
    }
}