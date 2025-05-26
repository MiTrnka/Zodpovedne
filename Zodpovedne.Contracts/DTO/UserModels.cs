// DTO - objekty slouží pro transfer dat mezi klientem (PostMan, aplikace...) a API
using System.ComponentModel.DataAnnotations;
using Zodpovedne.Contracts.Enums;

namespace Zodpovedne.Contracts.DTO;

public class RegisterModelDto
{
    [EmailAddress]
    [Required(ErrorMessage = "Email je povinný")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Heslo je povinné")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Heslo může mít maximálně 100 znaků")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Přezdívka je povinná")]
    [StringLength(30, MinimumLength = 2, ErrorMessage = "Přezdívka musí mít 2 až 30 znaků")]
    public string Nickname { get; set; } = "";
}

public class UserProfileDto
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string Nickname { get; set; } = "";
    public DateTime Created { get; set; }
    public DateTime? LastLogin { get; set; }
    public int LoginCount { get; set; }
    public List<string> Roles { get; set; } = new();
    public UserType UserType { get; set; }
}

public class UpdateNicknameDto
{
    [Required(ErrorMessage = "Přezdívka je povinná")]
    [StringLength(30, MinimumLength = 2, ErrorMessage = "Přezdívka může mít maximálně 30 znaků")]
    public string Nickname { get; set; } = "";
}

public class UpdateEmailDto
{
    [Required(ErrorMessage = "Email je povinný.")]
    [EmailAddress(ErrorMessage = "Zadejte platnou e-mailovou adresu.")]
    public string Email { get; set; } = "";
}

public class ChangePasswordModelDto
{
    [Required]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Heslo je povinné")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Heslo může mít maximálně 100 znaků")]
    public string NewPassword { get; set; } = "";
}

public class TokenResponseDto
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public string Email { get; set; } = "";
    public string Nickname { get; set; } = "";
}
public class UserListDto
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string Nickname { get; set; } = "";
    public DateTime? LastLogin { get; set; }
    public int LoginCount { get; set; }
    public UserType Type { get; set; }
}

public class PagedResultDto<T>
{
    // Seznam položek na aktuální stránce
    public List<T> Items { get; set; } = new();

    // Celkový počet všech položek (napříč všemi stránkami)
    public int TotalCount { get; set; }

    // Velikost stránky (kolik položek se načítá najednou)
    public int PageSize { get; set; }

    // Aktuální stránka (číslováno od 1)
    public int CurrentPage { get; set; }

    // Celkový počet stránek
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    // Indikátor, zda existují další stránky
    public bool HasNextPage => CurrentPage < TotalPages;

    // Indikátor, zda je toto první stránka
    public bool IsFirstPage => CurrentPage == 1;
}

/// <summary>
/// DTO pro reprezentaci diskuze s novými aktivitami (odpověďmi na komentáře uživatele a/nebo novými komentáři)
/// </summary>
public class DiscussionWithNewActivitiesDto
{
    /// <summary>
    /// ID diskuze
    /// </summary>
    public int DiscussionId { get; set; }

    /// <summary>
    /// Název diskuze
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// URL pro přístup k diskuzi
    /// </summary>
    public string DiscussionUrl { get; set; } = "";

    /// <summary>
    /// Název kategorie, do které diskuze patří
    /// </summary>
    public string CategoryName { get; set; } = "";

    /// <summary>
    /// Čas poslední aktivity v diskuzi (nová odpověď nebo nový komentář)
    /// </summary>
    public DateTime LatestActivityTime { get; set; }

    /// <summary>
    /// Počet komentářů uživatele, které mají nové odpovědi
    /// </summary>
    public int CommentsWithNewRepliesCount { get; set; }

    /// <summary>
    /// Počet nových komentářů v diskuzi založené uživatelem
    /// </summary>
    public int NewCommentsCount { get; set; }

    /// <summary>
    /// Typ aktivity - může být "new_replies", "new_comments" nebo "new_replies_and_comments"
    /// </summary>
    public string ActivityType { get; set; } = "";
}

// Dto pro deserializaci přátelství
public class FriendshipDto
{
    public int FriendshipId { get; set; }
    public string OtherUserId { get; set; } = "";
    public string OtherUserNickname { get; set; } = "";
    public FriendshipStatus Status { get; set; }  // Použití enumu FriendshipStatus
    public bool IsRequester { get; set; }
    public DateTime CreatedAt { get; set; }
}