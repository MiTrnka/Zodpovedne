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
    /// ID uživatele, který je/byl žádán o udělení povolení s ním komunikovat.
    /// </summary>
    [Required]
    public string GranterUserId { get; set; } = "";

    /// <summary>
    /// ID uživatele, který žádá/žádal o povolení
    /// </summary>
    [Required]
    public string RequesterUserId { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Typ/stav povolení (nové, schváleno, zamítnuto...).
    /// </summary>
    [Required]
    public MessagingPermissionType MessagingPermissionType { get; set; } = MessagingPermissionType.New;

    // Navigační vlastnosti
    [ForeignKey("GranterUserId")]
    public virtual ApplicationUser GranterUser { get; set; } = null!;

    [ForeignKey("RequesterUserId")]
    public virtual ApplicationUser RequesterUser { get; set; } = null!;
}