using System.ComponentModel.DataAnnotations;
using Zodpovedne.Contracts.Enums;

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
    [MaxLength(500, ErrorMessage = "Obsah komentáře nesmí být delší než 500 znaků.")]
    public string Content { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Pomocná vlastnost pro zjištění, zda jde o root komentář
    public bool IsRootComment => ParentCommentId == null;
    public CommentType Type { get; set; } = CommentType.Normal;

    // Navigační vlastnosti
    public virtual Discussion Discussion { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;

    // Nullable reference na rodičovský komentář
    public virtual Comment? ParentComment { get; set; }

    // Kolekce reakcí na tento komentář (bude obsahovat položky pouze pokud jde o root komentář)
    public virtual ICollection<Comment> Replies { get; set; } = new List<Comment>();
    public virtual ICollection<CommentLike> Likes { get; set; } = new List<CommentLike>();
}