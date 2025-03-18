using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zodpovedne.Contracts.Enums;
using Zodpovedne.Data.Data;
using Zodpovedne.Data.Models;

namespace Zodpovedne.Data.Services
{
    public class DataInitializer
    {
        private readonly ApplicationDbContext dbContext;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly RoleManager<IdentityRole> roleManager;

        public DataInitializer(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            this.dbContext = dbContext;
            this.userManager = userManager;
            this.roleManager = roleManager;
        }

        public async Task InitializeAsync()
        {
            await InitializeRolesAndAdminAsync();
            //await SeedTestDataAsync();
        }

        private async Task InitializeRolesAndAdminAsync()
        {
            // Vytvoření základních rolí Admin a Member, pokud ještě neexistují
            if (!await roleManager.RoleExistsAsync("Admin"))
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            if (!await roleManager.RoleExistsAsync("Member"))
                await roleManager.CreateAsync(new IdentityRole("Member"));

            // Vytvoření admin účtu pokud neexistuje
            var adminEmail = "admin@mz.cz";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    Nickname = "Admin",
                    EmailConfirmed = true,
                    Type = UserType.Normal
                };

                var result = await userManager.CreateAsync(admin, "abc");
                if (result.Succeeded)
                {
                    //Přidání role Admin k admin účtu
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
            }
        }

