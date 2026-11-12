using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Phonexis.Models;

namespace Phonexis.Services.Downloads;

/// <summary>yt-dlp: runs one process per video with an exact output path; progress parsed from stdout.</summary>
public sealed partial class YtDlpDownloader : IVideoDownloader
{
    private readonly ILogService _log;

    public YtDlpDownloader(ILogService log) => _log = log;

    public string Id => "ytdlp";
    public string DisplayName => "yt-dlp";
    public DownloaderLevel Level => DownloaderLevel.Direct;
    public string Cost => "free";
    public string HowItWorksKey => "DlHowYtDlp";

    public string? Locate(DownloadOptions options) => ToolLocator.FindExecutable("yt-dlp.exe", options.YtDlpPath);

    [GeneratedRegex(@"\[download\]\s+(\d+(?:\.\d+)?)%")]
    private static partial Regex PercentRegex();

    public async Task RunAsync(IReadOnlyList<DownloadJob> jobs, DownloadOptions options, IDownloadReporter reporter, CancellationToken ct)
    {
        var exe = Locate(options) ?? throw new FileNotFoundException("yt-dlp.exe");
        var ffmpeg = ToolLocator.FindFfmpeg(options.FfmpegPath);
        using var gate = new SemaphoreSlim(Math.Clamp(options.ParallelDownloads, 1, 4));

        await Task.WhenAll(jobs.Select(async job =>
        {
            await gate.WaitAsync(ct);
            try { await RunOneAsync(exe, ffmpeg, job, options, reporter, ct); }
            finally { gate.Release(); }
        }));
    }

    private async Task RunOneAsync(string exe, string? ffmpeg, DownloadJob job, DownloadOptions options,
                                   IDownloadReporter reporter, CancellationToken ct)
    {
        Directory.CreateDirectory(job.TargetFolder);
        var target = VideoNaming.ResolveTarget(job.TargetFolder, job.BaseName, ".mp4", options.ExistingFiles);
        if (target == null)
        {
            reporter.Report(job, DownloadState.Skipped, 1, Path.Combine(job.TargetFolder, job.BaseName + ".mp4"));
            return;
        }

        var q = Math.Max(144, options.MaxQuality);
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };
        foreach (var arg in new[]
                 {
                     "--no-playlist", "--newline", "--force-overwrites", "--socket-timeout", "30",
                     "-f", $"bv*[height<={q}][ext=mp4]+ba[ext=m4a]/b[height<={q}][ext=mp4]/b[height<={q}]",
                     "--merge-output-format", "mp4",
                     "-o", Path.ChangeExtension(target, null) + ".%(ext)s"
                 })
            psi.ArgumentList.Add(arg);
        if (ffmpeg != null)
        {
            psi.ArgumentList.Add("--ffmpeg-location");
            psi.ArgumentList.Add(ffmpeg);
        }
        psi.ArgumentList.Add(job.Url);

        var attempts = 1 + Math.Max(0, options.Retries);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            using var guard = new StallGuard(TimeSpan.FromSeconds(Math.Max(5, options.StallSeconds)), ct);
            reporter.Report(job, DownloadState.Downloading, 0, null, attempt > 1 ? $"RETRY:{attempt}" : null);
            try
            {
                using var process = Process.Start(psi)!;
                // The guard kills a yt-dlp that stops printing progress, as does the user's Stop.
                await using var _ = guard.Token.Register(() => { try { process.Kill(true); } catch { /* already gone */ } });
                var errors = new StringBuilder();
                var stderrTask = Task.Run(async () =>
                {
                    while (await process.StandardError.ReadLineAsync() is { } line) errors.AppendLine(line);
                });

                while (await process.StandardOutput.ReadLineAsync() is { } line)
                {
                    guard.Touch();
                    var m = PercentRegex().Match(line);
                    if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
                        reporter.Report(job, DownloadState.Downloading, Math.Clamp(pct / 100, 0, 1));
                }
                await process.WaitForExitAsync(guard.Token);
                await stderrTask;

                if (process.ExitCode == 0 && File.Exists(target))
                {
                    if (options.MatchAudioDates) FileDates.CopyFromAudio(job.Song.SourceAudioPath, target);
                    reporter.Report(job, DownloadState.Done, 1, target);
                    return;
                }
                if (guard.Stalled && attempt < attempts) continue;
                reporter.Report(job, DownloadState.Failed, 0, null,
                    guard.Stalled ? "STALLED" : LastLine(errors.ToString()) ?? $"exit {process.ExitCode}");
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                reporter.Report(job, DownloadState.None);
                return;
            }
            catch (OperationCanceledException) when (guard.Stalled && attempt < attempts)
            {
                _log.Info($"yt-dlp stalled on {job.VideoId} (attempt {attempt}), retrying");
            }
            catch (Exception ex)
            {
                _log.Error($"yt-dlp failed for {job.VideoId}", ex);
                reporter.Report(job, DownloadState.Failed, 0, null, guard.Stalled ? "STALLED" : ex.Message);
                return;
            }
        }
    }

    private static string? LastLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).LastOrDefault(l => l.Length > 0);
}

