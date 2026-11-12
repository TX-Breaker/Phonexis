using System.Net.Http;
using Phonexis.Models;
using Phonexis.Services;
using Phonexis.Services.Search;
using YoutubeExplode.Common;
using YoutubeExplode.Search;

namespace Phonexis.Tests;

public class SearchChainTests
{
    private sealed class FakeProvider : ISearchProvider
    {
        private readonly Func<List<SearchResult>> _answer;
        public int Calls { get; private set; }
        public SearchEngine Engine { get; }

        public FakeProvider(SearchEngine engine, Func<List<SearchResult>> answer)
        {
            Engine = engine;
            _answer = answer;
        }

        public Task<List<SearchResult>> SearchAsync(string query, int maxResults, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(_answer());
        }
    }

    private static List<SearchResult> One(string id) => new() { new SearchResult { VideoId = id } };
    private static List<SearchResult> Throw(Exception ex) => throw ex;

    [Fact]
    public void Modes_map_to_the_right_order()
    {
        Assert.Equal(new[] { SearchEngine.Free, SearchEngine.Api }, SearchEngineModes.Order(SearchEngineMode.FreeThenApi));
        Assert.Equal(new[] { SearchEngine.Api, SearchEngine.Free }, SearchEngineModes.Order(SearchEngineMode.ApiThenFree));
        Assert.Equal(new[] { SearchEngine.Free }, SearchEngineModes.Order(SearchEngineMode.FreeOnly));
        Assert.Equal(new[] { SearchEngine.Api }, SearchEngineModes.Order(SearchEngineMode.ApiOnly));
        Assert.Equal(SearchEngineMode.ApiThenFree, SearchEngineModes.Preferring(SearchEngine.Api));
        Assert.Equal(SearchEngineMode.FreeThenApi, SearchEngineModes.Preferring(SearchEngine.Free));
    }

