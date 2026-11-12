using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Phonexis.Models;

namespace Phonexis.Services;

public interface ICacheService
{
    void Initialize();

    List<SearchResult>? GetCachedResults(string query);
    void StoreResults(string query, List<SearchResult> results);
    void RemoveQuery(string query);
    int GetCachedQueryCount();
    bool ClearSearchCache();

    // YouTube quota accounting (units, not calls). Keys are stored hashed.
    int GetUnitsUsed(string apiKey, DateOnly day);
    void AddUnits(string apiKey, DateOnly day, int units);
    bool IsKeyExhausted(string apiKey, DateOnly day);
    void MarkKeyExhausted(string apiKey, DateOnly day);
    int GetTotalUnitsToday();

    event EventHandler? UsageChanged;
}

public class CacheService : ICacheService
{
    private readonly ILogService _log;
    private readonly string _connectionString;

    public event EventHandler? UsageChanged;

    public CacheService(ILogService log)
    {
        _log = log;
        _connectionString = $"Data Source={AppPaths.DatabaseFile}";
    }

    public void Initialize()
    {
        using var conn = Open();
        Exec(conn, """
            CREATE TABLE IF NOT EXISTS search_cache (
                query TEXT PRIMARY KEY,
                results TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS api_usage (
                key_hash TEXT NOT NULL,
                usage_date TEXT NOT NULL,
                units INTEGER NOT NULL DEFAULT 0,
                exhausted INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (key_hash, usage_date)
            );
            CREATE INDEX IF NOT EXISTS idx_api_usage_date ON api_usage (usage_date);
            """);
    }

    // ---- Search cache ----

    public List<SearchResult>? GetCachedResults(string query)
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT results FROM search_cache WHERE query = $q";
            cmd.Parameters.AddWithValue("$q", Normalize(query));
            if (cmd.ExecuteScalar() is string json)
            {
                var results = JsonSerializer.Deserialize<List<SearchResult>>(json);
                if (results != null)
                {
                    foreach (var r in results) r.Source = ResultSource.Cache;
                    return results;
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Cache read failed for '{query}'", ex);
        }
        return null;
    }

    public void StoreResults(string query, List<SearchResult> results)
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO search_cache (query, results, created_at)
                VALUES ($q, $r, $t)
                ON CONFLICT(query) DO UPDATE SET results = $r, created_at = $t
                """;
            cmd.Parameters.AddWithValue("$q", Normalize(query));
            cmd.Parameters.AddWithValue("$r", JsonSerializer.Serialize(results));
            cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _log.Error($"Cache write failed for '{query}'", ex);
        }
    }

    public void RemoveQuery(string query)
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM search_cache WHERE query = $q";
            cmd.Parameters.AddWithValue("$q", Normalize(query));
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _log.Error($"Cache delete failed for '{query}'", ex);
        }
    }

    public int GetCachedQueryCount()
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM search_cache";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
        catch
        {
            return 0;
        }
    }

    public bool ClearSearchCache()
    {
        try
        {
            using var conn = Open();
            Exec(conn, "DELETE FROM search_cache; VACUUM;");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Failed to clear search cache", ex);
            return false;
        }
    }

    // ---- Quota accounting ----

    public int GetUnitsUsed(string apiKey, DateOnly day)
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT units FROM api_usage WHERE key_hash = $k AND usage_date = $d";
            cmd.Parameters.AddWithValue("$k", Hash(apiKey));
            cmd.Parameters.AddWithValue("$d", day.ToString("yyyy-MM-dd"));
            return cmd.ExecuteScalar() is long u ? (int)u : 0;
        }
        catch
        {
            return 0;
        }
    }

    public void AddUnits(string apiKey, DateOnly day, int units)
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO api_usage (key_hash, usage_date, units, exhausted)
                VALUES ($k, $d, $u, 0)
                ON CONFLICT(key_hash, usage_date) DO UPDATE SET units = units + $u
                """;
            cmd.Parameters.AddWithValue("$k", Hash(apiKey));
            cmd.Parameters.AddWithValue("$d", day.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("$u", units);
            cmd.ExecuteNonQuery();
            UsageChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _log.Error("Failed to record API usage", ex);
        }
    }

    public bool IsKeyExhausted(string apiKey, DateOnly day)
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT exhausted FROM api_usage WHERE key_hash = $k AND usage_date = $d";
            cmd.Parameters.AddWithValue("$k", Hash(apiKey));
            cmd.Parameters.AddWithValue("$d", day.ToString("yyyy-MM-dd"));
            return cmd.ExecuteScalar() is long e && e == 1;
        }
        catch
        {
            return false;
        }
    }

    public void MarkKeyExhausted(string apiKey, DateOnly day)
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO api_usage (key_hash, usage_date, units, exhausted)
                VALUES ($k, $d, 0, 1)
                ON CONFLICT(key_hash, usage_date) DO UPDATE SET exhausted = 1
                """;
            cmd.Parameters.AddWithValue("$k", Hash(apiKey));
            cmd.Parameters.AddWithValue("$d", day.ToString("yyyy-MM-dd"));
            cmd.ExecuteNonQuery();
            UsageChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _log.Error("Failed to mark key exhausted", ex);
        }
    }

    public int GetTotalUnitsToday()
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(SUM(units), 0) FROM api_usage WHERE usage_date = $d";
            cmd.Parameters.AddWithValue("$d", DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"));
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
        catch
        {
            return 0;
        }
    }

    // ---- Helpers ----

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string Normalize(string query) => query.Trim().ToLowerInvariant();

    private static string Hash(string apiKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)))[..24];
}
