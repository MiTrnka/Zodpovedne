using System.ComponentModel.DataAnnotations;
using Zodpovedne.Contracts.Enums;

namespace Zodpovedne.Data.Models;

public class Discussion
{
    public int Id { get; set; }

    public int CategoryId { get; set; }

    public string UserId { get; set; } = "";

    [Required(ErrorMessage = "Nadpis diskuze je povinný")]
    [MaxLength(200)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Obsah diskuze je povinný")]
    public string Content { get; set; } = "";

    // Pro budoucí implementaci nahrávání obrázků
    public string? ImagePath { get; set; }

    // Pro možnost moderování obsahu
    public bool IsVisible { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public int ViewCount { get; set; }

    public DiscussionType Type { get; set; } = DiscussionType.Normal;

    // Navigační vlastnosti
    public virtual Category Category { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
}