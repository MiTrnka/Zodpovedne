namespace Zodpovedne.Web.Services
{
    /// <summary>
    /// Pomocná třída pro mazání dočasných adresářů a adresářů smazaných diskuzí
    /// </summary>
    public class PictureCleaner
    {
        /// <summary>
        /// Smaže všechny dočasné adresáře (začínající "temp_") v adresáři uploads/discussions
        /// </summary>
        /// <param name="app"></param>
        public static void CleanTempDirectories(string uploadsRoot)
        {
            // Kontrola existence adresáře
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
                    catch (Exception)
                    {
                    }
                }
            }

        }

        /// <summary>
        /// Smaže adresáře diskuzí, které již neexistují v databázi
        /// </summary>
        /// <param name="uploadsRoot">Kořenový adresář pro nahrané soubory diskuzí</param>
        /// <param name="apiBaseUrl">Základní URL pro API</param>
        public static void CleanupInvalidDiscussionDirectories(string uploadsRoot, string apiBaseUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(apiBaseUrl))
                {
                    Console.WriteLine("ApiBaseUrl není nastaven v konfiguračním souboru.");
                    return;
                }

                // Vytvoření HTTP klienta pro volání API
                using var httpClient = new HttpClient();

                // Získání seznamu kódů diskuzí z API
                var response = httpClient.GetAsync($"{apiBaseUrl}/discussions/codes").Result;
                if (response.IsSuccessStatusCode)
                {
                    // Deserializace seznamu kódů diskuzí
                    var validDiscussionCodes = response.Content.ReadFromJsonAsync<List<string>>().Result;

                    if (validDiscussionCodes != null)
                    {
                        // Vytvoření seznamu všech adresářů v uploads/discussions, které nejsou dočasné
                        var allDirectories = Directory.GetDirectories(uploadsRoot)
                            .Where(dir => !Path.GetFileName(dir).StartsWith("temp_"))
                            .Select(dir => Path.GetFileName(dir))
                            .ToList();

                        // Nalezení adresářů, které neodpovídají žádnému kódu diskuze
                        var invalidDirectories = allDirectories
                            .Where(dirName => !validDiscussionCodes.Contains(dirName))
                            .ToList();

                        // Smazání neplatných adresářů
                        foreach (var dirName in invalidDirectories)
                        {
                            try
                            {
                                string dirPath = Path.Combine(uploadsRoot, dirName);
                                Console.WriteLine($"Mazání neplatného adresáře: {dirPath}");
                                Directory.Delete(dirPath, true); // true = smazat i s obsahem
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Chyba při mazání adresáře {dirName}: {ex.Message}");
                            }
                        }

                        Console.WriteLine($"Celkem smazáno {invalidDirectories.Count} neplatných adresářů.");
                    }
                    else
                    {
                        Console.WriteLine("API vrátilo null místo seznamu kódů diskuzí.");
                    }
                }
                else
                {
                    Console.WriteLine($"Chyba při volání API pro získání kódů diskuzí: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při mazání neplatných adresářů: {ex.Message}");
            }
        }
    }
}
