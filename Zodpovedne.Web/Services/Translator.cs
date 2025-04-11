using Zodpovedne.Logging;

namespace Zodpovedne.Web.Services;

public class Translator
{
    private Dictionary<string, string> _translations = new Dictionary<string, string>();
    private readonly string _siteInstance;
    private readonly FileLogger _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiBaseUrl;
    private bool _isInitialized = false;
    private SemaphoreSlim _initializationLock = new SemaphoreSlim(1, 1);

    public Translator(IConfiguration configuration, FileLogger logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        _apiBaseUrl = configuration.GetValue<string>("ApiBaseUrl")
            ?? throw new ArgumentNullException("ApiBaseUrl není nastaven v konfiguraci");

        _siteInstance = configuration.GetValue<string>("SiteInstance") ?? "mamazodpovedne.cz";
        _logger.Log($"Translator inicializován pro instanci: {_siteInstance}");
    }

    // Asynchronní inicializace, která se volá při prvním použití služby
    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
            return;

        await _initializationLock.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            await LoadTranslationsAsync();
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task LoadTranslationsAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"{_apiBaseUrl}/Translations/{_siteInstance}");

            if (response.IsSuccessStatusCode)
            {
                _translations = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>()
                    ?? new Dictionary<string, string>();
                _logger.Log($"Načteno {_translations.Count} překladů pro instanci {_siteInstance}");
            }
            else
            {
                _logger.Log($"Chyba při načítání překladů: {response.StatusCode}");
                // Inicializujeme prázdný slovník, abychom předešli dalším chybám
                _translations = new Dictionary<string, string>();
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Výjimka při načítání překladů: {ex.Message}", ex);
            // Inicializujeme prázdný slovník, abychom předešli dalším chybám
            _translations = new Dictionary<string, string>();
        }
    }

    // Hlavní metoda pro získání překladu
    public async Task<string> GetTranslationAsync(string key, string defaultValue = "")
    {
        await EnsureInitializedAsync();

        if (_translations.TryGetValue(key, out string? value))
            return value;

        return defaultValue;
    }

    // Synchronní verze pro jednoduchost použití (vnitřně používá asynchronní metodu)
    public string GetTranslation(string key, string defaultValue = "")
    {
        // Synchronní volání asynchronní metody - není ideální, ale pro jednoduchost může být užitečné
        return GetTranslationAsync(key, defaultValue).GetAwaiter().GetResult();
    }

    // Metoda pro ruční aktualizaci překladů
    public async Task RefreshTranslationsAsync()
    {
        await LoadTranslationsAsync();
    }
}
