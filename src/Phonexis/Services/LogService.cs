using System.Diagnostics;
using System.IO;

namespace Phonexis.Services;

public interface ILogService
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
}

/// <summary>Minimal rolling file logger. One file, truncated when it grows past 2 MB.</summary>
public class LogService : ILogService
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private readonly object _lock = new();

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);

    public void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex == null ? message : $"{message} :: {ex.GetType().Name}: {ex.Message}");

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        Debug.WriteLine(line);
        try
        {
            lock (_lock)
            {
                var file = new FileInfo(AppPaths.LogFile);
                if (file.Exists && file.Length > MaxBytes)
                {
                    var old = AppPaths.LogFile + ".old";
                    File.Delete(old);
                    File.Move(AppPaths.LogFile, old);
                }
                File.AppendAllText(AppPaths.LogFile, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never crash the app.
        }
    }
}
