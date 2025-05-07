using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Zodpovedne.Data.Data;
using Zodpovedne.Data.Models;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Contracts.Enums;
using Zodpovedne.Data.Helpers;
using Microsoft.AspNetCore.Identity;
using Ganss.Xss;
using Zodpovedne.Logging;
using Zodpovedne.Logging.Services;
using System.Text.RegularExpressions;

namespace Zodpovedne.RESTAPI.Controllers;

/// <summary>
/// Kontroler pro práci s diskuzemi a komentáři.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DiscussionsController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly FileLogger _logger;
    // HtmlSanitizer pro bezpečné čištění HTML vstupu
    private readonly IHtmlSanitizer _sanitizer;

    public Translator Translator { get; }  // Translator pro překlady textů na stránkách


    public DiscussionsController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator)
    {
        _logger = logger;
        this.dbContext = dbContext;
        this.userManager = userManager;
        _sanitizer = sanitizer;
        Translator = translator ?? throw new ArgumentNullException(nameof(translator));
    }

    /// <summary>
    /// Načte pro zadanou kategorii netrackovaný seznam dostupných diskuzí s možností stránkování.
    /// Poskytuje základní informace o diskuzích včetně počtu komentářů a lajků.
    /// Respektuje viditelnost obsahu podle typu uživatele a přátelství.
    /// </summary>
    /// <param name="categoryId">Volitelný parametr pro filtrování podle kategorie</param>
    /// <param name="pageSize">Počet diskuzí na stránku</param>
    /// <param name="page">Číslo stránky (číslováno od 1)</param>
    /// <returns>Stránkovaný seznam diskuzí dle oprávnění přihlášeného uživatele</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<DiscussionListDto>>> GetDiscussions(int? categoryId = null, int page = 1, int pageSize = 10)
    {
        try
        {
            // Validace vstupních parametrů
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 100) pageSize = 100; // Omezení maximální velikosti stránky

            // Identifikace uživatele pro správné filtrování obsahu
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            // Získání seznamu ID přátel přihlášeného uživatele (pokud je uživatel přihlášen)
            // Důležité pro filtrování privátních diskuzí, které vidí jen přátelé autora
            var friendIds = new List<string>();
            if (!string.IsNullOrEmpty(userId))
            {
                // Vyhledání schválených přátelství, kde je uživatel buď žadatelem nebo schvalovatelem
                friendIds = await dbContext.Friendships
                    .AsNoTracking()
                    .Where(f => (f.ApproverUserId == userId || f.RequesterUserId == userId) &&
                               f.FriendshipStatus == FriendshipStatus.Approved)
                    .Select(f => f.ApproverUserId == userId ? f.RequesterUserId : f.ApproverUserId)
                    .ToListAsync();
            }

            // 1. KROK: Vytvoření základního dotazu pro počet a filtrování diskuzí
            // ----------------------------------------------------------------
            var baseQuery = dbContext.Discussions
                .AsNoTracking() // Nesledujeme změny entit pro lepší výkon
                .Where(d => d.Type != DiscussionType.Deleted)  // Smazané diskuze nikdo nevidí
                .Where(d =>
                    d.Type != DiscussionType.Hidden || // Hidden diskuze vidí jen:
                    isAdmin ||                         // - admin (může vidět vše)
                    d.UserId == userId)                // - autor diskuze (vidí své diskuze)
                .Where(d =>
                    d.Type != DiscussionType.Private || // Private diskuze vidí jen:
                    isAdmin ||                          // - admin (může vidět vše)
                    d.UserId == userId ||               // - autor diskuze (vidí své diskuze)
                    friendIds.Contains(d.UserId));      // - přátelé autora (vidí diskuze svých přátel)

            // Aplikace filtru podle kategorie, pokud je zadána
            if (categoryId.HasValue)
            {
                baseQuery = baseQuery.Where(d => d.CategoryId == categoryId.Value);
            }

            // 2. KROK: Zjištění celkového počtu diskuzí pro stránkování
            // -------------------------------------------------------
            var totalCount = await baseQuery.CountAsync();

            // 3. KROK: Získání seznamu diskuzí pro aktuální stránku
            // ---------------------------------------------------
            // Nejprve vybereme ID diskuzí pro aktuální stránku s korektním řazením
            var discussionIds = await baseQuery
                .OrderByDescending(d => d.Type == DiscussionType.Top)  // 1. TOP diskuze
                .ThenByDescending(d => d.UpdatedAt)                    // 2. Nejnovější první
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => d.Id)
                .ToArrayAsync();

            // 4. KROK: Efektivní načtení dat pro vybrané diskuze
            // ------------------------------------------------
            // Pro vybrané diskuze efektivně načteme jen potřebná data
            var discussions = await dbContext.Discussions
                .AsNoTracking()
                .Where(d => discussionIds.Contains(d.Id))
                .Include(d => d.Category)       // Pro zobrazení názvu kategorie
                .Include(d => d.User)           // Pro zobrazení autora
                .ToListAsync();

            // 5. KROK: Načtení počtu relevantních komentářů pro každou diskuzi
            // --------------------------------------------------------------
            var commentCounts = await dbContext.Comments
                .AsNoTracking()
                .Where(c => discussionIds.Contains(c.DiscussionId))
                .Where(c => c.Type != CommentType.Deleted &&     // Ignorujeme smazané
                    (c.Type != CommentType.Hidden ||            // Hidden komentáře počítáme pro:
                        isAdmin ||                              // - adminy
                        c.UserId == userId))                    // - autory komentářů
                .GroupBy(c => c.DiscussionId)
                .Select(g => new { DiscussionId = g.Key, Count = g.Count() })
                .ToListAsync();

            // Vytvoříme slovník pro rychlý přístup k počtu komentářů podle ID diskuze
            var commentCountByDiscussionId = commentCounts.ToDictionary(x => x.DiscussionId, x => x.Count);

            // 6. KROK: Načtení informací o lajcích pro každou diskuzi
            // -----------------------------------------------------
            // Získáme počet lajků pro každou diskuzi
            var likeCounts = await dbContext.DiscussionLikes
                .AsNoTracking()
                .Where(l => discussionIds.Contains(l.DiscussionId))
                .GroupBy(l => l.DiscussionId)
                .Select(g => new { DiscussionId = g.Key, Count = g.Count() })
                .ToListAsync();

            // Vytvoříme slovník pro rychlý přístup k počtu lajků podle ID diskuze
            var likeCountByDiscussionId = likeCounts.ToDictionary(x => x.DiscussionId, x => x.Count);

            // Zjistíme, zda aktuální uživatel dal lajk daným diskuzím
            var userLikes = string.IsNullOrEmpty(userId)
                ? new Dictionary<int, bool>()  // Pokud uživatel není přihlášen, prázdný slovník
                : await dbContext.DiscussionLikes
                    .AsNoTracking()
                    .Where(l => discussionIds.Contains(l.DiscussionId) && l.UserId == userId)
                    .Select(l => l.DiscussionId)
                    .Distinct()
                    .ToDictionaryAsync(id => id, id => true);

            // 7. KROK: Mapování diskuzí na DTO objekty
            // --------------------------------------
            var discussionDtos = discussions
                .OrderByDescending(d => d.Type == DiscussionType.Top)  // Znovu řazení pro konzistenci
                .ThenByDescending(d => d.UpdatedAt)
                .Select(d => new DiscussionListDto
                {
                    Id = d.Id,
                    Title = d.Title,
                    CategoryName = d.Category.Name,
                    CategoryCode = d.Category.Code,
                    AuthorNickname = d.User.Nickname,
                    CreatedAt = d.CreatedAt,
                    UpdatedAt = d.UpdatedAt,
                    // Efektivní přístup k počtu komentářů
                    CommentsCount = commentCountByDiscussionId.TryGetValue(d.Id, out var count) ? count : 0,
                    ViewCount = d.ViewCount,
                    Type = d.Type,
                    Code = d.Code,
                    VoteType = d.VoteType,
                    // Informace o lajcích
                    Likes = new LikeInfoDto
                    {
                        LikeCount = likeCountByDiscussionId.TryGetValue(d.Id, out var likeCount) ? likeCount : 0,
                        HasUserLiked = userLikes.ContainsKey(d.Id),
                        CanUserLike = isAdmin ||
                            (!string.IsNullOrEmpty(userId) &&
                            d.UserId != userId &&
                            !userLikes.ContainsKey(d.Id))
                    }
                })
                .ToList();

            // 8. KROK: Vytvoření a vrácení výsledku
            // -----------------------------------
            var result = new PagedResultDto<DiscussionListDto>
            {
                Items = discussionDtos,
                TotalCount = totalCount,
                PageSize = pageSize,
                CurrentPage = page
            };

            return Ok(result);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce GetDiscussions endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vrátí netrackovaný seznam diskuzí požadovaného uživatele.
    /// Respektuje viditelnost obsahu podle typu uživatele a přátelství.
    /// Pokud není zadán nickname, vrátí diskuze přihlášeného uživatele.
    /// </summary>
    /// <param name="nickname">Přezdívka uživatele, jehož diskuze chceme zobrazit (volitelný parametr)</param>
    /// <returns>Seznam základních informací o diskuzích uživatele</returns>
    [HttpGet("user-discussions/{nickname?}")]
    public async Task<ActionResult<IEnumerable<BasicDiscussionInfoDto>>> GetUserDiscussions(string? nickname = null)
    {
        string userId = String.Empty;
        bool isMyAccount = false;
        bool isAdmin = false;
        try
        {
            // Zjistím, jestli je uživatel přihlášen a pokud ano, zjistím jeho id a jestli je admin
            string? userIdFromAutentization = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdFromAutentization != null)
            {
                isAdmin = User?.IsInRole("Admin") ?? false;
            }

            string? userIdFromNickname = null;

            // Pokud nickname je zadán, najdu userId dle nickname
            if (!string.IsNullOrEmpty(nickname))
            {
                var user = dbContext.Users.Where(u => u.Nickname == nickname).FirstOrDefault();
                if (user == null)
                    return NotFound();
                userIdFromNickname = user.Id;
            }

            // Rozhodneme, jestli se jedná o vlastní profil nebo cizí profil
            // Koukám na svůj profil - pokud jsem přihlášen a buď:
            // - neuvedl jsem nickname, nebo
            // - uvedl jsem nickname a je to můj nickname
            if ((userIdFromAutentization != null) && ((userIdFromAutentization == userIdFromNickname) || (userIdFromNickname == null)))
            {
                userId = userIdFromAutentization;
                isMyAccount = true;
            }
            else
            // Koukám na cizí profil (nebo na svůj a nejsem přihlášen),
            // v tom případě musí být vyplněn nickname, abych věděl na jaký koukám
            {
                if (userIdFromNickname == null)
                    return Unauthorized();
                userId = userIdFromNickname;
                isMyAccount = false;
            }

            // Získání seznamu ID přátel přihlášeného uživatele (pokud je uživatel přihlášen)
            // Důležité pro filtrování privátních diskuzí, které vidí jen přátelé autora
            var friendIds = new List<string>();
            if (!string.IsNullOrEmpty(userIdFromAutentization))
            {
                // Vyhledání schválených přátelství, kde je uživatel buď žadatelem nebo schvalovatelem
                friendIds = await dbContext.Friendships
                    .AsNoTracking()
                    .Where(f => (f.ApproverUserId == userIdFromAutentization || f.RequesterUserId == userIdFromAutentization) &&
                               f.FriendshipStatus == FriendshipStatus.Approved)
                    .Select(f => f.ApproverUserId == userIdFromAutentization ? f.RequesterUserId : f.ApproverUserId)
                    .ToListAsync();
            }

            // Získání diskuzí uživatele a seřazené podle data aktualizace
            // Respektujeme pravidla viditelnosti diskuzí:
            // - Smazané diskuze nikdo nevidí
            // - Hidden diskuze vidí jen autor sám nebo admin
            // - Private diskuze vidí autor, admin nebo přátelé autora
            var discussions = await dbContext.Discussions
                .AsNoTracking()
                .Where(d => d.UserId == userId) // Filtrujeme diskuze daného uživatele
                .Where(d => d.Type != DiscussionType.Deleted) // Smazané diskuze nevidí nikdo
                .Where(d => d.Type != DiscussionType.Hidden || isMyAccount || isAdmin) // Hidden diskuze vidí jen autor nebo admin
                .Where(d => d.Type != DiscussionType.Private || isMyAccount || isAdmin || friendIds.Contains(d.UserId)) // Private diskuze vidí autor, admin nebo přátelé autora
                .Include(d => d.Category) // Načteme i kategorii pro zobrazení
                .OrderByDescending(d => d.UpdatedAt) // Řazení od nejnovějších
                .Select(d => new BasicDiscussionInfoDto
                {
                    Id = d.Id,
                    Title = d.Title,
                    Content = d.Content,
                    ImagePath = d.ImagePath,
                    CategoryName = d.Category.Name,
                    CategoryId = d.CategoryId,
                    CategoryCode = d.Category.Code,
                    DiscussionCode = d.Code,
                    AuthorNickname = d.User.Nickname,
                    AuthorId = d.UserId,
                    CreatedAt = d.CreatedAt,
                    UpdatedAt = d.UpdatedAt,
                    ViewCount = d.ViewCount,
                    Type = d.Type,
                    VoteType = d.VoteType
                })
                .ToListAsync();

            return Ok(discussions);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce GetUserDiscussions endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Načte detail diskuze včetně stránkovaných komentářů.
    /// Optimalizovaná verze používající Split Queries a přímé stránkování v SQL dotazu.
    /// </summary>
    /// <param name="discussionId">ID diskuze</param>
    /// <param name="page">Číslo stránky (číslováno od 1)</param>
    /// <param name="pageSize">Počet komentářů na stránku</param>
    /// <returns>Detail diskuze nebo NotFound, pokud diskuze neexistuje nebo není přístupná</returns>
    [HttpGet("{discussionId}")]
    public async Task<ActionResult<DiscussionDetailDto>> GetDiscussion(int discussionId, int page = 1, int pageSize = 10)
    {
        try
        {
            // Validace vstupních parametrů
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 100) pageSize = 100; // Omezení maximální velikosti stránky

            // Získání ID a role přihlášeného uživatele pro filtrování obsahu dle oprávnění
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            // Načtení aktuálního uživatele pro kontrolu nových odpovědí - děláme to pouze jednou
            // pro celý request, abychom nemuseli volat userManager pro každý komentář zvlášť
            ApplicationUser? currentUser = null;
            if (!string.IsNullOrEmpty(userId))
            {
                currentUser = await userManager.FindByIdAsync(userId);
            }

            // 1. KROK: Načtení základních dat diskuze
            // -------------------------------------------
            // Načítáme pouze diskuzi a její přímo související entity (kategorie, autor, lajky)
            // Komentáře načteme samostatným dotazem
            var discussion = await dbContext.Discussions
                .AsNoTracking() // Netrackujeme změny - zvýšení výkonu
                .Include(d => d.Category) // Kategorie pro zobrazení názvu
                .Include(d => d.User)     // Autor pro zobrazení nicknamu
                .Include(d => d.Likes)    // Lajky pro zobrazení počtu a kontrolu uživatelových lajků
                                          // Filtrování diskuzí dle oprávnění uživatele
                .FirstOrDefaultAsync(d => d.Id == discussionId &&
                    d.Type != DiscussionType.Deleted &&     // Nikdy nezobrazujeme smazané diskuze
                    (d.Type != DiscussionType.Hidden ||     // Pro skryté diskuze kontrolujeme:
                        isAdmin ||                          // - buď je uživatel admin (vidí vše)
                        d.UserId == userId));               // - nebo je autorem této diskuze

            // Kontrola existence diskuze
            if (discussion == null)
                return NotFound();

            // 2. KROK: Načtení počtu root komentářů pro stránkování
            // -----------------------------------------------------
            // Zjistíme celkový počet viditelných root komentářů pro výpočet stránkování
            var totalRootComments = await dbContext.Comments
                .AsNoTracking()
                .Where(c => c.DiscussionId == discussionId)        // Komentáře patřící k této diskuzi
                .Where(c => c.ParentCommentId == null)             // Pouze root komentáře (ne odpovědi)
                .Where(c => c.Type != CommentType.Deleted &&       // Nekompletně smazané
                    (c.Type != CommentType.Hidden ||               // Skryté zobrazit jen pro:
                        isAdmin ||                                 // - adminy
                        c.UserId == userId))                       // - autory komentáře
                .CountAsync();

            // 3. KROK: Načtení stránkovaných root komentářů
            // ---------------------------------------------
            // Načteme pouze root komentáře pro aktuální stránku (efektivní stránkování v SQL)
            var rootComments = await dbContext.Comments
                .AsNoTracking()
                .Where(c => c.DiscussionId == discussionId)        // Komentáře patřící k této diskuzi
                .Where(c => c.ParentCommentId == null)             // Pouze root komentáře (ne odpovědi)
                .Where(c => c.Type != CommentType.Deleted &&       // Nekompletně smazané
                    (c.Type != CommentType.Hidden ||               // Skryté zobrazit jen pro:
                        isAdmin ||                                 // - adminy
                        c.UserId == userId))                       // - autory komentáře
                .OrderByDescending(c => c.UpdatedAt)               // Seřadit dle data aktualizace
                .Skip((page - 1) * pageSize)                       // Stránkování - přeskočit předchozí stránky
                .Take(pageSize)                                    // Stránkování - vzít pouze pageSize záznamů
                                                                   // Načtení dat potřebných pro každý root komentář
                .Include(c => c.User)                              // Autor komentáře
                .Include(c => c.Likes)                             // Lajky komentáře
                .AsSplitQuery()                                    // Rozdělení na více SQL dotazů pro vyšší výkon
                .ToListAsync();

            // 4. KROK: Načtení odpovědí na root komentáře
            // -------------------------------------------
            // Získáme pole ID root komentářů pro efektivní filtrování
            var rootCommentIds = rootComments.Select(c => c.Id).ToArray();

            // Načteme odpovědi pouze pro root komentáře na aktuální stránce
            var replies = await dbContext.Comments
                .AsNoTracking()
                .Where(c => c.DiscussionId == discussionId)        // Komentáře patřící k této diskuzi
                .Where(c => c.ParentCommentId != null)             // Pouze odpovědi (ne root komentáře)
                .Where(c => c.ParentCommentId != null && rootCommentIds.Contains(c.ParentCommentId.Value)) // Pouze odpovědi na načtené root komentáře
                .Where(c => c.Type != CommentType.Deleted &&       // Nekompletně smazané
                    (c.Type != CommentType.Hidden ||               // Skryté zobrazit jen pro:
                        isAdmin ||                                 // - adminy
                        c.UserId == userId))                       // - autory komentáře
                                                                   // Načtení dat potřebných pro každou odpověď
                .Include(c => c.User)                              // Autor odpovědi
                .Include(c => c.Likes)                             // Lajky odpovědi
                .AsSplitQuery()                                    // Rozdělení na více SQL dotazů pro vyšší výkon
                .ToListAsync();

            // 5. KROK: Přiřazení odpovědí k root komentářům
            // ---------------------------------------------
            // Pro každý root komentář přiřadíme jeho odpovědi
            // Vytvoříme slovník s odpověďmi seskupenými podle ID rodičovského komentáře
            Dictionary<int, List<Comment>> repliesByParentId = replies
                .GroupBy(r => r.ParentCommentId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Přiřadíme odpovědi k jejich root komentářům
            foreach (var rootComment in rootComments)
            {
                // Efektivní vyhledání odpovědí pomocí slovníku
                if (repliesByParentId.TryGetValue(rootComment.Id, out var commentReplies))
                {
                    rootComment.Replies = commentReplies;
                }
            }

            // 6. KROK: Mapování dat do DTO objektu
            // -----------------------------------
            // Vytvoříme výsledné DTO s daty, která pošleme klientovi
            var result = new DiscussionDetailDto
            {
                // Základní údaje o diskuzi
                Id = discussion.Id,
                Title = discussion.Title,
                Content = discussion.Content,
                ImagePath = discussion.ImagePath,
                CategoryName = discussion.Category.Name,
                CategoryCode = discussion.Category.Code,
                DiscussionCode = discussion.Code,
                CategoryId = discussion.CategoryId,
                AuthorNickname = discussion.User.Nickname,
                AuthorId = discussion.UserId,
                CreatedAt = discussion.CreatedAt,
                UpdatedAt = discussion.UpdatedAt,
                ViewCount = discussion.ViewCount,
                Type = discussion.Type,
                VoteType = discussion.VoteType,

                // Informace o lajcích
                Likes = new LikeInfoDto
                {
                    LikeCount = discussion.Likes.Count,            // Celkový počet lajků
                    HasUserLiked = discussion.Likes                // Informace, zda uživatel již lajkoval
                        .Any(l => l.UserId == userId),
                    CanUserLike = isAdmin ||                       // Může lajkovat pokud:
                        (!string.IsNullOrEmpty(userId) &&          // - je přihlášen
                         discussion.UserId != userId &&            // - není autor
                         !discussion.Likes                         // - ještě nelajkoval
                            .Any(l => l.UserId == userId))
                },

                // Komentáře - předáváme načteného uživatele do mapování pro kontrolu nových odpovědí
                Comments = rootComments
                    .Select(c => MapCommentToDto(c, userId, isAdmin, currentUser))
                    .ToList(),

                // Informace o stránkování - máme další komentáře?
                HasMoreComments = totalRootComments > (page * pageSize)
            };

            return Ok(result);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce GetDiscussion endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Načte netrackovaný kompletní detail diskuze podle jejího URL-friendly kódu včetně všech souvisejících dat jako jsou komentáře, odpovědi, lajky a informace o autorech.
    /// Endpoint lze použít pro zobrazení diskuze, komentářů a lajků s respektováním oprávnění přihlášeného uživatele.
    /// </summary>
    /// <param name="code">URL-friendly kód diskuze</param>
    /// <returns>Detail diskuze nebo NotFound, pokud diskuze neexistuje nebo není přístupná</returns>
    [HttpGet("byCode/{code}")]
    public async Task<ActionResult<DiscussionDetailDto>> GetDiscussionByCode(string code, int page, int pageSize)
    {
        try
        {
            var discussionId = await dbContext.Discussions
                    .AsNoTracking()
                    .Where(d => d.Code == code)
                    .Select(d => d.Id)
                    .FirstOrDefaultAsync();

            if (discussionId == 0)  // diskuze s daným kódem neexistuje
                return NotFound();

            return await GetDiscussion(discussionId, page, pageSize);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce GetDiscussionByCode endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vrátí základní informace o diskuzi podle jejího ID bez načítání komentářů.
    /// Obsahuje metadata jako titulek, obsah, informace o kategorii a autorovi,
    /// ale neobsahuje komentáře ani další náročná data.
    /// </summary>
    /// <param name="discussionId">ID diskuze</param>
    /// <returns>Základní informace o diskuzi nebo NotFound</returns>
    [HttpGet("{discussionId}/basic-info")]
    public async Task<ActionResult<BasicDiscussionInfoDto>> GetBasicDiscussionInfo(int discussionId)
    {
        try
        {
            // Pro přihlášené uživatele získání ID a role
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            // Efektivní načtení diskuze s minimem souvisejících dat
            var discussion = await dbContext.Discussions
                .AsNoTracking()
                .Include(d => d.Category)
                .Include(d => d.User)
                .Where(d => d.Id == discussionId &&
                       d.Type != DiscussionType.Deleted &&
                       (d.Type != DiscussionType.Hidden || isAdmin || d.UserId == userId))
                .Select(d => new BasicDiscussionInfoDto
                {
                    Id = d.Id,
                    Title = d.Title,
                    Content = d.Content,
                    ImagePath = d.ImagePath,
                    CategoryName = d.Category.Name,
                    CategoryId = d.CategoryId,
                    CategoryCode = d.Category.Code,
                    DiscussionCode = d.Code,
                    AuthorNickname = d.User.Nickname,
                    AuthorId = d.UserId,
                    CreatedAt = d.CreatedAt,
                    UpdatedAt = d.UpdatedAt,
                    ViewCount = d.ViewCount,
                    Type = d.Type,
                    VoteType = d.VoteType
                })
                .FirstOrDefaultAsync();

            if (discussion == null)
            {
                _logger.Log($"Diskuze s ID {discussionId} nebyla nalezena.");
                return NotFound();
            }

            return Ok(discussion);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce GetBasicDiscussionInfo endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vrátí základní informace o diskuzi podle jejího URL kódu přesměrováním na endpoint podle ID.
    /// </summary>
    /// <param name="code">URL kód diskuze</param>
    /// <returns>Základní informace o diskuzi nebo NotFound</returns>
    [HttpGet("basic-info/by-code/{code}")]
    public async Task<ActionResult<BasicDiscussionInfoDto>> GetBasicDiscussionInfoByCode(string code)
    {
        try
        {
            // Nejprve zjistíme ID diskuze podle kódu
            var discussionId = await dbContext.Discussions
                .AsNoTracking()
                .Where(d => d.Code == code)
                .Select(d => d.Id)
                .FirstOrDefaultAsync();

            if (discussionId == 0)  // ID 0 znamená, že diskuze nebyla nalezena
            {
                _logger.Log($"Diskuze s kódem {code} nebyla nalezena.");
                return NotFound("Diskuze s daným kódem nebyla nalezena.");
            }

            // Přesměrování na endpoint podle ID
            return await GetBasicDiscussionInfo(discussionId);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce GetBasicDiscussionInfoByCode endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
    /// <summary>
    /// Vytvoří novou diskuzi
    /// Přístupné pouze pro přihlášené uživatele
    /// </summary>
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<DiscussionDetailDto>> CreateDiscussion(CreateDiscussionDto model)
    {
        try
        {
            // Ověříme, zda kategorie existuje
            var category = await dbContext.Categories.FindAsync(model.CategoryId);
            if (category == null)
                return BadRequest("Zvolená kategorie neexistuje.");

            // Získáme ID přihlášeného uživatele z tokenu
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Získáme detaily uživatele z databáze dle userId aktualně přihlášeného uživatele
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
                return Unauthorized();

            // Sanitizace vstupů
            var sanitizedTitle = _sanitizer.Sanitize(model.Title);
            var sanitizedContent = _sanitizer.Sanitize(model.Content);

            // Vygenerujeme URL-friendly kód
            var baseCode = UrlHelper.GenerateUrlFriendlyCode(sanitizedTitle);
            var suffix = UrlHelper.GenerateUniqueSuffix();
            var code = $"{baseCode}-{suffix}";


            var discussion = new Discussion
            {
                CategoryId = model.CategoryId,
                UserId = userId,
                Title = model.Title,
                Content = model.Content,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Type = user.Type == UserType.Hidden ? DiscussionType.Hidden : model.Type,
                Code = code,
                VoteType = model.VoteType
            };
            dbContext.Discussions.Add(discussion);
            await dbContext.SaveChangesAsync();

            // Vrátíme detail vytvořené diskuze
            return CreatedAtAction(nameof(GetDiscussion), new { discussionId = discussion.Id }, null);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce CreateDiscussion endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Upraví existující diskuzi
    /// Přístupné pouze pro autora diskuze nebo admina
    /// </summary>
    [Authorize]
    [HttpPut("{discussionId}")]
    public async Task<IActionResult> UpdateDiscussion(int discussionId, UpdateDiscussionDto model)
    {
        try
        {
            // Automatická validace modelu na základě anotací
            if (!ModelState.IsValid)
            {
                // Vrátíme chyby validace klientovi
                return BadRequest(ModelState);
            }

            var discussion = await dbContext.Discussions.FindAsync(discussionId);
            if (discussion == null)
                return NotFound();

            // Kontrola oprávnění - může editovat pouze autor nebo admin
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin && discussion.UserId != userId)
                return Forbid();

            // Přidaná sanitizace
            discussion.Title = _sanitizer.Sanitize(model.Title);
            discussion.Content = _sanitizer.Sanitize(model.Content);
            discussion.UpdatedAt = DateTime.UtcNow;
            // Typ diskuze může měnit pouze admin
            if (isAdmin)
            {
                discussion.Type = model.Type;
            }

            await dbContext.SaveChangesAsync();
            return NoContent();
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce UpdateDiscussion endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Přepne typ diskuze mezi Normal a Top
    /// Přístupné pouze pro adminy
    /// </summary>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPut("{discussionId}/toggle-top")]
    public async Task<IActionResult> ToggleDiscussionTop(int discussionId)
    {
        try
        {
            var discussion = await dbContext.Discussions.FindAsync(discussionId);
            if (discussion == null)
                return NotFound();

            // Přepnutí typu mezi Normal a Top je možné jen pro tyto dva stavy
            if (discussion.Type != DiscussionType.Normal && discussion.Type != DiscussionType.Top)
                return BadRequest("Nelze měnit TOP status pro tento typ diskuze.");

            // Přepnutí typu mezi Normal a Top
            discussion.Type = discussion.Type == DiscussionType.Normal
                ? DiscussionType.Top
                : DiscussionType.Normal;

            discussion.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();

            return Ok(new { type = discussion.Type });
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce ToggleDiscussionTop endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Přepne typ diskuze mezi Normal a Private
    /// Přístupné pouze pro adminy
    /// </summary>
    [HttpPut("{discussionId}/toggle-private")]
    public async Task<IActionResult> ToggleDiscussionPrivate(int discussionId)
    {
        try
        {
            var discussion = await dbContext.Discussions.FindAsync(discussionId);
            if (discussion == null)
                return NotFound();

            // Kontrola oprávnění - může editovat pouze autor nebo admin
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && discussion.UserId != userId)
                return Forbid();

            // Přepnutí typu mezi Normal a Top je možné jen pro tyto dva stavy
            if (discussion.Type != DiscussionType.Normal && discussion.Type != DiscussionType.Private)
                return BadRequest("Nelze měnit Private status pro tento typ diskuze.");

            // Přepnutí typu mezi Normal a Top
            discussion.Type = discussion.Type == DiscussionType.Normal
                ? DiscussionType.Private
                : DiscussionType.Normal;

            discussion.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();

            return Ok(new { type = discussion.Type });
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce ToggleDiscussionPrivate endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
    /// <summary>
    /// Smaže diskuzi a všechny její komentáře
    /// Přístupné pouze pro adminy
    /// </summary>
    [Authorize]
    [HttpDelete("{discussionId}")]
    public async Task<IActionResult> DeleteDiscussion(int discussionId)
    {
        try
        {
            var discussion = await dbContext.Discussions.FindAsync(discussionId);
            if (discussion == null)
                return NotFound();

            // Kontrola oprávnění - může smazat jen admin nebo autor
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && discussion.UserId != userId)
                return Forbid();

            // Nastavíme diskuzi jako smazanou (typ Deleted)
            discussion.Type = DiscussionType.Deleted;
            discussion.UpdatedAt = DateTime.UtcNow;

            // Smažeme i všechny její komentáře (nastavíme na typ Deleted)
            var comments = await dbContext.Comments
                .Where(c => c.DiscussionId == discussionId)
                .ToListAsync();

            foreach (var comment in comments)
            {
                comment.Type = CommentType.Deleted;
                comment.UpdatedAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync();
            return NoContent();
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce DeleteDiscussion endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Přidá nový komentář k diskuzi
    /// Přístupné pouze pro přihlášené uživatele
    /// </summary>
    [Authorize]
    [HttpPost("{discussionId}/comments")]
    public async Task<ActionResult<CommentDto>> CreateComment(int discussionId, CreateCommentDto model)
    {
        try
        {
            return await CreateCommentOrReply(discussionId, null, model);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce CreateComment endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Přidá reakci na existující komentář
    /// Přístupné pouze pro přihlášené uživatele
    /// </summary>
    [Authorize]
    [HttpPost("{discussionId}/comments/{commentId}/replies")]
    public async Task<ActionResult<CommentDto>> CreateReply(int discussionId, int commentId, CreateCommentDto model)
    {
        try
        {
            return await CreateCommentOrReply(discussionId, commentId, model);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce CreateReply endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Přesune diskuzi do jiné kategorie. Pouze pro administrátory.
    /// </summary>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPut("{discussionId}/change-category/{newCategoryId}")]
    public async Task<IActionResult> ChangeDiscussionCategory(int discussionId, int newCategoryId)
    {
        try
        {
            // Najdeme diskuzi
            var discussion = await dbContext.Discussions.FindAsync(discussionId);
            if (discussion == null)
            {
                _logger.Log($"Při pokusu o přesunutí diskuze {discussionId} do kategorie {newCategoryId} nebyla daná diskuze nalezena.");
                return NotFound("Diskuze nebyla nalezena.");
            }

            // Ověříme, zda nová kategorie existuje
            var newCategory = await dbContext.Categories.FindAsync(newCategoryId);
            if (newCategory == null)
            {
                _logger.Log($"Pokus o přesunutí diskuze {discussionId} do neexistující kategorie {newCategoryId}.");
                return NotFound("Cílová kategorie nebyla nalezena.");
            }

            // Uložíme původní kategorii pro odpověď
            var oldCategoryId = discussion.CategoryId;

            // Změníme kategorii diskuze
            discussion.CategoryId = newCategoryId;
            //discussion.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();

            // Vrátíme informace o přesunu
            return Ok(new
            {
                oldCategoryId,
                newCategoryId,
                updatedAt = discussion.UpdatedAt
            });
        }
        catch (Exception)
        {
            _logger.Log($"Chyba při přesunu diskuze {discussionId} do kategorie {newCategoryId}.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Pomocná metoda pro vytvoření komentáře nebo reakce na komentář.
    /// Společná logika pro vytvoření root komentáře i odpovědi.
    /// </summary>
    /// <param name="discussionId">ID diskuze</param>
    /// <param name="parentCommentId">ID rodičovského komentáře (null pro root komentář)</param>
    /// <param name="model">Data pro vytvoření komentáře</param>
    /// <returns>Vytvořený komentář nebo chybový stav</returns>
    private async Task<ActionResult<CommentDto>> CreateCommentOrReply(int discussionId, int? parentCommentId, CreateCommentDto model)
    {
        // Získáme detaily přihlášeného uživatele
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();
        var isAdmin = User.IsInRole("Admin");
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return Unauthorized();

        // Získáme diskuzi, ke které komentář bude patřit
        var discussion = await dbContext.Discussions.FindAsync(discussionId);
        if (discussion == null || discussion.Type == DiscussionType.Deleted)
            return NotFound("Diskuze neexistuje.");



        if (parentCommentId != null) // Jedná se o vytváření reakčního komentáře
        {
            // Ověříme existenci rodičovského komentáře
            var parentComment = await dbContext.Comments.FindAsync(parentCommentId);
            if (parentComment == null || parentComment.Type == CommentType.Deleted)
                return NotFound("Rodičovský komentář neexistuje.");


            // Ověříme, že rodičovský komentář patří k této diskuzi
            if (parentComment.DiscussionId != discussionId)
                return BadRequest("Komentář nepatří k této diskuzi.");

            // Ověříme, že rodičovský komentář je root komentář
            if (parentComment.ParentCommentId != null)
                return BadRequest("Lze reagovat pouze na hlavní komentáře.");

            // Aktualizujeme datum vytvoření rodičovského komentáře
            parentComment.UpdatedAt = DateTime.UtcNow;
        }

        // Sanitizace vstupů
        var sanitizedContent = _sanitizer.Sanitize(model.Content);

        var comment = new Comment
        {
            DiscussionId = discussionId,
            ParentCommentId = parentCommentId,
            UserId = userId,
            Content = _sanitizer.Sanitize(model.Content), //Pro zamezení XSS útoků
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Type = user.Type == UserType.Hidden ? CommentType.Hidden : model.Type
        };

        dbContext.Comments.Add(comment);
        await dbContext.SaveChangesAsync();

        // Načteme vytvořený komentář včetně uživatele pro správné mapování
        comment = await dbContext.Comments
            .Include(c => c.User)
            .FirstAsync(c => c.Id == comment.Id);

        return Ok(MapCommentToDto(comment, userId, isAdmin));
    }

    /// <summary>
    /// Smaže komentář a všechny jeho reakce
    /// </summary>
    [Authorize]
    [HttpDelete("{discussionId}/comments/{commentId}")]
    public async Task<IActionResult> DeleteComment(int discussionId, int commentId)
    {
        try
        {
            // Získáme ID přihlášeného uživatele
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.Log("Pokus o smazání komentáře neexistujícím uživatelem, který ale měl token...");
                return Unauthorized();
            }

            // Zjistíme, zda je uživatel admin
            var isAdmin = User.IsInRole("Admin");

            // Načteme komentář včetně odpovědí
            var comment = await dbContext.Comments
                .Include(c => c.Replies)
                .FirstOrDefaultAsync(c => c.Id == commentId && c.DiscussionId == discussionId);

            if (comment == null)
            {
                _logger.Log($"Pokus o smazání neexistujícího komentáře s ID {commentId}.");
                return NotFound();
            }

            // Kontrola oprávnění - smazat může pouze admin nebo autor komentáře
            if (!isAdmin && comment.UserId != userId)
            {
                _logger.Log($"Pokus o smazání komentáře s ID {commentId} uživatelem {userId}, který na to nemá právo.");
                return Forbid();
            }

            // Nastavíme komentář jako smazaný (typ Deleted)
            comment.Type = CommentType.Deleted;
            comment.UpdatedAt = DateTime.UtcNow;

            // Nastavíme i všechny reakce na smazané
            foreach (var reply in comment.Replies)
            {
                reply.Type = CommentType.Deleted;
                reply.UpdatedAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync();
            return NoContent();
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce DeleteComment endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Přidá like k diskuzi. Pro adminy není omezení počtu liků.
    /// Pro ostatní uživatele je možný jen jeden like na diskuzi.
    /// </summary>
    [Authorize]
    [HttpPost("{id}/like")]
    public async Task<ActionResult<LikeInfoDto>> AddDiscussionLike(int id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Najdeme diskuzi
            var discussion = await dbContext.Discussions
                .Include(d => d.Likes)
                .FirstOrDefaultAsync(d => d.Id == id &&
                    d.Type != DiscussionType.Deleted &&
                    (d.Type != DiscussionType.Hidden || isAdmin || d.UserId == userId));

            if (discussion == null)
                return NotFound();

            // Kontrola, zda už uživatel nedal like (přeskočíme pro adminy)
            if (!isAdmin && discussion.Likes.Any(l => l.UserId == userId))
                return BadRequest("Uživatel už dal této diskuzi like.");

            // Kontrola, zda uživatel nedává like své vlastní diskuzi
            if (!isAdmin && discussion.UserId == userId)
                return BadRequest("Nelze dát like vlastní diskuzi.");

            // Přidáme like
            var like = new DiscussionLike
            {
                DiscussionId = id,
                UserId = userId
            };

            dbContext.DiscussionLikes.Add(like);
            await dbContext.SaveChangesAsync();

            // Vrátíme aktuální stav liků
            return Ok(new LikeInfoDto
            {
                LikeCount = discussion.Likes.Count,
                HasUserLiked = true,
                CanUserLike = isAdmin  // Admin může dávat další liky
            });
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce AddDiscussionLike endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Přidá like ke komentáři. Pro adminy není omezení počtu liků.
    /// Pro ostatní uživatele je možný jen jeden like na komentář.
    /// </summary>
    [Authorize]
    [HttpPost("{discussionId}/comments/{commentId}/like")]
    public async Task<ActionResult<LikeInfoDto>> AddCommentLike(int discussionId, int commentId)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Najdeme komentář
            var comment = await dbContext.Comments
                .Include(c => c.Likes)
                .FirstOrDefaultAsync(c => c.Id == commentId &&
                    c.DiscussionId == discussionId &&
                    c.Type != CommentType.Deleted &&
                    (c.Type != CommentType.Hidden || isAdmin || c.UserId == userId));

            if (comment == null)
                return NotFound();

            // Kontrola, zda už uživatel nedal like (přeskočíme pro adminy)
            if (!isAdmin && comment.Likes.Any(l => l.UserId == userId))
                return BadRequest("Uživatel už dal tomuto komentáři like.");

            // Kontrola, zda uživatel nedává like svému vlastnímu komentáři (přeskočit pro adminy)
            if (!isAdmin && comment.UserId == userId)
                return BadRequest("Nelze dát like vlastnímu komentáři.");

            // Přidáme like
            var like = new CommentLike
            {
                CommentId = commentId,
                UserId = userId
            };

            dbContext.CommentLikes.Add(like);
            await dbContext.SaveChangesAsync();

            // Vrátíme aktuální stav liků
            return Ok(new LikeInfoDto
            {
                LikeCount = comment.Likes.Count,
                HasUserLiked = true,
                CanUserLike = isAdmin  // Admin může dávat další liky
            });
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce AddCommentLike endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Mapuje entitu Comment na DTO objekt CommentDto, včetně všech souvisejících dat
    /// </summary>
    /// <param name="comment">Entita komentáře s načtenými souvisejícími daty</param>
    /// <param name="userId">ID aktuálně přihlášeného uživatele (null pokud není přihlášen)</param>
    /// <param name="isAdmin">True pokud je přihlášený uživatel admin</param>
    /// <param name="currentUser">Již načtená entita přihlášeného uživatele (pro optimalizaci)</param>
    /// <returns>DTO objekt reprezentující komentář včetně oprávnění a vztahů</returns>
    private CommentDto MapCommentToDto(Comment comment, string? userId, bool isAdmin, ApplicationUser? currentUser = null)
    {
        // Zjistíme, zda aktuální uživatel už dal like tomuto komentáři
        var userLikes = comment.Likes.Any(l => l.UserId == userId);

        // Uživatel může dát like pokud:
        // - je admin (může dát neomezený počet liků)
        // - nebo je přihlášen, není autorem komentáře a ještě nedal like
        var canLike = isAdmin || (!string.IsNullOrEmpty(userId) &&
            comment.UserId != userId && !userLikes);

        // Zjištění, zda má komentář nové odpovědi od posledního přihlášení autora
        bool hasNewReplies = false;

        // Pouze pro rootové komentáře, kde je přihlášen autor komentáře
        if (comment.ParentCommentId == null && comment.UserId == userId && currentUser?.PreviousLastLogin != null)
        {
            // Kontrola nových odpovědí s již načteným uživatelem (optimalizace)
            hasNewReplies = comment.Replies
                .Any(r => r.CreatedAt > currentUser.PreviousLastLogin && r.UserId != userId);
        }

        return new CommentDto
        {
            Id = comment.Id,
            DiscussionId = comment.DiscussionId,
            Content = comment.Content,
            AuthorNickname = comment.User.Nickname,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt,
            ParentCommentId = comment.ParentCommentId,
            Type = comment.Type,
            // Informace o like pro tento komentář
            Likes = new LikeInfoDto
            {
                LikeCount = comment.Likes.Count,     // Celkový počet liků
                HasUserLiked = userLikes,            // Zda přihlášený uživatel dal like
                CanUserLike = canLike                // Zda může přihlášený uživatel dát like
            },
            // Indikátor nových odpovědí
            HasNewReplies = hasNewReplies,
            // Rekurzivně mapujeme odpovědi na tento komentář
            // Filtrujeme jen viditelné odpovědi pro aktuálního uživatele
            Replies = comment.Replies
                .Where(r => r.Type != CommentType.Deleted &&
                    (r.Type != CommentType.Hidden || isAdmin || r.UserId == userId))
                .Select(r => MapCommentToDto(r, userId, isAdmin, currentUser))
                .ToList()
        };
    }

    /// <summary>
    /// Přepne typ diskuze mezi Normal a Hidden
    /// Přístupné pouze pro adminy
    /// </summary>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPut("{discussionId}/toggle-visibility")]
    public async Task<IActionResult> ToggleDiscussionVisibility(int discussionId)
    {
        try
        {
            var discussion = await dbContext.Discussions.FindAsync(discussionId);
            if (discussion == null)
                return NotFound();

            // Přepnutí typu mezi Normal a Hidden
            discussion.Type = discussion.Type == DiscussionType.Normal
                ? DiscussionType.Hidden
                : DiscussionType.Normal;

            discussion.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();

            return Ok(new { type = discussion.Type });
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce ToggleDiscussionVisibility endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Přepne typ komentáře mezi Normal a Hidden
    /// Přístupné pouze pro adminy
    /// </summary>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPut("{discussionId}/comments/{commentId}/toggle-visibility")]
    public async Task<IActionResult> ToggleCommentVisibility(int discussionId, int commentId)
    {
        try
        {
            var comment = await dbContext.Comments
                .FirstOrDefaultAsync(c => c.Id == commentId && c.DiscussionId == discussionId);

            if (comment == null)
                return NotFound();

            // Přepnutí typu mezi Normal a Hidden
            comment.Type = comment.Type == CommentType.Normal
                ? CommentType.Hidden
                : CommentType.Normal;

            comment.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();

            return Ok(new { type = comment.Type });
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce ToggleCommentVisibility endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Atomicky inkrementuje počítadlo zobrazení diskuze.
    /// Používá ExecuteUpdateAsync pro přímou aktualizaci v databázi bez nutnosti načítání entity.
    /// </summary>
    /// <param name="discussionId">ID diskuze, u které se má zvýšit počítadlo</param>
    /// <returns>
    /// - Ok pokud byla diskuze nalezena a počítadlo úspěšně navýšeno
    /// - NotFound pokud diskuze s daným ID neexistuje
    /// </returns>
    /// {ApiBaseUrl}/discussions/{Discussion.Id}/increment-view-count",
    [HttpPost("{discussionId}/increment-view-count")]
    public async Task<IActionResult> IncrementViewCount(int discussionId)
    {
        try
        {
            // ExecuteUpdateAsync provede atomickou aktualizaci přímo v databázi
            // Vrací počet aktualizovaných řádků (0 pokud diskuze neexistuje, 1 pokud byla aktualizována)
            var updated = await dbContext.Discussions
                .Where(d => d.Id == discussionId)     // Najde diskuzi podle ID
                .ExecuteUpdateAsync(s => s.SetProperty(
                    d => d.ViewCount,                  // Vlastnost kterou aktualizujeme
                    d => d.ViewCount + 1               // Nová hodnota = současná hodnota + 1
                ));

            // Pokud nebyl aktualizován žádný řádek, diskuze neexistuje
            if (updated == 0)
                return NotFound();

            return Ok();
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce IncrementViewCount endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /* Pro správné fungování endpointu viz níže muselo být nad databází spuštěn script:
     -- Vytvoření slovníku
    CREATE text SEARCH DICTIONARY czech_spell (
	TEMPLATE=ispell,
	dictfile=czech,
	afffile=czech,
	stopwords=czech
    );
    CREATE TEXT SEARCH CONFIGURATION czech (COPY=english);
    ALTER TEXT SEARCH CONFIGURATION czech
	    ALTER MAPPING FOR word, asciiword WITH czech_spell, SIMPLE;

    -- Přidáme generovaný sloupec pro fulltextový vektor (A a B jsou váhy pro relevanci vyhledávání)
    ALTER TABLE "Discussions" ADD COLUMN "SearchVector" tsvector
       GENERATED ALWAYS AS (
	       setweight(to_tsvector('czech', coalesce("Title",'')), 'A') ||
	       setweight(to_tsvector('czech', coalesce("Content",'')), 'B')
       ) STORED;

    -- Vytvoříme GIN index pro rychlé vyhledávání
    CREATE INDEX IX_Discussions_SearchVector ON "Discussions" USING GIN ("SearchVector");
    */

    /// <summary>
    /// Vyhledává diskuze podle zadaných klíčových slov s podporou českého fulltextového vyhledávání
    /// </summary>
    /// <param name="query">Vyhledávací dotaz (jednotlivá slova oddělená mezerami)</param>
    /// <param name="limit">Maximální počet vrácených výsledků</param>
    /// <returns>Seznam diskuzí seřazených podle relevance</returns>
    [HttpGet("search")]
    public async Task<ActionResult<List<BasicDiscussionInfoDto>>> SearchDiscussions(string query, int limit = 10)
    {
        try
        {
            // Kontrola, zda byl zadán vyhledávací dotaz
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Vyhledávací dotaz je povinný");

            // Rozdělení dotazu na jednotlivá slova a odstranění příliš krátkých slov
            var searchTerms = query.Split(new[] { ' ', ',', ';', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(term => term.Length >= 2)
                .ToList();

            // Kontrola, zda zbyla nějaká slova po filtraci
            if (!searchTerms.Any())
                return BadRequest("Vyhledávací dotaz musí obsahovat alespoň jedno slovo delší než 1 znak");

            // Formátování dotazu pro tsquery - použití OR operátoru
            string formattedQuery = string.Join(" | ", searchTerms);

            // Získání ID přihlášeného uživatele a informace, zda je admin
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            // Seznam pro uložení výsledků prvního dotazu (ID a relevance)
            var relevantDiscussionsIds = new List<(int Id, float Score)>();

            // Krok 1: Nejprve získáme ID diskuzí s jejich skóre relevance ve správném pořadí
            // Použijeme přímý ADO.NET přístup k databázi
            var connection = dbContext.Database.GetDbConnection();
            try
            {
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                SELECT 
                    d.""Id"",
                    ts_rank(d.""SearchVector"", to_tsquery('czech', @query), 32) as relevance_score
                FROM ""Discussions"" d
                WHERE
                    d.""Type"" != @deletedType
                    AND (d.""Type"" != @hiddenType OR @isAdmin = TRUE OR d.""UserId"" = @userId)
                    AND (d.""Type"" != @privateType
                         OR @isAdmin = TRUE
                         OR d.""UserId"" = @userId
                         OR EXISTS (
                             SELECT 1 FROM ""Friendships"" f
                             WHERE ((f.""ApproverUserId"" = @userId AND f.""RequesterUserId"" = d.""UserId"")
                                    OR (f.""ApproverUserId"" = d.""UserId"" AND f.""RequesterUserId"" = @userId))
                                 AND f.""FriendshipStatus"" = @approvedStatus
                         )
                    )
                    AND d.""SearchVector"" @@ to_tsquery('czech', @query)
                ORDER BY relevance_score DESC
                LIMIT @limit";

                // Přidání parametrů pro zabezpečení proti SQL injection
                var queryParam = command.CreateParameter();
                queryParam.ParameterName = "@query";
                queryParam.Value = formattedQuery;
                command.Parameters.Add(queryParam);

                var deletedParam = command.CreateParameter();
                deletedParam.ParameterName = "@deletedType";
                deletedParam.Value = (int)DiscussionType.Deleted;
                command.Parameters.Add(deletedParam);

                var hiddenParam = command.CreateParameter();
                hiddenParam.ParameterName = "@hiddenType";
                hiddenParam.Value = (int)DiscussionType.Hidden;
                command.Parameters.Add(hiddenParam);

                var isAdminParam = command.CreateParameter();
                isAdminParam.ParameterName = "@isAdmin";
                isAdminParam.Value = isAdmin;
                command.Parameters.Add(isAdminParam);

                var userIdParam = command.CreateParameter();
                userIdParam.ParameterName = "@userId";
                userIdParam.Value = userId ?? string.Empty;
                command.Parameters.Add(userIdParam);

                var limitParam = command.CreateParameter();
                limitParam.ParameterName = "@limit";
                limitParam.Value = limit;
                command.Parameters.Add(limitParam);

                var privateTypeParam = command.CreateParameter();
                privateTypeParam.ParameterName = "@privateType";
                privateTypeParam.Value = (int)DiscussionType.Private;
                command.Parameters.Add(privateTypeParam);

                var approvedStatusParam = command.CreateParameter();
                approvedStatusParam.ParameterName = "@approvedStatus";
                approvedStatusParam.Value = (int)FriendshipStatus.Approved;
                command.Parameters.Add(approvedStatusParam);

                // Spuštění dotazu a zpracování výsledků
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    var score = reader.GetFloat(1);
                    relevantDiscussionsIds.Add((id, score));
                }
            }
            finally
            {
                // Ujistíme se, že spojení je uzavřeno
                if (connection.State == System.Data.ConnectionState.Open)
                    await connection.CloseAsync();
            }

            // Kontrola, zda byly nalezeny nějaké výsledky
            if (!relevantDiscussionsIds.Any())
                return Ok(new List<BasicDiscussionInfoDto>());

            // Krok 2: Získáme ID diskuzí jako seznam pro použití v dotazu IN
            var discussionIds = relevantDiscussionsIds.Select(x => x.Id).ToList();

            // Krok 3: Načteme kompletní data pro nalezené diskuze
            var discussions = await dbContext.Discussions
                .AsNoTracking()
                .Where(d => discussionIds.Contains(d.Id))
                .Select(d => new
                {
                    d.Id,
                    d.Title,
                    d.Content,
                    d.CategoryId,
                    d.CreatedAt,
                    d.UpdatedAt,
                    d.ViewCount,
                    d.Type,
                    d.Code,
                    d.UserId,
                    CategoryName = d.Category.Name,
                    CategoryCode = d.Category.Code,
                    AuthorNickname = d.User.Nickname,
                    d.VoteType
                })
                .ToListAsync();

            // Krok 4: Seřadíme podle původního pořadí z kroku 1
            var orderedResults = new List<BasicDiscussionInfoDto>();

            // Sestavení výsledků ve správném pořadí podle skóre relevance
            foreach (var (id, score) in relevantDiscussionsIds)
            {
                var discussion = discussions.FirstOrDefault(d => d.Id == id);
                if (discussion != null)
                {
                    orderedResults.Add(new BasicDiscussionInfoDto
                    {
                        Id = discussion.Id,
                        Title = discussion.Title,
                        // Oříznutí obsahu pro přehlednější zobrazení
                        Content = discussion.Content.Length > 300
                            ? discussion.Content.Substring(0, 300) + "..."
                            : discussion.Content,
                        CategoryName = discussion.CategoryName,
                        CategoryId = discussion.CategoryId,
                        CategoryCode = discussion.CategoryCode,
                        DiscussionCode = discussion.Code,
                        AuthorNickname = discussion.AuthorNickname,
                        AuthorId = discussion.UserId,
                        CreatedAt = discussion.CreatedAt,
                        UpdatedAt = discussion.UpdatedAt,
                        ViewCount = discussion.ViewCount,
                        Type = discussion.Type,
                        VoteType = discussion.VoteType
                    });
                }
            }

            // Vrácení seřazených výsledků
            return Ok(orderedResults);
        }
        catch (Exception e)
        {
            // Logování chyby
            _logger.Log("Chyba při vyhledávání diskuzí", e);

            // Vrácení chybové odpovědi
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vrací kombinovaný seznam nejnovějších diskuzí přátel a TOP diskuzí.
    /// Diskuze jsou seřazeny podle času aktualizace, nejnovější první.
    /// </summary>
    /// <param name="limit">Maximální počet vrácených diskuzí (výchozí hodnota 20)</param>
    /// <returns>Seznam diskuzí seřazený dle času aktualizace</returns>
    [Authorize]
    [HttpGet("combined-feed")]
    public async Task<ActionResult<List<DiscussionListDto>>> GetCombinedFeed(int limit = 20)
    {
        try
        {
            // Získání ID přihlášeného uživatele
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Získání seznamu ID přátel přihlášeného uživatele
            var friendIds = await dbContext.Friendships
                .Where(f => (f.ApproverUserId == userId || f.RequesterUserId == userId) &&
                       f.FriendshipStatus == FriendshipStatus.Approved)
                .Select(f => f.ApproverUserId == userId ? f.RequesterUserId : f.ApproverUserId)
                .ToListAsync();

            // Získat diskuze přátel (vytvořené přáteli)
            var friendsDiscussionsQuery = dbContext.Discussions
                .AsNoTracking()
                .Where(d => friendIds.Contains(d.UserId) &&
                       d.Type != DiscussionType.Deleted &&
                       d.Type != DiscussionType.Hidden &&
                       d.User.Type == UserType.Normal)
                .OrderByDescending(d => d.UpdatedAt);

            // Získat TOP diskuze (ne od přátel, ty už máme)
            var topDiscussionsQuery = dbContext.Discussions
                .AsNoTracking()
                .Where(d => d.Type == DiscussionType.Top &&
                       !friendIds.Contains(d.UserId) &&
                       d.UserId != userId &&
                       d.User.Type == UserType.Normal)
                .OrderByDescending(d => d.UpdatedAt)
                .Take(3);

            // Sloučení obou dotazů do jednoho seznamu
            var combinedDiscussionIds = await friendsDiscussionsQuery
                .Select(d => d.Id)
                .Union(topDiscussionsQuery.Select(d => d.Id))
                .Take(limit)
                .ToListAsync();

            // Pokud nemáme žádné výsledky, vrátíme prázdný seznam
            if (!combinedDiscussionIds.Any())
                return new List<DiscussionListDto>();

            // Načtení kompletních dat pro vybrané diskuze
            var discussions = await dbContext.Discussions
                .AsNoTracking()
                .Where(d => combinedDiscussionIds.Contains(d.Id))
                .Include(d => d.Category)
                .Include(d => d.User)
                .Include(d => d.Likes)
                .OrderByDescending(d => d.UpdatedAt)
                .ToListAsync();

            // Zjištění počtu komentářů pro každou diskuzi
            var commentCounts = await dbContext.Comments
                .AsNoTracking()
                .Where(c => combinedDiscussionIds.Contains(c.DiscussionId))
                .GroupBy(c => c.DiscussionId)
                .Select(g => new { DiscussionId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.DiscussionId, x => x.Count);

            // Mapování na DTO
            var result = discussions.Select(d => new DiscussionListDto
            {
                Id = d.Id,
                Title = d.Title,
                CategoryName = d.Category.Name,
                CategoryCode = d.Category.Code,
                AuthorNickname = d.User.Nickname,
                //AuthorId = d.UserId,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt,
                CommentsCount = commentCounts.TryGetValue(d.Id, out var count) ? count : 0,
                ViewCount = d.ViewCount,
                Type = d.Type,
                Code = d.Code,
                VoteType = d.VoteType,
                Likes = new LikeInfoDto
                {
                    LikeCount = d.Likes.Count,
                    HasUserLiked = d.Likes.Any(l => l.UserId == userId),
                    CanUserLike = d.UserId != userId && !d.Likes.Any(l => l.UserId == userId)
                }
            })
            .OrderByDescending(d => d.UpdatedAt)
            .Take(limit)
            .ToList();

            return Ok(result);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při načítání kombinovaného feedu diskuzí", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vrací seznam diskuzí, které uživatel označil jako "Líbí se mi"
    /// </summary>
    /// <param name="userId">ID uživatele</param>
    /// <param name="limit">Maximum vrácených diskuzí</param>
    /// <returns>Seznam diskuzí seřazený podle data lajku (nejnovější první)</returns>
    [HttpGet("user-liked/{userId}")]
    public async Task<ActionResult<IEnumerable<BasicDiscussionInfoDto>>> GetUserLikedDiscussions(string userId, [FromQuery] int limit = 3)
    {
        try
        {
            // Získání ID a role přihlášeného uživatele
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            // Ověření, že uživatel existuje
            var user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == userId && u.Type != UserType.Deleted);

            if (user == null)
                return NotFound();

            // Nejdřív získáme ID a datum lajkovaných diskuzí
            var likedDiscussionIds = await dbContext.DiscussionLikes
                .AsNoTracking()
                .Where(dl => dl.UserId == userId)
                .OrderByDescending(dl => dl.CreatedAt) // Seřadit podle data lajku (nejnovější první)
                .Take(limit)
                .Select(dl => dl.DiscussionId)
                .ToListAsync();

            // Pak načteme detaily diskuzí podle ID
            var likedDiscussions = await dbContext.Discussions
                .AsNoTracking()
                .Where(d => likedDiscussionIds.Contains(d.Id))
                .Where(d => d.Type != DiscussionType.Deleted) // Ignorovat smazané diskuze
                .Where(d => d.Type != DiscussionType.Hidden || isAdmin || d.UserId == currentUserId) // Skryté diskuze vidí jen admin nebo autor
                .Where(d => d.Type != DiscussionType.Private || isAdmin || d.UserId == currentUserId ||
                       dbContext.Friendships.Any(f =>
                           ((f.ApproverUserId == currentUserId && f.RequesterUserId == d.UserId) ||
                            (f.ApproverUserId == d.UserId && f.RequesterUserId == currentUserId)) &&
                           f.FriendshipStatus == FriendshipStatus.Approved))
                .Include(d => d.Category) // Include před Select
                .Include(d => d.User)     // Include před Select
                .ToListAsync();

            // Nyní mapujeme na DTO a zachováváme pořadí podle data lajku
            var result = likedDiscussionIds
                .Select(id => likedDiscussions.FirstOrDefault(d => d.Id == id))
                .Where(d => d != null) // Filtrujeme případné null hodnoty
                .Select(d => new BasicDiscussionInfoDto
                {
                    Id = d!.Id,
                    Title = d.Title,
                    Content = d.Content.Length > 100 ? d.Content.Substring(0, 100) + "..." : d.Content,
                    ImagePath = d.ImagePath,
                    CategoryName = d.Category.Name,
                    CategoryId = d.CategoryId,
                    CategoryCode = d.Category.Code,
                    DiscussionCode = d.Code,
                    AuthorNickname = d.User.Nickname,
                    AuthorId = d.UserId,
                    CreatedAt = d.CreatedAt,
                    UpdatedAt = d.UpdatedAt,
                    ViewCount = d.ViewCount,
                    Type = d.Type,
                    VoteType = d.VoteType
                })
                .ToList();

            return Ok(result);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při získávání lajkovaných diskuzí uživatele", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Aktualizuje cesty k obrázkům v obsahu diskuze
    /// </summary>
    /// <param name="discussionId">ID diskuze</param>
    /// <param name="oldPrefix">Původní prefix cesty k obrázkům (dočasný kód)</param>
    /// <param name="newPrefix">Nový prefix cesty k obrázkům (finální kód)</param>
    /// <returns>OK pokud aktualizace proběhla úspěšně</returns>
    [HttpPost("update-image-paths")]
    public async Task<IActionResult> UpdateImagePaths([FromBody] UpdateImagePathsModel model)
    {
        try
        {
            // Najít diskuzi podle ID
            var discussion = await dbContext.Discussions.FindAsync(model.DiscussionId);
            if (discussion == null)
            {
                return NotFound("Diskuze nebyla nalezena.");
            }

            // Kontrola oprávnění - pouze autor nebo admin může upravovat diskuzi
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin && discussion.UserId != userId)
            {
                return Forbid();
            }

            // Nahradit cesty k obrázkům v obsahu diskuze
            string oldPath = $"/uploads/discussions/{model.OldPrefix}/";
            string newPath = $"/uploads/discussions/{model.NewPrefix}/";

            // Použití Regex pro nahrazení všech výskytů cest
            string updatedContent = Regex.Replace(
                discussion.Content,
                Regex.Escape(oldPath),
                newPath,
                RegexOptions.IgnoreCase
            );

            // Aktualizace obsahu diskuze
            discussion.Content = updatedContent;

            // Uložení změn
            await dbContext.SaveChangesAsync();

            return Ok();
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při aktualizaci cest k obrázkům", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    // Třída pro model aktualizace cest k obrázkům
    public class UpdateImagePathsModel
    {
        public int DiscussionId { get; set; }
        public string OldPrefix { get; set; }
        public string NewPrefix { get; set; }
    }

    /// <summary>
    /// Vrátí seznam kódů všech diskuzí, které nejsou označeny jako smazané
    /// </summary>
    /// <returns>Seznam stringů reprezentujících kódy diskuzí</returns>
    [HttpGet("codes")]
    public async Task<ActionResult<IEnumerable<string>>> GetDiscussionCodes()
    {
        try
        {
            // Vytvoření efektivního dotazu, který vrátí pouze kódy diskuzí
            var discussionCodes = await dbContext.Discussions
                .AsNoTracking() // Pro lepší výkon, protože nepotřebujeme sledovat entity
                .Where(d => d.Type != DiscussionType.Deleted) // Pouze nesmazané diskuze
                .Select(d => d.Code) // Vybereme pouze vlastnost Code
                .ToListAsync();

            return Ok(discussionCodes);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce GetDiscussionCodes endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}