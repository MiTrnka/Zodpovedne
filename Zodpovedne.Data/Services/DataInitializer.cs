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

        public async Task InitializeAsync(bool testData)
        {
            await dbContext.Database.MigrateAsync();
            await InitializeRolesAndAdminAccountAsync();
            if (testData) await SeedTestDataAsync();
        }

        private async Task InitializeRolesAndAdminAccountAsync()
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
            // Vytvoření testovacích uživatelů, každý se nejprve dohledá podle emailu a založí se jen tehdy, pokud uživatel s daným emailem nebo Nickname není v databázi
            var users = new[]
            {
                new { Email = "adela.novakova@mz.cz", Nickname = "AdelaN" },
                new { Email = "adam.kral@mz.cz", Nickname = "AdamK" },
                new { Email = "alex.kovar@mz.cz", Nickname = "AlexK" },
                new { Email = "alena.kubikova@mz.cz", Nickname = "AlenaK" },
                new { Email = "andrea.kucerova@mz.cz", Nickname = "AndreaK" },
                new { Email = "aneta.hajkova@mz.cz", Nickname = "AnetaH" },
                new { Email = "anna.fiala@mz.cz", Nickname = "AnnaF" },
                new { Email = "antonin.kral@mz.cz", Nickname = "AntoninK" },
                new { Email = "barbora.novotna@mz.cz", Nickname = "BarboraN" },
                new { Email = "bohumil.holub@mz.cz", Nickname = "BohumilH" },
                new { Email = "bohuslav.cerny@mz.cz", Nickname = "BohuslavC" },
                new { Email = "dana.vesela@mz.cz", Nickname = "DanaV" },
                new { Email = "daniel.benes@mz.cz", Nickname = "DanielB" },
                new { Email = "david.sedlak@mz.cz", Nickname = "DavidS" },
                new { Email = "denisa.kubikova@mz.cz", Nickname = "DenisaK" },
                new { Email = "dominik.bartos@mz.cz", Nickname = "DominikB" },
                new { Email = "drahomir.kolar@mz.cz", Nickname = "DrahomirK" },
                new { Email = "eduard.havlik@mz.cz", Nickname = "EduardH" },
                new { Email = "eliska.kovarikova@mz.cz", Nickname = "EliskaK" },
                new { Email = "ema.horakova@mz.cz", Nickname = "EmaH" },
                new { Email = "filip.dvorak@mz.cz", Nickname = "FilipD" },
                new { Email = "frantisek.havran@mz.cz", Nickname = "FrantisekH" },
                new { Email = "gabriela.jelinek@mz.cz", Nickname = "GabrielaJ" },
                new { Email = "hana.machova@mz.cz", Nickname = "HanaM" },
                new { Email = "helena.novotna@mz.cz", Nickname = "HelenaN" },
                new { Email = "hynek.soucek@mz.cz", Nickname = "HynekS" },
                new { Email = "igor.krejci@mz.cz", Nickname = "IgorK" },
                new { Email = "iva.kubova@mz.cz", Nickname = "IvaK" },
                new { Email = "ivana.kolarova@mz.cz", Nickname = "IvanaK" },
                new { Email = "jakub.mala@mz.cz", Nickname = "JakubM" },
                new { Email = "jan.bartos@mz.cz", Nickname = "JanB" },
                new { Email = "jana.cermakova@mz.cz", Nickname = "JanaC" },
                new { Email = "jarmila.kralova@mz.cz", Nickname = "JarmilaK" },
                new { Email = "jaromir.novotny@mz.cz", Nickname = "JaromirN" },
                new { Email = "jaroslav.musil@mz.cz", Nickname = "JaroslavM" },
                new { Email = "jiri.simek@mz.cz", Nickname = "JiriS" },
                new { Email = "jitka.dostalova@mz.cz", Nickname = "JitkaD" },
                new { Email = "josef.novak@mz.cz", Nickname = "JosefN" },
                new { Email = "karolina.mala@mz.cz", Nickname = "KarolinaM" },
                new { Email = "klara.novakova@mz.cz", Nickname = "KlaraN" },
                new { Email = "kristyna.pokorny@mz.cz", Nickname = "KristynaP" },
                new { Email = "kvetoslava.horakova@mz.cz", Nickname = "KvetoslavaH" },
                new { Email = "linda.hajkova@mz.cz", Nickname = "LindaH" },
                new { Email = "lubos.kovar@mz.cz", Nickname = "LubosK" },
                new { Email = "lucie.kopecka@mz.cz", Nickname = "LucieK" },
                new { Email = "marek.kalina@mz.cz", Nickname = "MarekK" },
                new { Email = "marie.vesela@mz.cz", Nickname = "MarieV" },
                new { Email = "martin.kubicek@mz.cz", Nickname = "MartinK" },
                new { Email = "martina.svecova@mz.cz", Nickname = "MartinaS" },
                new { Email = "matej.kral@mz.cz", Nickname = "MatejK" },
                new { Email = "michal.prochazka@mz.cz", Nickname = "MichalP" },
                new { Email = "michaela.dvorakova@mz.cz", Nickname = "MichaelaD" },
                new { Email = "monika.smidova@mz.cz", Nickname = "MonikaS" },
                new { Email = "natalie.kostka@mz.cz", Nickname = "NatalieK" },
                new { Email = "nikola.fuksova@mz.cz", Nickname = "NikolaF" },
                new { Email = "oldrich.sedivy@mz.cz", Nickname = "OldrichS" },
                new { Email = "ondrej.novak@mz.cz", Nickname = "OndrejN" },
                new { Email = "otakar.pokorny@mz.cz", Nickname = "OtakarP" },
                new { Email = "pavel.cerny@mz.cz", Nickname = "PavelC" },
                new { Email = "pavlina.kovarova@mz.cz", Nickname = "PavlinaK" },
                new { Email = "petra.sikora@mz.cz", Nickname = "PetraS" },
                new { Email = "petr.vanecek@mz.cz", Nickname = "PetrV" },
                new { Email = "radek.horak@mz.cz", Nickname = "RadekH" },
                new { Email = "renata.havlova@mz.cz", Nickname = "RenataH" },
                new { Email = "roman.soucek@mz.cz", Nickname = "RomanS" },
                new { Email = "sabina.kratochvilova@mz.cz", Nickname = "SabinaK" },
                new { Email = "samuel.marek@mz.cz", Nickname = "SamuelM" },
                new { Email = "sarka.jelinek@mz.cz", Nickname = "SarkaJ" },
                new { Email = "simona.vlcek@mz.cz", Nickname = "SimonaV" },
                new { Email = "sofia.musilova@mz.cz", Nickname = "SofiaM" },
                new { Email = "stanislav.havel@mz.cz", Nickname = "StanislavH" },
                new { Email = "stepan.svoboda@mz.cz", Nickname = "StepanS" },
                new { Email = "tereza.urbanova@mz.cz", Nickname = "TerezaU" },
                new { Email = "tomáš.janousek@mz.cz", Nickname = "TomasJ" },
                new { Email = "vaclav.rybar@mz.cz", Nickname = "VaclavR" },
                new { Email = "veronika.vacek@mz.cz", Nickname = "VeronikaV" },
                new { Email = "viktor.sedlak@mz.cz", Nickname = "ViktorS" },
                new { Email = "vladimir.klima@mz.cz", Nickname = "VladimirK" },
                new { Email = "zdenek.benes@mz.cz", Nickname = "ZdenekB" }
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
                        Type = UserType.Normal,
                        Created = DateTime.UtcNow,
                        LastLogin = DateTime.UtcNow,
                        EmailConfirmed = true,
                    };
                    try
                    {
                        var result = await userManager.CreateAsync(user, "abc");
                        if (result.Succeeded)
                        {
                            await userManager.AddToRoleAsync(user, "Member");
                            createdUsers.Add(user);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            // Vytvoření kategorií, každá se nejprve dohledá podle Name a založí se jen tehdy, pokud kategorie s daným Name nebo Code není v databázi
            var categories = new[]
            {
                new { Name = "Vývoj", Code = "vyvoj", Description = "Kategorie s diskuzemi a komentáři vhodnými pro testování" },
                new { Name = "Katalogy", Code = "katalogy", Description = "Diskuze o našich katalozích, kde si můžete nakoupit různé zboží" },
                new { Name = "Těhotenství", Code = "tehotenstvi", Description = "Diskuze o těhotenství, přípravě na porod a období před porodem" },
                new { Name = "Porod", Code = "porod", Description = "Zkušenosti s porodem, porodnice, příprava na porod" },
                new { Name = "Kojení", Code = "kojeni", Description = "Vše o kojení, problémy a jejich řešení" },
                new { Name = "Výchova", Code = "vychova", Description = "Výchovné metody, řešení problémů s dětmi" },
                new { Name = "Školky a školy", Code = "skolky-a-skoly", Description = "Diskuze o školkách, školách a vzdělávání" },
                new { Name = "Strava a příkrmy", Code = "strava-a-prikrmy", Description = "Začínáme s příkrmy, strava pro miminka a děti" },
                new { Name = "A", Code = "a", Description = "Aaaaa" },
                new { Name = "B", Code = "b", Description = "Bbbbb" },
                new { Name = "C", Code = "c", Description = "Ccccc" },
                new { Name = "D", Code = "d", Description = "Ddddd" },
                new { Name = "E", Code = "e", Description = "Eeeee" },
                new { Name = "F", Code = "f", Description = "Fffff" },
                new { Name = "G", Code = "g", Description = "Ggggg" },
                new { Name = "H", Code = "h", Description = "Hhhhh" },
                new { Name = "I", Code = "i", Description = "Iiiii" },
                new { Name = "J", Code = "j", Description = "Jjjjj" },
                new { Name = "K", Code = "k", Description = "Kkkkk" },
                new { Name = "L", Code = "l", Description = "Lllll" }
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
                        DisplayOrder = createdCategories.Count + 1,
                        ImagePath = "category-img.svg"
                    };
                    try
                    {
                        dbContext.Categories.Add(category);
                        await dbContext.SaveChangesAsync();
                        createdCategories.Add(category);
                    }
                    catch (Exception)
                    { }
                }
            }

            // Vytvoření úvodních site instancí
            var siteInstances = new[] { "mamazodpovedne.cz", "mamazodpovedne.sk" };
            var createdsiteInstances = new List<SiteInstance>();
            foreach (var siteInstancesData in siteInstances)
            {
                if (!await dbContext.SiteInstances.AnyAsync(si => si.Code == siteInstancesData))
                {
                    var siteInstance = new SiteInstance
                    {
                        Code = siteInstancesData
                    };
                    try
                    {
                        dbContext.SiteInstances.Add(siteInstance);
                        await dbContext.SaveChangesAsync();
                    }
                    catch (Exception)
                    { }
                }
            }

            // Vytvoření náhodných diskuzí a komentářů pro zadefinované kategorie
            var random = new Random();
            foreach (var category in createdCategories)
            {
                if (category.Name == "Katalogy")
                    continue;

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
                        Type = (discussionData.Code == "pozadavky" || discussionData.Code == "hodne-komentaru") ? DiscussionType.Top : DiscussionType.Normal,
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
            if (discussion.Code == "pozadavky")
                return;

            // Vytvoření 3-7 root komentářů
            var rootCommentsCount = random.Next(3, 8);
            if (discussion.Code == "hodne-komentaru")
                rootCommentsCount = 100;

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
                case "Vývoj":
                    return new[]
                    {
                        (
                            "Požadavky",
                            "Diskuze o požadavcích na aplikaci ...",
                            "pozadavky"
                        ),
                        (
                            "Hodně komentářů",
                            "Diskuze s velkým množstvím komentářů ...",
                            "hodne-komentaru"
                        ),
                        (
                            "AA",
                            "AAAAAAAAAA AAAAAAAAAA AAAAAAAAAA AAAAAAAAAA AAAAAAAAAA AAAAAAAAAA ...",
                            "aa"
                        ),
                        (
                            "AB",
                            "AAAAAAAAAA BBBBBBBBBB BBBBBBBBBB BBBBBBBBBB BBBBBBBBBB BBBBBBBBBB ...",
                            "ab"
                        ),
                        (
                            "AC",
                            "AAAAAAAAAA CCCCCCCCCC CCCCCCCCCC CCCCCCCCCC CCCCCCCCCC CCCCCCCCCC ...",
                            "ac"
                        ),
                        (
                            "AD",
                            "AAAAAAAAAA DDDDDDDDDD DDDDDDDDDD DDDDDDDDDD DDDDDDDDDD DDDDDDDDDD ...",
                            "ad"
                        ),
                        (
                            "AE",
                            "AAAAAAAAAA EEEEEEEEEE EEEEEEEEEE EEEEEEEEEE EEEEEEEEEE EEEEEEEEEE ...",
                            "ae"
                        ),
                        (
                            "AF",
                            "AAAAAAAAAA FFFFFFFFFF FFFFFFFFFF FFFFFFFFFF FFFFFFFFFF FFFFFFFFFF ...",
                            "af"
                        ),
                        (
                            "AG",
                            "AAAAAAAAAA GGGGGGGGGG GGGGGGGGGG GGGGGGGGGG GGGGGGGGGG GGGGGGGGGG ...",
                            "ag"
                        ),
                        (
                            "AH",
                            "AAAAAAAAAA HHHHHHHHHH HHHHHHHHHH HHHHHHHHHH HHHHHHHHHH HHHHHHHHHH ...",
                            "ah"
                        ),
                        (
                            "AI",
                            "AAAAAAAAAA IIIIIIIIII IIIIIIIIII IIIIIIIIII IIIIIIIIII IIIIIIIIII ...",
                            "ai"
                        ),
                        (
                            "AJ",
                            "AAAAAAAAAA JJJJJJJJJJ JJJJJJJJJJ JJJJJJJJJJ JJJJJJJJJJ JJJJJJJJJJ ...",
                            "aj"
                        ),
                        (
                            "AK",
                            "AAAAAAAAAA KKKKKKKKKK KKKKKKKKKK KKKKKKKKKK KKKKKKKKKK KKKKKKKKKK ...",
                            "ak"
                        ),
                        (
                            "AL",
                            "AAAAAAAAAA LLLLLLLLLL LLLLLLLLLL LLLLLLLLLL LLLLLLLLLL LLLLLLLLLL ...",
                            "al"
                        ),
                        (
                            "AM",
                            "AAAAAAAAAA MMMMMMMMMM MMMMMMMMMM MMMMMMMMMM MMMMMMMMMM MMMMMMMMMM ...",
                            "am"
                        ),
                        (
                            "AN",
                            "AAAAAAAAAA NNNNNNNNNN NNNNNNNNNN NNNNNNNNNN NNNNNNNNNN NNNNNNNNNN ...",
                            "an"
                        ),
                        (
                            "AO",
                            "AAAAAAAAAA OOOOOOOOOO OOOOOOOOOO OOOOOOOOOO OOOOOOOOOO OOOOOOOOOO ...",
                            "ao"
                        ),
                        (
                            "AP",
                            "AAAAAAAAAA PPPPPPPPPP PPPPPPPPPP PPPPPPPPPP PPPPPPPPPP PPPPPPPPPP ...",
                            "ap"
                        ),
                        (
                            "AQ",
                            "AAAAAAAAAA QQQQQQQQQQ QQQQQQQQQQ QQQQQQQQQQ QQQQQQQQQQ QQQQQQQQQQ ...",
                            "aq"
                        ),
                        (
                            "AR",
                            "AAAAAAAAAA RRRRRRRRRR RRRRRRRRRR RRRRRRRRRR RRRRRRRRRR RRRRRRRRRR ...",
                            "ar"
                        ),
                        (
                            "AS",
                            "AAAAAAAAAA SSSSSSSSSS SSSSSSSSSS SSSSSSSSSS SSSSSSSSSS SSSSSSSSSS ...",
                            "as"
                        ),
                        (
                            "AT",
                            "AAAAAAAAAA TTTTTTTTTT TTTTTTTTTT TTTTTTTTTT TTTTTTTTTT TTTTTTTTTT ...",
                            "at"
                        ),
                        (
                            "AU",
                            "AAAAAAAAAA UUUUUUUUUU UUUUUUUUUU UUUUUUUUUU UUUUUUUUUU UUUUUUUUUU ...",
                            "au"
                        ),
                        (
                            "AV",
                            "AAAAAAAAAA VVVVVVVVVV VVVVVVVVVV VVVVVVVVVV VVVVVVVVVV VVVVVVVVVV ...",
                            "av"
                        ),
                        (
                            "AW",
                            "AAAAAAAAAA WWWWWWWWWW WWWWWWWWWW WWWWWWWWWW WWWWWWWWWW WWWWWWWWWW ...",
                            "aw"
                        ),
                        (
                            "AX",
                            "AAAAAAAAAA XXXXXXXXXX XXXXXXXXXX XXXXXXXXXX XXXXXXXXXX XXXXXXXXXX ...",
                            "ax"
                        ),
                        (
                            "AY",
                            "AAAAAAAAAA YYYYYYYYYY YYYYYYYYYY YYYYYYYYYY YYYYYYYYYY YYYYYYYYYY ...",
                            "ay"
                        ),
                        (
                            "AZ",
                            "AAAAAAAAAA ZZZZZZZZZZ ZZZZZZZZZZ ZZZZZZZZZZ ZZZZZZZZZZ ZZZZZZZZZZ ...",
                            "az"
                        ),
                        (
                            "BA",
                            "BBBBBBBBBB AAAAAAAAAA AAAAAAAAAA AAAAAAAAAA AAAAAAAAAA AAAAAAAAAA ...",
                            "ba"
                        ),
                        (
                            "BB",
                            "BBBBBBBBBB BBBBBBBBBB BBBBBBBBBB BBBBBBBBBB BBBBBBBBBB BBBBBBBBBB ...",
                            "bb"
                        ),
                        (
                            "BC",
                            "BBBBBBBBBB CCCCCCCCCC CCCCCCCCCC CCCCCCCCCC CCCCCCCCCC CCCCCCCCCC ...",
                            "bc"
                        ),
                        (
                            "BD",
                            "BBBBBBBBBB DDDDDDDDDD DDDDDDDDDD DDDDDDDDDD DDDDDDDDDD DDDDDDDDDD ...",
                            "bd"
                        ),
                        (
                            "BE",
                            "BBBBBBBBBB EEEEEEEEEE EEEEEEEEEE EEEEEEEEEE EEEEEEEEEE EEEEEEEEEE ...",
                            "be"
                        ),
                        (
                            "BF",
                            "BBBBBBBBBB FFFFFFFFFF FFFFFFFFFF FFFFFFFFFF FFFFFFFFFF FFFFFFFFFF ...",
                            "bf"
                        ),
                        (
                            "BG",
                            "BBBBBBBBBB GGGGGGGGGG GGGGGGGGGG GGGGGGGGGG GGGGGGGGGG GGGGGGGGGG ...",
                            "bg"
                        ),
                        (
                            "BH",
                            "BBBBBBBBBB HHHHHHHHHH HHHHHHHHHH HHHHHHHHHH HHHHHHHHHH HHHHHHHHHH ...",
                            "bh"
                        ),
                        (
                            "BI",
                            "BBBBBBBBBB IIIIIIIIII IIIIIIIIII IIIIIIIIII IIIIIIIIII IIIIIIIIII ...",
                            "bi"
                        ),
                        (
                            "BJ",
                            "BBBBBBBBBB JJJJJJJJJJ JJJJJJJJJJ JJJJJJJJJJ JJJJJJJJJJ JJJJJJJJJJ ...",
                            "bj"
                        ),
                        (
                            "BK",
                            "BBBBBBBBBB KKKKKKKKKK KKKKKKKKKK KKKKKKKKKK KKKKKKKKKK KKKKKKKKKK ...",
                            "bk"
                        ),
                        (
                            "BL",
                            "BBBBBBBBBB LLLLLLLLLL LLLLLLLLLL LLLLLLLLLL LLLLLLLLLL LLLLLLLLLL ...",
                            "bl"
                        ),
                        (
                            "BM",
                            "BBBBBBBBBB MMMMMMMMMM MMMMMMMMMM MMMMMMMMMM MMMMMMMMMM MMMMMMMMMM ...",
                            "bm"
                        ),
                        (
                            "BN",
                            "BBBBBBBBBB NNNNNNNNNN NNNNNNNNNN NNNNNNNNNN NNNNNNNNNN NNNNNNNNNN ...",
                            "bn"
                        ),
                        (
                            "BO",
                            "BBBBBBBBBB OOOOOOOOOO OOOOOOOOOO OOOOOOOOOO OOOOOOOOOO OOOOOOOOOO ...",
                            "bo"
                        ),
                        (
                            "BP",
                            "BBBBBBBBBB PPPPPPPPPP PPPPPPPPPP PPPPPPPPPP PPPPPPPPPP PPPPPPPPPP ...",
                            "bp"
                        ),
                        (
                            "BQ",
                            "BBBBBBBBBB QQQQQQQQQQ QQQQQQQQQQ QQQQQQQQQQ QQQQQQQQQQ QQQQQQQQQQ ...",
                            "bq"
                        ),
                        (
                            "BR",
                            "BBBBBBBBBB RRRRRRRRRR RRRRRRRRRR RRRRRRRRRR RRRRRRRRRR RRRRRRRRRR ...",
                            "br"
                        ),
                        (
                            "BS",
                            "BBBBBBBBBB SSSSSSSSSS SSSSSSSSSS SSSSSSSSSS SSSSSSSSSS SSSSSSSSSS ...",
                            "bs"
                        ),
                        (
                            "BT",
                            "BBBBBBBBBB TTTTTTTTTT TTTTTTTTTT TTTTTTTTTT TTTTTTTTTT TTTTTTTTTT ...",
                            "bt"
                        ),
                        (
                            "BU",
                            "BBBBBBBBBB UUUUUUUUUU UUUUUUUUUU UUUUUUUUUU UUUUUUUUUU UUUUUUUUUU ...",
                            "bu"
                        ),
                        (
                            "BV",
                            "BBBBBBBBBB VVVVVVVVVV VVVVVVVVVV VVVVVVVVVV VVVVVVVVVV VVVVVVVVVV ...",
                            "bv"
                        ),
                        (
                            "BW",
                            "BBBBBBBBBB WWWWWWWWWW WWWWWWWWWW WWWWWWWWWW WWWWWWWWWW WWWWWWWWWW ...",
                            "bw"
                        ),
                        (
                            "BX",
                            "BBBBBBBBBB XXXXXXXXXX XXXXXXXXXX XXXXXXXXXX XXXXXXXXXX XXXXXXXXXX ...",
                            "bx"
                        ),
                        (
                            "BY",
                            "BBBBBBBBBB YYYYYYYYYY YYYYYYYYYY YYYYYYYYYY YYYYYYYYYY YYYYYYYYYY ...",
                            "by"
                        ),
                        (
                            "BZ",
                            "BBBBBBBBBB ZZZZZZZZZZ ZZZZZZZZZZ ZZZZZZZZZZ ZZZZZZZZZZ ZZZZZZZZZZ ...",
                            "bz"
                        )
                    };

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
