// NuGet Microsoft.AspNetCore.Http.Features pro IFormFile
using System.ComponentModel.DataAnnotations;
using Zodpovedne.Contracts.Enums;

namespace Zodpovedne.Contracts.DTO;

// Pro vytvoření nové diskuze
public class CreateDiscussionDto
{
    [Required(ErrorMessage = "Kategorie je povinná")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Nadpis diskuze je povinný")]
    [MaxLength(70, ErrorMessage = "Nadpis diskuze nesmí být delší než 70 znaků.")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Obsah diskuze je povinný")]
    [MaxLength(10000, ErrorMessage = "Obsah diskuze může mít maximálně 3 000 znaků")]
    public string Content { get; set; } = "";

    // Pro budoucí implementaci
    //public IFormFile? Image { get; set; }

    // Typ diskuze nastavujeme jen při vytvoření, výchozí hodnota Normal
    public DiscussionType Type { get; set; } = DiscussionType.Normal;
    /// <summary>
    /// Typ hlasování v diskuzi - výchozí hodnota None (bez hlasování)
    /// </summary>
    public VoteType VoteType { get; set; } = VoteType.None;

    /// <summary>
    /// Seznam hlasovacích otázek (pokud má diskuze hlasování)
    /// </summary>
    public List<VotingQuestionDto>? VotingQuestions { get; set; }
}

// Pro editaci diskuze
public class UpdateDiscussionDto
{
    [Required(ErrorMessage = "Nadpis diskuze je povinný")]
    [MaxLength(70, ErrorMessage = "Nadpis diskuze nesmí být delší než 70 znaků.")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Obsah diskuze je povinný")]
    [MaxLength(10000, ErrorMessage = "Obsah diskuze může mít maximálně 3 000 znaků")]
    public string Content { get; set; } = "";

    // Typ diskuze může měnit jen admin
    public DiscussionType Type { get; set; }

    /// <summary>
    /// Typ hlasování v diskuzi
    /// </summary>
    public VoteType VoteType { get; set; }
}

// Pro výpis v seznamu diskuzí
public class DiscussionListDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string CategoryCode { get; set; } = "";
    public string AuthorNickname { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int CommentsCount { get; set; }
    public int ViewCount { get; set; }
    public DiscussionType Type { get; set; }
    public string Code { get; set; } = "";
    public LikeInfoDto Likes { get; set; } = new();
    public VoteType VoteType { get; set; } = VoteType.None;
}

// Pro detail diskuze včetně komentářů
public class DiscussionDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? ImagePath { get; set; }
    public string CategoryName { get; set; } = "";
    public int CategoryId { get; set; }
    public string CategoryCode { get; set; } = "";
    public string DiscussionCode { get; set; } = "";
    public string AuthorNickname { get; set; } = "";
    public string AuthorId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int ViewCount { get; set; }
    public DiscussionType Type { get; set; }
    public ICollection<CommentDto> Comments { get; set; } = new List<CommentDto>();
    public LikeInfoDto Likes { get; set; } = new();
    public bool HasMoreComments { get; set; }
    public VoteType VoteType { get; set; } = VoteType.None;
}

public class BasicDiscussionInfoDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? ImagePath { get; set; }
    public string CategoryName { get; set; } = "";
    public int CategoryId { get; set; }
    public string CategoryCode { get; set; } = "";
    public string DiscussionCode { get; set; } = "";
    public string AuthorNickname { get; set; } = "";
    public string AuthorId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int ViewCount { get; set; }
    public DiscussionType Type { get; set; }
    public VoteType VoteType { get; set; } = VoteType.None;
}

// Pro komentáře
public class CommentDto
{
    public int Id { get; set; }
    public int DiscussionId { get; set; }
    public string Content { get; set; } = "";
    public string AuthorNickname { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? ParentCommentId { get; set; }
    public CommentType Type { get; set; }
    public ICollection<CommentDto> Replies { get; set; } = new List<CommentDto>();
    public LikeInfoDto Likes { get; set; } = new();
    public bool HasNewReplies { get; set; }
}

// Pro vytvoření nového komentáře
public class CreateCommentDto
{
    [Required(ErrorMessage = "Obsah komentáře je povinný")]
    [MaxLength(500, ErrorMessage = "Obsah komentáře nesmí být delší než 500 znaků.")]
    public string Content { get; set; } = "";
    public CommentType Type { get; set; } = CommentType.Normal;
}

// Pro editaci komentáře (pouze admin)
public class UpdateCommentDto
{
    [Required(ErrorMessage = "Obsah komentáře je povinný")]
    [MaxLength(500, ErrorMessage = "Obsah komentáře nesmí být delší než 500 znaků.")]
    public string Content { get; set; } = "";
    public CommentType Type { get; set; } = CommentType.Normal;
}