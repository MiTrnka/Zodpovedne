// NuGet Microsoft.AspNetCore.Identity
// NuGet Microsoft.AspNetCore.Identity.EntityFrameworkCore
// NuGet Microsoft.EntityFrameworkCore.Tools
// NuGet Npgsql.EntityFrameworkCore.PostgreSQL
// NuGet Microsoft.Extensions.Configuration.Json
// NuGet Microsoft.EntityFrameworkCore.Design

using Zodpovedne.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Zodpovedne.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Vytvářím tabulky s vlastními názvy
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<IdentityRole>().ToTable("Roles");
        builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<string>>().ToTable("UserTokens");
    }
}