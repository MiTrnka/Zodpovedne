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
    public DbSet<MessagingPermission> AllowedMessagingPermissions { get; set; }
    public DbSet<Message> Messages { get; set; }

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

        // Konfigurace AllowedMessagingPermission s kaskádovým mazáním
        builder.Entity<MessagingPermission>(entity =>
        {
            entity.ToTable("MessagingPermissions"); // Název tabulky

            // Vztah k uživateli, který povolení dává (Granter)
            entity.HasOne(amp => amp.GranterUser)
                  .WithMany() // Pokud bys chtěl v ApplicationUser kolekci GrantedPermissions, přidej ji a sem dej WithMany(u => u.GrantedPermissions)
                  .HasForeignKey(amp => amp.GranterUserId)
                  .OnDelete(DeleteBehavior.Cascade); // Při smazání uživatele se smaže i toto povolení

            // Vztah k uživateli, kterému je povolení dáno (Allowed)
            entity.HasOne(amp => amp.AllowedUser)
                  .WithMany() // Pokud bys chtěl v ApplicationUser kolekci AllowedToMessage, přidej ji a sem dej WithMany(u => u.AllowedToMessage)
                  .HasForeignKey(amp => amp.AllowedUserId)
                  .OnDelete(DeleteBehavior.Cascade); // Při smazání uživatele se smaže i toto povolení

            // Unikátní index pro kombinaci GranterUserId a AllowedUserId, aby nemohlo být stejné povolení zadáno vícekrát
            entity.HasIndex(amp => new { amp.GranterUserId, amp.AllowedUserId }).IsUnique();
        });

        // Konfigurace Message s kaskádovým mazáním
        builder.Entity<Message>(entity =>
        {
            entity.ToTable("Messages"); // Název tabulky

            // Vztah k odesílateli
            entity.HasOne(m => m.SenderUser)
                  .WithMany() // Pokud bys chtěl v ApplicationUser kolekci SentMessages, přidej ji a sem dej WithMany(u => u.SentMessages)
                  .HasForeignKey(m => m.SenderUserId)
                  .OnDelete(DeleteBehavior.Cascade); // Při smazání odesílatele se smaže i zpráva

            // Vztah k příjemci
            entity.HasOne(m => m.RecipientUser)
                  .WithMany() // Pokud bys chtěl v ApplicationUser kolekci ReceivedMessages, přidej ji a sem dej WithMany(u => u.ReceivedMessages)
                  .HasForeignKey(m => m.RecipientUserId)
                  .OnDelete(DeleteBehavior.Cascade); // Při smazání příjemce se smaže i zpráva

            // Index pro rychlejší hledání zpráv podle času odeslání (užitečné)
            entity.HasIndex(m => m.SentAt);

            // Index pro rychlejší hledání konverzace mezi dvěma uživateli
            entity.HasIndex(m => new { m.SenderUserId, m.RecipientUserId });
            entity.HasIndex(m => new { m.RecipientUserId, m.SenderUserId }); // I pro opačný směr

            // INDEX pro seskupování/filtrování podle jednoho uživatele !!!
            entity.HasIndex(m => m.SenderUserId);     // Pro rychlé hledání zpráv ODESLANÝCH uživatelem
            entity.HasIndex(m => m.RecipientUserId);  // Pro rychlé hledání zpráv PŘIJATÝCH uživatelem
        });
    }
}