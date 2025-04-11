using System.ComponentModel.DataAnnotations;

namespace Zodpovedne.Data.Models;


public class Translation
{
    public int Id { get; set; }
    public int SiteInstanceId { get; set; }
    [Required]
    public string Code { get; set; } = "";
    [Required]
    public string TranslatedText { get; set; } = "";

    // Navigační vlastnosti
    public SiteInstance SiteInstance { get; set; } = null!;
}

