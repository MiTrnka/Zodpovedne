using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Zodpovedne.Contracts.Enums;

namespace Zodpovedne.Data.Models;

/// <summary>
/// Entita představující hlasovací otázku v diskuzi
/// </summary>
public class VotingQuestion
{
    /// <summary>
    /// Unikátní identifikátor hlasovací otázky
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// ID diskuze, ke které otázka patří
    /// </summary>
    public int DiscussionId { get; set; }

    /// <summary>
    /// Text hlasovací otázky (maximálně 400 znaků)
    /// </summary>
    [Required(ErrorMessage = "Text otázky je povinný")]
    [MaxLength(400, ErrorMessage = "Text otázky nesmí být delší než 400 znaků")]
    public string Text { get; set; } = "";

    /// <summary>
    /// Pořadí otázky v rámci hlasování (pro správné řazení při zobrazení)
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Počet hlasů "Ano" pro tuto otázku
    /// Udržuje se aktualizovaný pro optimalizaci výkonu při zobrazení výsledků
    /// </summary>
    public int YesVotes { get; set; } = 0;

    /// <summary>
    /// Počet hlasů "Ne" pro tuto otázku
    /// Udržuje se aktualizovaný pro optimalizaci výkonu při zobrazení výsledků
    /// </summary>
    public int NoVotes { get; set; } = 0;

    /// <summary>
    /// Datum a čas vytvoření otázky
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Datum a čas poslední aktualizace otázky
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigační vlastnost - diskuze, ke které tato otázka patří
    /// </summary>
    [ForeignKey("DiscussionId")]
    public virtual Discussion Discussion { get; set; } = null!;

    /// <summary>
    /// Navigační vlastnost - kolekce hlasů pro tuto otázku
    /// </summary>
    public virtual ICollection<Vote> Votes { get; set; } = new List<Vote>();
}