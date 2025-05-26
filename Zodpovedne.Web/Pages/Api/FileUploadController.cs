using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Zodpovedne.Logging;
using Zodpovedne.Logging.Services;
using Ganss.Xss;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;

namespace Zodpovedne.Web.Pages.Api;

/// <summary>
/// API controller pro nahrávání souborů z CKEditoru.
/// Zpracovává požadavky na nahrání a mazání obrázků.
/// </summary>
[Route("upload")]
[ApiController]
public class FileUploadController : ControllerBase
{
    private readonly FileLogger logger;
    private readonly IHtmlSanitizer sanitizer;
    private readonly IWebHostEnvironment _environment;

    public FileUploadController(FileLogger logger, IHtmlSanitizer sanitizer, IWebHostEnvironment environment)
    {
        this.logger = logger;
        this.sanitizer = sanitizer;
        _environment = environment;
    }

    /// <summary>
    /// Endpoint pro nahrání souboru s limitem velikosti
    /// </summary>
    [Authorize]
    [HttpPost("file")]
    public async Task<IActionResult> UploadImage(IFormFile upload, [FromQuery] string discussionCode)
    {
        try
        {
            // Kontrola, jestli požadavek jde z platného odkazu
            var referer = Request.Headers.Referer.ToString();
            if (!(referer.Contains("/discussion/create/") || (referer.Contains("/Categories"))))
            {
                return Forbid("Neoprávněný přístup");
            }

            // Kontrola zda byly předány všechny potřebné parametry
            if (upload == null || string.IsNullOrEmpty(discussionCode))
            {
                return BadRequest(new { error = "Chybí soubor nebo kód diskuze" });
            }

            // Kontrola velikosti souboru (omezení na 5MB)
            const int maxFileSize = 5 * 1024 * 1024; // 5MB
            if (upload.Length > maxFileSize)
            {
                return BadRequest(new
                {
                    uploaded = 0,
                    error = new { message = "Soubor je příliš velký. Maximální velikost je 5MB." }
                });
            }

            // Validace kódu diskuze - pro bezpečnost, abychom zabránili přístupu k nežádoucím adresářům
            if (!IsValidDiscussionCode(discussionCode))
            {
                logger.Log($"Neplatný kód diskuze při nahrávání souboru: {discussionCode}");
                return BadRequest(new
                {
                    uploaded = 0,
                    error = new { message = "Neplatný kód diskuze" }
                });
            }

            // Validace typu souboru - povolíme pouze obrázky
            if (!IsAllowedFileType(upload.FileName))
            {
                return BadRequest(new
                {
                    uploaded = 0,
                    error = new { message = "Nepodporovaný typ souboru. Povolené jsou pouze JPG, PNG a GIF." }
                });
            }

            // Vytvoření názvu souboru - použijeme GUID pro zajištění unikátnosti
            string fileName = $"{Guid.NewGuid()}_{Path.GetFileName(upload.FileName)}";

            // Cesta k adresáři pro ukládání souborů
            string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "discussions", discussionCode);

            // Vytvoření adresáře, pokud neexistuje
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Kompletní cesta k souboru
            string filePath = Path.Combine(uploadsFolder, fileName);

            // Uložení souboru
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await upload.CopyToAsync(fileStream);
            }

            // Vytvoření URL pro CKEditor
            var url = $"/uploads/discussions/{discussionCode}/{fileName}";

