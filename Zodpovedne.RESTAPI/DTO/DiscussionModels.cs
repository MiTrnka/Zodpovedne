using System.ComponentModel.DataAnnotations;

namespace Zodpovedne.RESTAPI.DTO;

// Pro vytvoření nové diskuze
public class CreateDiscussionDto
{
    [Required(ErrorMessage = "Kategorie je povinná")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Nadpis je povinný")]
    [StringLength(200)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Obsah diskuze je povinný")]
    public string Content { get; set; } = "";

    // Pro budoucí implementaci
    public IFormFile? Image { get; set; }
}

// Pro editaci diskuze
public class UpdateDiscussionDto
{
    [Required(ErrorMessage = "Nadpis je povinný")]
    [StringLength(200)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Obsah diskuze je povinný")]
    public string Content { get; set; } = "";
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
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int ViewCount { get; set; }
    public ICollection<CommentDto> Comments { get; set; } = new List<CommentDto>();
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
    public ICollection<CommentDto> Replies { get; set; } = new List<CommentDto>();
}

// Pro vytvoření nového komentáře
public class CreateCommentDto
{
    [Required(ErrorMessage = "Obsah komentáře je povinný")]
    public string Content { get; set; } = "";
}

// Pro editaci komentáře (pouze admin)
public class UpdateCommentDto
{
    [Required(ErrorMessage = "Obsah komentáře je povinný")]
    public string Content { get; set; } = "";
}