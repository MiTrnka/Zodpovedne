using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Zodpovedne.Contracts.Enums;

namespace Zodpovedne.Data.Models;

/// <summary>
/// Entita představující jednotlivý hlas uživatele pro konkrétní hlasovací otázku
/// </summary>
public class Vote
{
    /// <summary>
    /// Unikátní identifikátor hlasu
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// ID hlasovací otázky, ke které patří tento hlas
    /// </summary>
    public int VotingQuestionId { get; set; }

    /// <summary>
    /// ID uživatele, který hlasoval
    /// </summary>
    public string UserId { get; set; } = "";

    /// <summary>
    /// Hodnota hlasu:
    /// - true = "Ano"
    /// - false = "Ne"
    /// Pozn.: "Nehlasuji" se do databáze neukládá vůbec
    /// </summary>
    public bool VoteValue { get; set; }

    /// <summary>
    /// Datum a čas vytvoření hlasu
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Datum a čas poslední aktualizace hlasu
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigační vlastnost - hlasovací otázka, ke které patří tento hlas
    /// </summary>
    [ForeignKey("VotingQuestionId")]
    public virtual VotingQuestion VotingQuestion { get; set; } = null!;

    /// <summary>
    /// Navigační vlastnost - uživatel, který hlasoval
    /// </summary>
    [ForeignKey("UserId")]
    public virtual ApplicationUser User { get; set; } = null!;
}