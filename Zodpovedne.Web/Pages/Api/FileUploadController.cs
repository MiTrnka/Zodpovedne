using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Zodpovedne.Logging;
using Zodpovedne.Logging.Services;
using Ganss.Xss;
using Microsoft.AspNetCore.Http;

namespace Zodpovedne.Web.Pages.Api
{
    /// <summary>
    /// API controller pro nahrávání souborů z CKEditoru.
    /// Zpracovává požadavky na nahrání a mazání obrázků.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class FileUploadController : ControllerBase
    {
        private readonly FileLogger _logger;
        private readonly IHtmlSanitizer _sanitizer;
        private readonly IWebHostEnvironment _environment;

        public FileUploadController(FileLogger logger, IHtmlSanitizer sanitizer, IWebHostEnvironment environment)
        {
            _logger = logger;
            _sanitizer = sanitizer;
            _environment = environment;
        }

        /// <summary>
        /// Endpoint pro nahrání souboru
        /// </summary>
        /// <param name="upload">Nahrávaný soubor</param>
        /// <param name="discussionCode">Kód diskuze, pro kterou je soubor nahráván</param>
        /// <returns>URL nahraného souboru pro CKEditor</returns>
        [HttpPost("upload")]
        public async Task<IActionResult> UploadImage(IFormFile upload, [FromQuery] string discussionCode)
        {
            try
            {
                // Kontrola zda byly předány všechny potřebné parametry
                if (upload == null || string.IsNullOrEmpty(discussionCode))
                {
                    return BadRequest(new { error = "Chybí soubor nebo kód diskuze" });
                }

                // Validace kódu diskuze - pro bezpečnost, abychom zabránili přístupu k nežádoucím adresářům
                if (!IsValidDiscussionCode(discussionCode))
                {
                    _logger.Log($"Neplatný kód diskuze při nahrávání souboru: {discussionCode}");
                    return BadRequest(new { error = "Neplatný kód diskuze" });
                }

                // Validace typu souboru - povolíme pouze obrázky
                if (!IsAllowedFileType(upload.FileName))
                {
                    return BadRequest(new { error = "Nepodporovaný typ souboru" });
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
                _logger.Log("Chyba při nahrávání souboru", ex);
                return StatusCode(500, new { error = "Chyba při nahrávání souboru" });
            }
        }

        /// <summary>
        /// Endpoint pro smazání souboru
        /// </summary>
        /// <param name="discussionCode">Kód diskuze, ke které patří soubor</param>
        /// <param name="fileName">Název souboru, který má být smazán</param>
        /// <returns>Výsledek operace</returns>
        [HttpDelete("delete")]
        public IActionResult DeleteImage([FromQuery] string discussionCode, [FromQuery] string fileName)
        {
            try
            {
                // Kontrola zda byly předány všechny potřebné parametry
                if (string.IsNullOrEmpty(discussionCode) || string.IsNullOrEmpty(fileName))
                {
                    return BadRequest(new { error = "Chybí kód diskuze nebo název souboru" });
                }

                // Validace kódu diskuze - pro bezpečnost
                if (!IsValidDiscussionCode(discussionCode))
                {
                    _logger.Log($"Neplatný kód diskuze při mazání souboru: {discussionCode}");
                    return BadRequest(new { error = "Neplatný kód diskuze" });
                }

                // Validace názvu souboru - pro bezpečnost
                if (!IsValidFileName(fileName))
                {
                    _logger.Log($"Neplatný název souboru při mazání: {fileName}");
                    return BadRequest(new { error = "Neplatný název souboru" });
                }

                // Cesta k souboru
                string filePath = Path.Combine(_environment.WebRootPath, "uploads", "discussions", discussionCode, fileName);

                // Kontrola, zda soubor existuje
                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound(new { error = "Soubor nebyl nalezen" });
                }

                // Smazání souboru
                System.IO.File.Delete(filePath);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.Log("Chyba při mazání souboru", ex);
                return StatusCode(500, new { error = "Chyba při mazání souboru" });
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
            string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };

            // Získání přípony souboru a kontrola, zda je povolena
            string extension = Path.GetExtension(fileName).ToLowerInvariant();

            return allowedExtensions.Contains(extension);
        }


    }
}