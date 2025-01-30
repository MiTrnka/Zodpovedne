using System.ComponentModel.DataAnnotations;

namespace Zodpovedne.Data.Models;

public class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Název kategorie je povinný")]
    [MaxLength(100)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Kód kategorie je povinný")]
    [MaxLength(50)]
    public string Code { get; set; } = "";

    [MaxLength(500)]
    public string Description { get; set; } = "";

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<Discussion> Discussions { get; set; } = new List<Discussion>();
}