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

namespace Zodpovedne.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> userManager;
    private readonly SignInManager<ApplicationUser> signInManager;
    private readonly ApplicationDbContext dbContext;
    private readonly FileLogger _logger;

    private readonly IConfiguration configuration;

    public UsersController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IConfiguration configuration, FileLogger logger)
    {
        this.userManager = userManager;
        this.signInManager = signInManager;
        this.configuration = configuration;
        this.dbContext = dbContext;
        _logger = logger;
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
                .OrderBy(u => u.Nickname);

            var totalUsers = await query.CountAsync();
            var users = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserListDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    Nickname = u.Nickname,
                    LastLogin = u.LastLogin,
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
                Roles = roles.ToList()
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
            // Kontrola existence stejného emailu a stejného nickname
            if (await userManager.FindByEmailAsync(model.Email) != null)
                return BadRequest(new { error = $"Email {model.Email} je již používán." });
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
                // Přidání výchozí role "User"
                await this.userManager.AddToRoleAsync(user, "Member");

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
            // Kontrola existence stejného emailu a stejného nickname
            if (await userManager.FindByEmailAsync(model.Email) != null)
                return BadRequest(new { error = $"Email {model.Email} je již používán." });
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

            // Kontrola existence stejného nickname
            if (await userManager.Users.AnyAsync(u => u.Nickname == model.Nickname))
                return BadRequest("Tato přezdívka je již používána.");

            user.Nickname = model.Nickname;
            var result = await userManager.UpdateAsync(user);

            if (result.Succeeded)
                return Ok();

            return BadRequest(result.Errors);
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
                return BadRequest(result.Errors);

            // Musíme také změnit UserName, protože ten používáme jako login
            result = await userManager.SetUserNameAsync(user, model.Email);
            if (result.Succeeded)
                return Ok();

            return BadRequest(result.Errors);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce UpdateEmail endpointu.", e);
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
                    .Where(c => c.ParentCommentId != null && c.ParentComment.UserId == userId)
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

            if (result.Succeeded)
            {
                return Ok();
            }

            return BadRequest(new { errors = result.Errors });
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce UpdateAuthenticatedUserPassword endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vytvoří JWT token pro přihlášení uživatele ze zadaného emailu a hesla (LoginModel) (zkontroluje zadaný email a heslo a vytvoří JWT token)
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [HttpPost("token")]
    public async Task<IActionResult> CreateToken(LoginModelDto model)
    {
        try
        {
            var user = await this.userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return Unauthorized();

            if (user.Type == UserType.Deleted)
                return Unauthorized();

            //Metoda ověří heslo(false zde znamená, že se nezamkne účet při špatném hesle)
            var result = await this.signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
            if (result.IsLockedOut)
            {
                return Unauthorized(new { message = "Účet je uzamčen" });
            }
            if (result.Succeeded)
            {
                // Uložení předchozího posledního přihlášení
                user.PreviousLastLogin = user.LastLogin;
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

            return Unauthorized();
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce CreateToken endpointu.", e);
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
}