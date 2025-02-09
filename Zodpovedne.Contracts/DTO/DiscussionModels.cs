// NuGet Microsoft.AspNetCore.Http.Features pro IFormFile
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Zodpovedne.Contracts.Enums;

namespace Zodpovedne.Contracts.DTO;

// Pro vytvoření nové diskuze
public class CreateDiscussionDto
{
    [Required(ErrorMessage = "Kategorie je povinná")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Nadpis je povinný")]
    [StringLength(200)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Obsah diskuze je povinný")]
    [MaxLength(3000, ErrorMessage = "Obsah diskuze může mít maximálně 3 000 znaků")]
    public string Content { get; set; } = "";

    // Pro budoucí implementaci
    //public IFormFile? Image { get; set; }

    // Typ diskuze nastavujeme jen při vytvoření, výchozí hodnota Normal
    public DiscussionType Type { get; set; } = DiscussionType.Normal;
}

// Pro editaci diskuze
public class UpdateDiscussionDto
{
    [Required(ErrorMessage = "Nadpis je povinný")]
    [StringLength(200)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Obsah diskuze je povinný")]
    [MaxLength(3000, ErrorMessage = "Obsah diskuze může mít maximálně 3 000 znaků")]
    public string Content { get; set; } = "";

    // Typ diskuze může měnit jen admin
    public DiscussionType Type { get; set; }
}

// Pro výpis v seznamu diskuzí
public class DiscussionListDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string AuthorNickname { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int CommentsCount { get; set; }
    public int ViewCount { get; set; }
    public DiscussionType Type { get; set; }
    public string Code { get; set; } = "";
    public LikeInfoDto Likes { get; set; } = new();
}

// Pro detail diskuze včetně komentářů
public class DiscussionDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? ImagePath { get; set; }
    public string CategoryName { get; set; } = "";
    public string AuthorNickname { get; set; } = "";
    public string AuthorId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int ViewCount { get; set; }
    public DiscussionType Type { get; set; }
    public ICollection<CommentDto> Comments { get; set; } = new List<CommentDto>();
    public LikeInfoDto Likes { get; set; } = new();
}

// Pro komentáře
public class CommentDto
{
    public int Id { get; set; }
    public string Content { get; set; } = "";
    public string AuthorNickname { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? ParentCommentId { get; set; }
    public CommentType Type { get; set; }
    public ICollection<CommentDto> Replies { get; set; } = new List<CommentDto>();
    public LikeInfoDto Likes { get; set; } = new();
}

// Pro vytvoření nového komentáře
public class CreateCommentDto
{
    [Required(ErrorMessage = "Obsah komentáře je povinný")]
    public string Content { get; set; } = "";
    public CommentType Type { get; set; } = CommentType.Normal;
}

// Pro editaci komentáře (pouze admin)
public class UpdateCommentDto
{
    [Required(ErrorMessage = "Obsah komentáře je povinný")]
    public string Content { get; set; } = "";
    public CommentType Type { get; set; } = CommentType.Normal;
}