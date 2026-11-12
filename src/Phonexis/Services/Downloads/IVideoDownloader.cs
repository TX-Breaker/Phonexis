using Phonexis.Models;

namespace Phonexis.Services.Downloads;

/// <summary>
/// Direct: Phonexis decides file name and folder before the download starts.
/// Handoff: Phonexis passes the links to another app, then "Rename &amp; archive" fixes names afterwards.
/// </summary>
public enum DownloaderLevel
{
    Direct,
    Handoff
}

/// <summary>One video to fetch, already named after its track.</summary>
public sealed record DownloadJob(SongItem Song, string Url, string VideoId, string TargetFolder, string BaseName);

/// <summary>Receives per-job updates; implementations marshal to the UI thread.</summary>
public interface IDownloadReporter
{
    void Report(DownloadJob job, DownloadState state, double progress = 0, string? path = null, string? error = null);
}

public interface IVideoDownloader
{
    string Id { get; }
    string DisplayName { get; }
    DownloaderLevel Level { get; }

    /// <summary>"free", "freemium" or "paid" — shown next to the name.</summary>
    string Cost { get; }

    /// <summary>Path of the tool when found, or null. Built-in engines return their own marker.</summary>
    string? Locate(DownloadOptions options);

    /// <summary>One line on what this downloader does with the links.</summary>
    string HowItWorksKey { get; }

    Task RunAsync(IReadOnlyList<DownloadJob> jobs, DownloadOptions options, IDownloadReporter reporter, CancellationToken ct);

    /// <summary>Where this app saves videos, read from its own settings when possible (used by Rename &amp; archive).</summary>
    string? SuggestedDownloadFolder() => null;
}
