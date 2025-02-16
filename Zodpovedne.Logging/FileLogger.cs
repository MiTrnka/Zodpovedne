// NuGet Microsoft.Extensions.Configuration
using Microsoft.Extensions.Configuration;
using System.Runtime.CompilerServices;

namespace Zodpovedne.Logging;

public class FileLogger
{
    private readonly string _logPath;
    private readonly object _lock = new();

    public FileLogger(IConfiguration configuration)
    {
        var logDirectory = configuration["FileLogger:Directory"] ?? "Logs";
        var logFileName = configuration["FileLogger:FileName"] ?? "app.log";

        // Vytvoření adresáře, pokud neexistuje
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        _logPath = Path.Combine(logDirectory, logFileName);
    }

    public void Log(
        string message,
        Exception? exception = null,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        var logMessage = $"{DateTime.UtcNow.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}\n[{Path.GetFileName(file)}::{caller}::{line}]\n{message}";

        if (exception != null)
        {
            logMessage += $"\n{exception}";
        }

        logMessage += "\n\n";

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logPath, logMessage);
            }
            catch
            {
                // V případě chyby při zápisu do souboru mlčky selžeme
            }
        }
    }
}