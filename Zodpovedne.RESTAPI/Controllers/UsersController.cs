using Zodpovedne.Data.Models;
using Zodpovedne.Contracts.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Zodpovedne.Contracts.Enums;
using Zodpovedne.Data.Data;
using Zodpovedne.Logging;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using Zodpovedne.RESTAPI.Services;
using Zodpovedne.Logging.Services;

namespace Zodpovedne.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly SignInManager<ApplicationUser> signInManager;
    private readonly FileLogger _logger;
    private readonly IConfiguration configuration;
    private readonly IMemoryCache _cache;
    private readonly IEmailService _emailService;

    public Translator Translator { get; }  // Translator pro překlady textů na stránkách

    public UsersController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IConfiguration configuration, FileLogger logger, IMemoryCache memoryCache, IEmailService emailService, Translator translator)
    {
        this.userManager = userManager;
        this.signInManager = signInManager;
        this.configuration = configuration;
        this.dbContext = dbContext;
        _logger = logger;
        _cache = memoryCache;
        _emailService = emailService;
        Translator = translator ?? throw new ArgumentNullException(nameof(translator));
    }

    /// <summary>
    /// Vrátí netrackovaný seznam všech uživatelů s možností stránkování
    /// </summary>
    /// <param name="page"></param>
    /// <returns></returns>
    [HttpGet("paged")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<ActionResult<PagedResultDto<UserListDto>>> GetPagedUsers(int page = 1)
    {
        try
        {
            var pageSize = 50;
            var query = userManager.Users
                .AsNoTracking()
                .Where(u => u.Type != UserType.Deleted)
                .OrderByDescending(u => u.LastLogin);

            var totalUsers = await query.CountAsync();
            var users = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserListDto
                {
                    Id = u.Id,
                    Email = u.Email ?? "",
                    Nickname = u.Nickname,
                    LastLogin = u.LastLogin,
                    LoginCount = u.LoginCount,
                    Type = u.Type
                })
                .ToListAsync();

            return Ok(new PagedResultDto<UserListDto>
            {
                Items = users,
                TotalCount = totalUsers,
                PageSize = pageSize,
                CurrentPage = page
            });
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce GetPagedUsers endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vrátí informace o přihlášeném uživateli
    /// </summary>
    /// <returns></returns>
    [Authorize]
    [HttpGet("authenticated-user")]
    public async Task<ActionResult<UserProfileDto>> GetAuthenticatedUser()
    {
        try
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return NotFound();

            var roles = await this.userManager.GetRolesAsync(user);

            return Ok(new UserProfileDto
            {
                Id = user.Id,
                Email = user.Email!,
                Nickname = user.Nickname,
                Created = user.Created,
                LastLogin = user.LastLogin,
                Roles = roles.ToList(),
                LoginCount = user.LoginCount,
            });
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce GetAuthenticatedUser endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vrátí informace o uživateli
    /// </summary>
    /// <returns></returns>
    [HttpGet("{nickname}")]
    public async Task<ActionResult<UserProfileDto>> GetUser(string nickname)
    {
        try
        {
            var user = dbContext.Users.Where(u => u.Nickname == nickname).FirstOrDefault();

            if (user == null) return NotFound();

            var roles = await this.userManager.GetRolesAsync(user);

            return Ok(new UserProfileDto
            {
                Id = user.Id,
                Email = user.Email!,
                Nickname = user.Nickname,
                Created = user.Created,
                LastLogin = user.LastLogin,
                Roles = roles.ToList(),
                LoginCount = user.LoginCount
            });
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce GetAuthenticatedUser endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vytvoří nového uživatele s rolí Member
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("member")]
    public async Task<IActionResult> CreateMemberUser(RegisterModelDto model)
    {
        try
        {
            // Kontrola existence stejného emailu a stejného nickname a nepovolených znaků
            if (await userManager.FindByEmailAsync(model.Email) != null)
                return BadRequest($"Email {model.Email} je již používán.");
            if (Regex.IsMatch(model.Nickname, @"[<>&]"))
                return BadRequest(@"Přezdívka obsahuje některý z nepovolených znaků [<>&]");
            if (await userManager.Users.AnyAsync(u => u.Nickname == model.Nickname))
                return BadRequest($"Přezdívka {model.Nickname} je již používána.");

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                Nickname = model.Nickname
            };

            var result = await this.userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                // Přidání výchozí role "User"
                await this.userManager.AddToRoleAsync(user, "Member");

                // V produkci přidat:
                // - Odeslání potvrzovacího emailu
                // - Logování události
                // - Notifikace admina

                return Ok();
            }

            return BadRequest(result.Errors);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce CreateMemberUser endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vytvoří nového uživatele s rolí Admin
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("admin")]
    [Authorize(Policy = "RequireAdminRole")] // Pouze pro adminy
    public async Task<IActionResult> CreateAdminUser(RegisterModelDto model)
    {
        try
        {
            // Kontrola existence stejného emailu a stejného nickname a nepovolených znaků
            if (await userManager.FindByEmailAsync(model.Email) != null)
                return BadRequest(new { error = $"Email {model.Email} je již používán." });
            if (Regex.IsMatch(model.Nickname, @"[<>&]"))
                return BadRequest(@"Přezdívka obsahuje některý z nepovolených znaků [<>&]");
            if (await userManager.Users.AnyAsync(u => u.Nickname == model.Nickname))
                return BadRequest(new { error = $"Přezdívka {model.Nickname} je již používána." });


            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                Nickname = model.Nickname
            };

            var result = await this.userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                // Přidání výchozí role "Admin"
                await this.userManager.AddToRoleAsync(user, "Admin");

                // V produkci přidat:
                // - Odeslání potvrzovacího emailu
                // - Logování události
                // - Notifikace admina

                return Ok();
            }

            return BadRequest(new { errors = result.Errors });
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce CreateAdminUser endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Aktualizuje přezdívku přihlášeného uživatele
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [Authorize]
    [HttpPut("authenticated-user/nickname")]
    public async Task<IActionResult> UpdateNickname(UpdateNicknameDto model)
    {
        try
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return NotFound();

            // Kontrola znaků v nickname a existence stejného nickname
            if (Regex.IsMatch(model.Nickname, @"[<>&]"))
                return BadRequest(@"Přezdívka obsahuje některý z nepovolených znaků [<>&]");
            if (await userManager.Users.AnyAsync(u => u.Nickname == model.Nickname))
                return BadRequest("Tato přezdívka je již používána.");

            user.Nickname = model.Nickname;
            var result = await userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return BadRequest("Chyba při změně přezdívky");

            return Ok();
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce UpdateNickname endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Aktualizuje email přihlášeného uživatele
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPut("authenticated-user/email")]
    public async Task<IActionResult> UpdateEmail(UpdateEmailDto model)
    {
        try
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return NotFound();

            // Kontrola existence stejného emailu
            if (await userManager.FindByEmailAsync(model.Email) != null)
                return BadRequest("Tento email je již používán.");

            // Použití vestavěné metody Identity, která se postará o všechny potřebné změny
            var result = await userManager.SetEmailAsync(user, model.Email);
            if (!result.Succeeded)
                return BadRequest("Chyba při změně emailu");

            // Musíme také změnit UserName, protože ten používáme jako login
            result = await userManager.SetUserNameAsync(user, model.Email);
            if (!result.Succeeded)
                return BadRequest("Chyba při změně uživatelského jména (email)");

            return Ok();
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce UpdateEmail endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Změní heslo přihlášeného uživatele
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [HttpPut("authenticated-user/password")]
    public async Task<IActionResult> UpdateAuthenticatedUserPassword(ChangePasswordModelDto model)
    {
        try
        {
            // Získá aktuálně přihlášeného uživatele (z tokenu v http hlavičce Authorization)
            var user = await GetCurrentUserAsync();
            if (user == null) return NotFound();

            var result = await this.userManager.ChangePasswordAsync(
                user,
                model.CurrentPassword,
                model.NewPassword
            );

            if (!result.Succeeded)
            {
                // Standardizovaná chyba ve formátu ProblemDetails
                /*var problemDetails = new ValidationProblemDetails
                {
                    Title = "Password change failed",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "One or more errors occurred while changing the password.",
                    Instance = HttpContext.Request.Path
                };

                // Přidání chyb Identity API do formátu ProblemDetails
                foreach (var error in result.Errors)
                {
                    problemDetails.Errors.Add(error.Code, new[] { error.Description });
                }

                return BadRequest(problemDetails);*/
                return BadRequest("Nesprávné heslo");
            }

            return Ok();
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce UpdateAuthenticatedUserPassword endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Smaže uživatele podle ID
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    [HttpDelete("user/{userId}")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> DeleteUser([FromRoute] string userId)
    {
        try
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            user.Type = UserType.Deleted;
            await userManager.UpdateAsync(user);
            return Ok();
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce DeleteUser endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Provede úplné smazání uživatele a všech jeho dat z databáze.
    /// Přístupné pouze pro samotného uživatele nebo admina.
    /// </summary>
    [Authorize]
    [HttpDelete("permanently/{userId}")]
    public async Task<IActionResult> DeleteUserPermanently([FromRoute] string userId)
    {
        try
        {
            // Kontrola oprávnění - může mazat jen admin nebo samotný uživatel
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && currentUserId != userId)
                return Forbid();

            // Najdeme uživatele
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            using var transaction = await dbContext.Database.BeginTransactionAsync();
            try
            {
                // 1. Smažeme všechny lajky diskuzí od uživatele
                await dbContext.DiscussionLikes
                    .Where(l => l.UserId == userId)
                    .ExecuteDeleteAsync();

                // 2. Smažeme všechny lajky komentářů od uživatele
                await dbContext.CommentLikes
                    .Where(l => l.UserId == userId)
                    .ExecuteDeleteAsync();

                // 3. Smažeme všechny reakční komentáře od uživatele
                await dbContext.Comments
                    .Where(c => c.UserId == userId && c.ParentCommentId != null)
                    .ExecuteDeleteAsync();

                // 4. Smažeme všechny reakční komentáře na rootové komentáře mazaného uživatele
                await dbContext.Comments
                    .Where(c => c.ParentCommentId != null && c.ParentComment != null && c.ParentComment.UserId == userId)
                    .ExecuteDeleteAsync();

                // 5. Smažeme všechny komentáře od uživatele
                await dbContext.Comments
                    .Where(c => c.UserId == userId)
                    .ExecuteDeleteAsync();

                // 6. Smažeme všechny lajky na diskuzích uživatele
                await dbContext.DiscussionLikes
                    .Where(l => l.Discussion.UserId == userId)
                    .ExecuteDeleteAsync();

                // 7. Smažeme všechny diskuze uživatele
                await dbContext.Discussions
                    .Where(d => d.UserId == userId)
                    .ExecuteDeleteAsync();

                // 8. Smažeme samotného uživatele
                await userManager.DeleteAsync(user);

                await transaction.CommitAsync();
                return Ok();
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                _logger.Log("Chyba při vykonávání transakce v akci DeleteUserPermanently endpointu.", e);
                return BadRequest();
            }
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce DeleteUserPermanently endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Změní viditelnost uživatele podle jeho ID, používá se pro skrytí uživatele z veřejného seznamu
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    [HttpPut("{userId}/toggle-visibility")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> ToggleUserVisibility(string userId)
    {
        try
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            user.Type = user.Type == UserType.Normal ? UserType.Hidden : UserType.Normal;
            await userManager.UpdateAsync(user);

            return Ok(new { type = user.Type });
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce ToggleUserVisibility endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vytvoří JWT token pro přihlášení uživatele ze zadaného emailu a hesla (LoginModel) (zkontroluje zadaný email a heslo a vytvoří JWT token)
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [HttpPost("login")]
    public async Task<IActionResult> CreateToken(LoginModelDto model)
    {
        try
        {
            var user = await this.userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                _logger.Log($"Při pokusu o přihlášení nebyl uživatel s emailem {model.Email} nalezen.");
                return Unauthorized();
            }

            if (user.Type == UserType.Deleted)
            {
                _logger.Log($"Při pokusu o přihlášení byl uživatel s emailem {model.Email} identifikován se stavem smazaný.");
                return Unauthorized();
            }

            //Metoda ověří heslo(false zde znamená, že se nezamkne účet při špatném hesle)
            var result = await this.signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
            if (result.IsLockedOut)
            {
                _logger.Log($"Při pokusu o přihlášení uživatele s emailem {model.Email} bylo identifikováno, že uživatel je dočasně uzamknut pro přihlášení.");
                return Unauthorized(new { message = "Účet je uzamčen" });
            }
            if (!result.Succeeded)
            {
                _logger.Log($"Při pokusu o přihlášení uživatele s emailem {model.Email} bylo identifikováno špatně zadané heslo.");
                return Unauthorized();
            }
            // Uložení předchozího posledního přihlášení
            user.PreviousLastLogin = user.LastLogin;

            // Inkrementace počtu přihlášeních
            user.LoginCount++;
            // Aktualizace posledního přihlášení a rovnou uložení do databáze (SaveChanges)
            user.LastLogin = DateTime.UtcNow;
            await this.userManager.UpdateAsync(user);

            var token = await GenerateJwtToken(user);

            return Ok(new TokenResponseDto
            {
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddHours(Convert.ToDouble(this.configuration["Jwt:ExpirationInHours"] ?? throw new ArgumentNullException("JWT ExpirationInHours není vyplněn v konfiguračním souboru"))),
                Email = user.Email!,
                Nickname = user.Nickname
            });
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce CreateToken endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Zpracovává odhlášení uživatele, aktualizuje LastLogin na aktuální čas
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        try
        {
            // Získání ID přihlášeného uživatele
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.Log("Při odhlašování nebyl nalezen userId aktuálního uživatel");
                return Unauthorized("Při odhlašování nebyl nalezen userId aktuálního uživatel");
            }

            // Vyhledání uživatele
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.Log("Při odhlašování nebyl nalezen aktuální uživatel v databázi");
                return NotFound("Při odhlašování nebyl nalezen aktuální uživatel v databázi");
            }

            // Aktualizace LastLogin na aktuální čas
            user.LastLogin = DateTime.UtcNow;
            var result = await userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                _logger.Log($"Při odhlašování uživatele {user.Id} se nepodařilo aktualizovat je lastLogin");
                return BadRequest($"Při odhlašování uživatele {user.Id} se nepodařilo aktualizovat je lastLogin");
            }

            return Ok();
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce Logout endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Provede vyčištění databáze od záznamů označených jako smazané (Delete)
    /// Tato operace je dostupná pouze pro administrátory.
    /// </summary>
    /// <returns>Informace o počtech smazaných entit</returns>
    [HttpPost("cleanup-deleted")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<ActionResult<CleanupResultDto>> CleanupDeletedData()
    {
        try
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync();

            var result = new CleanupResultDto();

            try
            {
                // 1. Smazání lajků na komentářích se stavem Deleted
                var deletedCommentLikes = await dbContext.CommentLikes
                    .Where(cl => cl.Comment.Type == CommentType.Deleted)
                    .ExecuteDeleteAsync();

                result.DeletedCommentLikes = deletedCommentLikes;

                // 2. Smazání lajků na diskuzích se stavem Deleted
                var deletedDiscussionLikes = await dbContext.DiscussionLikes
                    .Where(dl => dl.Discussion.Type == DiscussionType.Deleted)
                    .ExecuteDeleteAsync();

                result.DeletedDiscussionLikes = deletedDiscussionLikes;

                // 3. Smazání odpovědí na komentáře (reakční komentáře) se stavem Deleted
                var deletedReplies = await dbContext.Comments
                    .Where(c => c.Type == CommentType.Deleted && c.ParentCommentId != null)
                    .ExecuteDeleteAsync();

                result.DeletedCommentReplies = deletedReplies;

                // 4. Smazání root komentářů se stavem Deleted
                var deletedRootComments = await dbContext.Comments
                    .Where(c => c.Type == CommentType.Deleted && c.ParentCommentId == null)
                    .ExecuteDeleteAsync();

                result.DeletedRootComments = deletedRootComments;

                // 5. Příprava na smazání uživatelů se stavem Deleted
                // Získáme ID uživatelů, které budeme mazat
                var usersToDelete = await dbContext.Users
                    .Where(u => u.Type == UserType.Deleted)
                    .Select(u => u.Id)
                    .ToListAsync();

                // Pro každého uživatele ke smazání musíme nejprve odstranit všechna jeho data
                int deletedUserDiscussions = 0;
                int deletedUserComments = 0;

                foreach (var userId in usersToDelete)
                {
                    // Smazání všech lajků komentářů, které uživatel vytvořil (i v cizích diskuzích)
                    await dbContext.CommentLikes
                        .Where(cl => cl.Comment.UserId == userId)
                        .ExecuteDeleteAsync();

                    // Smazání všech lajků, které uživatel dal na komentáře
                    await dbContext.CommentLikes
                        .Where(cl => cl.UserId == userId)
                        .ExecuteDeleteAsync();

                    // Smazání všech lajků, které uživatel dal na diskuze
                    await dbContext.DiscussionLikes
                        .Where(dl => dl.UserId == userId)
                        .ExecuteDeleteAsync();

                    // Smazání všech diskuzí uživatele a jejich obsahu
                    var discussionIds = await dbContext.Discussions
                        .Where(d => d.UserId == userId)
                        .Select(d => d.Id)
                        .ToListAsync();

                    foreach (var discussionId in discussionIds)
                    {
                        // Smazání všech lajků na komentářích v diskuzi
                        await dbContext.CommentLikes
                            .Where(cl => cl.Comment.DiscussionId == discussionId)
                            .ExecuteDeleteAsync();

                        // Smazání všech komentářů v diskuzi
                        await dbContext.Comments
                            .Where(c => c.DiscussionId == discussionId)
                            .ExecuteDeleteAsync();

                        // Smazání všech lajků na diskuzi
                        await dbContext.DiscussionLikes
                            .Where(dl => dl.DiscussionId == discussionId)
                            .ExecuteDeleteAsync();
                    }

                    // Smazání všech odpovědí na komentáře (reakční komentáře) vytvořené uživatelem
                    // Poznámka: Musíme nejprve smazat všechny reakce, protože na ně mohou odkazovat i jiné komentáře
                    var deletedUserReplies = await dbContext.Comments
                        .Where(c => c.UserId == userId && c.ParentCommentId != null)
                        .ExecuteDeleteAsync();

                    deletedUserComments += deletedUserReplies;

                    // Smazání všech root komentářů vytvořených uživatelem, ale nejprve musíme odstranit všechny odpovědi na ně
                    // Nejprve najdeme ID všech root komentářů uživatele
                    var rootCommentIds = await dbContext.Comments
                        .Where(c => c.UserId == userId && c.ParentCommentId == null)
                        .Select(c => c.Id)
                        .ToListAsync();

                    // Smazání všech odpovědí na root komentáře uživatele (i od jiných uživatelů)
                    foreach (var rootCommentId in rootCommentIds)
                    {
                        // Nejprve smazat lajky na odpovědích
                        await dbContext.CommentLikes
                            .Where(cl => cl.Comment.ParentCommentId == rootCommentId)
                            .ExecuteDeleteAsync();

                        // Pak smazat samotné odpovědi
                        var deletedReplies2 = await dbContext.Comments
                            .Where(c => c.ParentCommentId == rootCommentId)
                            .ExecuteDeleteAsync();

                        deletedUserComments += deletedReplies2;
                    }

                    // Nyní můžeme smazat root komentáře
                    var deletedUserRootComments = await dbContext.Comments
                        .Where(c => c.UserId == userId && c.ParentCommentId == null)
                        .ExecuteDeleteAsync();

                    deletedUserComments += deletedUserRootComments;

                    // Nyní můžeme smazat diskuze uživatele
                    var deletedDiscussions2 = await dbContext.Discussions
                        .Where(d => d.UserId == userId)
                        .ExecuteDeleteAsync();

                    deletedUserDiscussions += deletedDiscussions2;
                }

                result.DeletedUserDiscussions = deletedUserDiscussions;
                result.DeletedUserComments = deletedUserComments;

                // 6. Smazání diskuzí se stavem Deleted (které ještě nebyly smazány v předchozím kroku)
                var deletedDiscussions = await dbContext.Discussions
                    .Where(d => d.Type == DiscussionType.Deleted)
                    .ExecuteDeleteAsync();

                result.DeletedDiscussions = deletedDiscussions;

                // 7. Nyní můžeme smazat samotné uživatele a jejich identity entity
                foreach (var userId in usersToDelete)
                {
                    // Smazání tokenů
                    await dbContext.UserTokens
                        .Where(ut => ut.UserId == userId)
                        .ExecuteDeleteAsync();

                    // Smazání claimů
                    await dbContext.UserClaims
                        .Where(uc => uc.UserId == userId)
                        .ExecuteDeleteAsync();

                    // Smazání loginů
                    await dbContext.UserLogins
                        .Where(ul => ul.UserId == userId)
                        .ExecuteDeleteAsync();
                }

                // Nakonec smazat samotné uživatele
                var deletedUsers = await dbContext.Users
                    .Where(u => u.Type == UserType.Deleted)
                    .ExecuteDeleteAsync();

                result.DeletedUsers = deletedUsers;

                // Potvrzení transakce
                await transaction.CommitAsync();

                _logger.Log($"Administrátor {User.FindFirstValue(ClaimTypes.NameIdentifier)} provedl vyčištění databáze");

                return Ok(result);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.Log("Chyba při čištění databáze", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "Nastala chyba při čištění databáze.");
            }
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce CleanupDeletedData endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vrátí seznam diskuzí, kde má přihlášený uživatel nové odpovědi na své komentáře od posledního přihlášení.
    /// </summary>
    /// <returns>Seznam diskuzí s informacemi o nových odpovědích</returns>
    [Authorize]
    [HttpGet("discussions-with-new-replies")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)] // Zakázání kešování pro vždy aktuální data
    public async Task<ActionResult<IEnumerable<DiscussionWithNewRepliesDto>>> GetDiscussionsWithNewReplies()
    {
        try
        {
            // Získání ID přihlášeného uživatele z JWT tokenu
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Vyhledání uživatele v databázi pro získání času předchozího přihlášení
            var user = await userManager.FindByIdAsync(userId);
            if (user == null || user.PreviousLastLogin == null)
                return Ok(new List<DiscussionWithNewRepliesDto>()); // Pokud nemáme předchozí přihlášení, nemůžeme zjistit nové odpovědi

            //Pokus o načtení dat z keše. Pokud se načtou, tak se vrátí a metoda skončí, pokud se nenajdou, tak se musí zjistit z databáze a uložit do keše
            // Vytvoření klíče pro keš, který obsahuje:
            // - ID uživatele (každý uživatel má vlastní keš)
            // - Časové razítko posledního přihlášení (invaliduje keš při novém přihlášení)
            var cacheKey = $"NewReplies_{userId}_{user.LastLogin?.Ticks}";
            // Pokus o získání dat z keše
            // TryGetValue vrací true, pokud klíč existuje a hodnota je uložena do cachedResult
            if (_cache.TryGetValue(cacheKey, out List<DiscussionWithNewRepliesDto>? cachedResult))
            {
                return Ok(cachedResult);
            }

            //V keši nebyl seznam nalezen, takže se musí získat z databáze (pak uložit do keše) viz kód níže

            // Čas, od kterého hledáme nové odpovědi (předchozí přihlášení)
            var fromTime = user.PreviousLastLogin.Value;

            // 1. KROK: Efektivně najít ID root komentářů uživatele, které mají nové odpovědi. K tomu ještě čas nejnovější odpovědi.
            var rootCommentsWithNewReplies = await dbContext.Comments
                .AsNoTracking() // Pro lepší výkon - nesledujeme změny entit
                .Where(c => c.UserId == userId && c.ParentCommentId == null) // Jen rootové komentáře uživatele
                .Where(c => c.Replies.Any(r =>
                    r.CreatedAt > fromTime && // Odpověď je novější než předchozí přihlášení
                    r.UserId != userId &&     // Odpověď není od samotného uživatele
                    r.Type != CommentType.Deleted)) // Odpověď není smazaná
                .Select(c => new
                {
                    CommentId = c.Id,
                    DiscussionId = c.DiscussionId,
                    // Pro každý komentář najdeme nejnovější odpověď a pouze čas této odpovědi
                    LatestReplyTime = c.Replies
                        .Where(r => r.CreatedAt > fromTime && r.UserId != userId && r.Type != CommentType.Deleted)
                        .Max(r => r.CreatedAt)
                })
                .ToListAsync();

            // Výsledný seznam diskuzí s informacemi o nových odpovědích, zatím je prázdný
            var result = new List<DiscussionWithNewRepliesDto>();

            // Pokud uživatel má nějaké komentáře s novými odpověďmi, zjistíme o nich další informace
            if (rootCommentsWithNewReplies.Any())
            {
                // 2. KROK: Seskupit komentáře podle diskuzí
                // ----------------------------------------
                // Efektivní způsob seskupení bez další databázové operace
                var discussionGroups = rootCommentsWithNewReplies
                    .GroupBy(c => c.DiscussionId)
                    .Select(g => new
                    {
                        DiscussionId = g.Key,
                        LatestReplyTime = g.Max(c => c.LatestReplyTime), // Nejnovější odpověď v diskuzi
                        CommentsCount = g.Count() // Počet komentářů uživatele s novými odpověďmi
                    })
                    .OrderByDescending(g => g.LatestReplyTime) // Seřazení podle času nejnovější odpovědi
                    .ToList();

                // 3. KROK: Získat seznam ID diskuzí, které potřebujeme načíst
                // ---------------------------------------------------------
                var discussionIds = discussionGroups.Select(g => g.DiscussionId).ToArray();

                // 4. KROK: Efektivně načíst pouze potřebné informace o diskuzích
                // ------------------------------------------------------------
                // Načteme pouze potřebné informace o diskuzích v jednom dotazu
                var discussions = await dbContext.Discussions
                    .AsNoTracking()
                    .Where(d => discussionIds.Contains(d.Id))
                    .Include(d => d.Category) // Potřebujeme kategorii pro URL a název kategorie
                    .Select(d => new
                    {
                        d.Id,
                        d.Title,
                        DiscussionCode = d.Code,
                        d.Category.Name,
                        CategoryCode = d.Category.Code
                    })
                    .ToDictionaryAsync(d => d.Id); // Pro rychlý přístup podle ID diskuze

                // 5. KROK: Sestavení výsledku
                // --------------------------
                // Sestavíme výsledné DTO objekty s informacemi o diskuzích a nových odpovědích
                foreach (var group in discussionGroups)
                {
                    // Kontrola, zda máme informace o diskuzi (pro případ nesouladu dat)
                    if (discussions.TryGetValue(group.DiscussionId, out var discussion))
                    {
                        result.Add(new DiscussionWithNewRepliesDto
                        {
                            DiscussionId = group.DiscussionId,
                            Title = discussion.Title,
                            DiscussionUrl = $"/Categories/{discussion.CategoryCode}/{discussion.DiscussionCode}",
                            CategoryName = discussion.Name,
                            LatestReplyTime = group.LatestReplyTime,
                            CommentsWithNewRepliesCount = group.CommentsCount
                        });
                    }
                }
            }

            // Uložení výsledku do keše a vrácení výsledku

            // Nastavení možností kešování
            var cacheOptions = new MemoryCacheEntryOptions()
            // Absolutní expirace - data v keši vydrží maximálně 60 sekund
            // Zajišťuje, že i během session uživatel uvidí nová data nejpozději po minutě
            .SetAbsoluteExpiration(TimeSpan.FromSeconds(60))
            // Nastavení velikosti položky pro případnou správu paměti
            .SetSize(1);
            // Uložení výsledku do keše
            _cache.Set(cacheKey, result, cacheOptions);

            return Ok(result);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce GetDiscussionsWithNewReplies endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    // Endpoint pro žádost o obnovení hesla
    [HttpPost("forgot-password")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Neindikujeme, že uživatel neexistuje, z bezpečnostních důvodů
                return Ok();
            }

            // Vygenerování tokenu pro reset hesla
            var token = await userManager.GeneratePasswordResetTokenAsync(user);

            // Zakódování tokenu pro bezpečný přenos v URL
            var encodedToken = WebUtility.UrlEncode(token);

            // Vytvoření odkazu pro reset hesla
            var resetLink = $"{model.ResetPageUrl}?email={WebUtility.UrlEncode(user.Email)}&token={encodedToken}";

            // Odeslání e-mailu s odkazem
            if (user.Email == null)
            {
                _logger.Log("Byl proveden pokus o odeslání emailu pro obnovu hesla, ale email byl null.");
                return BadRequest();
            }
            await _emailService.SendPasswordResetEmailAsync(user.Email, user.Nickname, resetLink);

            return Ok();
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce ForgotPassword.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    // Endpoint pro resetování hesla pomocí tokenu
    [HttpPost("reset-password")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Neindikujeme, že uživatel neexistuje, z bezpečnostních důvodů
                return BadRequest(new { error = "Neplatný požadavek" });
            }

            // Reset hesla pomocí tokenu
            var result = await userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);

            if (result.Succeeded)
            {
                return Ok();
            }

            return BadRequest(new { error = "Neplatný token nebo heslo nesplňuje požadavky" });
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce ResetPassword.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Generuje JWT token pro uživatele
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    private async Task<string> GenerateJwtToken(ApplicationUser user)
    {
        if ((user == null) || (String.IsNullOrEmpty(user.Email)))
        {
            throw new ArgumentNullException("Při generování tokenu nebyl vyplněn uživatel nebo jeho email.");
        }

        var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Email, user.Email),

                //Používá se pro sledování a případné zneplatnění konkrétních tokenů, momentálně není využito
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

                // Obsahuje ID uživatele z databáze, momentálně není využito
                new Claim(JwtRegisteredClaimNames.NameId, user.Id)
            };

        // Přidání rolí do claims
        var roles = await this.userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.configuration["Jwt:Key"] ?? throw new ArgumentNullException("JWT Key není vyplněn v konfiguračním souboru")));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: this.configuration["Jwt:Issuer"] ?? throw new ArgumentNullException("JWT Issuer není vyplněn v konfiguračním souboru"),
            audience: this.configuration["Jwt:Audience"] ?? throw new ArgumentNullException("JWT Audience není vyplněn v konfiguračním souboru"),
            claims: claims,
            expires: DateTime.UtcNow.AddHours(Convert.ToDouble(this.configuration["Jwt:ExpirationInHours"] ?? throw new ArgumentNullException("JWT ExpirationInHours není vyplněn v konfiguračním souboru"))),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    /// <summary>
    /// Získá aktuálně přihlášeného uživatele z tokenu (z hlavičky Authorization z http požadavku na endpoint)
    /// User je property z ControllerBase, která obsahuje informace o přihlášeném uživateli extrahované z tokenu. ASP.NET Core ji automaticky naplní z Authorization hlavičky (místo tokenu by to mohlo být třeba autorizační cookie...)
    /// </summary>
    /// <returns>Instance uživatele nebo null</returns>
    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (email == null) return null;

        var user = await this.userManager.FindByEmailAsync(email);
        return user;
    }








    /// <summary>
    /// Endpoint pro získání stavu přátelství mezi dvěma uživateli.
    /// Vrací null, pokud záznam o přátelství neexistuje, nebo stav přátelství.
    /// </summary>
    /// <param name="targetUserId">ID cílového uživatele, u kterého zjišťujeme stav přátelství</param>
    /// <returns>Status přátelství nebo null, pokud neexistuje</returns>
    [Authorize]
    [HttpGet("friendship-status/{targetUserId}")]
    public async Task<ActionResult<FriendshipStatus?>> GetFriendshipStatus(string targetUserId)
    {
        try
        {
            // Získání ID přihlášeného uživatele (žadatele)
            var requesterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(requesterId))
                return Unauthorized();

            // Kontrola, že nejde o žádost na zjištění přátelství se sebou samým
            if (requesterId == targetUserId)
                return BadRequest("Nelze zjistit stav přátelství sám se sebou.");

            // Vyhledání záznamu přátelství v obou směrech
            // (buď přihlášený je žadatel, nebo je schvalovatel)
            var friendship = await dbContext.Friendships
                .FirstOrDefaultAsync(f =>
                    (f.ApproverUserId == targetUserId && f.RequesterUserId == requesterId) ||
                    (f.ApproverUserId == requesterId && f.RequesterUserId == targetUserId));

            // Pokud záznam neexistuje, vrátíme null
            if (friendship == null)
                return Ok((FriendshipStatus?)null);

            // Jinak vrátíme status přátelství
            return Ok(friendship.FriendshipStatus);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při zjišťování stavu přátelství", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Endpoint pro vytvoření žádosti o přátelství.
    /// Před vytvořením kontroluje, zda již nějaká vazba mezi uživateli neexistuje.
    /// </summary>
    /// <param name="targetUserId">ID cílového uživatele, kterému zasíláme žádost</param>
    /// <returns>Výsledek operace</returns>
    [Authorize]
    [HttpPost("request-friendship/{targetUserId}")]
    public async Task<IActionResult> RequestFriendship(string targetUserId)
    {
        try
        {
            // Získání ID přihlášeného uživatele (žadatele)
            var requesterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(requesterId))
                return Unauthorized();

            // Kontrola, že uživatel nežádá o přátelství sám sebe
            if (requesterId == targetUserId)
                return BadRequest("Nelze žádat o přátelství sám sebe.");

            // Kontrola existence cílového uživatele
            var targetUser = await userManager.FindByIdAsync(targetUserId);
            if (targetUser == null)
                return NotFound("Uživatel nebyl nalezen.");

            // Kontrola existence vazby přátelství v obou směrech
            var existingFriendship = await dbContext.Friendships
                .FirstOrDefaultAsync(f =>
                    (f.ApproverUserId == targetUserId && f.RequesterUserId == requesterId) ||
                    (f.ApproverUserId == requesterId && f.RequesterUserId == targetUserId));

            // Pokud vazba už existuje, vrátíme chybu
            if (existingFriendship != null)
                return BadRequest("Žádost o přátelství nebo přátelství již existuje.");

            // Vytvoření nové žádosti o přátelství
            var friendship = new Friendship
            {
                RequesterUserId = requesterId,    // ID žadatele (přihlášený uživatel)
                ApproverUserId = targetUserId,    // ID schvalovatele (cílový uživatel)
                FriendshipStatus = FriendshipStatus.Requested,  // Počáteční stav - žádost odeslána
                CreatedAt = DateTime.UtcNow       // Aktuální čas vytvoření
            };

            // Přidání do databáze a uložení změn
            dbContext.Friendships.Add(friendship);
            await dbContext.SaveChangesAsync();

            return Ok();
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vytváření žádosti o přátelství", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vrací seznam všech přátelství pro přihlášeného uživatele.
    /// Zahrnuje jak žádosti o přátelství, tak již potvrzená přátelství.
    /// </summary>
    /// <returns>Seznam přátelství s informacemi o uživatelích</returns>
    [Authorize]
    [HttpGet("friendships")]
    public async Task<ActionResult<IEnumerable<object>>> GetUserFriendships()
    {
        try
        {
            // Získání ID přihlášeného uživatele
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Vyhledání všech přátelství, kde uživatel figuruje
            // jako žadatel nebo schvalovatel
            var friendships = await dbContext.Friendships
                .Include(f => f.ApproverUser)    // Načtení souvisejících uživatelů pro přístup k jejich datům
                .Include(f => f.RequesterUser)   // Načtení souvisejících uživatelů pro přístup k jejich datům
                .Where(f =>
                    // Buď je přihlášený uživatel approver a requester je normal
                    (f.ApproverUserId == userId && f.RequesterUser.Type == UserType.Normal) ||
                    // Nebo je přihlášený uživatel requester a approver je normal
                    (f.RequesterUserId == userId && f.ApproverUser.Type == UserType.Normal))
                .ToListAsync();

            // Transformace dat pro vrácení klientovi
            var result = friendships.Select(f =>
            {
                // Zjištění, kdo je druhý uživatel (ten, který NENÍ přihlášený)
                var otherUser = f.ApproverUserId == userId ? f.RequesterUser : f.ApproverUser;

                // Zjištění role přihlášeného uživatele
                var isRequester = f.RequesterUserId == userId;

                return new
                {
                    FriendshipId = f.Id,
                    OtherUserId = otherUser.Id,
                    OtherUserNickname = otherUser.Nickname,
                    Status = f.FriendshipStatus,
                    IsRequester = isRequester,   // Indikuje, zda přihlášený uživatel je původním žadatelem
                    CreatedAt = f.CreatedAt
                };
            })
            // Řazení: nejprve žádosti o přátelství, pak potvrzená přátelství
            // a v rámci každé skupiny abecedně podle přezdívky
            .OrderBy(f => f.Status == FriendshipStatus.Requested ? 0 : 1)
            .ThenBy(f => f.OtherUserNickname);

            return Ok(result);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při načítání přátelství", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Schválí žádost o přátelství.
    /// </summary>
    /// <param name="friendshipId">ID záznamu přátelství</param>
    /// <returns>Výsledek operace</returns>
    [Authorize]
    [HttpPost("friendships/{friendshipId}/approve")]
    public async Task<IActionResult> ApproveFriendship(int friendshipId)
    {
        try
        {
            // Získání ID přihlášeného uživatele
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Vyhledání záznamu přátelství
            var friendship = await dbContext.Friendships.FindAsync(friendshipId);
            if (friendship == null)
                return NotFound("Žádost o přátelství nebyla nalezena.");

            // Kontrola, zda přihlášený uživatel je schvalovatel
            if (friendship.ApproverUserId != userId)
                return Forbid();

            // Kontrola, zda je žádost ve stavu Requested
            if (friendship.FriendshipStatus != FriendshipStatus.Requested)
                return BadRequest("Tato žádost o přátelství nemůže být schválena.");

            // Změna stavu na Approved
            friendship.FriendshipStatus = FriendshipStatus.Approved;
            await dbContext.SaveChangesAsync();

            return Ok();
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při schvalování žádosti o přátelství", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Zamítne žádost o přátelství.
    /// </summary>
    /// <param name="friendshipId">ID záznamu přátelství</param>
    /// <returns>Výsledek operace</returns>
    [Authorize]
    [HttpPost("friendships/{friendshipId}/deny")]
    public async Task<IActionResult> DenyFriendship(int friendshipId)
    {
        try
        {
            // Získání ID přihlášeného uživatele
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Vyhledání záznamu přátelství
            var friendship = await dbContext.Friendships.FindAsync(friendshipId);
            if (friendship == null)
                return NotFound("Žádost o přátelství nebyla nalezena.");

            // Kontrola, zda přihlášený uživatel je schvalovatel
            if (friendship.ApproverUserId != userId)
                return Forbid();

            // Kontrola, zda je žádost ve stavu Requested
            if (friendship.FriendshipStatus != FriendshipStatus.Requested)
                return BadRequest("Tato žádost o přátelství nemůže být zamítnuta.");

            // Změna stavu na Denied
            friendship.FriendshipStatus = FriendshipStatus.Denied;
            await dbContext.SaveChangesAsync();

            return Ok();
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při zamítání žádosti o přátelství", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Odstraní přátelství mezi uživateli.
    /// </summary>
    /// <param name="friendshipId">ID záznamu přátelství</param>
    /// <returns>Výsledek operace</returns>
    [Authorize]
    [HttpDelete("friendships/{friendshipId}")]
    public async Task<IActionResult> RemoveFriendship(int friendshipId)
    {
        try
        {
            // Získání ID přihlášeného uživatele
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Vyhledání záznamu přátelství
            var friendship = await dbContext.Friendships.FindAsync(friendshipId);
            if (friendship == null)
                return NotFound("Přátelství nebylo nalezeno.");

            // Kontrola, zda je přihlášený uživatel jednou ze stran přátelství
            if (friendship.ApproverUserId != userId && friendship.RequesterUserId != userId)
                return Forbid();

            // Odstranění přátelství z databáze
            dbContext.Friendships.Remove(friendship);
            await dbContext.SaveChangesAsync();

            return Ok();
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při odstraňování přátelství", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vrací počet žádostí o přátelství, které čekají na schválení přihlášeným uživatelem.
    /// </summary>
    /// <returns>Počet čekajících žádostí o přátelství</returns>
    [Authorize]
    [ResponseCache(Duration = 20, VaryByHeader = "Authorization")] // Cachování na 20 sekund, různé pro různé uživatele
    [HttpGet("friendship-requests-count")]
    public async Task<ActionResult<int>> GetFriendshipRequestsCount()
    {
        try
        {
            // Získání ID přihlášeného uživatele
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Zjištění počtu žádostí o přátelství, které čekají na schválení
            // Ignorujeme žádosti od skrytých nebo smazaných uživatelů
            var requestsCount = await dbContext.Friendships
                .Where(f => f.ApproverUserId == userId &&
                       f.FriendshipStatus == FriendshipStatus.Requested &&
                       f.RequesterUser.Type == UserType.Normal) // Pouze normální uživatelé
                .CountAsync();

            return Ok(requestsCount);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při zjišťování počtu žádostí o přátelství", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}