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
using System.ComponentModel.Design;


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


    public DiscussionsController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        this.dbContext = dbContext;
        this.userManager = userManager;
    }

    /// <summary>
    /// Načte seznam všech dostupných diskuzí s možností filtrování podle kategorie.
    /// Poskytuje základní informace o diskuzích včetně počtu komentářů a lajků.
    /// Respektuje viditelnost obsahu podle typu uživatele.
    /// </summary>
    /// <param name="categoryId">Volitelný parametr pro filtrování podle kategorie</param>
    /// <returns>Seznam diskuzí dle oprávnění přihlášeného uživatele</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DiscussionListDto>>> GetDiscussions(int? categoryId = null)
    {
        // Identifikace uživatele pro správné filtrování obsahu
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

        // Základní dotaz s eager loadingem souvisejících dat
        var query = dbContext.Discussions
            .Include(d => d.Category)        // Pro zobrazení názvu kategorie
            .Include(d => d.User)            // Pro zobrazení autora
            .Include(d => d.Comments)        // Pro počítání relevantních komentářů
            .Include(d => d.Likes)           // Pro informace o lajcích
                                             // Bezpečnostní filtry
            .Where(d => d.Type != DiscussionType.Deleted)  // Smazané diskuze nikdo nevidí
            .Where(d => d.Type != DiscussionType.Hidden || // Hidden diskuze vidí jen:
                isAdmin ||                                  // - admin
                d.UserId == userId);                       // - autor diskuze

        // Aplikace filtru podle kategorie, pokud je zadána
        if (categoryId.HasValue)
        {
            query = query.Where(d => d.CategoryId == categoryId.Value);
        }

        // Načtení dat s aplikovaným řazením a projekcí do DTO
        var discussions = await query
            // Řazení:
            .OrderByDescending(d => d.Type == DiscussionType.Top)  // 1. TOP diskuze
            .ThenByDescending(d => d.CreatedAt)                    // 2. Nejnovější první
                                                                   // Mapování na DTO přímo v databázovém dotazu
            .Select(d => new DiscussionListDto
            {
                Id = d.Id,
                Title = d.Title,
                CategoryName = d.Category.Name,
                AuthorNickname = d.User.Nickname,
                CreatedAt = d.CreatedAt,
                // Počítání relevantních komentářů s respektováním viditelnosti
                CommentsCount = d.Comments.Count(c =>
                    c.Type != CommentType.Deleted &&       // Ignorujeme smazané
                    (c.Type != CommentType.Hidden ||      // Hidden komentáře počítáme pro:
                        isAdmin ||                        // - adminy
                        c.UserId == userId)),            // - autory komentářů
                ViewCount = d.ViewCount,
                Type = d.Type,
                Code = d.Code,
                // Informace o lajcích
                Likes = new LikeInfoDto
                {
                    LikeCount = d.Likes.Count,           // Celkový počet lajků
                    HasUserLiked = d.Likes               // Zda přihlášený uživatel lajkoval
                        .Any(l => l.UserId == userId),
                    CanUserLike = isAdmin ||             // Může lajkovat pokud:
                        (!string.IsNullOrEmpty(userId) && // - je přihlášen
                         d.UserId != userId &&           // - není autor
                         !d.Likes                        // - ještě nelajkoval
                            .Any(l => l.UserId == userId))
                }
            })
            .ToListAsync();

        return Ok(discussions);
    }

    /// <summary>
    /// Načte kompletní detail diskuze podle ID včetně všech souvisejících dat.
    /// Endpoint lze použít pro zobrazení diskuze, komentářů a lajků s respektováním
    /// oprávnění přihlášeného uživatele.
    /// </summary>
    /// <param name="discussionId">ID diskuze</param>
    /// <returns>Detail diskuze nebo NotFound, pokud diskuze neexistuje nebo není přístupná</returns>
    [HttpGet("{discussionId}")]
    public async Task<ActionResult<DiscussionDetailDto>> GetDiscussion(int discussionId)
    {
        // Pro přihlášené uživatele získání ID a role přihlášeného uživatele pro případné dodání skrytého obsahu (pokud na to bude mít práva)
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

        // Načtení diskuze včetně všech souvisejících dat pomocí eager loading
        var discussion = await dbContext.Discussions
            .Include(d => d.Category)                    // Kategorie pro zobrazení názvu
            .Include(d => d.User)                        // Autor diskuze
            .Include(d => d.Likes)                       // Lajky diskuze
            .Include(d => d.Comments)                    // Všechny komentáře
                .ThenInclude(c => c.User)                // Autoři komentářů
            .Include(d => d.Comments)
                .ThenInclude(c => c.Likes)               // Lajky komentářů
            .Include(d => d.Comments)
                .ThenInclude(c => c.Replies)             // Odpovědi na komentáře
                    .ThenInclude(r => r.User)            // Autoři odpovědí
            .Include(d => d.Comments)
                .ThenInclude(c => c.Replies)
                    .ThenInclude(r => r.Likes)           // Lajky odpovědí
                                                         // Bezpečnostní filtr - přístup k diskuzi
            .FirstOrDefaultAsync(d => d.Id == discussionId &&   // Hledání podle ID
                d.Type != DiscussionType.Deleted &&      // Smazané nejsou dostupné nikomu
                (d.Type != DiscussionType.Hidden ||      // Hidden diskuze vidí:
                    isAdmin ||                           // - admin
                    d.UserId == userId));                // - autor diskuze

        if (discussion == null)
            return NotFound();

        // Filtrování komentářů podle oprávnění uživatele
        var filteredComments = discussion.Comments
            .Where(c => c.Type != CommentType.Deleted && // Smazané komentáře nevidí nikdo
                (c.Type != CommentType.Hidden ||         // Hidden komentáře vidí:
                    isAdmin ||                           // - admin
                    c.UserId == userId))                 // - autor komentáře
            .ToList();

        // Filtrování odpovědí na komentáře
        foreach (var comment in filteredComments)
        {
            comment.Replies = comment.Replies
                .Where(r => r.Type != CommentType.Deleted && // Smazané odpovědi nevidí nikdo
                    (r.Type != CommentType.Hidden ||         // Hidden odpovědi vidí:
                        isAdmin ||                           // - admin
                        r.UserId == userId))                 // - autor odpovědi
                .ToList();
        }

        // Mapování na DTO s respektováním oprávnění
        var result = new DiscussionDetailDto
        {
            Id = discussion.Id,
            Title = discussion.Title,
            Content = discussion.Content,
            ImagePath = discussion.ImagePath,
            CategoryName = discussion.Category.Name,
            AuthorNickname = discussion.User.Nickname,
            AuthorId = discussion.UserId,
            CreatedAt = discussion.CreatedAt,
            UpdatedAt = discussion.UpdatedAt,
            ViewCount = discussion.ViewCount,
            Type = discussion.Type,
            // Konfigurace lajků
            Likes = new LikeInfoDto
            {
                LikeCount = discussion.Likes.Count,            // Celkový počet lajků
                HasUserLiked = discussion.Likes                // Informace zda uživatel již lajkoval
                    .Any(l => l.UserId == userId),
                CanUserLike = isAdmin ||                       // Může lajkovat pokud:
                    (!string.IsNullOrEmpty(userId) &&          // - je přihlášen
                     discussion.UserId != userId &&            // - není autor
                     !discussion.Likes                         // - ještě nelajkoval
                        .Any(l => l.UserId == userId))
            },
            // Mapování komentářů - pouze root komentáře (bez rodičovského komentáře)
            Comments = filteredComments
                .Where(c => c.ParentCommentId == null)
                .Select(c => MapCommentToDto(c, userId, isAdmin))
                .ToList()
        };

        return Ok(result);
    }

    /// <summary>
    /// Načte detail diskuze podle jejího URL-friendly kódu včetně všech souvisejících dat
    /// jako jsou komentáře, odpovědi, lajky a informace o autorech.
    /// </summary>
    /// <param name="code">URL-friendly kód diskuze</param>
    /// <returns>Detail diskuze nebo NotFound, pokud diskuze neexistuje nebo není přístupná</returns>
    [HttpGet("byCode/{code}")]
    public async Task<ActionResult<DiscussionDetailDto>> GetDiscussionByCode(string code)
    {
        // Pro přihlášené uživatele získání ID a role přihlášeného uživatele pro případné dodání skrytého obsahu (pokud na to bude mít práva)
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

        // Načtení diskuze včetně všech souvisejících dat pomocí eager loading
        var discussion = await dbContext.Discussions
       .Include(d => d.Category)                    // Kategorie diskuze
       .Include(d => d.User)                        // Autor diskuze
       .Include(d => d.Likes)                       // Lajky diskuze
       .Include(d => d.Comments)                    // Komentáře
           .ThenInclude(c => c.User)                // Autoři komentářů
       .Include(d => d.Comments)
           .ThenInclude(c => c.Likes)               // Lajky komentářů
       .Include(d => d.Comments)
           .ThenInclude(c => c.Replies)             // Odpovědi na komentáře
               .ThenInclude(r => r.User)            // Autoři odpovědí
       .Include(d => d.Comments)
           .ThenInclude(c => c.Replies)
               .ThenInclude(r => r.Likes)           // Lajky odpovědí
                                                    // Filtrování přístupu k diskuzi:
       .FirstOrDefaultAsync(d => d.Code == code &&  // Hledání podle kódu
           d.Type != DiscussionType.Deleted &&      // Nikdy nezobrazovat smazané
           (d.Type != DiscussionType.Hidden ||      // Skryté zobrazit jen pro:
               isAdmin ||                           // - adminy
               d.UserId == userId));                // - autory diskuze

        if (discussion == null)
            return NotFound();

        // Filtrování komentářů podle viditelnosti
        var filteredComments = discussion.Comments
            .Where(c => c.Type != CommentType.Deleted && // Odstranění smazaných
                (c.Type != CommentType.Hidden ||         // Skryté zobrazit jen pro:
                    isAdmin ||                           // - adminy
                    c.UserId == userId))                 // - autory komentáře
            .ToList();

        // Filtrování odpovědí na komentáře podle viditelnosti
        foreach (var comment in filteredComments)
        {
            comment.Replies = comment.Replies
                .Where(r => r.Type != CommentType.Deleted && // Odstranění smazaných
                    (r.Type != CommentType.Hidden ||         // Skryté zobrazit jen pro:
                        isAdmin ||                           // - adminy
                        r.UserId == userId))                 // - autory odpovědi
                .ToList();
        }

        // Mapování entity na DTO objekt pro odpověď
        var result = new DiscussionDetailDto
        {
            Id = discussion.Id,
            Title = discussion.Title,
            Content = discussion.Content,
            ImagePath = discussion.ImagePath,
            CategoryName = discussion.Category.Name,
            AuthorNickname = discussion.User.Nickname,
            AuthorId = discussion.UserId,
            CreatedAt = discussion.CreatedAt,
            UpdatedAt = discussion.UpdatedAt,
            ViewCount = discussion.ViewCount,
            Type = discussion.Type,
            Likes = new LikeInfoDto
            {
                LikeCount = discussion.Likes.Count,      // Celkový počet lajků
                HasUserLiked = discussion.Likes          // Zda přihlášený dal lajk
               .Any(l => l.UserId == userId),
                CanUserLike = isAdmin ||                 // Může dát lajk pokud:
               (!string.IsNullOrEmpty(userId) &&         // - je přihlášen
                discussion.UserId != userId &&           // - není autor
                !discussion.Likes                        // - ještě nedal lajk
                   .Any(l => l.UserId == userId))
            },
            // Mapování pouze root komentářů (bez odpovědí)
            Comments = filteredComments
           .Where(c => c.ParentCommentId == null)   // Pouze root komentáře
           .Select(c => MapCommentToDto(c, userId, isAdmin))
           .ToList()
        };

        return Ok(result);
    }

    /// <summary>
    /// Vytvoří novou diskuzi
    /// Přístupné pouze pro přihlášené uživatele
    /// </summary>
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<DiscussionDetailDto>> CreateDiscussion(CreateDiscussionDto model)
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

        // Vygenerujeme URL-friendly kód
        var baseCode = UrlHelper.GenerateUrlFriendlyCode(model.Title);
        var suffix = UrlHelper.GenerateUniqueSuffix();
        var code = $"{baseCode}-{suffix}";

        var discussion = new Discussion
        {
            CategoryId = model.CategoryId,
            UserId = userId,
            Title = model.Title,
            Content = model.Content,
            CreatedAt = DateTime.UtcNow,
            Type = user.Type == UserType.Hidden ? DiscussionType.Hidden : model.Type,
            Code = code
        };
        dbContext.Discussions.Add(discussion);
        await dbContext.SaveChangesAsync();

        // Vrátíme detail vytvořené diskuze
        return CreatedAtAction(nameof(GetDiscussion), new { discussionId = discussion.Id }, null);
    }

    /// <summary>
    /// Upraví existující diskuzi
    /// Přístupné pouze pro autora diskuze nebo admina
    /// </summary>
    [Authorize]
    [HttpPut("{discussionId}")]
    public async Task<IActionResult> UpdateDiscussion(int discussionId, UpdateDiscussionDto model)
    {
        var discussion = await dbContext.Discussions.FindAsync(discussionId);
        if (discussion == null)
            return NotFound();

        // Kontrola oprávnění - může editovat pouze autor nebo admin
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

        if (!isAdmin && discussion.UserId != userId)
            return Forbid();

        discussion.Title = model.Title;
        discussion.Content = model.Content;
        discussion.UpdatedAt = DateTime.UtcNow;
        // Typ diskuze může měnit pouze admin
        if (isAdmin)
        {
            discussion.Type = model.Type;
        }

        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Smaže diskuzi a všechny její komentáře
    /// Přístupné pouze pro adminy
    /// </summary>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDiscussion(int id)
    {
        var discussion = await dbContext.Discussions.FindAsync(id);
        if (discussion == null)
            return NotFound();

        // Nastavíme diskuzi jako smazanou (typ Deleted)
        discussion.Type = DiscussionType.Deleted;
        discussion.UpdatedAt = DateTime.UtcNow;

        // Smažeme i všechny její komentáře (nastavíme na typ Deleted)
        var comments = await dbContext.Comments
            .Where(c => c.DiscussionId == id)
            .ToListAsync();

        foreach (var comment in comments)
        {
            comment.Type = CommentType.Deleted;
            comment.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Přidá nový komentář k diskuzi
    /// Přístupné pouze pro přihlášené uživatele
    /// </summary>
    [Authorize]
    [HttpPost("{discussionId}/comments")]
    public async Task<ActionResult<CommentDto>> CreateComment(int discussionId, CreateCommentDto model)
    {
        return await CreateCommentOrReply(discussionId, null, model);
    }

    /// <summary>
    /// Přidá reakci na existující komentář
    /// Přístupné pouze pro přihlášené uživatele
    /// </summary>
    [Authorize]
    [HttpPost("{discussionId}/comments/{commentId}/replies")]
    public async Task<ActionResult<CommentDto>> CreateReply(int discussionId, int commentId, CreateCommentDto model)
    {
        return await CreateCommentOrReply(discussionId, commentId, model);
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
        }

        var comment = new Comment
        {
            DiscussionId = discussionId,
            ParentCommentId = parentCommentId,
            UserId = userId,
            Content = model.Content,
            CreatedAt = DateTime.UtcNow,
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
    /// Upraví existující komentář
    /// Přístupné pouze pro adminy
    /// </summary>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPut("{discussionId}/comments/{commentId}")]
    public async Task<IActionResult> UpdateComment(int discussionId, int commentId, UpdateCommentDto model)
    {
        var comment = await dbContext.Comments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.DiscussionId == discussionId);

        if (comment == null)
            return NotFound();

        comment.Content = model.Content;
        comment.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Smaže komentář a všechny jeho reakce
    /// Přístupné pouze pro adminy
    /// </summary>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete("{discussionId}/comments/{commentId}")]
    public async Task<IActionResult> DeleteComment(int discussionId, int commentId)
    {
        var comment = await dbContext.Comments
            .Include(c => c.Replies)
            .FirstOrDefaultAsync(c => c.Id == commentId && c.DiscussionId == discussionId);

        if (comment == null)
            return NotFound();

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

    /// <summary>
    /// Přidá like k diskuzi. Pro adminy není omezení počtu liků.
    /// Pro ostatní uživatele je možný jen jeden like na diskuzi.
    /// </summary>
    [Authorize]
    [HttpPost("{id}/like")]
    public async Task<ActionResult<LikeInfoDto>> AddDiscussionLike(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

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
        if (discussion.UserId == userId)
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

    /// <summary>
    /// Přidá like ke komentáři. Pro adminy není omezení počtu liků.
    /// Pro ostatní uživatele je možný jen jeden like na komentář.
    /// </summary>
    [Authorize]
    [HttpPost("{discussionId}/comments/{commentId}/like")]
    public async Task<ActionResult<LikeInfoDto>> AddCommentLike(int discussionId, int commentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

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

    /// <summary>
    /// Tato pomocná metoda převádí entitu Discussion na DTO objekt, který bude odeslán klientovi.
    /// Zpracovává všechna související data včetně komentářů a informací o like.
    /// </summary>
    /// <param name="discussion">Entita diskuze s načtenými souvisejícími daty (Category, User, Comments, Likes)</param>
    /// <param name="userId">ID aktuálně přihlášeného uživatele, nebo null pokud není nikdo přihlášen</param>
    /// <param name="isAdmin">True pokud je přihlášený uživatel v roli Admin</param>
    /// <returns>DTO objekt obsahující všechny potřebné informace pro zobrazení detailu diskuze</returns>
    private DiscussionDetailDto MapDiscussionToDetailDto(Discussion discussion, string? userId, bool isAdmin)
    {
        // Zjistíme, zda aktuální uživatel už dal like této diskuzi
        var userLikes = discussion.Likes.Any(l => l.UserId == userId);

        // Uživatel může dát like pokud:
        // - je admin (může dát neomezený počet liků)
        // - nebo je přihlášen, není autorem diskuze a ještě nedal like
        var canLike = isAdmin || (!string.IsNullOrEmpty(userId) &&
            discussion.UserId != userId && !userLikes);

        return new DiscussionDetailDto
        {
            Id = discussion.Id,
            Title = discussion.Title,
            Content = discussion.Content,
            ImagePath = discussion.ImagePath,
            CategoryName = discussion.Category.Name,
            AuthorNickname = discussion.User.Nickname,
            AuthorId = discussion.UserId,
            CreatedAt = discussion.CreatedAt,
            UpdatedAt = discussion.UpdatedAt,
            ViewCount = discussion.ViewCount,
            Type = discussion.Type,
            // Informace o like pro tuto diskuzi
            Likes = new LikeInfoDto
            {
                LikeCount = discussion.Likes.Count,    // Celkový počet liků
                HasUserLiked = userLikes,              // Zda přihlášený uživatel dal like
                CanUserLike = canLike                  // Zda může přihlášený uživatel dát like
            },
            // Mapujeme jen root komentáře (bez reakcí)
            // Reakce budou mapovány v rámci každého komentáře
            Comments = discussion.Comments
                .Where(c => c.ParentCommentId == null)
                .Select(c => MapCommentToDto(c, userId, isAdmin))
                .ToList()
        };
    }

    /// <summary>
    /// Tato pomocná metoda převádí entitu Comment na DTO objekt, který bude odeslán klientovi.
    /// Rekurzivně zpracovává i všechny reakce na tento komentář.
    /// </summary>
    /// <param name="comment">Entita komentáře s načtenými souvisejícími daty (User, Replies, Likes)</param>
    /// <param name="userId">ID aktuálně přihlášeného uživatele, nebo null pokud není nikdo přihlášen</param>
    /// <param name="isAdmin">True pokud je přihlášený uživatel v roli Admin</param>
    /// <returns>DTO objekt obsahující všechny potřebné informace pro zobrazení komentáře</returns>
    private CommentDto MapCommentToDto(Comment comment, string? userId, bool isAdmin)
    {
        // Zjistíme, zda aktuální uživatel už dal like tomuto komentáři
        var userLikes = comment.Likes.Any(l => l.UserId == userId);

        // Uživatel může dát like pokud:
        // - je admin (může dát neomezený počet liků)
        // - nebo je přihlášen, není autorem komentáře a ještě nedal like
        var canLike = isAdmin || (!string.IsNullOrEmpty(userId) &&
            comment.UserId != userId && !userLikes);

        return new CommentDto
        {
            Id = comment.Id,
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
            // Rekurzivně mapujeme odpovědi na tento komentář
            // Filtrujeme jen viditelné odpovědi pro aktuálního uživatele
            Replies = comment.Replies
                .Where(r => r.Type != CommentType.Deleted &&
                    (r.Type != CommentType.Hidden || isAdmin || r.UserId == userId))
                .Select(r => MapCommentToDto(r, userId, isAdmin))
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

    /// <summary>
    /// Přepne typ komentáře mezi Normal a Hidden
    /// Přístupné pouze pro adminy
    /// </summary>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPut("{discussionId}/comments/{commentId}/toggle-visibility")]
    public async Task<IActionResult> ToggleCommentVisibility(int discussionId, int commentId)
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

    /// <summary>
    /// Inkrementuje počítadlo zobrazení diskuze
    /// </summary>
    [HttpPost("{discussionId}/increment-view")]
    public async Task<IActionResult> IncrementViewCount(int discussionId)
    {
        var discussion = await dbContext.Discussions.FindAsync(discussionId);
        if (discussion == null)
            return NotFound();

        discussion.ViewCount++;
        await dbContext.SaveChangesAsync();

        return Ok();
    }
}