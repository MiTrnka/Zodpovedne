using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zodpovedne.Data.Data;
using Zodpovedne.Data.Interfaces;
using Zodpovedne.Contracts.Enums;
using Zodpovedne.Data.Models;

namespace Zodpovedne.Data.Services;

public class TestDataSeeder : ITestDataSeeder
{
    private readonly ApplicationDbContext dbContext;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly ILogger<TestDataSeeder> logger;

    public TestDataSeeder(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<TestDataSeeder> logger)
    {
        this.dbContext = dbContext;
        this.userManager = userManager;
        this.logger = logger;
    }

    public async Task SeedTestDataAsync()
    {
        // Vytvoření testovacích uživatelů
        var users = new[]
        {
            new { Email = "jana@mz.cz", Password = "abc", Nickname = "JanaM" },
            new { Email = "petra@mz.cz", Password = "abc", Nickname = "Peta" },
            new { Email = "michal@mz.cz", Password = "abc", Nickname = "TataMichal" },
            new { Email = "lucie@mz.cz", Password = "abc", Nickname = "LucieS" },
            new { Email = "tomas@mz.cz", Password = "abc", Nickname = "TomasK" }
        };

        var createdUsers = new List<ApplicationUser>();
        foreach (var userData in users)
        {
            if (await userManager.FindByEmailAsync(userData.Email) == null)
            {
                var user = new ApplicationUser
                {
                    UserName = userData.Email,
                    Email = userData.Email,
                    Nickname = userData.Nickname,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(user, userData.Password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "Member");
                    createdUsers.Add(user);
                    logger.LogInformation($"Vytvořen testovací účet {userData.Email}");
                }
            }
        }

        // Vytvoření kategorií
        var categories = new[]
        {
    new { Name = "Těhotenství", Code = "tehotenstvi", Description = "Diskuze o těhotenství, přípravě na porod a období před porodem" },
    new { Name = "Porod", Code = "porod", Description = "Zkušenosti s porodem, porodnice, příprava na porod" },
    new { Name = "Kojení", Code = "kojeni", Description = "Vše o kojení, problémy a jejich řešení" },
    new { Name = "Výchova", Code = "vychova", Description = "Výchovné metody, řešení problémů s dětmi" },
    new { Name = "Školky a školy", Code = "skolky-a-skoly", Description = "Diskuze o školkách, školách a vzdělávání" }
};

        var createdCategories = new List<Category>();
        foreach (var categoryData in categories)
        {
            if (!await dbContext.Categories.AnyAsync(c => c.Name == categoryData.Name))
            {
                var category = new Category
                {
                    Name = categoryData.Name,
                    Code = categoryData.Code,  // Přidáno
                    Description = categoryData.Description,
                    DisplayOrder = createdCategories.Count + 1
                };
                dbContext.Categories.Add(category);
                await dbContext.SaveChangesAsync();
                createdCategories.Add(category);
                logger.LogInformation($"Vytvořena kategorie {categoryData.Name}");
            }
        }

        // Vytvoření diskuzí pro každou kategorii
        var random = new Random();

        foreach (var category in createdCategories)
        {
            var discussions = GetDiscussionsForCategory(category.Name);
            foreach (var discussionData in discussions)
            {
                var author = createdUsers[random.Next(createdUsers.Count)];
                var discussion = new Discussion
                {
                    CategoryId = category.Id,
                    UserId = author.Id,
                    Title = discussionData.Title,
                    Code = discussionData.Code,
                    Content = discussionData.Content,
                    Type = DiscussionType.Normal,
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30))
                };
                dbContext.Discussions.Add(discussion);
                await dbContext.SaveChangesAsync();

                // Vytvoření komentářů k diskuzi
                await CreateCommentsForDiscussion(discussion, createdUsers, random);
            }
        }
    }

    private async Task CreateCommentsForDiscussion(Discussion discussion, List<ApplicationUser> users, Random random)
    {
        // Vytvoření 3-7 root komentářů
        var rootCommentsCount = random.Next(3, 8);
        var createdRootComments = new List<Comment>();

        for (int i = 0; i < rootCommentsCount; i++)
        {
            var author = users[random.Next(users.Count)];
            var rootComment = new Comment
            {
                DiscussionId = discussion.Id,
                UserId = author.Id,
                Content = GetRandomComment(discussion.Title),
                Type = CommentType.Normal,
                CreatedAt = discussion.CreatedAt.AddHours(random.Next(1, 72))
            };
            dbContext.Comments.Add(rootComment);
            await dbContext.SaveChangesAsync();
            createdRootComments.Add(rootComment);

            // Pro některé root komentáře vytvoříme 1-3 reakce
            if (random.Next(2) == 1) // 50% šance na reakce
            {
                var repliesCount = random.Next(1, 4);
                for (int j = 0; j < repliesCount; j++)
                {
                    var replyAuthor = users[random.Next(users.Count)];
                    var reply = new Comment
                    {
                        DiscussionId = discussion.Id,
                        UserId = replyAuthor.Id,
                        ParentCommentId = rootComment.Id,
                        Content = GetRandomReply(rootComment.Content),
                        Type = CommentType.Normal,
                        CreatedAt = rootComment.CreatedAt.AddHours(random.Next(1, 24))
                    };
                    dbContext.Comments.Add(reply);
                    await dbContext.SaveChangesAsync();
                }
            }
        }
    }

    private (string Title, string Content, string Code)[] GetDiscussionsForCategory(string categoryName)
    {
        switch (categoryName)
        {
            case "Těhotenství":
                return new[]
                {
                    (
                        "První příznaky těhotenství",
                        "Chtěla bych se podělit o své první příznaky těhotenství. Nejvíc mě překvapilo...",
                        "prvni-priznaky-tehotenstvi"
                    ),
                    (
                        "Cvičení v těhotenství",
                        "Jaké cviky jsou vhodné během těhotenství? Já osobně mám dobrou zkušenost s...",
                        "cviceni-v-tehotenstvi"
                    ),
                    (
                        "Strava v těhotenství",
                        "Zajímalo by mě, jak jste upravily jídelníček v těhotenství. Já například...",
                        "strava-v-tehotenstvi"
                    ),
                    (
                        "Příprava na miminko",
                        "Co všechno jste pořizovaly před narozením miminka? Já mám seznam...",
                        "priprava-na-miminko"
                    )
                };

            case "Porod":
                return new[]
                {
                    (
                        "Porodnice v Praze",
                        "Máte někdo zkušenost s porodnicí v Podolí? Zajímají mě především...",
                        "porodnice-v-praze"
                    ),
                    (
                        "Porodní plán",
                        "Jak vypadal váš porodní plán? Já do něj zahrnula následující body...",
                        "porodni-plan"
                    ),
                    (
                        "První doba porodní",
                        "Jak dlouho vám trvala první doba porodní? U mě to bylo...",
                        "prvni-doba-porodni"
                    ),
                    (
                        "Epidurální analgezie",
                        "Rodila jsem s epidurálem a chtěla bych se podělit o zkušenost...",
                        "epiduralni-analgezie"
                    )
                };

            case "Kojení":
                return new[]
                {
                    (
                        "Problémy s kojením",
                        "Řešila jsem problém se špatným přisáváním. Pomohlo mi...",
                        "problemy-s-kojenim"
                    ),
                    (
                        "Kojení a práce",
                        "Jak zvládáte kojení při návratu do práce? Já to řeším takto...",
                        "kojeni-a-prace"
                    ),
                    (
                        "Odstříkávání",
                        "Jakou odsávačku používáte? Já mám zkušenost s...",
                        "odstrikavani"
                    ),
                    (
                        "Dokrmy při kojení",
                        "Kdy jste začaly s dokrmy? My jsme začali...",
                        "dokrmy-pri-kojeni"
                    )
                };

            case "Výchova":
                return new[]
                {
                    (
                        "Vzdorovité období",
                        "Jak zvládáte období vzdoru u dvouletého dítěte? Nám pomáhá...",
                        "vzdorovite-obdobi"
                    ),
                    (
                        "Sourozenecké vztahy",
                        "Jak jste řešili žárlení staršího sourozence? My používáme metodu...",
                        "sourozenecke-vztahy"
                    ),
                    (
                        "Spánkový režim",
                        "Jaký máte spánkový režim u ročního dítěte? My jsme zavedli...",
                        "spankovy-rezim"
                    ),
                    (
                        "Hranice ve výchově",
                        "Jak stanovujete hranice ve výchově? Osvědčilo se nám...",
                        "hranice-ve-vychove"
                    )
                };

            case "Školky a školy":
                return new[]
                {
                    (
                        "Adaptace ve školce",
                        "Jak probíhala adaptace vašeho dítěte ve školce? U nás to bylo...",
                        "adaptace-ve-skolce"
                    ),
                    (
                        "Výběr základní školy",
                        "Podle čeho vybíráte základní školu? My se zaměřujeme na...",
                        "vyber-zakladni-skoly"
                    ),
                    (
                        "Domácí příprava do školy",
                        "Kolik času věnujete domácí přípravě? My máme systém...",
                        "domaci-priprava-do-skoly"
                    ),
                    (
                        "Alternativní vzdělávání",
                        "Máte zkušenost s Montessori školkou? Naše zkušenosti jsou...",
                        "alternativni-vzdelavani"
                    )
                };

            default:
                return Array.Empty<(string, string, string)>();
        }
    }

    private string GetRandomComment(string discussionTitle)
    {
        var comments = new[]
        {
            "Děkuji za sdílení zkušeností. Mám podobnou zkušenost...",
            "To je zajímavý pohled. U nás to bylo trochu jinak...",
            "Můžu potvrdit, také jsme to tak měli...",
            "Díky za tip, určitě vyzkouším...",
            "Tohle téma mě také zajímá. Chtěla bych se zeptat...",
            "Super příspěvek, hodně mi to pomohlo..."
        };

        return comments[new Random().Next(comments.Length)];
    }

    private string GetRandomReply(string parentComment)
    {
        var replies = new[]
        {
            "Souhlasím s vámi, také máme podobnou zkušenost...",
            "Díky za odpověď, to mi pomohlo...",
            "Můžete to prosím více rozvést?",
            "To je dobrý nápad, díky za tip...",
            "Zajímavý pohled, zamyslím se nad tím..."
        };

        return replies[new Random().Next(replies.Length)];
    }
}