using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Zodpovedne.Data.Models;

/// <summary>
/// Entita reprezentující historii přihlášení uživatelů
/// </summary>
public class LoginHistory
{
    /// <summary>
    /// Unikátní identifikátor záznamu přihlášení
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// ID uživatele, který se přihlásil
    /// </summary>
    [Required]
    public string UserId { get; set; } = "";

    /// <summary>
    /// Datum a čas přihlášení
    /// </summary>
    public DateTime LoginTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// IP adresa, ze které se uživatel přihlásil
    /// </summary>
    [Required]
    [MaxLength(45)] // Dostatečná délka pro IPv6 adresy
    public string IpAddress { get; set; } = "";

    /// <summary>
    /// Navigační vlastnost - uživatel, který se přihlásil
    /// </summary>
    [ForeignKey("UserId")]
    public virtual ApplicationUser User { get; set; } = null!;
}