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
    public DbSet<Friendship> Friendships { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Translation> Translations { get; set; }
    public DbSet<SiteInstance> SiteInstances { get; set; }
    public DbSet<VotingQuestion> VotingQuestions { get; set; }
    public DbSet<Vote> Votes { get; set; }
    public DbSet<LoginHistory> LoginHistory { get; set; }

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

            // Indexy
            entity.HasIndex(d => d.UpdatedWhateverAt);
            entity.HasIndex(d => d.Type);
            entity.HasIndex(d => new { d.Type, d.UpdatedWhateverAt });
            entity.HasIndex(d => d.ViewCount);
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

            // Indexy
            entity.HasIndex(c => c.UpdatedAt);
            entity.HasIndex(c => c.Type);
            entity.HasIndex(c => new { c.Type, c.DiscussionId });
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
        builder.Entity<Friendship>(entity =>
        {
            entity.ToTable("Friendships"); // Název tabulky

            // Vztah k uživateli, který povolení uděluje (Granter)
            entity.HasOne(f => f.ApproverUser)
                  .WithMany()
                  .HasForeignKey(f => f.ApproverUserId)
                  .OnDelete(DeleteBehavior.Cascade); // Při smazání uživatele se smaže i toto povolení

            // Vztah k uživateli/žadateli, kterému je povolení uděleno
            entity.HasOne(f => f.RequesterUser)
                  .WithMany()
                  .HasForeignKey(f => f.RequesterUserId)
                  .OnDelete(DeleteBehavior.Cascade); // Při smazání uživatele se smaže i toto povolení

            // Unikátní index pro kombinaci GranterUserId a AllowedUserId, aby nemohlo být stejné povolení zadáno vícekrát
            entity.HasIndex(mp => new { mp.ApproverUserId, mp.RequesterUserId }).IsUnique();
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

        // Konfigurace SiteInstance
        builder.Entity<SiteInstance>(entity =>
        {
            entity.ToTable("SiteInstances");

            // Id musí být unikátní
            entity.HasKey(e => e.Id);

            // Kód instance musí být unikátní
            entity.HasIndex(e => e.Code)
                .IsUnique();
        });

        // Konfigurace Translation
        builder.Entity<Translation>(entity =>
        {
            entity.ToTable("Translations");

            // Primární klíč
            entity.HasKey(e => e.Id);

            // Kód překladu musí být vyplněn
            entity.Property(e => e.Code)
                .IsRequired();

            // Text překladu musí být vyplněn
            entity.Property(e => e.TranslatedText)
                .IsRequired();

            // Vztah k Translation (N:1)
            entity.HasOne(t => t.SiteInstance)
                .WithMany()
                .HasForeignKey(t => t.SiteInstanceId)
                .OnDelete(DeleteBehavior.Restrict); // Zakázat kaskádové mazání

            // Unikátní index na kombinaci SiteInstanceId a Code
            entity.HasIndex(e => new { e.SiteInstanceId, e.Code })
                .IsUnique();
        });

        // Konfigurace VotingQuestion
        builder.Entity<VotingQuestion>(entity =>
        {
            entity.ToTable("VotingQuestions");

            // Vztah k Discussion (N:1)
            entity.HasOne(vq => vq.Discussion)
                .WithMany(d => d.VotingQuestions)
                .HasForeignKey(vq => vq.DiscussionId)
                .OnDelete(DeleteBehavior.Cascade); // Při smazání diskuze se smažou i všechny otázky

            // Index pro rychlejší vyhledávání otázek podle diskuze
            entity.HasIndex(vq => vq.DiscussionId);

            // Index pro řazení otázek podle DisplayOrder
            entity.HasIndex(vq => new { vq.DiscussionId, vq.DisplayOrder });
        });

        // Konfigurace Vote
        builder.Entity<Vote>(entity =>
        {
            entity.ToTable("Votes");

            // Vztah k VotingQuestion (N:1)
            entity.HasOne(v => v.VotingQuestion)
                .WithMany(vq => vq.Votes)
                .HasForeignKey(v => v.VotingQuestionId)
                .OnDelete(DeleteBehavior.Cascade); // Při smazání otázky se smažou i všechny hlasy

            // Vztah k User (N:1)
            entity.HasOne(v => v.User)
                .WithMany()
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Cascade); // Při smazání uživatele se smažou i všechny jeho hlasy

            // Unikátní index pro kombinaci VotingQuestionId a UserId,
            // aby jeden uživatel mohl hlasovat pro jednu otázku pouze jednou
            entity.HasIndex(v => new { v.VotingQuestionId, v.UserId }).IsUnique();

            // Index pro rychlejší vyhledávání hlasů podle uživatele
            entity.HasIndex(v => v.UserId);
        });

        // Konfigurace LoginHistory
        builder.Entity<LoginHistory>(entity =>
        {
            entity.ToTable("LoginHistory");

            // Vztah k uživateli (N:1)
            entity.HasOne(lh => lh.User)
                  .WithMany()
                  .HasForeignKey(lh => lh.UserId)
                  .OnDelete(DeleteBehavior.Cascade); // Při smazání uživatele se smažou i záznamy o jeho přihlášení

            // Index pro rychlejší vyhledávání podle uživatele
            entity.HasIndex(lh => lh.UserId);

            // Index pro rychlejší vyhledávání podle času přihlášení
            entity.HasIndex(lh => lh.LoginTime);
        });

    }
}