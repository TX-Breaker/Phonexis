using Phonexis.Models;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Search;

namespace Phonexis.Services.Search;

/// <summary>
/// Key-less search through YouTube's public search page (YoutubeExplode). Search results carry
/// title, channel, duration and thumbnail; views and upload date come from a best-effort pass over
/// each video's watch page, so the ranker and the filters get the same fields as the API.
/// </summary>
public class FreeSearchProvider : ISearchProvider
{
    private static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan EnrichTimeout = TimeSpan.FromSeconds(10);
    private const int EnrichParallelism = 5;

    private readonly ILogService _log;
    private readonly YoutubeClient _client = new();

    public FreeSearchProvider(ILogService log) => _log = log;

    public SearchEngine Engine => SearchEngine.Free;

    public async Task<List<SearchResult>> SearchAsync(string query, int maxResults, CancellationToken ct)
    {
        var results = new List<SearchResult>();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(SearchTimeout);
        try
        {
            await foreach (var video in _client.Search.GetVideosAsync(query, timeout.Token))
            {
                results.Add(Map(video));
                if (results.Count >= maxResults) break;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Free search gave no answer within {SearchTimeout.TotalSeconds:0} s.");
        }

        await EnrichAsync(results, ct);
        _log.Info($"Free search '{query}': {results.Count} results");
        return results;
    }

    public static SearchResult Map(VideoSearchResult video) => new()
    {
        VideoId = video.Id.Value,
        Title = video.Title,
        ChannelTitle = video.Author.ChannelTitle,
        DurationSeconds = video.Duration is { } d ? (int)d.TotalSeconds : null,
        ThumbnailUrl = video.Thumbnails.TryGetWithHighestResolution()?.Url
                       ?? $"https://i.ytimg.com/vi/{video.Id.Value}/hqdefault.jpg",
        Source = ResultSource.Free
    };

    /// <summary>Fills views, upload date and description. Failures leave the fields unknown.</summary>
    private async Task EnrichAsync(List<SearchResult> results, CancellationToken ct)
    {
        if (results.Count == 0) return;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(EnrichTimeout);
        try
        {
            await Parallel.ForEachAsync(results,
                new ParallelOptions { MaxDegreeOfParallelism = EnrichParallelism, CancellationToken = timeout.Token },
                async (result, token) =>
                {
                    try
                    {
                        var video = await _client.Videos.GetAsync(result.VideoId, token);
                        result.ViewCount = video.Engagement.ViewCount;
                        result.PublishedAt = video.UploadDate;
                        result.Description = video.Description; // full text: the card shows one line, expandable
                        result.DurationSeconds ??= video.Duration is { } d ? (int)d.TotalSeconds : null;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _log.Warn($"Free search details failed for {result.VideoId}: {ex.Message}");
                    }
                });
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log.Warn("Free search details timed out; showing results without views/date for the rest");
        }
    }
}
