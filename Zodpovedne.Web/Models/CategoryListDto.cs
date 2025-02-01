namespace Zodpovedne.Web.Models;

public class CategoryListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public int DisplayOrder { get; set; }
}
