using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zodpovedne.Data.Data;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Logging;
using Zodpovedne.Logging.Services;

namespace Zodpovedne.RESTAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly FileLogger _logger;
    public Translator Translator { get; }  // Translator pro překlady textů na stránkách

    public CategoriesController(ApplicationDbContext dbContext, FileLogger logger, Translator translator)
    {
        this.dbContext = dbContext;
        _logger = logger;
        Translator = translator ?? throw new ArgumentNullException(nameof(translator));
    }

    /// <summary>
    /// Vrátí seznam všech kategorií (netrackovaný) seřazený podle DisplayOrder
    /// Tento endpoint může volat kdokoliv (i nepřihlášený uživatel)
    /// </summary>
    // Cachování seznamu všech kategorií pro nepřihlášené uživatele na 10 sekund
    [ResponseCache(Duration = 10)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories()
    {
        try
        {
            var categories = await dbContext.Categories
                .AsNoTracking()
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Code = c.Code,
                    Description = c.Description,
                    DisplayOrder = c.DisplayOrder,
                    ImagePath = c.ImagePath
                })
                .ToListAsync();

            return Ok(categories);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce GetCategories endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Vrátí netrackovaný detail konkrétní kategorie podle jejího kódu
    /// </summary>
    // Cachování konkrétního (pro každý code) detailu kategorie pro nepřihlášené uživatele na 10 sekund
    [ResponseCache(Duration = 10)]
    [HttpGet("{code}")]
    public async Task<ActionResult<CategoryDto>> GetCategoryByCode(string code)
    {
        try
        {
            var category = await dbContext.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Code == code);

            if (category == null)
                return NotFound();

            var categoryDto = new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Code = category.Code,
                Description = category.Description,
                DisplayOrder = category.DisplayOrder,
                ImagePath = category.ImagePath
            };

            return Ok(categoryDto);
        }
        catch (Exception e)
        {
            _logger.Log("Chyba při vykonávání akce GetCategoryByCode endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}