using System.ComponentModel.DataAnnotations;
using Zodpovedne.Contracts.Enums;

namespace Zodpovedne.Data.Models;

public class Discussion
{
    public int Id { get; set; }

    public int CategoryId { get; set; }

    public string UserId { get; set; } = "";

    [Required(ErrorMessage = "Nadpis diskuze je povinný")]
    [MaxLength(70, ErrorMessage = "Nadpis diskuze nesmí být delší než 70 znaků.")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Obsah diskuze je povinný")]
    [MaxLength(3000, ErrorMessage = "Obsah diskuze může mít maximálně 3 000 znaků")]
    public string Content { get; set; } = "";

    // Pro budoucí implementaci nahrávání obrázků
    public string? ImagePath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int ViewCount { get; set; }

    public DiscussionType Type { get; set; } = DiscussionType.Normal;

    [Required]
    [MaxLength(200)]  // Dostatečně dlouhé pro title + suffix
    public string Code { get; set; } = "";

    /// <summary>
    /// Typ hlasování v diskuzi - určuje stav a viditelnost hlasování
    /// </summary>
    public VoteType VoteType { get; set; } = VoteType.None;

    // Navigační vlastnosti
    public virtual Category Category { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public virtual ICollection<DiscussionLike> Likes { get; set; } = new List<DiscussionLike>();

    /// <summary>
    /// Navigační vlastnost - kolekce hlasovacích otázek v této diskuzi
    /// </summary>
    public virtual ICollection<VotingQuestion> VotingQuestions { get; set; } = new List<VotingQuestion>();
}