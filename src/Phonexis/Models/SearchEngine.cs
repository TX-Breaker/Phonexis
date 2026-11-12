namespace Phonexis.Models;

/// <summary>A way of searching YouTube.</summary>
public enum SearchEngine
{
    /// <summary>Public YouTube search page via YoutubeExplode: no key, no quota, may break when YouTube changes.</summary>
    Free,

    /// <summary>YouTube Data API v3: official and stable, needs a key, ~100 searches per key per day.</summary>
    Api
}

/// <summary>Which engines to use and in what order. The second engine runs only when the first fails.</summary>
public enum SearchEngineMode
{
    FreeThenApi,
    ApiThenFree,
    FreeOnly,
    ApiOnly
}

public static class SearchEngineModes
{
    public static readonly SearchEngineMode[] All =
    {
        SearchEngineMode.FreeThenApi, SearchEngineMode.ApiThenFree, SearchEngineMode.FreeOnly, SearchEngineMode.ApiOnly
    };

    public static IReadOnlyList<SearchEngine> Order(SearchEngineMode mode) => mode switch
    {
        SearchEngineMode.ApiThenFree => new[] { SearchEngine.Api, SearchEngine.Free },
        SearchEngineMode.FreeOnly => new[] { SearchEngine.Free },
        SearchEngineMode.ApiOnly => new[] { SearchEngine.Api },
        _ => new[] { SearchEngine.Free, SearchEngine.Api }
    };

    /// <summary>The mode that puts <paramref name="engine"/> first and keeps the other as backup.</summary>
    public static SearchEngineMode Preferring(SearchEngine engine) =>
        engine == SearchEngine.Api ? SearchEngineMode.ApiThenFree : SearchEngineMode.FreeThenApi;

    public static string I18nKey(SearchEngineMode mode) => "Mode" + mode;

    public static string I18nKey(SearchEngine engine) => "Engine" + engine;
}
