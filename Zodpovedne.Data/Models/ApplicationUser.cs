// Rozšiřuje standardní identitu o FirstName, LastName, Created
using Microsoft.AspNetCore.Identity;

namespace Zodpovedne.Models;

// Rozšíření standardního IdentityUser o vlastní pole
public class ApplicationUser : IdentityUser
{
    // Rozšíření standardního IdentityUser o vlastní pole
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime? LastLogin { get; set; }

    // Pro produkci zvážit přidání dalších polí jako:
    // - LastLoginDate
    // - ProfilePicture
    // - TwoFactorEnabled
    // - PreferredLanguage
}
