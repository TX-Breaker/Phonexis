using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Phonexis.Models;

namespace Phonexis.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    void Save();
}

public class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILogService _log;

    public AppSettings Settings { get; }

    public SettingsService(ILogService log)
    {
        _log = log;
        Settings = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded != null) return loaded;
            }
        }
        catch (Exception ex)
        {
            _log.Error("Failed to load settings, using defaults", ex);
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(Settings, JsonOptions));
        }
        catch (Exception ex)
        {
            _log.Error("Failed to save settings", ex);
        }
    }
}
