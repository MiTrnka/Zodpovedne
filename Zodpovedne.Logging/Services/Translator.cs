using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace Zodpovedne.Logging.Services;

public class Translator
{
    private Dictionary<string, string> _translations = new Dictionary<string, string>();
    public string SiteInstance { get; }
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

        SiteInstance = configuration.GetValue<string>("SiteInstance") ?? "mamazodpovedne.cz";
    }

    // Asynchronní inicializace, která se volá při prvním použití služby, zavolá metodu pro načtení překladů pro danou site instanci
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

    /// <summary>
    /// Načte překlady z API pro danou instanci na+ctenou z konfigurace v appsettings.json
    /// </summary>
    /// <returns></returns>
    private async Task LoadTranslationsAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"{_apiBaseUrl}/Translations/{SiteInstance}");

            if (response.IsSuccessStatusCode)
            {
                _translations = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>()
                    ?? new Dictionary<string, string>();
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
    public async Task<string> TranslateAsync(string key, string defaultValue = "")
    {
        await EnsureInitializedAsync();

        if (_translations.TryGetValue(key, out string? value))
            return value;

        return defaultValue;
    }

    // Synchronní verze pro jednoduchost použití (vnitřně používá asynchronní metodu)
    public string Translate(string key, string defaultValue = "")
    {
        // Synchronní volání asynchronní metody - není ideální, ale pro jednoduchost může být užitečné
        return TranslateAsync(key, defaultValue).GetAwaiter().GetResult();
    }

    // Metoda pro ruční aktualizaci překladů
    public async Task RefreshTranslationsAsync()
    {
        await LoadTranslationsAsync();
    }
}
