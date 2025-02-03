namespace Zodpovedne.Contracts.DTO;

public class LikeInfoDto
{
    public int LikeCount { get; set; }
    public bool HasUserLiked { get; set; }
    public bool CanUserLike { get; set; }
}