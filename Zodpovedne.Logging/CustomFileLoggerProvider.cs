// NuGet Microsoft.Extensions.Logging
using Microsoft.Extensions.Logging;

namespace Zodpovedne.Logging;

/// <summary>
/// Provider pro vytváření vlastních loggerů.
/// Implementuje ILoggerProvider, což je rozhraní vyžadované ASP.NET Core pro vlastní logging providery.
/// Tento provider používá náš FileLogger pro zápis logů ve stejném formátu jako zbytek aplikace.
/// </summary>
public class CustomFileLoggerProvider : ILoggerProvider
{
    private readonly FileLogger _fileLogger;

    public CustomFileLoggerProvider(FileLogger fileLogger)
    {
        _fileLogger = fileLogger;
    }

    /// <summary>
    /// Vytvoří novou instanci loggeru pro danou kategorii.
    /// Tato metoda je volána ASP.NET Core frameworkem pro každou kategorii logování zvlášť.
    /// </summary>
    /// <param name="categoryName">Název kategorie logování (typicky název třídy nebo namespace)</param>
    public ILogger CreateLogger(string categoryName)
    {
        return new CustomFileLogger(_fileLogger, categoryName);
    }

    /// <summary>
    /// Dispose metoda vyžadovaná rozhraním ILoggerProvider.
    /// V našem případě není potřeba nic dispozovat.
    /// </summary>
    public void Dispose()
    {
    }
}