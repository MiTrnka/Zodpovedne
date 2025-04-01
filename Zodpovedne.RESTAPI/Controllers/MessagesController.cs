using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Contracts.Enums;
using Zodpovedne.Data.Data;
using Zodpovedne.Data.Models;
using Zodpovedne.Logging;

namespace Zodpovedne.RESTAPI.Controllers;

/// <summary>
/// Controller pro práci se zprávami mezi uživateli.
/// Umožňuje odesílání, příjem a správu zpráv mezi přáteli.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize] // Všechny endpointy vyžadují přihlášeného uživatele
public class MessagesController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly FileLogger _logger;

    /// <summary>
    /// Konstruktor controlleru zpráv
    /// </summary>
    /// <param name="dbContext">Databázový kontext pro přístup k datům</param>
    /// <param name="logger">Logger pro zaznamenávání chyb a událostí</param>
    public MessagesController(ApplicationDbContext dbContext, FileLogger logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Získá zprávy z konverzace s konkrétním uživatelem.
    /// Podporuje stránkování pro postupné načítání starších zpráv.
    /// </summary>
    /// <param name="otherUserId">ID uživatele, se kterým probíhá konverzace</param>
    /// <param name="page">Číslo stránky (číslováno od 1)</param>
    /// <param name="pageSize">Počet zpráv na stránku</param>
    /// <returns>Seznam zpráv v konverzaci s informací o stránkování</returns>
    [HttpGet("conversation/{otherUserId}")]
    public async Task<ActionResult<MessagesPaginationDto>> GetConversation(string otherUserId, int page = 1, int pageSize = 20)
    {
        try
        {
            // Získání ID přihlášeného uživatele
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            // Validace parametrů stránkování
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 50) pageSize = 50; // Omezení maximální velikosti stránky

            // 1. KROK: Ověření, že uživatel má přístup ke konverzaci (jsou přátelé)
            bool areFriends = await _dbContext.Friendships
                .AnyAsync(f =>
                    ((f.ApproverUserId == currentUserId && f.RequesterUserId == otherUserId) ||
                     (f.ApproverUserId == otherUserId && f.RequesterUserId == currentUserId)) &&
                    f.FriendshipStatus == FriendshipStatus.Approved);

            if (!areFriends)
                return Forbid(); // Uživatelé nejsou přátelé, nemají přístup ke konverzaci

            // 2. KROK: Ověření, že druhý uživatel existuje a není smazaný
            var otherUser = await _dbContext.Users
                .Where(u => u.Id == otherUserId && u.Type == UserType.Normal)
                .Select(u => new { u.Id, u.Nickname })
                .FirstOrDefaultAsync();

            if (otherUser == null)
                return NotFound(); // Uživatel neexistuje nebo je skrytý/smazaný

            // 3. KROK: Získání celkového počtu zpráv v konverzaci pro stránkování
            var totalCount = await _dbContext.Messages
                .Where(m =>
                    (m.SenderUserId == currentUserId && m.RecipientUserId == otherUserId) ||
                    (m.SenderUserId == otherUserId && m.RecipientUserId == currentUserId)
                )
                .CountAsync();

            // 4. KROK: Získání zpráv pro aktuální stránku
            // Řazení: od nejnovějších (nahoře) po nejstarší (dole) - ale v UI se zobrazí obráceně
            var messages = await _dbContext.Messages
                .Where(m =>
                    (m.SenderUserId == currentUserId && m.RecipientUserId == otherUserId) ||
                    (m.SenderUserId == otherUserId && m.RecipientUserId == currentUserId)
                )
                .OrderByDescending(m => m.SentAt) // Nejnovější první
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MessageDto
                {
                    Id = m.Id,
                    SenderUserId = m.SenderUserId,
                    SenderNickname = m.SenderUser.Nickname,
                    RecipientUserId = m.RecipientUserId,
                    RecipientNickname = m.RecipientUser.Nickname,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    ReadAt = m.ReadAt,
                    MessageType = m.MessageType
                })
                .ToListAsync();

            // 5. KROK: Označit přijaté zprávy jako přečtené
            var unreadMessageIds = await _dbContext.Messages
                .Where(m =>
                    m.SenderUserId == otherUserId &&
                    m.RecipientUserId == currentUserId &&
                    m.ReadAt == null
                )
                .Select(m => m.Id)
                .ToListAsync();

            if (unreadMessageIds.Any())
            {
                var now = DateTime.UtcNow;
                await _dbContext.Messages
                    .Where(m => unreadMessageIds.Contains(m.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.ReadAt, now));
            }

            // 6. KROK: Sestavení odpovědi s informacemi o stránkování
            return new MessagesPaginationDto
            {
                Messages = messages.OrderBy(m => m.SentAt).ToList(), // Pro UI seřazujeme zpět chronologicky
                HasOlderMessages = (page * pageSize) < totalCount,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            _logger.Log("Chyba při získávání zpráv konverzace", ex);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Odešle novou zprávu určenému příjemci.
    /// Zprávu lze odeslat pouze uživateli, který je v seznamu přátel.
    /// </summary>
    /// <param name="model">Data pro odeslání zprávy</param>
    /// <returns>Detaily odeslané zprávy</returns>
    [HttpPost]
    public async Task<ActionResult<MessageDto>> SendMessage(SendMessageDto model)
    {
        try
        {
            // Získání ID přihlášeného uživatele
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            // 1. KROK: Ověření, že příjemce existuje a není smazaný
            var recipient = await _dbContext.Users
                .Where(u => u.Id == model.RecipientUserId && u.Type == UserType.Normal)
                .Select(u => new { u.Id, u.Nickname })
                .FirstOrDefaultAsync();

            if (recipient == null)
                return NotFound("Příjemce neexistuje nebo je skrytý/smazaný.");

            // 2. KROK: Ověření, že uživatelé jsou přátelé
            bool areFriends = await _dbContext.Friendships
                .AnyAsync(f =>
                    ((f.ApproverUserId == currentUserId && f.RequesterUserId == model.RecipientUserId) ||
                     (f.ApproverUserId == model.RecipientUserId && f.RequesterUserId == currentUserId)) &&
                    f.FriendshipStatus == FriendshipStatus.Approved);

            if (!areFriends)
                return BadRequest("Zprávy lze odesílat pouze přátelům.");

            // 3. KROK: Získání informací o odesílateli (aktuálním uživateli)
            var sender = await _dbContext.Users
                .Where(u => u.Id == currentUserId)
                .Select(u => new { u.Id, u.Nickname })
                .FirstOrDefaultAsync();

            // 4. KROK: Validace obsahu zprávy
            if (string.IsNullOrWhiteSpace(model.Content))
                return BadRequest("Obsah zprávy nemůže být prázdný.");

            if (model.Content.Length > 1000)
                return BadRequest("Zpráva nemůže být delší než 1000 znaků.");

            // 5. KROK: Vytvoření a uložení nové zprávy
            var message = new Message
            {
                SenderUserId = currentUserId,
                RecipientUserId = model.RecipientUserId,
                Content = model.Content,
                SentAt = DateTime.UtcNow,
                MessageType = MessageType.Normal
            };

            _dbContext.Messages.Add(message);
            await _dbContext.SaveChangesAsync();

            // 6. KROK: Vrácení detailů odeslané zprávy
            return new MessageDto
            {
                Id = message.Id,
                SenderUserId = message.SenderUserId,
                SenderNickname = sender?.Nickname ?? "Unknown",
                RecipientUserId = message.RecipientUserId,
                RecipientNickname = recipient.Nickname,
                Content = message.Content,
                SentAt = message.SentAt,
                ReadAt = message.ReadAt,
                MessageType = message.MessageType
            };
        }
        catch (Exception ex)
        {
            _logger.Log("Chyba při odesílání zprávy", ex);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vrací celkový počet nepřečtených zpráv pro přihlášeného uživatele.
    /// Používá se pro zobrazení indikátoru na ikoně zpráv v menu.
    /// </summary>
    /// <returns>Počet nepřečtených zpráv</returns>
    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadMessagesCount()
    {
        try
        {
            // Získání ID přihlášeného uživatele
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            // 1. KROK: Získání seznamu ID přátel pro filtrování
            var friendIds = await _dbContext.Friendships
                .Where(f => (f.ApproverUserId == currentUserId || f.RequesterUserId == currentUserId) &&
                       f.FriendshipStatus == FriendshipStatus.Approved)
                .Select(f => f.ApproverUserId == currentUserId ? f.RequesterUserId : f.ApproverUserId)
                .ToListAsync();

            // 2. KROK: Získání počtu nepřečtených zpráv od přátel
            var unreadCount = await _dbContext.Messages
                .Where(m =>
                    m.RecipientUserId == currentUserId && // Uživatel je příjemcem
                    m.ReadAt == null &&                   // Zpráva ještě nebyla přečtena
                    friendIds.Contains(m.SenderUserId)    // Odesílatel je přítel
                )
                .CountAsync();

            return unreadCount;
        }
        catch (Exception ex)
        {
            _logger.Log("Chyba při získávání počtu nepřečtených zpráv", ex);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vrací počet nepřečtených zpráv pro každého přítele s nepřečtenými zprávami
    /// </summary>
    /// <returns>Slovník s ID uživatelů jako klíči a počtem nepřečtených zpráv jako hodnotami</returns>
    [HttpGet("unread-counts-by-user")]
    public async Task<ActionResult<Dictionary<string, int>>> GetUnreadMessagesByUser()
    {
        try
        {
            // Získání ID přihlášeného uživatele
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            // 1. KROK: Získání seznamu ID přátel pro filtrování
            var friendIds = await _dbContext.Friendships
                .Where(f => (f.ApproverUserId == currentUserId || f.RequesterUserId == currentUserId) &&
                      f.FriendshipStatus == FriendshipStatus.Approved)
                .Select(f => f.ApproverUserId == currentUserId ? f.RequesterUserId : f.ApproverUserId)
                .ToListAsync();

            // 2. KROK: Získání počtu nepřečtených zpráv od každého přítele
            var unreadCounts = await _dbContext.Messages
                .Where(m =>
                    m.RecipientUserId == currentUserId && // Uživatel je příjemcem
                    m.ReadAt == null &&                   // Zpráva ještě nebyla přečtena
                    friendIds.Contains(m.SenderUserId)    // Odesílatel je přítel
                )
                .GroupBy(m => m.SenderUserId)             // Seskupení podle odesílatele
                .Select(g => new                          // Počet zpráv pro každého odesílatele
                {
                    SenderId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.SenderId, x => x.Count);

            return unreadCounts;
        }
        catch (Exception ex)
        {
            _logger.Log("Chyba při získávání počtu nepřečtených zpráv podle uživatelů", ex);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}