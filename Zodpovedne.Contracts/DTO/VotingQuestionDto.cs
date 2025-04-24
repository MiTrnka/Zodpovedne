using System.ComponentModel.DataAnnotations;
using Zodpovedne.Contracts.Enums;

namespace Zodpovedne.Contracts.DTO;

/// <summary>
/// DTO pro vytvoření nebo aktualizaci hlasovací otázky
/// </summary>
public class VotingQuestionDto
{
    /// <summary>
    /// ID hlasovací otázky (null při vytváření nové otázky)
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    /// Text hlasovací otázky
    /// </summary>
    [Required(ErrorMessage = "Text otázky je povinný")]
    [MaxLength(400, ErrorMessage = "Text otázky nesmí být delší než 400 znaků")]
    public string Text { get; set; } = "";

    /// <summary>
    /// Pořadí otázky v rámci hlasování (pro správné řazení při zobrazení)
    /// </summary>
    public int DisplayOrder { get; set; }
}

/// <summary>
/// DTO pro detail hlasovací otázky včetně výsledků a informace o hlasování aktuálního uživatele
/// </summary>
public class VotingQuestionDetailDto
{
    /// <summary>
    /// ID hlasovací otázky
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Text hlasovací otázky
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Pořadí otázky v rámci hlasování
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Počet hlasů "Ano" pro tuto otázku
    /// </summary>
    public int YesVotes { get; set; }

    /// <summary>
    /// Počet hlasů "Ne" pro tuto otázku
    /// </summary>
    public int NoVotes { get; set; }

    /// <summary>
    /// Celkový počet hlasů pro tuto otázku (YesVotes + NoVotes)
    /// </summary>
    public int TotalVotes => YesVotes + NoVotes;

    /// <summary>
    /// Procentuální vyjádření hlasů "Ano" (0-100)
    /// </summary>
    public int YesPercentage => TotalVotes > 0 ? (int)Math.Round((double)YesVotes / TotalVotes * 100) : 0;

    /// <summary>
    /// Procentuální vyjádření hlasů "Ne" (0-100)
    /// </summary>
    public int NoPercentage => TotalVotes > 0 ? (int)Math.Round((double)NoVotes / TotalVotes * 100) : 0;

    /// <summary>
    /// Hodnota hlasu aktuálního uživatele:
    /// - true = uživatel hlasoval "Ano"
    /// - false = uživatel hlasoval "Ne"
    /// - null = uživatel nehlasoval nebo zvolil "Nehlasuji"
    /// </summary>
    public bool? CurrentUserVote { get; set; }
}

/// <summary>
/// DTO pro kompletní hlasování v diskuzi
/// </summary>
public class VotingDto
{
    /// <summary>
    /// ID diskuze, ke které patří hlasování
    /// </summary>
    public int DiscussionId { get; set; }

    /// <summary>
    /// Typ hlasování (jeho stav)
    /// </summary>
    public VoteType VoteType { get; set; }

    /// <summary>
    /// Seznam hlasovacích otázek v hlasování
    /// </summary>
    public List<VotingQuestionDetailDto> Questions { get; set; } = new();
}

/// <summary>
/// DTO pro kompletní vytvoření nebo aktualizaci hlasování v diskuzi
/// </summary>
public class CreateOrUpdateVotingDto
{
    /// <summary>
    /// ID diskuze, ke které patří hlasování
    /// </summary>
    public int DiscussionId { get; set; }

    /// <summary>
    /// Typ hlasování (jeho stav)
    /// </summary>
    public VoteType VoteType { get; set; }

    /// <summary>
    /// Seznam hlasovacích otázek v hlasování
    /// </summary>
    public List<VotingQuestionDto> Questions { get; set; } = new();
}

/// <summary>
/// DTO pro odeslání hlasů uživatele
/// </summary>
public class SubmitVotesDto
{
    /// <summary>
    /// ID diskuze, ke které patří hlasování
    /// </summary>
    public int DiscussionId { get; set; }

    /// <summary>
    /// Seznam ID otázek a odpovědí uživatele
    /// Pokud uživatel zvolil "Nehlasuji", otázka se v seznamu neobjeví
    /// </summary>
    public Dictionary<int, bool> Votes { get; set; } = new();
}

/// <summary>
/// Rozšíření DTO pro diskuzi o informace o hlasování
/// </summary>
public class DiscussionDetailWithVotingDto : DiscussionDetailDto
{
    /// <summary>
    /// Typ hlasování v diskuzi
    /// </summary>
    public VoteType VoteType { get; set; }

    /// <summary>
    /// Hlasování v diskuzi (null pokud diskuze nemá hlasování)
    /// </summary>
    public VotingDto? Voting { get; set; }
}