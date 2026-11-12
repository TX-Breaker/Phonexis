using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using Phonexis.Models;

namespace Phonexis.Services;

/// <summary>Thrown when every configured API key is exhausted or invalid.</summary>
public class ApiQuotaException : Exception
{
    public ApiQuotaException(string message) : base(message) { }
}

public interface IYouTubeService
{
    IReadOnlyList<string> ApiKeys { get; }
    void SetApiKeys(IEnumerable<string> keys);

    /// <summary>Cache first, then the engines of <paramref name="mode"/> in order.</summary>
    /// <exception cref="Search.SearchFailedException">Every engine failed.</exception>
    Task<Search.SearchOutcome> SearchAsync(string query, int maxResults, SearchEngineMode mode,
                                           bool bypassCache = false, CancellationToken ct = default);

    /// <summary>True when the engine failed several times in a row and is currently tried last.</summary>
    bool IsCoolingDown(SearchEngine engine);
    List<SearchResult> GenerateDummyResults(string query, int count);

    /// <summary>Estimated searches still possible today across all keys (quota units / cost per search).</summary>
    int EstimateSearchesLeftToday();
}

public class YouTubeService : IYouTubeService
{
    private const string SearchUrl = "https://www.googleapis.com/youtube/v3/search";
    private const string VideosUrl = "https://www.googleapis.com/youtube/v3/videos";

    // Real YouTube Data API v3 costs: search.list = 100 units, videos.list = 1 unit.
    private const int SearchCost = 100;
    private const int VideosCost = 1;
    private const int DailyQuotaPerKey = 10_000;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly ILogService _log;
    private readonly ICacheService _cache;
    private readonly IApiKeyStore _keyStore;
    private readonly Random _random = new();

    private List<string> _apiKeys;
    private int _currentKeyIndex;
    private readonly Search.SearchChain _chain;

    public IReadOnlyList<string> ApiKeys => _apiKeys;

    public YouTubeService(ILogService log, ICacheService cache, IApiKeyStore keyStore)
    {
        _log = log;
        _cache = cache;
        _keyStore = keyStore;
        _apiKeys = _keyStore.Load();
        _chain = new Search.SearchChain(
            new Search.ISearchProvider[] { new Search.FreeSearchProvider(log), new ApiSearchProvider(this) }, log);
        _log.Info($"YouTube service ready with {_apiKeys.Count} API key(s)");
    }

    public bool IsCoolingDown(SearchEngine engine) => _chain.IsCoolingDown(engine);

    public void SetApiKeys(IEnumerable<string> keys)
    {
        _apiKeys = keys.Select(k => k.Trim()).Where(k => k.Length > 0).Distinct().ToList();
        _currentKeyIndex = 0;
        _keyStore.Save(_apiKeys);
        _log.Info($"Configured {_apiKeys.Count} API key(s)");
    }

    public async Task<Search.SearchOutcome> SearchAsync(string query, int maxResults, SearchEngineMode mode,
                                                        bool bypassCache = false, CancellationToken ct = default)
    {
        query = query.Trim();
        if (query.Length == 0) return new Search.SearchOutcome();

        if (!bypassCache && _cache.GetCachedResults(query) is { Count: > 0 } cached)
        {
            _log.Info($"Cache hit: '{query}'");
            return new Search.SearchOutcome { Results = cached.Take(maxResults).ToList() };
        }

        var outcome = await _chain.RunAsync(query, maxResults, SearchEngineModes.Order(mode), ct);
        if (outcome.Results.Count > 0) _cache.StoreResults(query, outcome.Results);
        return outcome;
    }

    /// <summary>The API leg of the chain: rotates keys until one answers.</summary>
    private sealed class ApiSearchProvider : Search.ISearchProvider
    {
        private readonly YouTubeService _owner;
        public ApiSearchProvider(YouTubeService owner) => _owner = owner;
        public SearchEngine Engine => SearchEngine.Api;
        public Task<List<SearchResult>> SearchAsync(string query, int maxResults, CancellationToken ct) =>
            _owner.ApiSearchAsync(query, maxResults, ct);
    }