            // Formát odpovědi pro CKEditor
            return new JsonResult(new
            {
                uploaded = 1,
                fileName = fileName,
                url = url
            });
        }
        catch (Exception ex)
        {
            logger.Log("Chyba při nahrávání souboru", ex);
            return StatusCode(500, new
            {
                uploaded = 0,
                error = new { message = "Chyba při nahrávání souboru" }
            });
        }
    }

    /// <summary>
    /// Endpoint pro mazání souborů, které nejsou v dané diskuzi již používány
    /// </summary>
    /// <param name="discussionCode"></param>
    /// <param name="currentImages"></param>
    /// <returns></returns>
    [Authorize]
    [HttpPost("delete-files")]
    public IActionResult DeleteImage([FromQuery] string discussionCode, [FromBody] string[] currentImages)
    {
        if (currentImages == null)
        {
            return BadRequest(new { error = "Chybí seznam aktuálních obrázků v těle požadavku" });
        }

        try
        {
            // Kontrola, jestli požadavek jde z platného odkazu
            var referer = Request.Headers.Referer.ToString();
            if (!(referer.Contains("/discussion/create/")||(referer.Contains("/Categories"))))
            {
                return Forbid("Neoprávněný přístup");
            }


            // Kontrola zda byl předán kód diskuze
            if (string.IsNullOrEmpty(discussionCode))
            {
                return BadRequest(new { error = "Chybí kód diskuze" });
            }

            // Validace kódu diskuze - pro bezpečnost
            if (!IsValidDiscussionCode(discussionCode))
            {
                logger.Log($"Neplatný kód diskuze při promazávání souborů: {discussionCode}");
                return BadRequest(new { error = "Neplatný kód diskuze" });
            }

            string directoryPath = Path.Combine(_environment.WebRootPath, "uploads", "discussions", discussionCode);

            if (!Directory.Exists(directoryPath))
            {
                return Ok(new { success = true, message = "Adresář s obrázky neexistuje, není co mazat." });
            }

            var currentImagesSet = new HashSet<string>(currentImages);
            var deletedFilesCount = 0;

            foreach (var file in Directory.GetFiles(directoryPath))
            {
                var fileName = Path.GetFileName(file);
                if (!currentImagesSet.Contains(fileName))
                {
                    try
                    {
                        System.IO.File.Delete(file);
                        deletedFilesCount++;
                    }
                    catch (Exception ex)
                    {
                        logger.Log($"Chyba při mazání souboru {fileName} v diskuzi {discussionCode}: {ex.Message}");
                        // Nepřerušujeme proces mazání, ale logujeme chybu
                    }
                }
            }

            // Kontrola, zda je adresář prázdný a případné jeho smazání
            if (Directory.Exists(directoryPath) && !Directory.EnumerateFileSystemEntries(directoryPath).Any())
            {
                try
                {
                    Directory.Delete(directoryPath);
                }
                catch (Exception ex)
                {
                    logger.Log($"Chyba při mazání prázdného adresáře diskuze {discussionCode}: {ex.Message}");
                }
            }

            return Ok(new { success = true, deletedCount = deletedFilesCount });
        }
        catch (Exception ex)
        {
            logger.Log("Chyba při promazávání nepoužívaných obrázků", ex);
            return StatusCode(500, new { error = "Chyba při promazávání nepoužívaných obrázků" });
        }
    }

    /// <summary>
    /// Endpoint pro získání seznamu souborů v diskuzi
    /// </summary>
    [HttpGet("list")]
    public IActionResult ListDiscussionFiles([FromQuery] string discussionCode)
    {
        try
        {
            // Validace kódu diskuze - pro bezpečnost
            if (string.IsNullOrEmpty(discussionCode) || !IsValidDiscussionCode(discussionCode))
            {
                logger.Log($"Neplatný kód diskuze při výpisu souborů: {discussionCode}");
                return BadRequest(new { error = "Neplatný kód diskuze" });
            }

            // Cesta k adresáři
            string directoryPath = Path.Combine(_environment.WebRootPath, "uploads", "discussions", discussionCode);

            // Kontrola, zda adresář existuje
            if (!Directory.Exists(directoryPath))
            {
                return Ok(new { files = new List<object>() });
            }

            // Získání seznamu souborů
            var files = Directory.GetFiles(directoryPath)
                .Select(f => new FileInfo(f))
                .Select(f => new
                {
                    name = f.Name,
                    url = $"/uploads/discussions/{discussionCode}/{f.Name}",
                    size = f.Length,
                    lastModified = f.LastWriteTime
                })
                .ToList();

            return Ok(new { files });
        }
        catch (Exception ex)
        {
            logger.Log("Chyba při výpisu souborů", ex);
            return StatusCode(500, new { error = "Chyba při výpisu souborů" });
        }
    }

    /// <summary>
    /// Kontroluje, zda je název souboru platný a bezpečný
    /// </summary>
    private bool IsValidFileName(string fileName)
    {
        // Název souboru by neměl obsahovat neplatné znaky pro cesty a neměl by umožňovat path traversal
        return !string.IsNullOrEmpty(fileName) &&
               !fileName.Contains("..") &&
               !Path.GetInvalidFileNameChars().Any(c => fileName.Contains(c));
    }

    /// <summary>
    /// Kontroluje, zda je kód diskuze platný a bezpečný pro použití v cestě k souboru
    /// </summary>
    private bool IsValidDiscussionCode(string code)
    {
        // Kód diskuze by měl obsahovat pouze alfanumerické znaky, pomlčky a podtržítka
        // a neměl by obsahovat např. "../" pro zabránění traversal útokům
        return !string.IsNullOrEmpty(code) &&
               System.Text.RegularExpressions.Regex.IsMatch(code, @"^[a-zA-Z0-9\-_]+$") &&
               !code.Contains("..");
    }

    /// <summary>
    /// Kontroluje, zda je typ souboru povolen pro nahrání
    /// </summary>
    private bool IsAllowedFileType(string fileName)
    {
        // Povolené přípony souborů
        string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        // Získání přípony souboru a kontrola, zda je povolena
        string extension = Path.GetExtension(fileName).ToLowerInvariant();

        return allowedExtensions.Contains(extension);
    }

    /// <summary>
    /// Endpoint pro přejmenování adresáře s obrázky diskuze
    /// </summary>
    /// <param name="oldCode">Dočasný kód diskuze</param>
    /// <param name="newCode">Finální kód diskuze</param>
    [HttpPost("rename-directory")]
    public IActionResult RenameDirectory([FromBody] RenameDirectoryModel model)
    {
        try
        {
            // Kontrola vstupních parametrů
            if (string.IsNullOrEmpty(model.OldCode) || string.IsNullOrEmpty(model.NewCode))
            {
                return BadRequest(new { error = "Chybí kód zdrojové nebo cílové diskuze" });
            }

            // Validace kódů diskuzí
            if (!IsValidDiscussionCode(model.OldCode) || !IsValidDiscussionCode(model.NewCode))
            {
                logger.Log($"Neplatný kód diskuze při přejmenování adresáře: {model.OldCode} -> {model.NewCode}");
                return BadRequest(new { error = "Neplatný kód diskuze" });
            }

            string sourcePath = Path.Combine(_environment.WebRootPath, "uploads", "discussions", model.OldCode);
            string destinationPath = Path.Combine(_environment.WebRootPath, "uploads", "discussions", model.NewCode);

            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
                return BadRequest(new { error = "Chybí zdrojová nebo cílová cesta" });

            if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                // Pokud jsou cesty stejné, není co přejmenovávat - vratíme úspěch
                return Ok(new { success = true, message = "Cílový a zdrojový adresář jsou stejné, není co přejmenovávat." });
            }

            // Kontrola, zda zdrojový adresář existuje
            if (!Directory.Exists(sourcePath))
            {
                // Pokud neexistuje, není co přejmenovávat - vratíme úspěch
                return Ok(new { success = true, message = "Zdrojový adresář neexistuje, není co přejmenovat." });
            }

            // Kontrola, zda cílový adresář již existuje
            if (Directory.Exists(destinationPath))
            {
                // Pokud již existuje, smažeme ho (to by se nemělo stát, ale pro jistotu)
                Directory.Delete(destinationPath, true);
            }

            // Vytvoříme adresářovou strukturu pro cíl
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? throw new ArgumentNullException("Prazdna cesta pro vytvoreni adresare"));

            // Přejmenování adresáře
            Directory.Move(sourcePath, destinationPath);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            logger.Log("Chyba při přejmenování adresáře", ex);
            return StatusCode(500, new { error = "Chyba při přejmenování adresáře" });
        }
    }

    // Třída pro model přejmenování adresáře
    public class RenameDirectoryModel
    {
        public string OldCode { get; set; } = string.Empty;
        public string NewCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Endpoint pro smazání celého adresáře s obrázky diskuze
    /// </summary>
    /// <param name="discussionCode">Kód diskuze</param>
    [Authorize]
    [HttpPost("delete-directory")]
    public IActionResult DeleteDirectory([FromBody] DeleteDirectoryModel model)
    {
        try
        {
            // Kontrola, jestli požadavek jde z platného odkazu
            var referer = Request.Headers.Referer.ToString();
            if (!referer.Contains("/discussion/create/"))
            {
                return Forbid("Neoprávněný přístup");
            }

            // Kontrola vstupních parametrů
            if (string.IsNullOrEmpty(model.Code))
            {
                return BadRequest(new { error = "Chybí kód diskuze" });
            }

            // Validace kódu diskuze
            if (!IsValidDiscussionCode(model.Code))
            {
                logger.Log($"Neplatný kód diskuze při mazání adresáře: {model.Code}");
                return BadRequest(new { error = "Neplatný kód diskuze" });
            }

            string directoryPath = Path.Combine(_environment.WebRootPath, "uploads", "discussions", model.Code);

            // Kontrola, zda adresář existuje
            if (!Directory.Exists(directoryPath))
            {
                return Ok(new { success = true, message = "Adresář neexistuje, není co mazat." });
            }

            // Smazání adresáře a všech jeho souborů
            Directory.Delete(directoryPath, true);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            logger.Log("Chyba při mazání adresáře", ex);
            return StatusCode(500, new { error = "Chyba při mazání adresáře" });
        }
    }

    // Třída pro model mazání adresáře
    public class DeleteDirectoryModel
    {
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>
    /// Endpoint pro smazání jednoho souboru v adresáři diskuze
    /// </summary>
    /// <param name="discussionCode">Kód diskuze</param>
    /// <param name="fileName">Název souboru</param>
    [Authorize]
    [HttpPost("delete-file")]
    public IActionResult DeleteFile([FromBody] DeleteFileModel model)
    {
        try
        {
            // Kontrola, jestli požadavek jde z platného odkazu
            var referer = Request.Headers.Referer.ToString();
            if (!referer.Contains("/discussion/create/"))
            {
                return Forbid("Neoprávněný přístup");
            }

            // Kontrola vstupních parametrů
            if (string.IsNullOrEmpty(model.DiscussionCode) || string.IsNullOrEmpty(model.FileName))
            {
                return BadRequest(new { error = "Chybí kód diskuze nebo název souboru" });
            }

            // Validace kódu diskuze a názvu souboru
            if (!IsValidDiscussionCode(model.DiscussionCode) || !IsValidFileName(model.FileName))
            {
                logger.Log($"Neplatný kód diskuze nebo název souboru při mazání: {model.DiscussionCode}/{model.FileName}");
                return BadRequest(new { error = "Neplatný kód diskuze nebo název souboru" });
            }

            string filePath = Path.Combine(_environment.WebRootPath, "uploads", "discussions", model.DiscussionCode, model.FileName);

            // Kontrola, zda soubor existuje
            if (!System.IO.File.Exists(filePath))
            {
                return Ok(new { success = true, message = "Soubor neexistuje, není co mazat." });
            }

            // Smazání souboru
            System.IO.File.Delete(filePath);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            logger.Log("Chyba při mazání souboru", ex);
            return StatusCode(500, new { error = "Chyba při mazání souboru" });
        }
    }

    // Třída pro model mazání souboru
    public class DeleteFileModel
    {
        public string DiscussionCode { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

}