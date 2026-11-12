using System.Diagnostics;
using System.IO;
using System.Windows;
using Phonexis.Models;

namespace Phonexis.Services.Downloads;

/// <summary>A desktop app Phonexis can pass links to but cannot name files for.</summary>
public sealed record HandoffProfile(
    string Id,
    string DisplayName,
    string Cost,
    string RegistryName,
    string? ExeName,
    string HowItWorksKey,
    Func<string?>? DownloadFolder = null,
    Func<CancellationToken, Task<bool>>? AutoAdd = null);

/// <summary>
/// Level-2 downloader: copies all links to the clipboard (most of these apps pick them up or take Ctrl+V),
/// writes them to a .txt next to the session for apps with list import, and launches the app.
/// Names are fixed afterwards with Rename &amp; archive.
/// </summary>
public sealed class HandoffDownloader : IVideoDownloader
{
    private readonly HandoffProfile _profile;
    private readonly ILogService _log;

    public HandoffDownloader(HandoffProfile profile, ILogService log)
    {
        _profile = profile;
        _log = log;
    }

    public string Id => _profile.Id;
    public string DisplayName => _profile.DisplayName;
    public DownloaderLevel Level => DownloaderLevel.Handoff;
    public string Cost => _profile.Cost;
    public string HowItWorksKey => _profile.HowItWorksKey;

    public string? Locate(DownloadOptions options) => ToolLocator.FindInstalledProgram(_profile.RegistryName, _profile.ExeName);

    public string? SuggestedDownloadFolder()
    {
        try
        {
            var folder = _profile.DownloadFolder?.Invoke();
            return folder != null && Directory.Exists(folder) ? folder : null;
        }
        catch (Exception ex)
        {
            _log.Error($"Could not read {_profile.DisplayName} download folder", ex);
            return null;
        }
    }

    /// <summary>True when the last run also added the links inside the app (not just on the clipboard).</summary>
    public bool LastAutoAdded { get; private set; }

    /// <summary>The text file the links were last written to.</summary>
    public static string LinksFile => Path.Combine(AppPaths.Root, "phonexis-links.txt");

    public async Task RunAsync(IReadOnlyList<DownloadJob> jobs, DownloadOptions options, IDownloadReporter reporter, CancellationToken ct)
    {
        LastAutoAdded = false;
        var text = string.Join(Environment.NewLine, jobs.Select(j => j.Url));
        File.WriteAllText(LinksFile, text);
        Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(text));

        var exe = Locate(options);
        if (exe != null && File.Exists(exe))
        {
            try
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _log.Error($"Could not launch {_profile.DisplayName}", ex);
            }
        }

        if (_profile.AutoAdd != null)
        {
            try
            {
                LastAutoAdded = await _profile.AutoAdd(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.Error($"Automatic add in {_profile.DisplayName} failed", ex);
            }
        }

        foreach (var job in jobs) reporter.Report(job, DownloadState.Sent, 1);
    }
}

/// <summary>Every downloader Phonexis knows, in the order shown in Settings.</summary>
public sealed class DownloaderRegistry
{
    public IReadOnlyList<IVideoDownloader> All { get; }

    public DownloaderRegistry(ILogService log)
    {
        var handoff = new[]
        {
            // MediaHuman (Qt) keeps its settings in HKCU; video files are named {VideoTitle} by default,
            // so Rename & archive matches them by title and duration. Clipboard auto-add is off by default.
            new HandoffProfile("mediahuman", "MediaHuman YouTube Downloader", "freemium", "MediaHuman YouTube Downloader",
                "YouTubeDownloader.exe", "DlHowMediaHuman",
                () => Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\MediaHuman\YouTube Downloader\video",
                    "outputFolder", null) as string,
                ct => MediaHumanAutomation.TryPasteLinksAsync(TimeSpan.FromSeconds(30), ct)),
            // IDM's command line saves a YouTube page as HTML; it only grabs YouTube videos through its browser
            // extension, so Phonexis hands the links over like any other app.
            new HandoffProfile("idm", "Internet Download Manager", "paid", "Internet Download Manager", "IDMan.exe", "DlHowIdm"),
            new HandoffProfile("4kvd", "4K Video Downloader+", "freemium", "4K Video Downloader", null, "DlHowHandoffPaste"),
            new HandoffProfile("fdm", "Free Download Manager", "free", "Free Download Manager", null, "DlHowHandoffPaste"),
            new HandoffProfile("stacher", "Stacher", "free", "Stacher", null, "DlHowHandoffPaste"),
            new HandoffProfile("parabolic", "Parabolic", "free", "Parabolic", null, "DlHowHandoffPaste"),
            new HandoffProfile("ovd", "Open Video Downloader", "free", "Open Video Downloader", null, "DlHowHandoffPaste"),
            new HandoffProfile("clipgrab", "ClipGrab", "free", "ClipGrab", null, "DlHowHandoffPaste"),
            new HandoffProfile("ytd", "YTD Video Downloader", "freemium", "YTD Video Downloader", null, "DlHowHandoffPaste"),
            new HandoffProfile("freemake", "Freemake Video Downloader", "freemium", "Freemake Video Downloader", null, "DlHowHandoffPaste"),
            new HandoffProfile("other", "Other app (clipboard)", "", "\u0000", null, "DlHowHandoffOther")
        };

        All = new List<IVideoDownloader>
        {
            new BuiltinDownloader(log),
            new JDownloaderDownloader(),
            new YtDlpDownloader(log)
        }.Concat(handoff.Select(p => (IVideoDownloader)new HandoffDownloader(p, log))).ToList();
    }

    public IVideoDownloader Get(string id) => All.FirstOrDefault(d => d.Id == id) ?? All[0];
}
