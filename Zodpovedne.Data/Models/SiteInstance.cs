using System.ComponentModel.DataAnnotations;

namespace Zodpovedne.Data.Models;

public class SiteInstance
{
    public int Id { get; set; }
    [MaxLength(50, ErrorMessage = "Název instance nesmí být delší než 50 znaků."), Required]
    public string Code { get; set; } = "";
}
