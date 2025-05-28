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
using Zodpovedne.RESTAPI.Controllers;
//using Microsoft.Extensions.Logging;
//using Zodpovedne.Data.Migrations;

namespace Zodpovedne.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerZodpovedneBase
{
    private readonly SignInManager<ApplicationUser> signInManager;
    private readonly IConfiguration configuration;
    private readonly IMemoryCache _cache;
    private readonly IEmailService _emailService;

    public UsersController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, FileLogger logger, Translator translator, SignInManager<ApplicationUser> signInManager, IConfiguration configuration, IMemoryCache memoryCache, IEmailService emailService)
        : base(dbContext, userManager, logger, translator)
    {
        this.signInManager = signInManager;
        this.configuration = configuration;
        _cache = memoryCache;
        _emailService = emailService;
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
            logger.Log("Chyba při vykonávání akce GetPagedUsers endpointu.", e);
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
                UserType = user.Type
            });
        }
        catch (Exception e)
        {
            logger.Log("Chyba při vykonávání akce GetAuthenticatedUser endpointu.", e);
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
            // Case-insensitive vyhledávání uživatele pomocí EF.Functions.ILike
            // EF Core přeloží toto na SQL dotaz používající ILIKE operátor v PostgreSQL
            var user = await dbContext.Users
                .Where(u => EF.Functions.ILike(u.Nickname, nickname))
                .FirstOrDefaultAsync();

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
                UserType = user.Type
            });
        }
        catch (Exception e)
        {
            logger.Log("Chyba při vykonávání akce GetAuthenticatedUser endpointu.", e);
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

            // Odstranění mezer z začátku a konce a normalizace vstupu
            var normalizedNickname = model.Nickname.Trim();

            // Kontrola neviditelných znaků a dalších problematických znaků
            if (ContainsInvisibleCharacters(normalizedNickname))
                return BadRequest("Přezdívka obsahuje neviditelné nebo nepovolené znaky.");

            // Kontrola, zda existuje uživatel se stejnou přezdívkou (case-insensitive)
            // EF.Functions.ILike používá case-insensitive porovnání v PostgreSQL
            if (await userManager.Users.AnyAsync(u => EF.Functions.ILike(u.Nickname, normalizedNickname)))
                return BadRequest($"Přezdívka {normalizedNickname} je již používána.");

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                Nickname = normalizedNickname
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
            logger.Log("Chyba při vykonávání akce CreateMemberUser endpointu.", e);
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
            logger.Log("Chyba při vykonávání akce CreateAdminUser endpointu.", e);
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

            // Odstranění mezer z začátku a konce a normalizace vstupu
            var normalizedNickname = model.Nickname.Trim();

            // Kontrola neviditelných znaků a dalších problematických znaků
            if (ContainsInvisibleCharacters(normalizedNickname))
                return BadRequest("Přezdívka obsahuje neviditelné nebo nepovolené znaky.");

            // Kontrola, zda existuje jiný uživatel se stejnou přezdívkou (case-insensitive)
            if (await userManager.Users.AnyAsync(u => u.Id != user.Id && EF.Functions.ILike(u.Nickname, normalizedNickname)))
                return BadRequest("Tato přezdívka je již používána.");

            user.Nickname = normalizedNickname;

            var result = await userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return BadRequest("Chyba při změně přezdívky");

            return Ok();
        }
        catch (Exception e)
        {
            logger.Log("Chyba při vykonávání akce UpdateNickname endpointu.", e);
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
            logger.Log("Chyba při vykonávání akce UpdateEmail endpointu.", e);
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
            logger.Log("Chyba při vykonávání akce UpdateAuthenticatedUserPassword endpointu.", e);
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
            logger.Log("Chyba při vykonávání akce DeleteUser endpointu.", e);
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
            if (!IsAdmin && UserId != userId)
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
                logger.Log("Chyba při vykonávání transakce v akci DeleteUserPermanently endpointu.", e);
                return BadRequest();
            }
        }
        catch (Exception e)
        {
            logger.Log("Chyba při vykonávání akce DeleteUserPermanently endpointu.", e);
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
            logger.Log("Chyba při vykonávání akce ToggleUserVisibility endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Pomocná metoda pro získání IP adresy klienta
    /// </summary>
    /// <returns>IP adresa klienta</returns>
    /*private string GetClientIpAddress()
    {
        try
        {
            // Logování všech HTTP hlaviček pro kompletní diagnostiku
            string allHeaders = string.Join(", ", HttpContext.Request.Headers.Select(h => $"{h.Key}: {h.Value}"));
            //logger.Log($"DEBUG - All HTTP Headers: {allHeaders}");

            // Získání různých hodnot IP adres pro diagnostiku
            var remoteIpAddress = HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "null";
            var xForwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].ToString();
            var xRealIp = HttpContext.Request.Headers["X-Real-IP"].ToString();
            var xClientIp = HttpContext.Request.Headers["X-Client-IP"].ToString();
            var xDebugRemoteAddr = HttpContext.Request.Headers["X-Debug-Remote-Addr"].ToString();

            // Detailní log pro diagnostiku - VŽDY LOGUJEME PRO ANALÝZU PROBLÉMU
            logger.Log($"IP INFO - RemoteIpAddress: {remoteIpAddress}, " +
                       $"X-Forwarded-For: {xForwardedFor}, " +
                       $"X-Real-IP: {xRealIp}, " +
                       $"X-Client-IP: {xClientIp}, " +
                       $"X-Debug-Remote-Addr: {xDebugRemoteAddr}");

            // Funkce pro převod IPv6 adres na IPv4
            string NormalizeIpAddress(string ip)
            {
                if (ip != null && ip.StartsWith("::ffff:"))
                {
                    return ip.Substring(7);
                }
                return ip;
            }

            // 1. Nejprve zkontrolujeme vlastní hlavičku X-Debug-Remote-Addr
            if (!string.IsNullOrEmpty(xDebugRemoteAddr) && xDebugRemoteAddr != "127.0.0.1")
            {
                return NormalizeIpAddress(xDebugRemoteAddr);
            }

            // 2. Pak zkontrolujeme X-Real-IP
            if (!string.IsNullOrEmpty(xRealIp) && xRealIp != "127.0.0.1")
            {
                return NormalizeIpAddress(xRealIp);
            }

            // 3. Pak zkontrolujeme X-Forwarded-For
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                var ips = xForwardedFor.Split(',');
                if (ips.Length > 0)
                {
                    var clientIp = NormalizeIpAddress(ips[0].Trim());
                    if (clientIp != "127.0.0.1")
                    {
                        return clientIp;
                    }
                }
            }

            // 4. Pak zkontrolujeme X-Client-IP
            if (!string.IsNullOrEmpty(xClientIp) && xClientIp != "127.0.0.1")
            {
                return NormalizeIpAddress(xClientIp);
            }

            // 5. Jako poslední možnost použijeme RemoteIpAddress
            if (remoteIpAddress != "::1" && remoteIpAddress != "127.0.0.1" && remoteIpAddress != "null")
            {
                return NormalizeIpAddress(remoteIpAddress);
            }

            return "unknown-ip";
        }
        catch (Exception ex)
        {
            logger.Log($"Chyba při získávání IP adresy: {ex.Message}", ex);
            return "error-ip";
        }
    }*/

    /// <summary>
    /// Vytváří JWT autentizační token na základě emailu a hesla uživatele.
    /// Provádí kompletní proces ověření přihlašovacích údajů včetně:
    /// - Vyhledání uživatele v databázi podle emailu
    /// - Kontroly stavu uživatelského účtu (není smazaný)
    /// - Ověření hesla pomocí ASP.NET Core Identity
    /// - Zpracování uzamčených účtů (lockout protection)
    /// - Aktualizace statistik přihlášení (počet, poslední přihlášení)
    /// - Generování JWT tokenu s příslušnými claims a rolemi
    /// - Vrácení tokenu spolu s metadaty (expirace, uživatelské údaje)
    /// Implementuje bezpečnostní opatření proti brute-force útokům prostřednictvím lockout mechanismu.
    /// </summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [HttpPost("login")]
    public async Task<IActionResult> CreateToken(LoginModelDto model)
    {
        try
        {
            var user = await this.userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                logger.Log($"Při pokusu o přihlášení nebyl uživatel s emailem {model.Email} nalezen.");
                return Unauthorized();
            }

            if (user.Type == UserType.Deleted)
            {
                logger.Log($"Při pokusu o přihlášení byl uživatel s emailem {model.Email} identifikován se stavem smazaný.");
                return Unauthorized();
            }

            //Metoda ověří heslo(false zde znamená, že se nezamkne účet při špatném hesle)
            var result = await this.signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
            if (result.IsLockedOut)
            {
                logger.Log($"Při pokusu o přihlášení uživatele s emailem {model.Email} bylo identifikováno, že uživatel je dočasně uzamknut pro přihlášení.");
                return Unauthorized(new { message = "Účet je uzamčen" });
            }
            if (!result.Succeeded)
            {
                logger.Log($"Při pokusu o přihlášení uživatele s emailem {model.Email} bylo identifikováno špatně zadané heslo.");
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
            logger.Log("Chyba při vykonávání akce CreateToken endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vrací historii přihlášení uživatele
    /// </summary>
    /// <param name="userId">ID uživatele</param>
    /// <param name="limit">Maximální počet záznamů</param>
    /// <returns>Seznam posledních přihlášení</returns>
    [Authorize]
    [HttpGet("login-history/{userId}")]
    public async Task<ActionResult<IEnumerable<object>>> GetUserLoginHistory(string userId, [FromQuery] int limit = 20)
    {
        try
        {
            // Kontrola oprávnění - historii vidí pouze admin nebo samotný uživatel
            if (!IsAdmin && UserId != userId)
                return Forbid();

            // Ověření existence uživatele
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound("Uživatel nebyl nalezen.");

            // Načtení historie přihlášení seřazené od nejnovějších
            var loginHistory = await dbContext.LoginHistory
                .AsNoTracking()
                .Where(lh => lh.UserId == userId)
                .OrderByDescending(lh => lh.LoginTime)
                .Take(limit)
                .Select(lh => new
                {
                    lh.Id,
                    lh.LoginTime,
                    lh.IpAddress
                })
                .ToListAsync();

            return Ok(loginHistory);
        }
        catch (Exception e)
        {
            logger.Log("Chyba při získávání historie přihlášení uživatele", e);
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
            if (string.IsNullOrEmpty(UserId))
            {
                logger.Log("Při odhlašování nebyl nalezen userId aktuálního uživatel");
                return Unauthorized("Při odhlašování nebyl nalezen userId aktuálního uživatel");
            }

            // Vyhledání uživatele
            var user = await userManager.FindByIdAsync(UserId);
            if (user == null)
            {
                logger.Log("Při odhlašování nebyl nalezen aktuální uživatel v databázi");
                return NotFound("Při odhlašování nebyl nalezen aktuální uživatel v databázi");
            }

            // Aktualizace LastLogin na aktuální čas
            user.LastLogin = DateTime.UtcNow;
            var result = await userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                logger.Log($"Při odhlašování uživatele {user.Id} se nepodařilo aktualizovat je lastLogin");
                return BadRequest($"Při odhlašování uživatele {user.Id} se nepodařilo aktualizovat je lastLogin");
            }

            return Ok();
        }
        catch (Exception e)
        {
            logger.Log("Chyba při vykonávání akce Logout endpointu.", e);
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

                logger.Log($"Administrátor {UserId} provedl vyčištění databáze");

                return Ok(result);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                logger.Log("Chyba při čištění databáze", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "Nastala chyba při čištění databáze.");
            }
        }
        catch (Exception e)
        {
            logger.Log("Chyba při vykonávání akce CleanupDeletedData endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vrátí seznam diskuzí, kde má přihlášený uživatel nové aktivity od posledního přihlášení.
    /// Nové aktivity zahrnují:
    /// 1. Nové odpovědi na komentáře uživatele
    /// 2. Nové komentáře v diskuzích, které uživatel založil
    /// Výsledek obsahuje unikátní diskuze (bez duplicit), i když diskuze splňuje oba typy aktivit.
    /// </summary>
    /// <returns>Seznam diskuzí s informacemi o nových aktivitách</returns>
    [Authorize]
    [HttpGet("discussions-with-new-activities")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)] // Zakázání kešování pro vždy aktuální data
    public async Task<ActionResult<IEnumerable<DiscussionWithNewActivitiesDto>>> GetDiscussionsWithNewActivities()
    {
        try
        {
            if (string.IsNullOrEmpty(UserId))
                return Unauthorized();

            // Vyhledání uživatele v databázi pro získání času předchozího přihlášení
            var user = await userManager.FindByIdAsync(UserId);
            if (user == null || user.PreviousLastLogin == null)
                return Ok(new List<DiscussionWithNewActivitiesDto>()); // Pokud nemáme předchozí přihlášení, nemůžeme zjistit nové aktivity

            // Pokus o načtení dat z keše
            // Vytvoření klíče pro keš, který obsahuje:
            // - ID uživatele (každý uživatel má vlastní keš)
            // - Časové razítko posledního přihlášení (invaliduje keš při novém přihlášení)
            var cacheKey = $"NewActivities_{UserId}_{user.LastLogin?.Ticks}";

            // Pokus o získání dat z keše
            if (_cache.TryGetValue(cacheKey, out List<DiscussionWithNewActivitiesDto>? cachedResult))
            {
                return Ok(cachedResult);
            }

            // V keši nebyl seznam nalezen, takže se musí získat z databáze
            // Čas, od kterého hledáme nové aktivity (předchozí přihlášení)
            var fromTime = user.PreviousLastLogin.Value;

            // Výsledný slovník diskuzí s informacemi o aktivitách (používáme slovník pro efektivní slučování)
            var resultDictionary = new Dictionary<int, DiscussionWithNewActivitiesDto>();

            // 1. PRVNÍ TYP AKTIVIT: Nové odpovědi na komentáře uživatele
            // ----------------------------------------------------------

            // 1.1 KROK: Efektivně najít ID root komentářů uživatele, které mají nové odpovědi.
            var rootCommentsWithNewReplies = await dbContext.Comments
                .AsNoTracking() // Pro lepší výkon - nesledujeme změny entit
                .Where(c => c.UserId == UserId && c.ParentCommentId == null) // Jen rootové komentáře uživatele
                .Where(c => c.Replies.Any(r =>
                    r.CreatedAt > fromTime && // Odpověď je novější než předchozí přihlášení
                    r.UserId != UserId &&     // Odpověď není od samotného uživatele
                    r.Type != CommentType.Deleted)) // Odpověď není smazaná
                .Select(c => new
                {
                    CommentId = c.Id,
                    DiscussionId = c.DiscussionId,
                    // Pro každý komentář najdeme nejnovější odpověď a pouze čas této odpovědi
                    LatestReplyTime = c.Replies
                        .Where(r => r.CreatedAt > fromTime && r.UserId != UserId && r.Type != CommentType.Deleted)
                        .OrderByDescending(r => r.CreatedAt)
                        .Select(r => r.CreatedAt)
                        .FirstOrDefault()
                })
                .ToListAsync();

            // 1.2 KROK: Seskupit komentáře podle diskuzí
            if (rootCommentsWithNewReplies.Any())
            {
                var discussionGroups = rootCommentsWithNewReplies
                    .GroupBy(c => c.DiscussionId)
                    .Select(g => new
                    {
                        DiscussionId = g.Key,
                        LatestReplyTime = g.Max(c => c.LatestReplyTime),
                        CommentsCount = g.Count()
                    })
                    .ToList();

                // 1.3 KROK: Získat seznam ID diskuzí s novými odpověďmi
                var discussionIdsWithReplies = discussionGroups.Select(g => g.DiscussionId).ToList();

                // 1.4 KROK: Efektivně načíst potřebné informace o těchto diskuzích
                var discussionsWithReplies = await dbContext.Discussions
                    .AsNoTracking()
                    .Where(d => discussionIdsWithReplies.Contains(d.Id))
                    .Include(d => d.Category)
                    .Select(d => new
                    {
                        d.Id,
                        d.Title,
                        DiscussionCode = d.Code,
                        d.Category.Name,
                        CategoryCode = d.Category.Code
                    })
                    .ToDictionaryAsync(d => d.Id);

                // 1.5 KROK: Přidání diskuzí s novými odpověďmi do výsledného slovníku
                foreach (var group in discussionGroups)
                {
                    if (discussionsWithReplies.TryGetValue(group.DiscussionId, out var discussion))
                    {
                        resultDictionary[group.DiscussionId] = new DiscussionWithNewActivitiesDto
                        {
                            DiscussionId = group.DiscussionId,
                            Title = discussion.Title,
                            DiscussionUrl = $"/Categories/{discussion.CategoryCode}/{discussion.DiscussionCode}",
                            CategoryName = discussion.Name,
                            LatestActivityTime = group.LatestReplyTime,
                            CommentsWithNewRepliesCount = group.CommentsCount,
                            NewCommentsCount = 0, // Zatím žádné nové komentáře v této diskuzi
                            ActivityType = "new_replies"
                        };
                    }
                }
            }

            // 2. DRUHÝ TYP AKTIVIT: Nové komentáře v diskuzích, které uživatel založil
            // ----------------------------------------------------------------------

            // 2.1 KROK: Najít diskuze založené uživatelem, kde existují nové komentáře
            var discussionsWithNewComments = await dbContext.Discussions
                .AsNoTracking()
                .Where(d => d.UserId == UserId) // Diskuze založené přihlášeným uživatelem
                .Where(d => d.Comments.Any(c =>
                    c.CreatedAt > fromTime && // Komentář je novější než předchozí přihlášení
                    c.UserId != UserId &&     // Komentář není od samotného uživatele
                    c.Type != CommentType.Deleted)) // Komentář není smazaný
                .Select(d => new
                {
                    d.Id,
                    d.Title,
                    d.Code,
                    d.Category.Name,
                    CategoryCode = d.Category.Code,
                    LatestCommentTime = d.Comments
                        .Where(c => c.CreatedAt > fromTime && c.UserId != UserId && c.Type != CommentType.Deleted)
                        .OrderByDescending(c => c.CreatedAt)
                        .Select(c => c.CreatedAt)
                        .FirstOrDefault(),
                    NewCommentsCount = d.Comments
                        .Count(c => c.CreatedAt > fromTime && c.UserId != UserId && c.Type != CommentType.Deleted)
                })
                .ToListAsync();

            // 2.2 KROK: Zpracování diskuzí s novými komentáři a přidání/aktualizace ve výsledném slovníku
            foreach (var discussion in discussionsWithNewComments)
            {
                // Pokud diskuze už existuje ve výsledném slovníku (má i nové odpovědi), aktualizujeme ji
                if (resultDictionary.TryGetValue(discussion.Id, out var existingDto))
                {
                    // Aktualizace počtu nových komentářů
                    existingDto.NewCommentsCount = discussion.NewCommentsCount;

                    // Aktualizace typu aktivity
                    existingDto.ActivityType = "new_replies_and_comments";

                    // Aktualizace času poslední aktivity, pokud je novější komentář než odpověď
                    if (discussion.LatestCommentTime > existingDto.LatestActivityTime)
                    {
                        existingDto.LatestActivityTime = discussion.LatestCommentTime;
                    }
                }
                else
                {
                    // Jinak přidáme novou položku do výsledného slovníku
                    resultDictionary[discussion.Id] = new DiscussionWithNewActivitiesDto
                    {
                        DiscussionId = discussion.Id,
                        Title = discussion.Title,
                        DiscussionUrl = $"/Categories/{discussion.CategoryCode}/{discussion.Code}",
                        CategoryName = discussion.Name,
                        LatestActivityTime = discussion.LatestCommentTime,
                        CommentsWithNewRepliesCount = 0, // Žádné nové odpovědi na komentáře uživatele v této diskuzi
                        NewCommentsCount = discussion.NewCommentsCount,
                        ActivityType = "new_comments"
                    };
                }
            }

            // 3. KROK: Sestavení výsledného seznamu (seřazeného podle času poslední aktivity)
            // -----------------------------------------------------------------------------
            var result = resultDictionary.Values
                .OrderByDescending(d => d.LatestActivityTime)
                .ToList();

            // 4. KROK: Uložení výsledku do keše
            // --------------------------------
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(60)) // Expirace po 60 sekundách
                .SetSize(1);

            _cache.Set(cacheKey, result, cacheOptions);

            return Ok(result);
        }
        catch (Exception e)
        {
            logger.Log("Chyba při vykonávání akce GetDiscussionsWithNewActivities endpointu.", e);
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
                logger.Log("Byl proveden pokus o odeslání emailu pro obnovu hesla, ale email byl null.");
                return BadRequest();
            }
            await _emailService.SendPasswordResetEmailAsync(user.Email, user.Nickname, resetLink);

            return Ok();
        }
        catch (Exception e)
        {
            logger.Log("Chyba při vykonávání akce ForgotPassword.", e);
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
            logger.Log("Chyba při vykonávání akce ResetPassword.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Generuje JWT (JSON Web Token) pro autentizovaného uživatele.
    /// Token obsahuje standardní claims včetně emailu, uživatelského ID a všech přidělených rolí.
    /// Konfigurace tokenu zahrnuje:
    /// - Symetrické šifrování pomocí klíče ze konfigurace
    /// - Nastavení vydavatele (issuer) a příjemce (audience)
    /// - Definování doby platnosti tokenu
    /// - Přidání bezpečnostního identifikátoru (JTI) pro možné budoucí sledování
    /// - Zahrnutí všech uživatelských rolí pro autorizační účely
    /// Token je podepsán pomocí HMAC SHA256 algoritmu pro zajištění integrity.
    /// </summary>
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
            var requesterId = UserId;
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
            logger.Log("Chyba při zjišťování stavu přátelství", e);
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
            var requesterId = UserId;
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
            logger.Log("Chyba při vytváření žádosti o přátelství", e);
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
            if (string.IsNullOrEmpty(UserId))
                return Unauthorized();

            // Vyhledání všech přátelství, kde uživatel figuruje
            // jako žadatel nebo schvalovatel
            var friendships = await dbContext.Friendships
                .Include(f => f.ApproverUser)    // Načtení souvisejících uživatelů pro přístup k jejich datům
                .Include(f => f.RequesterUser)   // Načtení souvisejících uživatelů pro přístup k jejich datům
                .Where(f =>
                    // Buď je přihlášený uživatel approver a requester je normal
                    (f.ApproverUserId == UserId && f.RequesterUser.Type == UserType.Normal) ||
                    // Nebo je přihlášený uživatel requester a approver je normal
                    (f.RequesterUserId == UserId && f.ApproverUser.Type == UserType.Normal))
                .OrderBy(f => f.Id)
                .ToListAsync();

            // Transformace dat pro vrácení klientovi
            var result = friendships.Select(f =>
            {
                // Zjištění, kdo je druhý uživatel (ten, který NENÍ přihlášený)
                var otherUser = f.ApproverUserId == UserId ? f.RequesterUser : f.ApproverUser;

                // Zjištění role přihlášeného uživatele
                var isRequester = f.RequesterUserId == UserId;

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
            logger.Log("Chyba při načítání přátelství", e);
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
            if (string.IsNullOrEmpty(UserId))
                return Unauthorized();

            // Vyhledání záznamu přátelství
            var friendship = await dbContext.Friendships.FindAsync(friendshipId);
            if (friendship == null)
                return NotFound("Žádost o přátelství nebyla nalezena.");

            // Kontrola, zda přihlášený uživatel je schvalovatel
            if (friendship.ApproverUserId != UserId)
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
            logger.Log("Chyba při schvalování žádosti o přátelství", e);
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
            if (string.IsNullOrEmpty(UserId))
                return Unauthorized();

            // Vyhledání záznamu přátelství
            var friendship = await dbContext.Friendships.FindAsync(friendshipId);
            if (friendship == null)
                return NotFound("Žádost o přátelství nebyla nalezena.");

            // Kontrola, zda přihlášený uživatel je schvalovatel
            if (friendship.ApproverUserId != UserId)
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
            logger.Log("Chyba při zamítání žádosti o přátelství", e);
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
            if (string.IsNullOrEmpty(UserId))
                return Unauthorized();

            // Vyhledání záznamu přátelství
            var friendship = await dbContext.Friendships.FindAsync(friendshipId);
            if (friendship == null)
                return NotFound("Přátelství nebylo nalezeno.");

            // Kontrola, zda je přihlášený uživatel jednou ze stran přátelství
            if (friendship.ApproverUserId != UserId && friendship.RequesterUserId != UserId)
                return Forbid();

            // Odstranění přátelství z databáze
            dbContext.Friendships.Remove(friendship);
            await dbContext.SaveChangesAsync();

            return Ok();
        }
        catch (Exception e)
        {
            logger.Log("Chyba při odstraňování přátelství", e);
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
            if (string.IsNullOrEmpty(UserId))
                return Unauthorized();

            // Zjištění počtu žádostí o přátelství, které čekají na schválení
            // Ignorujeme žádosti od skrytých nebo smazaných uživatelů
            var requestsCount = await dbContext.Friendships
                .Where(f => f.ApproverUserId == UserId &&
                       f.FriendshipStatus == FriendshipStatus.Requested &&
                       f.RequesterUser.Type == UserType.Normal) // Pouze normální uživatelé
                .CountAsync();

            return Ok(requestsCount);
        }
        catch (Exception e)
        {
            logger.Log("Chyba při zjišťování počtu žádostí o přátelství", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vrací seznam přátel konkrétního uživatele.
    /// Přístupné pouze pro přihlášené uživatele, kteří jsou přáteli s daným uživatelem nebo je to jejich vlastní profil.
    /// </summary>
    /// <param name="userId">ID uživatele, jehož přátele chceme zobrazit</param>
    /// <returns>Seznam přátel</returns>
    [Authorize]
    [HttpGet("user-friends/{userId}")]
    public async Task<ActionResult<IEnumerable<object>>> GetUserFriends(string userId)
    {
        try
        {
            // Získání ID přihlášeného uživatele
            var currentUserId = UserId;
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            // Kontrola oprávnění - může vidět přátele jen vlastník profilu nebo jeho přátelé
            bool canViewFriends = (currentUserId == userId); // Je to vlastní profil

            if (!canViewFriends)
            {
                // Zkontrolujeme, zda jsou přátelé
                bool areFriends = await dbContext.Friendships
                    .AnyAsync(f => (
                        (f.ApproverUserId == currentUserId && f.RequesterUserId == userId) ||
                        (f.ApproverUserId == userId && f.RequesterUserId == currentUserId))
                        && f.FriendshipStatus == FriendshipStatus.Approved);

                if ((!areFriends) && (!IsAdmin))
                    return Forbid("Nejste přátelé s tímto uživatelem");
            }

            // Vyhledání všech přátelství uživatele, kde je schváleno
            var friendships = await dbContext.Friendships
                .Include(f => f.ApproverUser)
                .Include(f => f.RequesterUser)
                .Where(f =>
                    // Buď je uživatel approver a requester je normal
                    (f.ApproverUserId == userId && f.RequesterUser.Type == UserType.Normal) ||
                    // Nebo je uživatel requester a approver je normal
                    (f.RequesterUserId == userId && f.ApproverUser.Type == UserType.Normal))
                .Where(f => f.FriendshipStatus == FriendshipStatus.Approved)
                .ToListAsync();

            // Transformace dat pro vrácení klientovi
            var result = friendships.Select(f =>
            {
                // Zjištění, kdo je druhý uživatel (ten, který NENÍ zobrazovaný uživatel)
                var otherUser = f.ApproverUserId == userId ? f.RequesterUser : f.ApproverUser;

                // Zjištění role zobrazovaného uživatele
                var isRequester = f.RequesterUserId == userId;

                return new
                {
                    FriendshipId = f.Id,
                    OtherUserId = otherUser.Id,
                    OtherUserNickname = otherUser.Nickname,
                    Status = f.FriendshipStatus,
                    IsRequester = isRequester,
                    CreatedAt = f.CreatedAt
                };
            })
            .OrderBy(f => f.OtherUserNickname);

            return Ok(result);
        }
        catch (Exception e)
        {
            logger.Log("Chyba při načítání přátel uživatele", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Kontroluje, zda řetězec obsahuje neviditelné nebo speciální znaky
    /// </summary>
    /// <param name="input">Řetězec k kontrole</param>
    /// <returns>true pokud obsahuje neviditelné znaky, jinak false</returns>
    private bool ContainsInvisibleCharacters(string input)
    {
        // Kontrola prázdného vstupu
        if (string.IsNullOrEmpty(input))
            return true;

        // Kontrola whitespace znaků na začátku nebo konci (již by měly být oříznuty pomocí Trim)
        if (input != input.Trim())
            return true;

        // Kontrola, zda obsahuje řídicí znaky nebo jiné potenciálně problematické znaky
        foreach (char c in input)
        {
            // Řídicí znaky (ASCII 0-31 kromě Tab, LF, CR)
            if (c <= 31 && c != 9 && c != 10 && c != 13)
                return true;

            // Znaky Unicode, které jsou označeny jako neviditelné nebo formátovací
            // Například zero-width spaces, joiners, variation selectors
            if (char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format ||
                char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Control ||
                char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.OtherNotAssigned)
                return true;

            // Specifické neviditelné Unicode znaky
            // U+200B (Zero Width Space), U+200C (Zero Width Non-Joiner), atd.
            if ((int)c == 0x200B || (int)c == 0x200C || (int)c == 0x200D ||
                (int)c == 0x2060 || (int)c == 0xFEFF || (c >= 0x2000 && c <= 0x200F))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Vrátí počet aktivních uživatelů (typ UserType.Normal)
    /// Nekešovaný endpoint dostupný pro veřejnost
    /// </summary>
    /// <returns>Počet aktivních uživatelů v systému</returns>
    [HttpGet("count")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<ActionResult<int>> UserCount()
    {
        try
        {
            // Získání počtu uživatelů typu Normal
            var normalUsersCount = await dbContext.Users
                .AsNoTracking()
                .Where(u => u.Type == UserType.Normal)
                .CountAsync();

            return Ok(normalUsersCount);
        }
        catch (Exception e)
        {
            logger.Log("Chyba při vykonávání akce UserCount endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Zaznamená přihlášení uživatele včetně IP adresy
    /// </summary>
    /// <param name="model">Data o přihlášení</param>
    [Authorize]
    [HttpPost("record-login")]
    public async Task<IActionResult> RecordLogin([FromBody] RecordLoginDto model)
    {
        try
        {
            // Ověření, že uživatel může zapisovat pouze svůj vlastní login
            if (string.IsNullOrEmpty(UserId) || UserId != model.UserId)
                return Forbid();

            // Vytvoření záznamu o přihlášení
            var loginHistory = new LoginHistory
            {
                UserId = model.UserId,
                LoginTime = DateTime.UtcNow,
                IpAddress = model.IpAddress
            };

            // Přidání záznamu do databáze
            dbContext.LoginHistory.Add(loginHistory);
            await dbContext.SaveChangesAsync();

            return Ok();
        }
        catch (Exception e)
        {
            logger.Log("Chyba při zaznamenávání historie přihlášení", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}