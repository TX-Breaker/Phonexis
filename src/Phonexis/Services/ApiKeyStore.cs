using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Phonexis.Services;

public interface IApiKeyStore
{
    List<string> Load();
    bool Save(List<string> keys);
}

/// <summary>
/// Stores YouTube API keys encrypted with Windows DPAPI (per-user scope).
/// Transparently imports the plaintext key file left behind by Phonexis V1.
/// </summary>
public class ApiKeyStore : IApiKeyStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Phonexis.V2.KeyStore");
    private readonly ILogService _log;

    public ApiKeyStore(ILogService log)
    {
        _log = log;
    }

    public List<string> Load()
    {
        try
        {
            if (File.Exists(AppPaths.ApiKeysFile))
            {
                var encrypted = File.ReadAllBytes(AppPaths.ApiKeysFile);
                var raw = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                var keys = JsonSerializer.Deserialize<List<string>>(Encoding.UTF8.GetString(raw)) ?? new();
                return Sanitize(keys);
            }

            // One-time migration from V1 plaintext storage.
            if (File.Exists(AppPaths.LegacyApiKeysFile))
            {
                var legacy = Sanitize(File.ReadAllText(AppPaths.LegacyApiKeysFile).Split(',').ToList());
                if (legacy.Count > 0 && Save(legacy))
                {
                    _log.Info($"Imported {legacy.Count} API key(s) from Phonexis V1");
                    return legacy;
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error("Failed to load API keys", ex);
        }
        return new List<string>();
    }

    public bool Save(List<string> keys)
    {
        try
        {
            var json = JsonSerializer.Serialize(Sanitize(keys));
            var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(AppPaths.ApiKeysFile, encrypted);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Failed to save API keys", ex);
            return false;
        }
    }

    private static List<string> Sanitize(IEnumerable<string> keys) =>
        keys.Select(k => k.Trim())
            .Where(k => k.Length > 0)
            .Distinct()
            .ToList();
}
