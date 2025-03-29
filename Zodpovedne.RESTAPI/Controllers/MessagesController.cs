using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zodpovedne.Data.Data;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Logging;
using Ganss.Xss;
using Microsoft.AspNetCore.Identity;
using Zodpovedne.Data.Models;
using Microsoft.AspNetCore.Authorization;

namespace Zodpovedne.RESTAPI.Controllers;

/// <summary>
/// Kontroller pro práci s privátními zprávami
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly FileLogger _logger;

    // HtmlSanitizer pro bezpečné čištění vstupů od uživatelů
    private readonly HtmlSanitizer _sanitizer;

    public MessagesController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, FileLogger logger)
    {
        _logger = logger;
        this.dbContext = dbContext;
        this.userManager = userManager;

        // Inicializace a konfigurace HTML sanitizeru pro bezpečné čištění HTML vstupu
        _sanitizer = new HtmlSanitizer();

        try
        {
            // Povolené HTML tagy
            _sanitizer.AllowedTags.Clear();
            _sanitizer.AllowedTags.Add("p");
            _sanitizer.AllowedTags.Add("br");
            _sanitizer.AllowedTags.Add("b");
            _sanitizer.AllowedTags.Add("strong");
            _sanitizer.AllowedTags.Add("i");
            _sanitizer.AllowedTags.Add("em");
            _sanitizer.AllowedTags.Add("ul");
            _sanitizer.AllowedTags.Add("ol");
            _sanitizer.AllowedTags.Add("li");
            _sanitizer.AllowedTags.Add("h2");
            _sanitizer.AllowedTags.Add("h3");
            _sanitizer.AllowedTags.Add("h4");
            _sanitizer.AllowedTags.Add("a");
            _sanitizer.AllowedTags.Add("img");

            // Povolené HTML atributy
            _sanitizer.AllowedAttributes.Clear();
            _sanitizer.AllowedAttributes.Add("href");
            _sanitizer.AllowedAttributes.Add("src");
            _sanitizer.AllowedAttributes.Add("alt");

            _sanitizer.KeepChildNodes = true;

            // Povolené CSS styly (žádné)
            _sanitizer.AllowedCssProperties.Clear();
            _sanitizer.AllowedCssProperties.Add("color");
            _sanitizer.AllowedCssProperties.Add("font-weight");
            _sanitizer.AllowedCssProperties.Add("text-align");
        }
        catch (Exception e)
        {
            _logger.Log("V konstruktoru DiscussionsController se nepodařilo nastavit sanitizer", e);
        }
    }

}