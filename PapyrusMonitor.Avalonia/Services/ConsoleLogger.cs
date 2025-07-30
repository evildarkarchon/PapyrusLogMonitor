using PapyrusMonitor.Core.Interfaces;

namespace PapyrusMonitor.Avalonia.Services;

public class ConsoleLogger : ILogger
{
    public void LogInformation(string message)
    {
        Console.WriteLine($"[INFO] {message}");
    }

    public void LogWarning(string message)
    {
        Console.WriteLine($"[WARN] {message}");
    }

    public void LogError(string message, Exception? exception = null)
    {
        Console.WriteLine($"[ERROR] {message}");
        if (exception != null)
        {
            Console.WriteLine($"[ERROR] Exception: {exception}");
        }
    }

    public void LogDebug(string message)
    {
        Console.WriteLine($"[DEBUG] {message}");
    }
}
