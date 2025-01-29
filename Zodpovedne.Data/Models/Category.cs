using System.ComponentModel.DataAnnotations;

namespace Zodpovedne.Data.Models;

public class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Název kategorie je povinný")]
    [MaxLength(100)]
    public string Name { get; set; } = "";

    [MaxLength(500)]
    public string Description { get; set; } = "";

    // Určuje pořadí zobrazení na hlavní stránce
    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigační vlastnost pro diskuze v této kategorii
    public virtual ICollection<Discussion> Discussions { get; set; } = new List<Discussion>();
}