        private async Task SeedTestDataAsync()
        {
            // Vytvoření testovacích uživatelů
            var users = new[]
            {
            new { Email = "jana@mz.cz", Password = "abc", Nickname = "JanaM", Type = UserType.Normal },
            new { Email = "petra@mz.cz", Password = "abc", Nickname = "Peta", Type = UserType.Normal },
            new { Email = "michal@mz.cz", Password = "abc", Nickname = "TataMichal", Type = UserType.Normal },
            new { Email = "lucie@mz.cz", Password = "abc", Nickname = "LucieS", Type = UserType.Normal },
            new { Email = "tomas@mz.cz", Password = "abc", Nickname = "TomasK", Type = UserType.Normal },
            new { Email = "tereza.novakova@mz.cz", Password = "abc", Nickname = "TerezaN", Type = UserType.Normal },
            new { Email = "jan.dvorak@mz.cz", Password = "abc", Nickname = "TataHonza", Type = UserType.Normal },
            new { Email = "marketa.svobodova@mz.cz", Password = "abc", Nickname = "MarketaS", Type = UserType.Normal },
            new { Email = "pavel.novotny@mz.cz", Password = "abc", Nickname = "PavelTata", Type = UserType.Normal },
            new { Email = "lenka.horakova@mz.cz", Password = "abc", Nickname = "LenkaH", Type = UserType.Normal },
            new { Email = "jiri.prochazka@mz.cz", Password = "abc", Nickname = "JirkaP", Type = UserType.Normal },
            new { Email = "eva.cerna@mz.cz", Password = "abc", Nickname = "EvickaC", Type = UserType.Normal },
            new { Email = "martin.kucera@mz.cz", Password = "abc", Nickname = "Martin123", Type = UserType.Normal },
            new { Email = "katerina.vesela@mz.cz", Password = "abc", Nickname = "KackaV", Type = UserType.Normal },
            new { Email = "petr.kral@mz.cz", Password = "abc", Nickname = "PetrK", Type = UserType.Normal },
            new { Email = "hana.malikova@mz.cz", Password = "abc", Nickname = "HankaM", Type = UserType.Normal },
            new { Email = "roman.benes@mz.cz", Password = "abc", Nickname = "RomanB", Type = UserType.Normal },
            new { Email = "barbora.sedlackova@mz.cz", Password = "abc", Nickname = "BaraS", Type = UserType.Normal },
            new { Email = "david.pospisil@mz.cz", Password = "abc", Nickname = "DavidP", Type = UserType.Normal },
            new { Email = "michaela.urbanova@mz.cz", Password = "abc", Nickname = "MisaU", Type = UserType.Normal },
            new { Email = "ondrej.mach@mz.cz", Password = "abc", Nickname = "OndraM", Type = UserType.Normal },
            new { Email = "kristyna.benesova@mz.cz", Password = "abc", Nickname = "KikaB", Type = UserType.Normal },
            new { Email = "lukas.toman@mz.cz", Password = "abc", Nickname = "LukasT", Type = UserType.Normal },
            new { Email = "simona.maskova@mz.cz", Password = "abc", Nickname = "SimcaM", Type = UserType.Normal },
            new { Email = "adam.kratochvil@mz.cz", Password = "abc", Nickname = "AdamK", Type = UserType.Normal },
            new { Email = "nikola.krejcova@mz.cz", Password = "abc", Nickname = "NikiK", Type = UserType.Normal },
            new { Email = "vojtech.hruska@mz.cz", Password = "abc", Nickname = "VojtaH", Type = UserType.Normal },
            new { Email = "monika.soukupova@mz.cz", Password = "abc", Nickname = "MoniS", Type = UserType.Normal },
            new { Email = "filip.riha@mz.cz", Password = "abc", Nickname = "FilipR", Type = UserType.Normal },
            new { Email = "adela.bila@mz.cz", Password = "abc", Nickname = "AdelkaB", Type = UserType.Normal },
            new { Email = "radek.syka@mz.cz", Password = "abc", Nickname = "RadekS", Type = UserType.Normal },
            new { Email = "eliska.nemcova@mz.cz", Password = "abc", Nickname = "ElaNem", Type = UserType.Normal },
            new { Email = "tomas.strnad@mz.cz", Password = "abc", Nickname = "TomasS", Type = UserType.Normal },
            new { Email = "vendula.kralova@mz.cz", Password = "abc", Nickname = "VendaK", Type = UserType.Normal },
            new { Email = "patrik.musil@mz.cz", Password = "abc", Nickname = "PatrikM", Type = UserType.Normal },
            new { Email = "denisa.vlckova@mz.cz", Password = "abc", Nickname = "DenisaV", Type = UserType.Normal },
            new { Email = "stepan.jaros@mz.cz", Password = "abc", Nickname = "StepanJ", Type = UserType.Normal },
            new { Email = "andrea.polakova@mz.cz", Password = "abc", Nickname = "AndiP", Type = UserType.Normal },
            new { Email = "jakub.hajek@mz.cz", Password = "abc", Nickname = "KubaH", Type = UserType.Normal },
            new { Email = "veronika.ruzickova@mz.cz", Password = "abc", Nickname = "VeraR", Type = UserType.Normal },
            new { Email = "marek.holy@mz.cz", Password = "abc", Nickname = "MarekH", Type = UserType.Normal },
            new { Email = "martina.klimova@mz.cz", Password = "abc", Nickname = "MartinaK", Type = UserType.Normal },
            new { Email = "daniel.simek@mz.cz", Password = "abc", Nickname = "DanS", Type = UserType.Normal },
            new { Email = "petra.kolarova@mz.cz", Password = "abc", Nickname = "PetraK", Type = UserType.Normal },
            new { Email = "robert.stehlik@mz.cz", Password = "abc", Nickname = "RobertS", Type = UserType.Normal },
            new { Email = "jana.mackova@mz.cz", Password = "abc", Nickname = "JanaM2", Type = UserType.Normal },
            new { Email = "vladimir.gregor@mz.cz", Password = "abc", Nickname = "VladaG", Type = UserType.Normal },
            new { Email = "gabriela.fojtikova@mz.cz", Password = "abc", Nickname = "GabiF", Type = UserType.Normal },
            new { Email = "milan.bartos@mz.cz", Password = "abc", Nickname = "MilanB", Type = UserType.Normal },
            new { Email = "sarka.huskova@mz.cz", Password = "abc", Nickname = "SarkaH", Type = UserType.Normal },
            new { Email = "kamil.zeman@mz.cz", Password = "abc", Nickname = "KamilZ", Type = UserType.Normal },
            new { Email = "iveta.kavkova@mz.cz", Password = "abc", Nickname = "IvetaK", Type = UserType.Normal },
            new { Email = "radim.fiala@mz.cz", Password = "abc", Nickname = "RadimF", Type = UserType.Normal },
            new { Email = "renata.jezkova@mz.cz", Password = "abc", Nickname = "RenataJ", Type = UserType.Normal },
            new { Email = "matej.kopecky@mz.cz", Password = "abc", Nickname = "MatejK", Type = UserType.Normal },
            new { Email = "linda.blazkova@mz.cz", Password = "abc", Nickname = "LindaB", Type = UserType.Normal },
            new { Email = "michal.kolman@mz.cz", Password = "abc", Nickname = "MichalK", Type = UserType.Normal },
            new { Email = "natalie.richtrova@mz.cz", Password = "abc", Nickname = "NatalieR", Type = UserType.Normal },
            new { Email = "dominik.vacek@mz.cz", Password = "abc", Nickname = "DominikV", Type = UserType.Normal },
            new { Email = "olga.duskova@mz.cz", Password = "abc", Nickname = "OlgaD", Type = UserType.Normal },
            new { Email = "richard.beran@mz.cz", Password = "abc", Nickname = "RichardB", Type = UserType.Normal },
            new { Email = "alena.hromadkova@mz.cz", Password = "abc", Nickname = "AlenaH", Type = UserType.Normal },
            new { Email = "boris.sykora@mz.cz", Password = "abc", Nickname = "BorisS", Type = UserType.Normal },
            new { Email = "zuzana.moravcova@mz.cz", Password = "abc", Nickname = "ZuzkaM", Type = UserType.Normal },
            new { Email = "gustav.maly@mz.cz", Password = "abc", Nickname = "GustavM", Type = UserType.Normal },
            new { Email = "aneta.vackova@mz.cz", Password = "abc", Nickname = "AnetaV", Type = UserType.Normal },
            new { Email = "viktor.kubat@mz.cz", Password = "abc", Nickname = "ViktorK", Type = UserType.Normal },
            new { Email = "marcela.stranska@mz.cz", Password = "abc", Nickname = "MarcelaS", Type = UserType.Normal },
            new { Email = "ivan.nemec@mz.cz", Password = "abc", Nickname = "IvanN", Type = UserType.Normal },
            new { Email = "jitka.stanikova@mz.cz", Password = "abc", Nickname = "JitkaS", Type = UserType.Normal },
            new { Email = "alex.kraus@mz.cz", Password = "abc", Nickname = "AlexK", Type = UserType.Normal },
            new { Email = "magdalena.dolezalova@mz.cz", Password = "abc", Nickname = "MagdaD", Type = UserType.Normal },
            new { Email = "erik.valenta@mz.cz", Password = "abc", Nickname = "ErikV", Type = UserType.Normal },
            new { Email = "klara.prokopova@mz.cz", Password = "abc", Nickname = "KlaraP", Type = UserType.Normal },
            new { Email = "eduard.pesek@mz.cz", Password = "abc", Nickname = "EduardP", Type = UserType.Normal },
            new { Email = "silvie.hrdlickova@mz.cz", Password = "abc", Nickname = "SilvieH", Type = UserType.Normal },
            new { Email = "lubomir.kovarik@mz.cz", Password = "abc", Nickname = "LubaK", Type = UserType.Normal },
            new { Email = "sabina.berankova@mz.cz", Password = "abc", Nickname = "SabinaB", Type = UserType.Normal },
            new { Email = "kveta.richtarova@mz.cz", Password = "abc", Nickname = "KvetaR", Type = UserType.Normal },
            new { Email = "ludek.havel@mz.cz", Password = "abc", Nickname = "LudekH", Type = UserType.Normal },
            new { Email = "nadezda.liskova@mz.cz", Password = "abc", Nickname = "NadaL", Type = UserType.Normal },
            new { Email = "emil.stark@mz.cz", Password = "abc", Nickname = "EmilS", Type = UserType.Normal },
            new { Email = "sona.kadlecova@mz.cz", Password = "abc", Nickname = "SonaK", Type = UserType.Normal },
            new { Email = "oldrich.vrana@mz.cz", Password = "abc", Nickname = "OldaV", Type = UserType.Normal },
            new { Email = "dagmar.bartosova@mz.cz", Password = "abc", Nickname = "DasaB", Type = UserType.Normal },
            new { Email = "vlastimil.kriz@mz.cz", Password = "abc", Nickname = "VlastaK", Type = UserType.Normal },
            new { Email = "radka.fialova@mz.cz", Password = "abc", Nickname = "RadkaF", Type = UserType.Normal },
            new { Email = "libor.trnka@mz.cz", Password = "abc", Nickname = "LiborT", Type = UserType.Normal },
            new { Email = "marta.smolkova@mz.cz", Password = "abc", Nickname = "MartaS", Type = UserType.Normal },
            new { Email = "stanislav.hradil@mz.cz", Password = "abc", Nickname = "StandaH", Type = UserType.Normal },
            new { Email = "dita.mikova@mz.cz", Password = "abc", Nickname = "DitaM", Type = UserType.Normal },
            new { Email = "rostislav.kaspar@mz.cz", Password = "abc", Nickname = "RostaK", Type = UserType.Normal },
            new { Email = "milena.jandova@mz.cz", Password = "abc", Nickname = "MilenaJ", Type = UserType.Normal },
            new { Email = "bronislav.urban@mz.cz", Password = "abc", Nickname = "BronekU", Type = UserType.Normal },
            new { Email = "zdena.fuchsova@mz.cz", Password = "abc", Nickname = "ZdenaF", Type = UserType.Normal },
            new { Email = "otakar.safarik@mz.cz", Password = "abc", Nickname = "OtaS", Type = UserType.Normal },
            new { Email = "bohumila.klimesova@mz.cz", Password = "abc", Nickname = "BohumilaK", Type = UserType.Normal },
            new { Email = "rudolf.burda@mz.cz", Password = "abc", Nickname = "RudaB", Type = UserType.Normal },
            new { Email = "jolana.uhlirova@mz.cz", Password = "abc", Nickname = "JolanaU", Type = UserType.Normal },
            new { Email = "zbynek.hrdina@mz.cz", Password = "abc", Nickname = "ZbynekH", Type = UserType.Normal },
            new { Email = "daniela.pokorna@mz.cz", Password = "abc", Nickname = "DanielaP", Type = UserType.Normal },
            new { Email = "hugo.stetina@mz.cz", Password = "abc", Nickname = "HugoS", Type = UserType.Normal },
            new { Email = "blazena.adamcova@mz.cz", Password = "abc", Nickname = "BlazenaA", Type = UserType.Normal },
            new { Email = "norbert.rada@mz.cz", Password = "abc", Nickname = "NorbertR", Type = UserType.Normal },
            new { Email = "kamila.bedrova@mz.cz", Password = "abc", Nickname = "KamilaB", Type = UserType.Normal },
            new { Email = "leopold.keller@mz.cz", Password = "abc", Nickname = "LeopoldK", Type = UserType.Normal },
            new { Email = "doubravka.sevcikova@mz.cz", Password = "abc", Nickname = "DoubravkaS", Type = UserType.Normal },
            new { Email = "radovan.janousek@mz.cz", Password = "abc", Nickname = "RadovanJ", Type = UserType.Normal }
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
                        EmailConfirmed = true,
                        Type = userData.Type
                    };
                    var result = await userManager.CreateAsync(user, userData.Password);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, "Member");
                        createdUsers.Add(user);
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
                    DateTime createdUpdatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30));
                    var discussion = new Discussion
                    {
                        CategoryId = category.Id,
                        UserId = author.Id,
                        Title = discussionData.Title,
                        Code = discussionData.Code,
                        Content = discussionData.Content,
                        Type = DiscussionType.Normal,
                        CreatedAt = createdUpdatedAt,
                        UpdatedAt = createdUpdatedAt
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
            DateTime createdUpdatedAt;

            for (int i = 0; i < rootCommentsCount; i++)
            {
                var author = users[random.Next(users.Count)];
                createdUpdatedAt = discussion.CreatedAt.AddHours(random.Next(1, 72));
                var rootComment = new Comment
                {
                    DiscussionId = discussion.Id,
                    UserId = author.Id,
                    Content = GetRandomComment(discussion.Title),
                    Type = CommentType.Normal,
                    CreatedAt = createdUpdatedAt,
                    UpdatedAt = createdUpdatedAt
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
                        createdUpdatedAt = rootComment.CreatedAt.AddHours(random.Next(1, 24));
                        var reply = new Comment
                        {
                            DiscussionId = discussion.Id,
                            UserId = replyAuthor.Id,
                            ParentCommentId = rootComment.Id,
                            Content = GetRandomReply(rootComment.Content),
                            Type = CommentType.Normal,
                            CreatedAt = createdUpdatedAt,
                            UpdatedAt = createdUpdatedAt
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
}
