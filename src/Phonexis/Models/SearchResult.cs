using System.Text.Json.Serialization;

namespace Phonexis.Models;

/// <summary>Where a search result came from.</summary>
public enum ResultSource
{
    Api,
    Free,
    Cache,
    Dummy
}

/// <summary>A single YouTube video returned by a search.</summary>
public class SearchResult
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ChannelTitle { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; set; }

    /// <summary>View count from videos.list enrichment. Long: popular videos exceed int range.</summary>
    public long ViewCount { get; set; }

    /// <summary>Duration in seconds from videos.list enrichment; null when unknown.</summary>
    public int? DurationSeconds { get; set; }

    [JsonIgnore]
    public ResultSource Source { get; set; } = ResultSource.Api;

    /// <summary>Set by ResultRanker: the preselected result for this song.</summary>
    [JsonIgnore]
    public bool IsRecommended { get; set; }

    [JsonIgnore]
    public int MatchScore { get; set; }

    [JsonIgnore]
    public string MatchReasons { get; set; } = string.Empty;

    [JsonIgnore]
    public string VideoUrl => $"https://www.youtube.com/watch?v={VideoId}";

    /// <summary>
    /// Description on one line. Full descriptions (free search, older cache entries) carry line breaks
    /// that would grow the card past the fixed result scale.
    /// </summary>
    [JsonIgnore]
    public string DescriptionLine => OneLine(Description);

    /// <summary>More text than the collapsed line shows: the card offers "Show full description".</summary>
    [JsonIgnore]
    public bool HasLongDescription => Description.Contains('\n') || Description.Trim().Length > 110;

    public static string OneLine(string text, int max = 240)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var line = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return line.Length <= max ? line : line[..max].TrimEnd() + "…";
    }

    [JsonIgnore]
    public string DurationText
    {
        get
        {
            if (DurationSeconds is not > 0) return string.Empty;
            var t = TimeSpan.FromSeconds(DurationSeconds.Value);
            return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
        }
    }

    [JsonIgnore]
    public string ViewCountText => ViewCount switch
    {
        >= 1_000_000_000 => $"{ViewCount / 1_000_000_000.0:0.#}B",
        >= 1_000_000 => $"{ViewCount / 1_000_000.0:0.#}M",
        >= 1_000 => $"{ViewCount / 1_000.0:0.#}K",
        _ => ViewCount.ToString()
    };

    [JsonIgnore]
    public string PublishedText => PublishedAt == default ? string.Empty : PublishedAt.LocalDateTime.ToString("dd/MM/yyyy");

    [JsonIgnore]
    public string SourceText => Source switch
    {
        ResultSource.Cache => Services.I18n.Instance["SourceCache"],
        ResultSource.Dummy => Services.I18n.Instance["SourceTest"],
        ResultSource.Free => Services.I18n.Instance["SourceFree"],
        _ => Services.I18n.Instance["SourceApi"]
    };
}
