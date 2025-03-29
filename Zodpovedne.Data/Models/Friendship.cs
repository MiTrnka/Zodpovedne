using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Zodpovedne.Contracts.Enums;

namespace Zodpovedne.Data.Models;

/// <summary>
/// Stav přátelství mezi 2 uživateli.
/// </summary>
public class Friendship
{
    public int Id { get; set; }

    /// <summary>
    /// ID uživatele, který je/byl žádán o přátelství.
    /// </summary>
    [Required]
    public string ApproverUserId { get; set; } = "";

    /// <summary>
    /// ID uživatele, který žádá/žádal o přátelství
    /// </summary>
    [Required]
    public string RequesterUserId { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Stav přátelství
    /// </summary>
    [Required]
    public FriendshipStatus FriendshipStatus { get; set; } = FriendshipStatus.Requested;

    // Navigační vlastnosti
    [ForeignKey("ApproverUserId")]
    public virtual ApplicationUser ApproverUser { get; set; } = null!;

    [ForeignKey("RequesterUserId")]
    public virtual ApplicationUser RequesterUser { get; set; } = null!;
}