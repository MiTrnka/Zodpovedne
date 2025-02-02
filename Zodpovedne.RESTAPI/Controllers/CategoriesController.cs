using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zodpovedne.Data.Data;
using Zodpovedne.Contracts.DTO;

namespace Zodpovedne.RESTAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;

    public CategoriesController(ApplicationDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <summary>
    /// Vrátí seznam všech kategorií seřazený podle DisplayOrder
    /// Tento endpoint může volat kdokoliv (i nepřihlášený uživatel)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryListDto>>> GetCategories()
    {
        var categories = await dbContext.Categories
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new CategoryListDto
            {
                Id = c.Id,
                Name = c.Name,
                Code = c.Code,
                Description = c.Description,
                DisplayOrder = c.DisplayOrder
            })
            .ToListAsync();

        return Ok(categories);
    }

    /// <summary>
    /// Vrátí detail konkrétní kategorie podle jejího kódu
    /// </summary>
    [HttpGet("{code}")]
    public async Task<ActionResult<CategoryListDto>> GetCategoryByCode(string code)
    {
        var category = await dbContext.Categories
            .FirstOrDefaultAsync(c => c.Code == code);

        if (category == null)
            return NotFound();

        var categoryDto = new CategoryListDto
        {
            Id = category.Id,
            Name = category.Name,
            Code = category.Code,
            Description = category.Description,
            DisplayOrder = category.DisplayOrder
        };

        return Ok(categoryDto);
    }
}