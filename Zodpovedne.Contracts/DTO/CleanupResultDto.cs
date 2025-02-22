namespace Zodpovedne.Contracts.DTO;

/// <summary>
/// DTO objekt obsahující informace o počtech smazaných entit během čištění databáze
/// </summary>
public class CleanupResultDto
{
    /// <summary>
    /// Počet smazaných lajků na komentářích
    /// </summary>
    public int DeletedCommentLikes { get; set; }

    /// <summary>
    /// Počet smazaných lajků na diskuzích
    /// </summary>
    public int DeletedDiscussionLikes { get; set; }

    /// <summary>
    /// Počet smazaných odpovědí na komentáře (reakční komentáře)
    /// </summary>
    public int DeletedCommentReplies { get; set; }

    /// <summary>
    /// Počet smazaných root komentářů
    /// </summary>
    public int DeletedRootComments { get; set; }

    /// <summary>
    /// Počet smazaných diskuzí
    /// </summary>
    public int DeletedDiscussions { get; set; }

    /// <summary>
    /// Počet smazaných uživatelů
    /// </summary>
    public int DeletedUsers { get; set; }

    /// <summary>
    /// Počet smazaných diskuzí při mazání uživatelů
    /// </summary>
    public int DeletedUserDiscussions { get; set; }

    /// <summary>
    /// Počet smazaných komentářů při mazání uživatelů
    /// </summary>
    public int DeletedUserComments { get; set; }

    /// <summary>
    /// Celkový počet smazaných entit
    /// </summary>
    public int TotalDeleted =>
        DeletedCommentLikes +
        DeletedDiscussionLikes +
        DeletedCommentReplies +
        DeletedRootComments +
        DeletedDiscussions +
        DeletedUserDiscussions +
        DeletedUserComments +
        DeletedUsers;
}