    [Fact]
    public void Every_mode_and_engine_has_a_label_in_every_language()
    {
        foreach (var lang in new[] { "en", "it", "de", "fr", "es" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Phonexis", "Resources", "i18n", lang + ".json");
            var keys = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)).RootElement
                .EnumerateObject().Select(p => p.Name).ToHashSet();
            foreach (var mode in SearchEngineModes.All)
                Assert.Contains(SearchEngineModes.I18nKey(mode), keys);
            foreach (var engine in Enum.GetValues<SearchEngine>())
                Assert.Contains(SearchEngineModes.I18nKey(engine), keys);
        }
    }

    [Fact]
    public async Task First_engine_answers_and_second_is_not_called()
    {
        var free = new FakeProvider(SearchEngine.Free, () => One("free"));
        var api = new FakeProvider(SearchEngine.Api, () => One("api"));
        var chain = new SearchChain(new[] { free, api });

        var outcome = await chain.RunAsync("q", 5, SearchEngineModes.Order(SearchEngineMode.FreeThenApi), default);

        Assert.Equal(SearchEngine.Free, outcome.Engine);
        Assert.Empty(outcome.Failures);
        Assert.Equal(0, api.Calls);
    }

    [Fact]
    public async Task Failing_free_search_falls_back_to_the_api()
    {
        var free = new FakeProvider(SearchEngine.Free, () => Throw(new TimeoutException()));
        var api = new FakeProvider(SearchEngine.Api, () => One("api"));
        var chain = new SearchChain(new[] { free, api });

        var outcome = await chain.RunAsync("q", 5, SearchEngineModes.Order(SearchEngineMode.FreeThenApi), default);

        Assert.Equal(SearchEngine.Api, outcome.Engine);
        Assert.Equal("api", outcome.Results.Single().VideoId);
        Assert.Equal(SearchEngine.Free, outcome.Failures.Single().Engine);
    }

    [Fact]
    public async Task Exhausted_api_falls_back_to_free_search()
    {
        var free = new FakeProvider(SearchEngine.Free, () => One("free"));
        var api = new FakeProvider(SearchEngine.Api, () => Throw(new ApiQuotaException("quota")));
        var chain = new SearchChain(new[] { free, api });

        var outcome = await chain.RunAsync("q", 5, SearchEngineModes.Order(SearchEngineMode.ApiThenFree), default);

        Assert.Equal(SearchEngine.Free, outcome.Engine);
        Assert.IsType<ApiQuotaException>(outcome.Failures.Single().Error);
    }

    [Fact]
    public async Task Empty_answer_is_not_a_failure()
    {
        var free = new FakeProvider(SearchEngine.Free, () => new List<SearchResult>());
        var api = new FakeProvider(SearchEngine.Api, () => One("api"));
        var chain = new SearchChain(new[] { free, api });

        var outcome = await chain.RunAsync("q", 5, SearchEngineModes.Order(SearchEngineMode.FreeThenApi), default);

        Assert.Equal(SearchEngine.Free, outcome.Engine);
        Assert.Empty(outcome.Results);
        Assert.Equal(0, api.Calls); // no quota burnt on songs YouTube does not have
    }

    [Fact]
    public async Task Single_engine_mode_never_touches_the_other_engine()
    {
        var free = new FakeProvider(SearchEngine.Free, () => Throw(new HttpRequestException("down")));
        var api = new FakeProvider(SearchEngine.Api, () => One("api"));
        var chain = new SearchChain(new[] { free, api });

        var ex = await Assert.ThrowsAsync<SearchFailedException>(() =>
            chain.RunAsync("q", 5, SearchEngineModes.Order(SearchEngineMode.FreeOnly), default));

        Assert.Equal(SearchEngine.Free, ex.Failures.Single().Engine);
        Assert.Equal(0, api.Calls);
    }

    [Fact]
    public async Task All_engines_failing_reports_each_failure()
    {
        var free = new FakeProvider(SearchEngine.Free, () => Throw(new TimeoutException()));
        var api = new FakeProvider(SearchEngine.Api, () => Throw(new ApiQuotaException("none")));
        var chain = new SearchChain(new[] { free, api });

        var ex = await Assert.ThrowsAsync<SearchFailedException>(() =>
            chain.RunAsync("q", 5, SearchEngineModes.Order(SearchEngineMode.FreeThenApi), default));

        Assert.Equal(new[] { SearchEngine.Free, SearchEngine.Api }, ex.Failures.Select(f => f.Engine));
    }

    [Fact]
    public async Task Cancellation_is_not_treated_as_a_failure()
    {
        using var cts = new CancellationTokenSource();
        var free = new FakeProvider(SearchEngine.Free, () => { cts.Cancel(); throw new OperationCanceledException(cts.Token); });
        var api = new FakeProvider(SearchEngine.Api, () => One("api"));
        var chain = new SearchChain(new[] { free, api });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            chain.RunAsync("q", 5, SearchEngineModes.Order(SearchEngineMode.FreeThenApi), cts.Token));
        Assert.Equal(0, api.Calls);
    }

    [Fact]
    public async Task Repeatedly_failing_engine_is_tried_last_until_the_cooldown_ends()
    {
        var now = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);
        var freeWorks = false;
        var free = new FakeProvider(SearchEngine.Free, () => freeWorks ? One("free") : Throw(new TimeoutException()));
        var api = new FakeProvider(SearchEngine.Api, () => One("api"));
        var chain = new SearchChain(new[] { free, api }, clock: () => now);
        var order = SearchEngineModes.Order(SearchEngineMode.FreeThenApi);

        for (var i = 0; i < SearchChain.FailureThreshold; i++)
            await chain.RunAsync("q", 5, order, default);
        Assert.True(chain.IsCoolingDown(SearchEngine.Free));

        // Cooling down: the API goes first, free search is not even called.
        var callsBefore = free.Calls;
        var outcome = await chain.RunAsync("q", 5, order, default);
        Assert.Equal(SearchEngine.Api, outcome.Engine);
        Assert.Equal(callsBefore, free.Calls);

        // After the cooldown free search gets its turn again and a success clears the record.
        now += SearchChain.Cooldown + TimeSpan.FromSeconds(1);
        freeWorks = true;
        outcome = await chain.RunAsync("q", 5, order, default);
        Assert.Equal(SearchEngine.Free, outcome.Engine);
        Assert.False(chain.IsCoolingDown(SearchEngine.Free));
    }

    [Fact]
    public async Task Cooling_engine_is_still_tried_when_it_is_the_only_one()
    {
        var now = DateTime.UtcNow;
        var free = new FakeProvider(SearchEngine.Free, () => Throw(new TimeoutException()));
        var chain = new SearchChain(new[] { free }, clock: () => now);
        var order = SearchEngineModes.Order(SearchEngineMode.FreeOnly);

        for (var i = 0; i < SearchChain.FailureThreshold + 1; i++)
            await Assert.ThrowsAsync<SearchFailedException>(() => chain.RunAsync("q", 5, order, default));

        Assert.Equal(SearchChain.FailureThreshold + 1, free.Calls);
    }

    [Fact]
    public void Free_results_map_to_search_results()
    {
        var video = new VideoSearchResult(
            new YoutubeExplode.Videos.VideoId("dQw4w9WgXcQ"),
            "Rick Astley - Never Gonna Give You Up",
            new Author(new YoutubeExplode.Channels.ChannelId("UCuAXFkgsw1L7xaCfnd5JJOw"), "Rick Astley"),
            TimeSpan.FromSeconds(213),
            new[] { new Thumbnail("https://i.ytimg.com/vi/dQw4w9WgXcQ/hq720.jpg", new Resolution(1280, 720)),
                    new Thumbnail("https://i.ytimg.com/vi/dQw4w9WgXcQ/default.jpg", new Resolution(120, 90)) });

        var r = FreeSearchProvider.Map(video);

        Assert.Equal("dQw4w9WgXcQ", r.VideoId);
        Assert.Equal("Rick Astley", r.ChannelTitle);
        Assert.Equal(213, r.DurationSeconds);
        Assert.EndsWith("hq720.jpg", r.ThumbnailUrl);
        Assert.Equal(ResultSource.Free, r.Source);
    }

    /// <summary>Hits the real YouTube. Opt in with PHONEXIS_LIVE_TESTS=1 so offline builds stay green.</summary>
    [Fact]
    public async Task Live_free_search_returns_results_when_enabled()
    {
        if (Environment.GetEnvironmentVariable("PHONEXIS_LIVE_TESTS") != "1") return;

        var provider = new FreeSearchProvider(new LogService());
        var results = await provider.SearchAsync("Bicep Glue", 5, default);

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(11, r.VideoId.Length));
        Assert.Contains(results, r => r.ViewCount > 0 && r.PublishedAt != default);
    }
}

public class DescriptionLineTests
{
    [Fact]
    public void Full_descriptions_collapse_to_one_short_line()
    {
        var r = new Phonexis.Models.SearchResult { Description = "Listen now:\nhttps://x\n\n  Follow us\r\nTwitter" };
        Assert.Equal("Listen now: https://x Follow us Twitter", r.DescriptionLine);
        Assert.Equal(241, Phonexis.Models.SearchResult.OneLine(new string('a', 500)).Length); // 240 + ellipsis
        Assert.Equal(string.Empty, Phonexis.Models.SearchResult.OneLine("  \n "));
    }
}