/// <summary>
/// JDownloader 2: one .crawljob per video in the "folderwatch" folder (filename only applies to single-link jobs).
/// Folder watch must be enabled in JDownloader's advanced settings.
/// </summary>
public sealed class JDownloaderDownloader : IVideoDownloader
{
    public string Id => "jdownloader";
    public string DisplayName => "JDownloader 2";
    public DownloaderLevel Level => DownloaderLevel.Direct;
    public string Cost => "free";
    public string HowItWorksKey => "DlHowJDownloader";

    public string? Locate(DownloadOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.JDownloaderFolderWatch) && Directory.Exists(options.JDownloaderFolderWatch))
            return options.JDownloaderFolderWatch;

        var candidates = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JDownloader 2.0"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "JDownloader"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "JDownloader")
        };
        var installed = ToolLocator.FindInstalledProgram("JDownloader");
        if (installed != null) candidates.Insert(0, File.Exists(installed) ? Path.GetDirectoryName(installed)! : installed);

        foreach (var root in candidates.Where(Directory.Exists))
        {
            var watch = Path.Combine(root, "folderwatch");
            if (Directory.Exists(watch)) return watch;
            if (File.Exists(Path.Combine(root, "JDownloader2.exe")) || File.Exists(Path.Combine(root, "JDownloader.jar")))
                return watch; // created on first use
        }
        return null;
    }

    /// <summary>
    /// False when JDownloader's "extensions" folder exists but holds no Folder Watch module (the .crawljob files would
    /// then never be picked up); null when it cannot be told.
    /// </summary>
    public static bool? FolderWatchInstalled(string watchFolder)
    {
        var extensions = Path.Combine(Path.GetDirectoryName(watchFolder.TrimEnd('\\', '/')) ?? string.Empty, "extensions");
        if (!Directory.Exists(extensions)) return null;
        return Directory.EnumerateFiles(extensions)
            .Any(f => Path.GetFileName(f).Contains("folderwatch", StringComparison.OrdinalIgnoreCase));
    }

    public Task RunAsync(IReadOnlyList<DownloadJob> jobs, DownloadOptions options, IDownloadReporter reporter, CancellationToken ct)
    {
        var watch = Locate(options) ?? throw new DirectoryNotFoundException("JDownloader folderwatch");
        Directory.CreateDirectory(watch);
        var package = "Phonexis " + DateTime.Now.ToString("yyyy-MM-dd HH.mm");

        foreach (var job in jobs)
        {
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(job.TargetFolder);
            var target = VideoNaming.ResolveTarget(job.TargetFolder, job.BaseName, ".mp4", options.ExistingFiles);
            if (target == null)
            {
                reporter.Report(job, DownloadState.Skipped, 1, Path.Combine(job.TargetFolder, job.BaseName + ".mp4"));
                continue;
            }
            var lines = new[]
            {
                "text=" + job.Url,
                "downloadFolder=" + job.TargetFolder,
                "filename=" + Path.GetFileName(target),
                "packageName=" + package,
                "enabled=TRUE",
                "autoStart=TRUE",
                "forcedStart=Default",
                "autoConfirm=TRUE",
                "overwritePackagizerEnabled=true"
            };
            var file = Path.Combine(watch, $"phonexis-{job.VideoId}-{DateTime.Now:HHmmssfff}.crawljob");
            File.WriteAllLines(file, lines, new UTF8Encoding(false));
            reporter.Report(job, DownloadState.Sent, 1, target);
        }
        return Task.CompletedTask;
    }
}
