using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Zodpovedne.Data.Data;
using Zodpovedne.Data.Models;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Contracts.Enums;


namespace Zodpovedne.RESTAPI.Controllers;

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
    /// <param name="categoryId">Nepovinný parametr pro filtrování podle kategorie</param>
    /// <returns>Seznam diskuzí s základními informacemi</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DiscussionListDto>>> GetDiscussions(int? categoryId = null)
    {
        var query = dbContext.Discussions
            .Include(d => d.Category)
            .Include(d => d.User)
            .Include(d => d.Comments)
            .Where(d => d.IsVisible);

        // Pokud je zadaná kategorie, filtrujeme podle ní
        if (categoryId.HasValue)
        {
            query = query.Where(d => d.CategoryId == categoryId.Value);
        }

        var discussions = await query
            .OrderByDescending(d => d.Type == DiscussionType.Top) // Nejdřív Top diskuze
            .ThenByDescending(d => d.CreatedAt)                   // Pak podle data
            .Select(d => new DiscussionListDto
            {
                Id = d.Id,
                Title = d.Title,
                CategoryName = d.Category.Name,
                AuthorNickname = d.User.Nickname,
                CreatedAt = d.CreatedAt,
                CommentsCount = d.Comments.Count(c => c.IsVisible),
                ViewCount = d.ViewCount,
                Type = d.Type
            })
            .ToListAsync();

        return Ok(discussions);
    }

    /// <summary>
    /// Vrátí detail konkrétní diskuze včetně všech komentářů
    /// </summary>
    /// <param name="id">ID diskuze</param>
    /// <returns>Detail diskuze s komentáři</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<DiscussionDetailDto>> GetDiscussion(int id)
    {
        // Načteme diskuzi včetně všech souvisejících dat
        var discussion = await dbContext.Discussions
            .Include(d => d.Category)
            .Include(d => d.User)
            .Include(d => d.Comments.Where(c => c.IsVisible))
                .ThenInclude(c => c.User)
            .Include(d => d.Comments.Where(c => c.IsVisible))
                .ThenInclude(c => c.Replies.Where(r => r.IsVisible))
                    .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(d => d.Id == id && d.IsVisible);

        if (discussion == null)
            return NotFound();

        // Zvýšíme počet zobrazení
        discussion.ViewCount++;
        await dbContext.SaveChangesAsync();

        // Mapujeme na DTO
        var result = new DiscussionDetailDto
        {
            Id = discussion.Id,
            Title = discussion.Title,
            Content = discussion.Content,
            ImagePath = discussion.ImagePath,
            CategoryName = discussion.Category.Name,
            AuthorNickname = discussion.User.Nickname,
            CreatedAt = discussion.CreatedAt,
            UpdatedAt = discussion.UpdatedAt,
            ViewCount = discussion.ViewCount,
            // Mapujeme pouze root komentáře (komentáře přímo k diskuzi)
            Comments = discussion.Comments
                .Where(c => c.ParentCommentId == null)
                .Select(c => MapCommentToDto(c))
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

        var discussion = new Discussion
        {
            CategoryId = model.CategoryId,
            UserId = userId,
            Title = model.Title,
            Content = model.Content,
            CreatedAt = DateTime.UtcNow,
            IsVisible = true
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

        // Nastavíme příznak IsVisible na false místo fyzického smazání
        discussion.IsVisible = false;
        discussion.UpdatedAt = DateTime.UtcNow;

        // Skryjeme i všechny komentáře
        var comments = await dbContext.Comments
            .Where(c => c.DiscussionId == id)
            .ToListAsync();

        foreach (var comment in comments)
        {
            comment.IsVisible = false;
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

        var comment = new Comment
        {
            DiscussionId = id,
            UserId = userId,
            Content = model.Content,
            CreatedAt = DateTime.UtcNow,
            IsVisible = true
        };

        dbContext.Comments.Add(comment);
        await dbContext.SaveChangesAsync();

        // Načteme vytvořený komentář včetně uživatele pro správné mapování
        comment = await dbContext.Comments
            .Include(c => c.User)
            .FirstAsync(c => c.Id == comment.Id);

        return Ok(MapCommentToDto(comment));
    }

    /// <summary>
    /// Přidá reakci na existující komentář
    /// Přístupné pouze pro přihlášené uživatele
    /// </summary>
    [Authorize]
    [HttpPost("{discussionId}/comments/{commentId}/replies")]
    public async Task<ActionResult<CommentDto>> CreateReply(int discussionId, int commentId, CreateCommentDto model)
    {
        // Ověříme existenci diskuze a komentáře
        var discussion = await dbContext.Discussions.FindAsync(discussionId);
        if (discussion == null)
            return NotFound("Diskuze neexistuje.");

        var parentComment = await dbContext.Comments.FindAsync(commentId);
        if (parentComment == null)
            return NotFound("Rodičovský komentář neexistuje.");

        // Ověříme, že rodičovský komentář patří k této diskuzi
        if (parentComment.DiscussionId != discussionId)
            return BadRequest("Komentář nepatří k této diskuzi.");

        // Ověříme, že rodičovský komentář je root komentář
        if (parentComment.ParentCommentId != null)
            return BadRequest("Lze reagovat pouze na hlavní komentáře.");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var reply = new Comment
        {
            DiscussionId = discussionId,
            ParentCommentId = commentId,
            UserId = userId,
            Content = model.Content,
            CreatedAt = DateTime.UtcNow,
            IsVisible = true
        };

        dbContext.Comments.Add(reply);
        await dbContext.SaveChangesAsync();

        // Načteme vytvořenou odpověď včetně uživatele pro správné mapování
        reply = await dbContext.Comments
            .Include(c => c.User)
            .FirstAsync(c => c.Id == reply.Id);

        return Ok(MapCommentToDto(reply));
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

        // Skryjeme komentář místo fyzického smazání
        comment.IsVisible = false;
        comment.UpdatedAt = DateTime.UtcNow;

        // Skryjeme i všechny reakce
        foreach (var reply in comment.Replies)
        {
            reply.IsVisible = false;
            reply.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Pomocná metoda pro mapování komentáře na DTO
    /// </summary>
    private static CommentDto MapCommentToDto(Comment comment)
    {
        return new CommentDto
        {
            Id = comment.Id,
            Content = comment.Content,
            AuthorNickname = comment.User.Nickname,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt,
            ParentCommentId = comment.ParentCommentId,
            Replies = comment.Replies
                .Where(r => r.IsVisible)
                .Select(r => new CommentDto
                {
                    Id = r.Id,
                    Content = r.Content,
                    AuthorNickname = r.User.Nickname,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    ParentCommentId = r.ParentCommentId
                })
                .ToList()
        };
    }
}