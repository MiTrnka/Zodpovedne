using System.ComponentModel.DataAnnotations;

namespace Zodpovedne.Data.Models;

public class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Název kategorie je povinný")]
    [MaxLength(100, ErrorMessage = "Název kategorie nesmí být delší než 100 znaků.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Kód kategorie je povinný")]
    [MaxLength(50, ErrorMessage = "Kód kategorie nesmí být delší než 50 znaků.")]
    public string Code { get; set; } = "";

    [MaxLength(200, ErrorMessage = "Popis kategorie nesmí být delší než 200 znaků.")]
    public string Description { get; set; } = "";

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? ImagePath { get; set; }

    public virtual ICollection<Discussion> Discussions { get; set; } = new List<Discussion>();
}