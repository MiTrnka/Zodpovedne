// NuGet Microsoft.AspNetCore.Identity
// NuGet Microsoft.AspNetCore.Identity.EntityFrameworkCore
// NuGet Microsoft.EntityFrameworkCore.Tools
// NuGet Npgsql.EntityFrameworkCore.PostgreSQL
// NuGet Microsoft.Extensions.Configuration.Json
// NuGet Microsoft.EntityFrameworkCore.Design

using Zodpovedne.Data.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Zodpovedne.Data.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // DbSety pro naše nové entity
    public DbSet<Category> Categories { get; set; }
    public DbSet<Discussion> Discussions { get; set; }
    public DbSet<Comment> Comments { get; set; }
    public DbSet<DiscussionLike> DiscussionLikes { get; set; }
    public DbSet<CommentLike> CommentLikes { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Vlastní názvy tabulek pro Identity
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<IdentityRole>().ToTable("Roles");
        builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<string>>().ToTable("UserTokens");

        // Unikátní nickname pro uživatele
        builder.Entity<ApplicationUser>()
            .HasIndex(u => u.Nickname)
            .IsUnique();

        // Konfigurace Category
        builder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");

            // Name musí být unikátní
            entity.HasIndex(e => e.Name)
                .IsUnique();

            // Code musí být unikátní
            entity.HasIndex(e => e.Code)
                .IsUnique();
        });

        // Konfigurace Discussion
        builder.Entity<Discussion>(entity =>
        {
            entity.ToTable("Discussions");

            // Vztah k Category (N:1)
            entity.HasOne(d => d.Category)
                .WithMany(c => c.Discussions)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Restrict); // Zakázat kaskádové mazání

            // Vztah k User (N:1)
            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Konfigurace Comment
        builder.Entity<Comment>(entity =>
        {
            entity.ToTable("Comments");

            // Vztah k Discussion (N:1)
            entity.HasOne(c => c.Discussion)
                .WithMany(d => d.Comments)
                .HasForeignKey(c => c.DiscussionId)
                .OnDelete(DeleteBehavior.Cascade); // Při smazání diskuze se smažou i komentáře

            // Vztah k User (N:1)
            entity.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Vztah k nadřazenému komentáři (self-referencing)
            entity.HasOne(c => c.ParentComment)
                .WithMany(c => c.Replies)
                .HasForeignKey(c => c.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        });

        // Konfigurace DiscussionLike
        builder.Entity<DiscussionLike>(entity =>
        {
            entity.ToTable("DiscussionLikes");

            // Vztah k Discussion (N:1)
            entity.HasOne(dl => dl.Discussion)
                .WithMany(d => d.Likes)
                .HasForeignKey(dl => dl.DiscussionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Vztah k User (N:1)
            entity.HasOne(dl => dl.User)
                .WithMany()
                .HasForeignKey(dl => dl.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Konfigurace CommentLike
        builder.Entity<CommentLike>(entity =>
        {
            entity.ToTable("CommentLikes");

            // Vztah k Comment (N:1)
            entity.HasOne(cl => cl.Comment)
                .WithMany(c => c.Likes)
                .HasForeignKey(cl => cl.CommentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Vztah k User (N:1)
            entity.HasOne(cl => cl.User)
                .WithMany()
                .HasForeignKey(cl => cl.UserId)
                .OnDelete(DeleteBehavior.Restrict);

        });
    }
}