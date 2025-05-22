using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zodpovedne.Data.Data;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Logging;
using Zodpovedne.Data.Models;
using Zodpovedne.Logging.Services;
//using Zodpovedne.Data.Migrations;
using Microsoft.AspNetCore.Identity;

namespace Zodpovedne.RESTAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TranslationsController : ControllerZodpovedneBase
{
    public TranslationsController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, FileLogger logger, Translator translator)
        : base(dbContext, userManager, logger, translator)
    {
    }

    /// <summary>
    /// Vrátí netrackovaný detail konkrétní kategorie podle jejího kódu
    /// </summary>
    // Cachování vypnuto
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [HttpGet("{siteInstanceCode}")]
    public async Task<ActionResult<Dictionary<string, string>>> GetDictionaryForSiteInstance(string siteInstanceCode)
    {
        try
        {
            var translations = await dbContext.Translations
                .AsNoTracking()
                .Where(t => t.SiteInstance.Code == siteInstanceCode)
                .Select(t => new { t.Code, t.TranslatedText })
                .ToDictionaryAsync(t => t.Code, x => x.TranslatedText);

            if ((translations == null) || (translations.Count == 0))
                return NotFound();

            return Ok(translations);
        }
        catch (Exception e)
        {
            logger.Log("Chyba při vykonávání akce GetDictionaryForSiteInstance endpointu.", e);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}