using Microsoft.Extensions.Logging;

using Zodpovedne.Logging;

/// <summary>
/// Vlastní implementace ILogger, která používá náš FileLogger.
/// Filtruje logy tak, aby se zapisovaly pouze Critical a Error úrovně.
/// Zachovává formát logování používaný v celé aplikaci.
/// </summary>
public class CustomFileLogger : ILogger
{
    private readonly FileLogger _fileLogger;
    private readonly string _categoryName;

    public CustomFileLogger(FileLogger fileLogger, string categoryName)
    {
        _fileLogger = fileLogger;
        _categoryName = categoryName;
    }

    /// <summary>
    /// Metoda pro vytváření logického scope.
    /// V naší implementaci není potřeba, proto vrací null.
    /// </summary>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <summary>
    /// Určuje, zda se má daná úroveň logování zaznamenávat.
    /// V našem případě logujeme pouze Critical a Error úrovně.
    /// </summary>
    /// <param name="logLevel">Úroveň logu ke kontrole</param>
    /// <returns>true pokud se má log zaznamenat, jinak false</returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        // Logujeme pouze Critical a Error
        return logLevel == LogLevel.Critical || logLevel == LogLevel.Error || logLevel == LogLevel.Warning;
    }

    /// <summary>
    /// Hlavní metoda pro zápis logů.
    /// Formátuje log zprávu a používá náš FileLogger pro její zápis.
    /// </summary>
    /// <typeparam name="TState">Typ stavu logu</typeparam>
    /// <param name="logLevel">Úroveň logu</param>
    /// <param name="eventId">ID události (nepoužíváme)</param>
    /// <param name="state">Stav logu</param>
    /// <param name="exception">Výjimka, pokud nastala</param>
    /// <param name="formatter">Funkce pro formátování zprávy</param>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // Kontrola, zda se má log zaznamenat
        if (!IsEnabled(logLevel))
            return;

        // Získání zformátované zprávy
        var message = formatter(state, exception);

        // Zápis do našeho FileLoggeru s přidanou informací o zdroji logu
        _fileLogger.Log($"[ASP.NET Core {logLevel}] {_categoryName}: {message}", exception);
    }
}