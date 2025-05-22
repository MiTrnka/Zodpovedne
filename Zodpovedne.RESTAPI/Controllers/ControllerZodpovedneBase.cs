using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Zodpovedne.Data.Data;
using Zodpovedne.Data.Models;
using Zodpovedne.Contracts.DTO;
using Zodpovedne.Contracts.Enums;
using Zodpovedne.Data.Helpers;
using Microsoft.AspNetCore.Identity;
using Ganss.Xss;
using Zodpovedne.Logging;
using Zodpovedne.Logging.Services;
using System.Text.RegularExpressions;

namespace Zodpovedne.RESTAPI.Controllers;

public abstract class ControllerZodpovedneBase : ControllerBase
{
    protected readonly ApplicationDbContext dbContext;
    protected readonly UserManager<ApplicationUser> userManager;
    protected readonly FileLogger logger;

    public Translator Translator { get; }  // Translator pro překlady textů na stránkách

    protected ControllerZodpovedneBase(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, FileLogger logger, Translator translator)
    {
        this.logger = logger;
        this.dbContext = dbContext;
        this.userManager = userManager;
        this.Translator = translator ?? throw new ArgumentNullException(nameof(translator));
    }
}
