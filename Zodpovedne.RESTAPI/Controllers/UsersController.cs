using Zodpovedne.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Zodpovedne.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> userManager;
    private readonly SignInManager<ApplicationUser> signInManager;
    private readonly IConfiguration configuration;

    public UsersController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IConfiguration configuration)
    {
        this.userManager = userManager;
        this.signInManager = signInManager;
        this.configuration = configuration;
    }

    /// <summary>
    /// Vrátí seznam všech uživatelů
    /// </summary>
    /// <returns></returns>
    [HttpGet("users")]
    [Authorize(Policy = "RequireAdminRole")] // Pouze pro adminy
    public async Task<IActionResult> GetUsers()
    {
        var users = await this.userManager.Users
            .Select(u => new {
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Created
            })
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>
    /// Vrátí informace o přihlášeném uživateli
    /// </summary>
    /// <returns></returns>
    [HttpGet("authenticated-user")]
    public async Task<IActionResult> GetAuthenticatedUser()
    {
        // Získá aktuálně přihlášeného uživatele (z tokenu v http hlavičce Authorization)
        var user = await GetCurrentUserAsync();
        if (user == null) return NotFound();

        var roles = await this.userManager.GetRolesAsync(user);

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Created,
            Roles = roles
        });
    }

    /// <summary>
    /// Vytvoří nového uživatele s rolí Member
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("user/member")]
    public async Task<IActionResult> CreateMemberUser(RegisterModel model)
    {
        // V produkci přidat validaci složitosti hesla a dalších údajů
        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName
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

    /// <summary>
    /// Vytvoří nového uživatele s rolí Admin
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("user/admin")]
    [Authorize(Policy = "RequireAdminRole")] // Pouze pro adminy
    public async Task<IActionResult> CreateAdminUser(RegisterModel model)
    {
        // V produkci přidat validaci složitosti hesla a dalších údajů
        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName
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

    /// <summary>
    /// Aktualizuje profil přihlášeného uživatele (FirstName, LastName)
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPut("authenticated-user")]
    public async Task<IActionResult> UpdateAuthenticatedUser(UpdateProfileModel model)
    {
        // Získá aktuálně přihlášeného uživatele (z tokenu v http hlavičce Authorization)
        var user = await GetCurrentUserAsync();
        if (user == null) return NotFound();


        user.FirstName = model.FirstName;
        user.LastName = model.LastName;

        var result = await this.userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            return Ok();
        }

        return BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// Smaže uživatele podle emailu
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    [HttpDelete("user/{email}")]
    [Authorize(Policy = "RequireAdminRole")]  // Pouze admin může mazat uživatele
    public async Task<IActionResult> DeleteUser([FromRoute] string email)
    {
        var user = await this.userManager.FindByEmailAsync(email);
        if (user == null)
            return NotFound();

        var result = await this.userManager.DeleteAsync(user);
        if (result.Succeeded)
            return Ok();

        return BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// Změní heslo přihlášeného uživatele
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPut("authenticated-user/password")]
    public async Task<IActionResult> UpdateAuthenticatedUserPassword(ChangePasswordModel model)
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

    /// <summary>
    /// Vytvoří JWT token pro přihlášení uživatele ze zadaného emailu a hesla (LoginModel) (zkontroluje zadaný email a heslo a vytvoří JWT token)
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("token")]
    public async Task<IActionResult> CreateToken(LoginModel model)
    {
        var user = await this.userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return Unauthorized();
        }

        //Metoda ověří heslo(false zde znamená, že se nezamkne účet při špatném hesle)
        var result = await this.signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return Unauthorized(new { message = "Účet je uzamčen" });
        }
        if (result.Succeeded)
        {
            // Aktualizace posledního přihlášení a rovnou uložení do databáze (SaveChanges)
            user.LastLogin = DateTime.UtcNow;
            await this.userManager.UpdateAsync(user);

            var token = await GenerateJwtToken(user);

            return Ok(new TokenResponse
            {
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddHours(Convert.ToDouble(this.configuration["Jwt:ExpirationInHours"] ?? throw new ArgumentNullException("JWT ExpirationInHours není vyplněn v konfiguračním souboru")))
            });
        }

        return Unauthorized();
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