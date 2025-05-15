namespace Zodpovedne.Contracts.DTO;

public class LoginHistoryDto
{
    public int Id { get; set; }
    public DateTime LoginTime { get; set; }
    public string IpAddress { get; set; } = "";
}
