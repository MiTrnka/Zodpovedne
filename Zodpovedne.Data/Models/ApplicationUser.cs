// Rozšiřuje standardní identitu o FirstName, LastName, Created
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using Zodpovedne.Contracts.Enums;

namespace Zodpovedne.Data.Models;

// Rozšíření standardního IdentityUser o vlastní pole
public class ApplicationUser : IdentityUser
{
    // Rozšíření standardního IdentityUser o vlastní pole
    [Required(ErrorMessage = "Přezdívka je povinná")]
    public string Nickname { get; set; } = "";
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime? LastLogin { get; set; }
    public DateTime? PreviousLastLogin { get; set; }
    public UserType Type { get; set; } = UserType.Normal;
}
