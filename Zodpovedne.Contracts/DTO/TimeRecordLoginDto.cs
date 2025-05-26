namespace Zodpovedne.Contracts.DTO;

public class TimeRecordLoginDto
{
    public int Id { get; set; }
    public DateTime LoginTime { get; set; }
    public string IpAddress { get; set; } = "";
}

public class RecordLoginDto
{
    public string UserId { get; set; } = "";
    public string IpAddress { get; set; } = "";
}