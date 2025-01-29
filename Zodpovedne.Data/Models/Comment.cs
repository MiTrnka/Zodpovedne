using System.ComponentModel.DataAnnotations;

namespace Zodpovedne.Data.Models;

public class Comment
{
    public int Id { get; set; }

    // Povinný odkaz na diskuzi, ke které komentář patří
    public int DiscussionId { get; set; }

    // Povinný odkaz na uživatele, který komentář vytvořil
    public string UserId { get; set; } = "";

    // Nepovinný odkaz na rodičovský komentář (pouze pokud jde o reakci na root komentář)
    public int? ParentCommentId { get; set; }

    [Required(ErrorMessage = "Obsah komentáře je povinný")]
    public string Content { get; set; } = "";

    // Pro možnost moderování obsahu
    public bool IsVisible { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Pomocná vlastnost pro zjištění, zda jde o root komentář
    public bool IsRootComment => ParentCommentId == null;

    // Navigační vlastnosti
    public virtual Discussion Discussion { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;

    // Nullable reference na rodičovský komentář
    public virtual Comment? ParentComment { get; set; }

    // Kolekce reakcí na tento komentář (bude obsahovat položky pouze pokud jde o root komentář)
    public virtual ICollection<Comment> Replies { get; set; } = new List<Comment>();
}