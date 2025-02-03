namespace Zodpovedne.Data.Models;

/// <summary>
/// Entita reprezentující like na komentáři od konkrétního uživatele
/// </summary>
public class CommentLike
{
    public int Id { get; set; }

    /// <summary>
    /// ID komentáře, ke kterému se like vztahuje
    /// </summary>
    public int CommentId { get; set; }

    /// <summary>
    /// ID uživatele, který dal like
    /// </summary>
    public string UserId { get; set; } = "";

    /// <summary>
    /// Datum a čas, kdy byl like přidán
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigační vlastnosti
    public virtual Comment Comment { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
}