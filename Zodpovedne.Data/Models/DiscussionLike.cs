namespace Zodpovedne.Data.Models;

/// <summary>
/// Entita reprezentující like na diskuzi od konkrétního uživatele
/// </summary>
public class DiscussionLike
{
    public int Id { get; set; }

    /// <summary>
    /// ID diskuze, ke které se like vztahuje
    /// </summary>
    public int DiscussionId { get; set; }

    /// <summary>
    /// ID uživatele, který dal like
    /// </summary>
    public string UserId { get; set; } = "";

    /// <summary>
    /// Datum a čas, kdy byl like přidán
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigační vlastnosti
    public virtual Discussion Discussion { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
}