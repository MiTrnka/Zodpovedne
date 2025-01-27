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
    [HttpGet("user")]
    public async Task<IActionResult> GetUser()
    {
        //Získání emailu z přihlášeného uživatele, User je vlastnost z ControllerBase
        //obsahuje informace o přihlášeném uživateli extrahované z tokenu. ASP.NET Core ji automaticky naplní z Authorization hlavičky (místo tokenu by to mohlo být třeba autorizační cookie...)
        var email = User.FindFirstValue(ClaimTypes.Email);
        var user = await this.userManager.FindByEmailAsync(email);

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
    /// Vytvoří nového uživatele s rolí User
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("user")]
    public async Task<IActionResult> CreateUser(RegisterModel model)
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
            await this.userManager.AddToRoleAsync(user, "User");

            // V produkci přidat:
            // - Odeslání potvrzovacího emailu
            // - Logování události
            // - Notifikace admina

            return Ok(new { message = "Registration successful" });
        }

        return BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// Vytvoří nového uživatele s rolí Admin
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("admin-user")]
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
            // Přidání výchozí role "User"
            await this.userManager.AddToRoleAsync(user, "Admin");

            // V produkci přidat:
            // - Odeslání potvrzovacího emailu
            // - Logování události
            // - Notifikace admina

            return Ok(new { message = "Registration successful" });
        }

        return BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// Aktualizuje profil přihlášeného uživatele
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPut("user")]
    public async Task<IActionResult> UpdateUser(UpdateProfileModel model)
    {
        //Získání emailu z přihlášeného uživatele, User je vlastnost z ControllerBase
        //obsahuje informace o přihlášeném uživateli extrahované z tokenu. ASP.NET Core ji automaticky naplní z Authorization hlavičky (místo tokenu by to mohlo být třeba autorizační cookie...)
        var email = User.FindFirstValue(ClaimTypes.Email);
        var user = await this.userManager.FindByEmailAsync(email);

        if (user == null) return NotFound();

        user.FirstName = model.FirstName;
        user.LastName = model.LastName;

        var result = await this.userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            return Ok(new { message = "Profile updated successfully" });
        }

        return BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// Smaže uživatele podle emailu
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    [HttpDelete("user")]
    [Authorize(Policy = "RequireAdminRole")]  // Pouze admin může mazat uživatele
    public async Task<IActionResult> DeleteUser([FromQuery] string email)
    {
        var user = await this.userManager.FindByEmailAsync(email);
        if (user == null)
            return NotFound($"Uživatel s emailem {email} nebyl nalezen.");

        var result = await this.userManager.DeleteAsync(user);
        if (result.Succeeded)
            return Ok($"Uživatel s emailem {email} byl úspěšně smazán.");

        return BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// Změní heslo přihlášeného uživatele
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPut("password")]
    public async Task<IActionResult> UpdatePassword(ChangePasswordModel model)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var user = await this.userManager.FindByEmailAsync(email);

        if (user == null) return NotFound();

        var result = await this.userManager.ChangePasswordAsync(
            user,
            model.CurrentPassword,
            model.NewPassword
        );

        if (result.Succeeded)
        {
            return Ok(new { message = "Password changed successfully" });
        }

        return BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// Vytvoří JWT token pro přihlášení uživatele (zkontroluje zadaný email a heslo a vytvoří JWT token)
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

        //Metoda ověří heslo a provede přihlášení (false zde znamená, že se nezamkne účet při špatném hesle)
        var result = await this.signInManager.CheckPasswordSignInAsync(user, model.Password, false);
        if (result.Succeeded)
        {
            var token = await GenerateJwtToken(user);

            // V produkci přidat:
            // - Aktualizace LastLoginDate
            // - Logování přihlášení
            // - Kontrola suspicious aktivit

            return Ok(new { token });
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

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.configuration["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: this.configuration["Jwt:Issuer"],
            audience: this.configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddDays(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}