namespace Zodpovedne.Web.Services;

public class FileService
{
    public static void TempDirectoryDelete()
    {
        // Cesta k adresáři s obrázky diskuzí
        /*string uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "discussions");
        // Kontrola existence hlavního adresáře
        if (Directory.Exists(uploadsRoot))
        {
            // 1. Smazání všech dočasných adresářů (začínajících "temp_")
            var tempDirectories = Directory.GetDirectories(uploadsRoot)
                .Where(dir => Path.GetFileName(dir).StartsWith("temp_"))
                .ToList();
            foreach (var tempDir in tempDirectories)
            {
                try
                {
                    Directory.Delete(tempDir, true); // true = smazat i s obsahem
                }
                catch (Exception ex)
                {
                }
            }
        }*/

    }
}
