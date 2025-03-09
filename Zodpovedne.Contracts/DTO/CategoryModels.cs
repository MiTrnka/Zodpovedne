namespace Zodpovedne.Contracts.DTO;

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public int DisplayOrder { get; set; }
    public string? ImagePath { get; set; }
}