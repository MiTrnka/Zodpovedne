using Ganss.Xss;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Contracts.Enums;
using Zodpovedne.Data.Data;
using Zodpovedne.Data.Models;
using Zodpovedne.Logging;
using Zodpovedne.Logging.Services;

namespace Zodpovedne.RESTAPI.Controllers;

/// <summary>
/// Kontroler pro správu hlasování v diskuzích
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VotingsController : ControllerZodpovedneBase
{
    // HtmlSanitizer pro bezpečné čištění HTML vstupu
    private readonly IHtmlSanitizer _sanitizer;

    public VotingsController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, FileLogger logger, IHtmlSanitizer sanitizer, Translator translator)
        : base(dbContext, userManager, logger, translator)
    {
        _sanitizer = sanitizer;
    }

    /// <summary>
    /// Získá informace o hlasování u konkrétní diskuze
    /// </summary>
    /// <param name="discussionId">ID diskuze</param>
    /// <returns>Objekt s hlasováním včetně otázek a výsledků</returns>
    [HttpGet("discussion/{discussionId}")]
    public async Task<ActionResult<VotingDto>> GetVotingForDiscussion(int discussionId)
    {
        try
        {
            // Získání aktuálně přihlášeného uživatele
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            // Ověření, zda diskuze existuje a má hlasování
            var discussion = await dbContext.Discussions
                .AsNoTracking()
                .Where(d => d.Id == discussionId && d.Type != DiscussionType.Deleted)
                .FirstOrDefaultAsync();

            if (discussion == null)
                return NotFound("Diskuze nebyla nalezena.");

            // Kontrola, zda diskuze má hlasování a zda je viditelné pro aktuálního uživatele
            if (discussion.VoteType == VoteType.None)
                return NotFound("Diskuze nemá hlasování.");

            // Skryté hlasování vidí pouze autor nebo admin
            if (discussion.VoteType == VoteType.Hidden && !isAdmin && discussion.UserId != userId)
                return NotFound("Diskuze nemá hlasování.");

            // Načtení hlasovacích otázek
            var questions = await dbContext.VotingQuestions
                .AsNoTracking()
                .Where(q => q.DiscussionId == discussionId)
                .OrderBy(q => q.DisplayOrder)
                .Select(q => new VotingQuestionDetailDto
                {
                    Id = q.Id,
                    Text = q.Text,
                    DisplayOrder = q.DisplayOrder,
                    YesVotes = q.YesVotes,
                    NoVotes = q.NoVotes,
                    // Pokud je přihlášený uživatel, zjistíme jeho hlas
                    CurrentUserVote = userId != null
                        ? q.Votes.Where(v => v.UserId == userId)
                            .Select(v => (bool?)v.VoteValue)
                            .FirstOrDefault()
                        : null
                })
                .ToListAsync();

            // Vytvoření a vrácení DTO odpovědi
            var result = new VotingDto
            {
                DiscussionId = discussionId,
                VoteType = discussion.VoteType,
                Questions = questions
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.Log("Chyba při získávání informací o hlasování", ex);
            return StatusCode(StatusCodes.Status500InternalServerError, "Při zpracování požadavku došlo k chybě.");
        }
    }

    /// <summary>
    /// Vytvoří nebo aktualizuje hlasování pro diskuzi
    /// Přístupné pouze pro autora diskuze nebo adminy
    /// </summary>
    /// <param name="model">Data pro vytvoření/aktualizaci hlasování</param>
    /// <returns>Vytvořené/aktualizované hlasování</returns>
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<VotingDto>> CreateOrUpdateVoting(CreateOrUpdateVotingDto model)
    {
        try
        {
            // Získání aktuálně přihlášeného uživatele
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Ověření, zda diskuze existuje
            var discussion = await dbContext.Discussions
                .Where(d => d.Id == model.DiscussionId && d.Type != DiscussionType.Deleted)
                .FirstOrDefaultAsync();

            if (discussion == null)
                return NotFound("Diskuze nebyla nalezena.");

            // Ověření oprávnění - pouze autor diskuze nebo admin může upravovat hlasování
            if (!isAdmin && discussion.UserId != userId)
                return Forbid();

            // Ověření, zda jsou zadány otázky, pokud má být hlasování aktivní
            if (model.VoteType != VoteType.None && (model.Questions == null || !model.Questions.Any()))
                return BadRequest("Hlasování musí obsahovat alespoň jednu otázku.");

            // Aktuální čas pro jednotné použití
            var now = DateTime.UtcNow;

            // Pokud chceme odstranit hlasování, odstraníme všechny otázky a nastavíme VoteType na None
            if (model.VoteType == VoteType.None)
            {
                await dbContext.VotingQuestions
                    .Where(q => q.DiscussionId == model.DiscussionId)
                    .ExecuteDeleteAsync();

                discussion.VoteType = VoteType.None;
                discussion.UpdatedWhateverAt = now;

                await dbContext.SaveChangesAsync();

                return Ok(new VotingDto
                {
                    DiscussionId = model.DiscussionId,
                    VoteType = VoteType.None,
                    Questions = new List<VotingQuestionDetailDto>()
                });
            }

            // Aktualizace typu hlasování v diskuzi
            discussion.VoteType = model.VoteType;
            discussion.UpdatedWhateverAt = now;

            // Použijeme transakci pro zajištění konzistence dat
            using var transaction = await dbContext.Database.BeginTransactionAsync();
            try
            {
                // Získáme aktuálně existující otázky pro tuto diskuzi
                var existingQuestions = await dbContext.VotingQuestions
                    .Where(q => q.DiscussionId == model.DiscussionId)
                    .ToListAsync();

                // Získáme ID existujících otázek
                var existingIds = existingQuestions.Select(q => q.Id).ToHashSet();

                // Získáme ID otázek z modelu (pro aktualizaci)
                var modelIds = model.Questions
                    .Where(q => q.Id.HasValue)
                    .Select(q => q.Id.Value)
                    .ToHashSet();

                // Zjistíme, které otázky odstranit (ty, které nejsou v modelu)
                var idsToRemove = existingIds.Except(modelIds).ToList();

                // Odstraníme otázky, které nejsou v modelu
                if (idsToRemove.Any())
                {
                    // Nejprve odstraníme hlasy pro tyto otázky
                    foreach (var idToRemove in idsToRemove)
                    {
                        await dbContext.Votes
                            .Where(v => v.VotingQuestionId == idToRemove)
                            .ExecuteDeleteAsync();
                    }

                    // Poté odstraníme samotné otázky
                    await dbContext.VotingQuestions
                        .Where(q => idsToRemove.Contains(q.Id))
                        .ExecuteDeleteAsync();
                }

                // Zpracujeme otázky z modelu - aktualizujeme nebo vytvoříme nové
                foreach (var questionDto in model.Questions)
                {
                    if (questionDto.Id.HasValue)
                    {
                        // Aktualizace existující otázky
                        var existingQuestion = existingQuestions.FirstOrDefault(q => q.Id == questionDto.Id.Value);
                        if (existingQuestion != null)
                        {
                            existingQuestion.Text = questionDto.Text;
                            existingQuestion.DisplayOrder = questionDto.DisplayOrder;
                            existingQuestion.UpdatedAt = now;
                            dbContext.VotingQuestions.Update(existingQuestion);
                        }
                    }
                    else
                    {
                        // Vytvoření nové otázky
                        var newQuestion = new VotingQuestion
                        {
                            DiscussionId = model.DiscussionId,
                            Text = questionDto.Text,
                            DisplayOrder = questionDto.DisplayOrder,
                            CreatedAt = now,
                            UpdatedAt = now
                        };

                        dbContext.VotingQuestions.Add(newQuestion);
                    }
                }

                // Uložíme změny
                await dbContext.SaveChangesAsync();

                // Potvrdíme transakci
                await transaction.CommitAsync();

                // Načteme aktualizované hlasování pro vrácení
                var updatedQuestions = await dbContext.VotingQuestions
                    .AsNoTracking()
                    .Where(q => q.DiscussionId == model.DiscussionId)
                    .OrderBy(q => q.DisplayOrder)
                    .Select(q => new VotingQuestionDetailDto
                    {
                        Id = q.Id,
                        Text = q.Text,
                        DisplayOrder = q.DisplayOrder,
                        YesVotes = q.YesVotes,
                        NoVotes = q.NoVotes,
                        CurrentUserVote = q.Votes
                            .Where(v => v.UserId == userId)
                            .Select(v => (bool?)v.VoteValue)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                // Vytvoření a vrácení DTO odpovědi
                var result = new VotingDto
                {
                    DiscussionId = model.DiscussionId,
                    VoteType = discussion.VoteType,
                    Questions = updatedQuestions
                };

                return Ok(result);
            }
            catch
            {
                // Při chybě vrátíme transakci
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.Log("Chyba při vytváření/aktualizaci hlasování", ex);
            return StatusCode(StatusCodes.Status500InternalServerError, "Při zpracování požadavku došlo k chybě.");
        }
    }

    /// <summary>
    /// Odešle hlasy uživatele pro hlasování v diskuzi
    /// </summary>
    /// <param name="model">Data s odpověďmi uživatele</param>
    /// <returns>Aktualizované výsledky hlasování</returns>
    [Authorize]
    [HttpPost("submit")]
    public async Task<ActionResult<VotingDto>> SubmitVotes(SubmitVotesDto model)
    {
        try
        {
            // Získání aktuálně přihlášeného uživatele
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Ověření, zda diskuze existuje a má hlasování
            var discussion = await dbContext.Discussions
                .FirstOrDefaultAsync(d => d.Id == model.DiscussionId && d.Type != DiscussionType.Deleted);

            if (discussion == null)
                return NotFound("Diskuze nebyla nalezena.");

            // Kontrola, zda diskuze má aktivní hlasování, ve kterém lze hlasovat
            if (discussion.VoteType != VoteType.Visible)
                return BadRequest("V této diskuzi nelze momentálně hlasovat.");

            // Získání seznamu ID otázek v tomto hlasování
            var questionIds = await dbContext.VotingQuestions
                .Where(q => q.DiscussionId == model.DiscussionId)
                .Select(q => q.Id)
                .ToListAsync();

            // Ověření, že všechny ID otázek v modelu patří k této diskuzi
            foreach (var questionId in model.Votes.Keys)
            {
                if (!questionIds.Contains(questionId))
                    return BadRequest($"Otázka s ID {questionId} nepatří k této diskuzi.");
            }

            // Aktuální čas pro jednotné použití
            var now = DateTime.UtcNow;

            // Použijeme transakci pro zajištění konzistence dat
            using var transaction = await dbContext.Database.BeginTransactionAsync();
            try
            {
                // Získáme všechny existující hlasy uživatele pro tuto diskuzi
                var existingVotes = await dbContext.Votes
                    .Where(v => v.UserId == userId && v.VotingQuestion.DiscussionId == model.DiscussionId)
                    .ToListAsync();

                // Vytvoříme slovník existujících hlasů podle ID otázky
                var existingVotesByQuestionId = existingVotes.ToDictionary(v => v.VotingQuestionId);

                // Vytvoříme seznam všech dostupných otázek pro tuto diskuzi
                var allQuestionsForDiscussion = await dbContext.VotingQuestions
                    .Where(q => q.DiscussionId == model.DiscussionId)
                    .ToListAsync();

                // Nejprve zpracujeme hlasy, které JSOU v model.Votes
                foreach (var questionVote in model.Votes)
                {
                    var questionId = questionVote.Key;
                    var voteValue = questionVote.Value;

                    // Načteme otázku, se kterou budeme pracovat
                    var question = allQuestionsForDiscussion.FirstOrDefault(q => q.Id == questionId);
                    if (question == null) continue;

                    // Pokud uživatel již hlasoval pro tuto otázku
                    if (existingVotesByQuestionId.TryGetValue(questionId, out var existingVote))
                    {
                        // Pokud se hodnota hlasu změnila
                        if (existingVote.VoteValue != voteValue)
                        {
                            // Aktualizujeme počty hlasů u otázky
                            if (existingVote.VoteValue) // Původně Ano
                                question.YesVotes--;
                            else // Původně Ne
                                question.NoVotes--;

                            // Přidáme nový hlas
                            if (voteValue) // Nově Ano
                                question.YesVotes++;
                            else // Nově Ne
                                question.NoVotes++;

                            // Aktualizujeme hlas uživatele
                            existingVote.VoteValue = voteValue;
                            existingVote.UpdatedAt = now;
                        }

                        // Označíme tuto otázku jako zpracovanou, aby se nevyhodnotila v druhém kroku (mazání)
                        existingVotesByQuestionId.Remove(questionId);
                    }
                    else
                    {
                        // Uživatel ještě nehlasoval pro tuto otázku - vytvoříme nový hlas
                        var newVote = new Vote
                        {
                            VotingQuestionId = questionId,
                            UserId = userId,
                            VoteValue = voteValue,
                            CreatedAt = now,
                            UpdatedAt = now
                        };

                        dbContext.Votes.Add(newVote);

                        // Inkrementujeme příslušný počet hlasů u otázky
                        if (voteValue)
                            question.YesVotes++;
                        else
                            question.NoVotes++;
                    }

                    // Aktualizujeme časovou značku poslední aktualizace otázky
                    question.UpdatedAt = now;
                }

                // V druhém kroku zpracujeme otázky, na které uživatel dříve hlasoval, ale nyní jsou
                // nastaveny na "Nehlasuji" (tzn. nejsou v model.Votes)
                // Všechny zbývající záznamy v existingVotesByQuestionId jsou ty, které by měly být smazány
                foreach (var voteToDelete in existingVotesByQuestionId.Values)
                {
                    // Najdeme odpovídající otázku
                    var question = allQuestionsForDiscussion.FirstOrDefault(q => q.Id == voteToDelete.VotingQuestionId);
                    if (question == null) continue;

                    // Snížíme počet hlasů podle typu původního hlasu
                    if (voteToDelete.VoteValue) // Bylo Ano
                        question.YesVotes--;
                    else // Bylo Ne
                        question.NoVotes--;

                    // Aktualizujeme časovou značku
                    question.UpdatedAt = now;

                    // Smažeme hlas
                    dbContext.Votes.Remove(voteToDelete);
                }

                // Aktualizace UpdatedWhateverAt v diskuzi
                discussion.UpdatedWhateverAt = now;

                // Uložíme změny
                await dbContext.SaveChangesAsync();

                // Potvrdíme transakci
                await transaction.CommitAsync();

                // Načteme aktualizované hlasování pro vrácení
                var updatedQuestions = await dbContext.VotingQuestions
                    .AsNoTracking()
                    .Where(q => q.DiscussionId == model.DiscussionId)
                    .OrderBy(q => q.DisplayOrder)
                    .Select(q => new VotingQuestionDetailDto
                    {
                        Id = q.Id,
                        Text = q.Text,
                        DisplayOrder = q.DisplayOrder,
                        YesVotes = q.YesVotes,
                        NoVotes = q.NoVotes,
                        CurrentUserVote = q.Votes
                            .Where(v => v.UserId == userId)
                            .Select(v => (bool?)v.VoteValue)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                // Vytvoření a vrácení DTO odpovědi
                var result = new VotingDto
                {
                    DiscussionId = model.DiscussionId,
                    VoteType = discussion.VoteType,
                    Questions = updatedQuestions
                };

                return Ok(result);
            }
            catch
            {
                // Při chybě vrátíme transakci
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.Log("Chyba při odesílání hlasů", ex);
            return StatusCode(StatusCodes.Status500InternalServerError, "Při zpracování požadavku došlo k chybě.");
        }
    }

    /// <summary>
    /// Změní stav hlasování (žádné, viditelné, uzavřené, skryté)
    /// Přístupné pouze pro autora diskuze nebo adminy
    /// </summary>
    /// <param name="discussionId">ID diskuze</param>
    /// <param name="voteType">Nový stav hlasování</param>
    /// <returns>Výsledek operace</returns>
    [Authorize]
    [HttpPut("discussion/{discussionId}/status")]
    public async Task<IActionResult> ChangeVotingStatus(int discussionId, [FromQuery] VoteType voteType)
    {
        try
        {
            // Získání aktuálně přihlášeného uživatele
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Ověření, zda diskuze existuje
            var discussion = await dbContext.Discussions
                .FirstOrDefaultAsync(d => d.Id == discussionId && d.Type != DiscussionType.Deleted);

            if (discussion == null)
                return NotFound("Diskuze nebyla nalezena.");

            // Ověření oprávnění - pouze autor diskuze nebo admin může změnit stav hlasování
            if (!isAdmin && discussion.UserId != userId)
                return Forbid();

            // Pokud chceme vypnout hlasování (None), zkontrolujeme, zda existují otázky
            if (voteType == VoteType.None)
            {
                var hasQuestions = await dbContext.VotingQuestions
                    .AnyAsync(q => q.DiscussionId == discussionId);

                if (hasQuestions)
                    return BadRequest("Nelze vypnout hlasování, dokud existují otázky. Nejprve odstraňte všechny otázky.");
            }
            // Pokud zapínáme hlasování, zkontrolujeme, zda existují otázky
            else if (discussion.VoteType == VoteType.None)
            {
                var hasQuestions = await dbContext.VotingQuestions
                    .AnyAsync(q => q.DiscussionId == discussionId);

                if (!hasQuestions)
                    return BadRequest("Pro aktivaci hlasování musí existovat alespoň jedna otázka.");
            }

            // Aktuální čas pro jednotné použití
            var now = DateTime.UtcNow;

            // Aktualizace typu hlasování
            discussion.VoteType = voteType;
            discussion.UpdatedWhateverAt = now;
            await dbContext.SaveChangesAsync();

            return Ok(new { Status = "success", VoteType = voteType });
        }
        catch (Exception ex)
        {
            logger.Log("Chyba při změně stavu hlasování", ex);
            return StatusCode(StatusCodes.Status500InternalServerError, "Při zpracování požadavku došlo k chybě.");
        }
    }
}