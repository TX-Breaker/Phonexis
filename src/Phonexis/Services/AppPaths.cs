using System.IO;

namespace Phonexis.Services;

/// <summary>Central definition of all on-disk locations used by the app.</summary>
public static class AppPaths
{
    /// <summary>
    /// PHONEXIS_DATA_DIR points the app at a separate data folder (demos, tests, portable use)
    /// so the user's own settings, keys and session are never touched.
    /// </summary>
    public static string Root { get; } =
        Environment.GetEnvironmentVariable("PHONEXIS_DATA_DIR") is { Length: > 0 } custom
            ? Path.GetFullPath(custom)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Phonexis", "V2");

    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string SessionFile => Path.Combine(Root, "session.json");
    public static string DatabaseFile => Path.Combine(Root, "cache.db");
    public static string ApiKeysFile => Path.Combine(Root, "keys.bin");
    public static string LogFile => Path.Combine(Root, "logs", "phonexis.log");

    /// <summary>Plaintext key file used by Phonexis V1, imported on first run.</summary>
    /// <remarks>With PHONEXIS_DATA_DIR set it stays inside that folder: a separate data folder never imports real keys.</remarks>
    public static string LegacyApiKeysFile =>
        Environment.GetEnvironmentVariable("PHONEXIS_DATA_DIR") is { Length: > 0 }
            ? Path.Combine(Root, "api_keys.env")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Phonexis", "api_keys.env");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Path.Combine(Root, "logs"));
    }
}