    private async Task<List<SearchResult>> ApiSearchAsync(string query, int maxResults, CancellationToken ct)
    {
        if (_apiKeys.Count == 0)
            throw new ApiQuotaException("No API keys configured.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var attempts = 0; attempts < _apiKeys.Count; attempts++)
        {
            var key = _apiKeys[_currentKeyIndex];

            if (_cache.IsKeyExhausted(key, today) ||
                _cache.GetUnitsUsed(key, today) + SearchCost + VideosCost > DailyQuotaPerKey)
            {
                _log.Warn($"Key #{_currentKeyIndex + 1} exhausted, rotating");
                RotateKey();
                continue;
            }

            try
            {
                var results = await ExecuteSearchAsync(key, query, maxResults, today, ct);
                if (results != null) return results;
                // null = key failed (quota/auth), already marked; try next.
                RotateKey();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                _log.Error($"Network error searching '{query}'", ex);
                throw; // network issue: rotating keys will not help
            }
        }

        throw new ApiQuotaException("All API keys failed or reached their daily quota.");
    }

    /// <summary>Returns results on success, null when the current key should be rotated.</summary>
    private async Task<List<SearchResult>?> ExecuteSearchAsync(string key, string query, int maxResults, DateOnly today, CancellationToken ct)
    {
        var searchUri = $"{SearchUrl}?key={Uri.EscapeDataString(key)}&part=snippet&type=video&maxResults={maxResults}&q={Uri.EscapeDataString(query)}";
        var response = await Http.GetAsync(searchUri, ct);
        _cache.AddUnits(key, today, SearchCost);

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized or HttpStatusCode.TooManyRequests)
        {
            _log.Warn($"Key #{_currentKeyIndex + 1} rejected ({(int)response.StatusCode}), marking exhausted");
            _cache.MarkKeyExhausted(key, today);
            return null;
        }
        response.EnsureSuccessStatusCode();

        var results = ParseSearchResponse(await response.Content.ReadAsStringAsync(ct));
        if (results.Count == 0) return results;

        // Enrich with duration + view count (search.list does not provide them).
        try
        {
            var ids = string.Join(',', results.Select(r => r.VideoId));
            var videosUri = $"{VideosUrl}?key={Uri.EscapeDataString(key)}&part=contentDetails,statistics&id={ids}";
            var videosResponse = await Http.GetAsync(videosUri, ct);
            _cache.AddUnits(key, today, VideosCost);
            if (videosResponse.IsSuccessStatusCode)
                MergeVideoDetails(results, await videosResponse.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Warn($"Video detail enrichment failed: {ex.Message}");
        }

        _log.Info($"API search '{query}': {results.Count} results (key #{_currentKeyIndex + 1})");
        return results;
    }

    private void RotateKey() => _currentKeyIndex = (_currentKeyIndex + 1) % Math.Max(1, _apiKeys.Count);

    private static List<SearchResult> ParseSearchResponse(string json)
    {
        var results = new List<SearchResult>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("items", out var items)) return results;

        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var id) ||
                !id.TryGetProperty("videoId", out var videoId)) continue;
            if (!item.TryGetProperty("snippet", out var snippet)) continue;

            results.Add(new SearchResult
            {
                VideoId = videoId.GetString() ?? string.Empty,
                Title = WebUtility.HtmlDecode(GetString(snippet, "title")),
                Description = GetString(snippet, "description"),
                ChannelTitle = WebUtility.HtmlDecode(GetString(snippet, "channelTitle")),
                PublishedAt = snippet.TryGetProperty("publishedAt", out var p) &&
                              DateTimeOffset.TryParse(p.GetString(), out var dt) ? dt : default,
                ThumbnailUrl = GetThumbnail(snippet),
                Source = ResultSource.Api
            });
        }
        return results;
    }

    private static void MergeVideoDetails(List<SearchResult> results, string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("items", out var items)) return;

        var byId = results.ToDictionary(r => r.VideoId);
        foreach (var item in items.EnumerateArray())
        {
            var id = GetString(item, "id");
            if (!byId.TryGetValue(id, out var result)) continue;

            if (item.TryGetProperty("contentDetails", out var cd))
            {
                var iso = GetString(cd, "duration");
                if (iso.Length > 0)
                {
                    try { result.DurationSeconds = (int)XmlConvert.ToTimeSpan(iso).TotalSeconds; }
                    catch { /* malformed duration: leave unknown */ }
                }
            }
            if (item.TryGetProperty("statistics", out var st) &&
                st.TryGetProperty("viewCount", out var vc) &&
                long.TryParse(vc.GetString(), out var views))
            {
                result.ViewCount = views;
            }
        }
    }

    private static string GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) ? p.GetString() ?? string.Empty : string.Empty;

    private static string GetThumbnail(JsonElement snippet)
    {
        if (!snippet.TryGetProperty("thumbnails", out var thumbs)) return string.Empty;
        foreach (var quality in new[] { "high", "medium", "default" })
        {
            if (thumbs.TryGetProperty(quality, out var t) && t.TryGetProperty("url", out var url))
                return url.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    public List<SearchResult> GenerateDummyResults(string query, int count)
    {
        var channels = new[] { "MetaGroove Records", "DeepCuts", "VinylVault", "BeatArchive", "GrooveLab" };
        var results = new List<SearchResult>();
        for (var i = 0; i < count; i++)
        {
            results.Add(new SearchResult
            {
                VideoId = RandomVideoId(),
                Title = $"[TEST] {query} — Result {i + 1}",
                Description = $"Dummy result for '{query}' (test mode, no API call made).",
                ChannelTitle = channels[_random.Next(channels.Length)],
                ThumbnailUrl = string.Empty,
                PublishedAt = DateTimeOffset.Now.AddDays(-_random.Next(1, 2000)),
                ViewCount = _random.Next(100, 5_000_000),
                DurationSeconds = _random.Next(120, 600),
                Source = ResultSource.Dummy
            });
        }
        return results;
    }

    public int EstimateSearchesLeftToday()
    {
        if (_apiKeys.Count == 0) return 0;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var left = 0;
        foreach (var key in _apiKeys)
        {
            if (_cache.IsKeyExhausted(key, today)) continue;
            var remaining = DailyQuotaPerKey - _cache.GetUnitsUsed(key, today);
            left += Math.Max(0, remaining / (SearchCost + VideosCost));
        }
        return left;
    }

    private string RandomVideoId()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        return new string(Enumerable.Range(0, 11).Select(_ => chars[_random.Next(chars.Length)]).ToArray());
    }
}
