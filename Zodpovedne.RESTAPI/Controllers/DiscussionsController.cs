using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Zodpovedne.Data.Data;
using Zodpovedne.Data.Models;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Contracts.Enums;
using Zodpovedne.Data.Helpers;


namespace Zodpovedne.RESTAPI.Controllers;

/// <summary>
/// Kontroler pro práci s diskuzemi a komentáři.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DiscussionsController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;

    public DiscussionsController(ApplicationDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <summary>
    /// Vrátí seznam všech diskuzí s možností filtrování podle kategorie
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DiscussionListDto>>> GetDiscussions(int? categoryId = null)
    {
        // Získání ID a role přihlášeného uživatele pro filtrování skrytého obsahu
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

        // Základní LINQ dotaz s načtením souvisejících entit
        var query = dbContext.Discussions
            .Include(d => d.Category)
            .Include(d => d.User)
            .Include(d => d.Comments)
            .Include(d => d.Likes)  // Přidáno načítání lajků pro diskuze
            .Where(d => d.Type != DiscussionType.Deleted)  // Filtrujeme smazané diskuze
            .Where(d => d.Type != DiscussionType.Hidden || // Filtrujeme skryté diskuze, kromě:
                isAdmin ||                                  // - pro adminy
                d.UserId == userId);                       // - pro autory diskuze

        // Volitelné filtrování podle kategorie
        if (categoryId.HasValue)
        {
            query = query.Where(d => d.CategoryId == categoryId.Value);
        }

        // Načtení dat s řazením a mapováním na DTO
        var discussions = await query
            .OrderByDescending(d => d.Type == DiscussionType.Top)  // TOP diskuze první
            .ThenByDescending(d => d.CreatedAt)                    // Pak podle data vytvoření
            .Select(d => new DiscussionListDto
            {
                Id = d.Id,
                Title = d.Title,
                CategoryName = d.Category.Name,
                AuthorNickname = d.User.Nickname,
                CreatedAt = d.CreatedAt,
                CommentsCount = d.Comments.Count(c =>
                    c.Type != CommentType.Deleted &&               // Nepočítáme smazané komentáře
                    (c.Type != CommentType.Hidden ||              // Skryté komentáře počítáme jen:
                        isAdmin ||                                // - pro adminy
                        c.UserId == userId)),                     // - pro autory komentáře
                ViewCount = d.ViewCount,
                Type = d.Type,
                Code = d.Code,
                Likes = new LikeInfoDto                          // Přidáno mapování lajků
                {
                    LikeCount = d.Likes.Count,
                    HasUserLiked = d.Likes.Any(l => l.UserId == userId),
                    CanUserLike = isAdmin ||
                        (!string.IsNullOrEmpty(userId) &&
                         d.UserId != userId &&
                         !d.Likes.Any(l => l.UserId == userId))
                }
            })
            .ToListAsync();

        return Ok(discussions);
    }

    /// <summary>
    /// Vrátí detail konkrétní diskuze včetně všech komentářů
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<DiscussionDetailDto>> GetDiscussion(int id)
    {
        // Získání ID a role přihlášeného uživatele
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

        // Načtení diskuze včetně souvisejících dat
        var discussion = await dbContext.Discussions
            .Include(d => d.Category)
            .Include(d => d.User)
            .Include(d => d.Likes)  // Přidáno načítání lajků pro diskuzi
            .Include(d => d.Comments)
                .ThenInclude(c => c.User)
            .Include(d => d.Comments)
                .ThenInclude(c => c.Likes)  // Přidáno načítání lajků pro komentáře
            .Include(d => d.Comments)
                .ThenInclude(c => c.Replies)
                    .ThenInclude(r => r.User)
            .Include(d => d.Comments)
                .ThenInclude(c => c.Replies)
                    .ThenInclude(r => r.Likes)  // Přidáno načítání lajků pro odpovědi
            .FirstOrDefaultAsync(d => d.Id == id &&
                d.Type != DiscussionType.Deleted &&
                (d.Type != DiscussionType.Hidden || isAdmin || d.UserId == userId));

        if (discussion == null)
            return NotFound();

        // Filtrujeme komentáře dle viditelnosti
        var filteredComments = discussion.Comments
            .Where(c => c.Type != CommentType.Deleted &&
                (c.Type != CommentType.Hidden || isAdmin || c.UserId == userId))
            .ToList();

        // Filtrujeme odpovědi na komentáře
        foreach (var comment in filteredComments)
        {
            comment.Replies = comment.Replies
                .Where(r => r.Type != CommentType.Deleted &&
                    (r.Type != CommentType.Hidden || isAdmin || r.UserId == userId))
                .ToList();
        }

        // Zvýšení počtu zobrazení
        discussion.ViewCount++;
        await dbContext.SaveChangesAsync();

        // Mapování na DTO včetně informací o lajcích
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
            Likes = new LikeInfoDto  // Přidáno mapování lajků diskuze
            {
                LikeCount = discussion.Likes.Count,
                HasUserLiked = discussion.Likes.Any(l => l.UserId == userId),
                CanUserLike = isAdmin ||
                    (!string.IsNullOrEmpty(userId) &&
                     discussion.UserId != userId &&
                     !discussion.Likes.Any(l => l.UserId == userId))
            },
            Comments = filteredComments
                .Where(c => c.ParentCommentId == null)
                .Select(c => MapCommentToDto(c, userId, isAdmin))
                .ToList()
        };

        return Ok(result);
    }

    [HttpGet("byCode/{code}")]
    public async Task<ActionResult<DiscussionDetailDto>> GetDiscussionByCode(string code)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

        var discussion = await dbContext.Discussions
            .Include(d => d.Category)
            .Include(d => d.User)
            .Include(d => d.Likes)  // Přidáno pro diskuzi
            .Include(d => d.Comments)
                .ThenInclude(c => c.User)
            .Include(d => d.Comments)
                .ThenInclude(c => c.Likes)  // Přidáno pro komentáře
            .Include(d => d.Comments)
                .ThenInclude(c => c.Replies)
                    .ThenInclude(r => r.User)
            .Include(d => d.Comments)
                .ThenInclude(c => c.Replies)
                    .ThenInclude(r => r.Likes)  // Přidáno pro odpovědi
            .FirstOrDefaultAsync(d => d.Code == code &&
                d.Type != DiscussionType.Deleted &&
                (d.Type != DiscussionType.Hidden || isAdmin || d.UserId == userId));

        if (discussion == null)
            return NotFound();

        var filteredComments = discussion.Comments
            .Where(c => c.Type != CommentType.Deleted &&
                (c.Type != CommentType.Hidden || isAdmin || c.UserId == userId))
            .ToList();

        foreach (var comment in filteredComments)
        {
            comment.Replies = comment.Replies
                .Where(r => r.Type != CommentType.Deleted &&
                    (r.Type != CommentType.Hidden || isAdmin || r.UserId == userId))
                .ToList();
        }

        discussion.ViewCount++;
        await dbContext.SaveChangesAsync();

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
            Likes = new LikeInfoDto  // Přidáno mapování lajků
            {
                LikeCount = discussion.Likes.Count,
                HasUserLiked = discussion.Likes.Any(l => l.UserId == userId),
                CanUserLike = isAdmin ||
                    (!string.IsNullOrEmpty(userId) &&
                     discussion.UserId != userId &&
                     !discussion.Likes.Any(l => l.UserId == userId))
            },
            Comments = filteredComments
                .Where(c => c.ParentCommentId == null)
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
            Type = model.Type,
            Code = code
        };
        dbContext.Discussions.Add(discussion);
        await dbContext.SaveChangesAsync();

        // Vrátíme detail vytvořené diskuze
        return CreatedAtAction(nameof(GetDiscussion), new { id = discussion.Id }, null);
    }

    /// <summary>
    /// Upraví existující diskuzi
    /// Přístupné pouze pro autora diskuze nebo admina
    /// </summary>
    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDiscussion(int id, UpdateDiscussionDto model)
    {
        var discussion = await dbContext.Discussions.FindAsync(id);
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
    [HttpPost("{id}/comments")]
    public async Task<ActionResult<CommentDto>> CreateComment(int id, CreateCommentDto model)
    {
        var discussion = await dbContext.Discussions.FindAsync(id);
        if (discussion == null)
            return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();
        var isAdmin = User.IsInRole("Admin");

        var comment = new Comment
        {
            DiscussionId = id,
            UserId = userId,
            Content = model.Content,
            Type = model.Type,
            CreatedAt = DateTime.UtcNow
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
    /// Přidá reakci na existující komentář
    /// Přístupné pouze pro přihlášené uživatele
    /// </summary>
    [Authorize]
    [HttpPost("{discussionId}/comments/{commentId}/replies")]
    public async Task<ActionResult<CommentDto>> CreateReply(int discussionId, int commentId, CreateCommentDto model)
    {
        // Ověříme existenci diskuze a její viditelnost pro uživatele
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

        var discussion = await dbContext.Discussions.FindAsync(discussionId);
        if (discussion == null ||
            discussion.Type == DiscussionType.Deleted ||  // Smazané diskuze nejsou dostupné nikomu
            (discussion.Type == DiscussionType.Hidden && !isAdmin && discussion.UserId != userId))  // Hidden diskuze jen pro adminy a autory
        {
            return NotFound("Diskuze neexistuje.");
        }

        // Ověříme existenci rodičovského komentáře a jeho viditelnost
        var parentComment = await dbContext.Comments.FindAsync(commentId);
        if (parentComment == null ||
            parentComment.Type == CommentType.Deleted ||  // Smazané komentáře nejsou dostupné nikomu
            (parentComment.Type == CommentType.Hidden && !isAdmin && parentComment.UserId != userId))  // Hidden komentáře jen pro adminy a autory
        {
            return NotFound("Rodičovský komentář neexistuje.");
        }

        // Ověříme, že rodičovský komentář patří k této diskuzi
        if (parentComment.DiscussionId != discussionId)
            return BadRequest("Komentář nepatří k této diskuzi.");

        // Ověříme, že rodičovský komentář je root komentář
        if (parentComment.ParentCommentId != null)
            return BadRequest("Lze reagovat pouze na hlavní komentáře.");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var reply = new Comment
        {
            DiscussionId = discussionId,
            ParentCommentId = commentId,
            UserId = userId,
            Content = model.Content,
            CreatedAt = DateTime.UtcNow,
            Type = model.Type  // Použijeme typ z modelu
        };

        dbContext.Comments.Add(reply);
        await dbContext.SaveChangesAsync();

        // Načteme vytvořenou odpověď včetně uživatele pro správné mapování
        reply = await dbContext.Comments
            .Include(c => c.User)
            .FirstAsync(c => c.Id == reply.Id);

        return Ok(MapCommentToDto(reply, userId, isAdmin));
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
            LikeCount = discussion.Likes.Count + 1,
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

        // Kontrola, zda uživatel nedává like svému vlastnímu komentáři
        if (comment.UserId == userId)
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
}