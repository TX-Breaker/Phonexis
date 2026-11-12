using System.IO;
using Phonexis.Models;
using YoutubeExplode;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos.Streams;

namespace Phonexis.Services.Downloads;

/// <summary>
/// In-app downloader built on YoutubeExplode. Up to ~360p YouTube serves muxed MP4; above that
/// video and audio come separately and FFmpeg joins them (no re-encode when both are MP4/M4A).
/// </summary>
public sealed class BuiltinDownloader : IVideoDownloader
{
    private readonly ILogService _log;

    public BuiltinDownloader(ILogService log) => _log = log;

    public string Id => "builtin";
    public string DisplayName => "Phonexis";
    public DownloaderLevel Level => DownloaderLevel.Direct;
    public string Cost => "free";
    public string HowItWorksKey => "DlHowBuiltin";

    public string? Locate(DownloadOptions options) => "built-in";

    public async Task RunAsync(IReadOnlyList<DownloadJob> jobs, DownloadOptions options, IDownloadReporter reporter, CancellationToken ct)
    {
        var ffmpeg = ToolLocator.FindFfmpeg(options.FfmpegPath);
        using var youtube = new YoutubeClient();
        using var gate = new SemaphoreSlim(Math.Clamp(options.ParallelDownloads, 1, 4));

        var tasks = jobs.Select(async job =>
        {
            await gate.WaitAsync(ct);
            try
            {
                await DownloadOneAsync(youtube, job, options, ffmpeg, reporter, ct);
            }
            finally
            {
                gate.Release();
            }
        });
        await Task.WhenAll(tasks);
    }

    private async Task DownloadOneAsync(YoutubeClient youtube, DownloadJob job, DownloadOptions options,
                                        string? ffmpeg, IDownloadReporter reporter, CancellationToken ct)
    {
        Directory.CreateDirectory(job.TargetFolder);
        var target = VideoNaming.ResolveTarget(job.TargetFolder, job.BaseName, ".mp4", options.ExistingFiles);
        if (target == null)
        {
            reporter.Report(job, DownloadState.Skipped, 1, Path.Combine(job.TargetFolder, job.BaseName + ".mp4"));
            return;
        }

        var attempts = 1 + Math.Max(0, options.Retries);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var partial = target + ".part";
            using var guard = new StallGuard(TimeSpan.FromSeconds(Math.Max(5, options.StallSeconds)), ct);
            try
            {
                reporter.Report(job, DownloadState.Downloading, 0, null, attempt > 1 ? $"RETRY:{attempt}" : null);
                await DownloadStreamsAsync(youtube, job, options, ffmpeg, partial, guard, reporter);

                if (File.Exists(target)) File.Delete(target); // Overwrite policy already approved this.
                File.Move(partial, target);
                if (options.MatchAudioDates) FileDates.CopyFromAudio(job.Song.SourceAudioPath, target);
                reporter.Report(job, DownloadState.Done, 1, target);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                TryDelete(partial);
                reporter.Report(job, DownloadState.None);
                return;
            }
            catch (Exception ex) when (attempt < attempts && IsTransient(ex, guard))
            {
                TryDelete(partial);
                _log.Info($"Download of {job.VideoId} {(guard.Stalled ? "stalled" : "failed")} (attempt {attempt}), retrying: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(3 * attempt), ct);
            }
            catch (Exception ex)
            {
                TryDelete(partial);
                _log.Error($"Built-in download failed for {job.VideoId}", ex);
                reporter.Report(job, DownloadState.Failed, 0, null, guard.Stalled ? "STALLED" : ex.Message);
                return;
            }
        }
    }

    /// <summary>Stalls, timeouts and network errors are worth another attempt; a missing stream is not.</summary>
    private static bool IsTransient(Exception ex, StallGuard guard) =>
        guard.Stalled || ex is System.Net.Http.HttpRequestException or IOException or TimeoutException
                      || (ex is OperationCanceledException && !guard.Token.IsCancellationRequested);

    private static async Task DownloadStreamsAsync(YoutubeClient youtube, DownloadJob job, DownloadOptions options,
                                                   string? ffmpeg, string partial, StallGuard guard, IDownloadReporter reporter)
    {
        var ct = guard.Token;
        var manifest = await youtube.Videos.Streams.GetManifestAsync(job.VideoId, ct);
        guard.Touch();
        var progress = new Progress<double>(p =>
        {
            guard.Touch();
            reporter.Report(job, DownloadState.Downloading, p);
        });

        var maxHeight = Math.Max(144, options.MaxQuality);
        var video = manifest.GetVideoOnlyStreams()
            .Where(s => s.Container == Container.Mp4 && s.VideoQuality.MaxHeight <= maxHeight)
            .OrderByDescending(s => s.VideoQuality.MaxHeight)
            .ThenByDescending(s => s.Bitrate)
            .FirstOrDefault();
        var audio = manifest.GetAudioOnlyStreams()
            .Where(s => s.Container == Container.Mp4)
            .OrderByDescending(s => s.Bitrate)
            .FirstOrDefault();
        var muxed = manifest.GetMuxedStreams()
            .Where(s => s.Container == Container.Mp4 && s.VideoQuality.MaxHeight <= maxHeight)
            .OrderByDescending(s => s.VideoQuality.MaxHeight)
            .FirstOrDefault();

        var separateIsBetter = video != null && audio != null &&
                               (muxed == null || video.VideoQuality.MaxHeight > muxed.VideoQuality.MaxHeight);

        if (ffmpeg != null && separateIsBetter)
        {
            var request = new ConversionRequestBuilder(partial)
                .SetFFmpegPath(ffmpeg)
                .SetContainer(Container.Mp4)
                .SetPreset(ConversionPreset.UltraFast)
                .Build();
            await youtube.Videos.DownloadAsync(new IStreamInfo[] { video!, audio! }, request, progress, ct);
        }
        else if (muxed != null)
        {
            await youtube.Videos.Streams.DownloadAsync(muxed, partial, progress, ct);
        }
        else
        {
            throw new InvalidOperationException(ffmpeg == null ? "FFMPEG_MISSING" : "NO_STREAM");
        }
    }

    private static void TryDelete(string? path)
    {
        try
        {
            if (path != null && File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // Left behind; harmless .part file.
        }
    }
}
