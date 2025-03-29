using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Zodpovedne.Contracts.Enums;

namespace Zodpovedne.Data.Models;

/// <summary>
/// Povolení pro zasílání zpráv mezi uživateli.
/// </summary>
public class MessagingPermission
{
    public int Id { get; set; }

    /// <summary>
    /// ID uživatele, který UDĚLUJE povolení (ten, komu mohou ostatní psát).
    /// </summary>
    [Required]
    public string GranterUserId { get; set; } = "";

    /// <summary>
    /// ID uživatele, kterému JE UDĚLENO povolení (ten, kdo může psát Granter uživateli).
    /// </summary>
    [Required]
    public string AllowedUserId { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Typ/stav povolení (nové, schváleno, zamítnuto...).
    /// </summary>
    [Required]
    public MessagingPermissionType MessagingPermissionType { get; set; } = MessagingPermissionType.New;

    // Navigační vlastnosti
    [ForeignKey("GranterUserId")]
    public virtual ApplicationUser GranterUser { get; set; } = null!;

    [ForeignKey("AllowedUserId")]
    public virtual ApplicationUser AllowedUser { get; set; } = null!;
}