using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phonexis.Models;
using Phonexis.Services.Downloads;

namespace Phonexis.ViewModels;

/// <summary>Video downloads: hands picked links to the chosen downloader and tracks per-song state.</summary>
public partial class MainViewModel : IDownloadReporter
{
    private CancellationTokenSource? _downloadCts;
    private List<SongItem> _jobSongs = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadVideosCommand))]
    private bool _isDownloading;

    [ObservableProperty] private string _downloadStatusText = string.Empty;
    [ObservableProperty] private double _downloadFraction;

    public IVideoDownloader CurrentDownloader => _downloaders.Get(_settings.Settings.Download.Downloader);

    /// <summary>"via Phonexis · 1080p · video\" — what the download button will do.</summary>
    public string DownloadSetupText
    {
        get
        {
            var o = _settings.Settings.Download;
            var where = o.Destination switch
            {
                VideoDestination.SameFolder => L["DlDestSame"],
                VideoDestination.VideoSubfolder => L.Format("DlDestSubShort", o.SubfolderName),
                _ => VideoNaming.FallbackRoot(o)
            };
            var quality = CurrentDownloader.Level == DownloaderLevel.Direct ? $" · {o.MaxQuality}p" : string.Empty;
            return L.Format("DlSetupFormat", CurrentDownloader.DisplayName) + quality + " · " + where;
        }
    }

    public int DownloadedCount => Songs.Count(s => s.DownloadState is DownloadState.Done or DownloadState.Skipped);

    private bool CanDownloadVideos() => !IsDownloading;

    [RelayCommand(CanExecute = nameof(CanDownloadVideos))]
    private Task DownloadVideosAsync() => RunDownloadsAsync(s => s.DownloadState != DownloadState.Done);

    /// <summary>After a run with errors: tries again only the videos that failed.</summary>
    [RelayCommand(CanExecute = nameof(CanDownloadVideos))]
    private Task RetryFailedDownloadsAsync() => RunDownloadsAsync(s => s.DownloadState == DownloadState.Failed);

    public int FailedDownloadCount => Songs.Count(s => s.DownloadState == DownloadState.Failed);

    private async Task RunDownloadsAsync(Func<SongItem, bool> which)
    {
        var options = _settings.Settings.Download;
        var downloader = CurrentDownloader;
        var candidates = Songs.Where(s => s.SelectedUrl != null && which(s)).ToList();
        if (candidates.Count == 0)
        {
            MessageBox.Show(L[Songs.Any(s => s.SelectedUrl != null) ? "DlNothingLeft" : "NothingToExport"],
                L["DlTitle"], MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (downloader.Level == DownloaderLevel.Direct && downloader.Locate(options) == null)
        {
            MessageBox.Show(L.Format("DlToolMissingFormat", downloader.DisplayName), L["DlTitle"],
                MessageBoxButton.OK, MessageBoxImage.Warning);
            OpenSettingsCommand.Execute(null);
            return;
        }

        if (downloader.Id == "jdownloader" && downloader.Locate(options) is { } watch &&
            JDownloaderDownloader.FolderWatchInstalled(watch) == false)
        {
            var go = MessageBox.Show(L["DlJdNoFolderWatch"], L["DlTitle"], MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (go != MessageBoxResult.Yes) return;
        }

        if (downloader.Id == "builtin" && options.MaxQuality > 360 && ToolLocator.FindFfmpeg(options.FfmpegPath) == null)
        {
            var answer = MessageBox.Show(L["DlFfmpegAsk"], L["DlTitle"], MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel) return;
            if (answer == MessageBoxResult.Yes && !await DownloadFfmpegAsync()) return;
        }

        var jobs = new List<DownloadJob>();
        foreach (var song in candidates)
        {
            var id = song.SelectedVideoId ?? Services.YouTubeUrl.TryGetVideoId(song.SelectedUrl);
            if (id == null) continue;
            song.SelectedVideoId = id;
            song.DownloadError = null;
            song.DownloadProgress = 0;
            song.DownloadState = DownloadState.Queued;
            jobs.Add(new DownloadJob(song, song.SelectedUrl!, id, VideoNaming.TargetFolder(song, options), VideoNaming.BaseName(song)));
        }

        _jobSongs = jobs.Select(j => j.Song).ToList();
        _downloadCts = new CancellationTokenSource();
        IsDownloading = true;
        UpdateDownloadSummary();

        try
        {
            await Task.Run(() => downloader.RunAsync(jobs, options, this, _downloadCts.Token));
        }
        catch (OperationCanceledException)
        {
            // Per-job states already reset by the downloader.
        }
        catch (Exception ex)
        {
            _log.Error("Download run failed", ex);
            MessageBox.Show(ex.Message, L["Error"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            foreach (var s in jobs.Select(j => j.Song).Where(s => s.DownloadState == DownloadState.Queued))
                s.DownloadState = DownloadState.None;
            IsDownloading = false;
            UpdateDownloadSummary();
            SaveSessionInternal(null);
        }

        if (downloader.Level == DownloaderLevel.Handoff)
        {
            // Point Rename & archive at the folder this app actually saves into.
            if (downloader.SuggestedDownloadFolder() is { } appFolder)
            {
                options.HandoffDownloadFolder = appFolder;
                _settings.Save();
            }
            var message = downloader is HandoffDownloader { LastAutoAdded: true }
                ? L.Format("DlAutoAddedFormat", jobs.Count, downloader.DisplayName)
                : L.Format("DlHandoffDoneFormat", jobs.Count, downloader.DisplayName, HandoffDownloader.LinksFile);
            MessageBox.Show(message, L["DlTitle"], MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else if (downloader.Id == "jdownloader")
        {
            MessageBox.Show(L.Format("DlSentFormat", jobs.Count, downloader.DisplayName),
                L["DlTitle"], MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    private void CancelDownloads() => _downloadCts?.Cancel();

    /// <summary>Shows the first-run guide; runs the action the user picked on its last step.</summary>
    [RelayCommand]
    public void OpenOnboarding()
    {
        var dialog = new Views.OnboardingDialog(_settings, _youtube, _downloaders) { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
        HasApiKeys = _youtube.ApiKeys.Count > 0;
        OnPropertyChanged(nameof(ShowNoApiKeyBanner));
        UpdateDownloadSummary();
        if (dialog.Next == Views.OnboardingNext.LoadTxt) LoadFileCommand.Execute(null);
        else if (dialog.Next == Views.OnboardingNext.FromFolder) OpenExtractorCommand.Execute(null);
    }

    public bool NeedsOnboarding => !_settings.Settings.FirstRunCompleted;

    [RelayCommand]
    private void OpenArchive()
    {
        var dialog = new Views.ArchiveDialog(Songs.ToList(), _settings, CurrentDownloader.SuggestedDownloadFolder())
        {
            Owner = Application.Current.MainWindow
        };
        dialog.ShowDialog();
        UpdateDownloadSummary();
        MarkDirty();
        SaveSessionInternal(null);
    }

    public async Task<bool> DownloadFfmpegAsync()
    {
        try
        {
            DownloadStatusText = L["DlFfmpegDownloading"];
            IsDownloading = true;
            var progress = new Progress<double>(p => DownloadStatusText = $"{L["DlFfmpegDownloading"]} {p:P0}");
            var path = await ToolLocator.DownloadFfmpegAsync(progress, CancellationToken.None);
            _settings.Settings.Download.FfmpegPath = path;
            _settings.Save();
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("FFmpeg download failed", ex);
            MessageBox.Show(L.Format("DlFfmpegFailedFormat", ex.Message), L["Error"], MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        finally
        {
            IsDownloading = false;
            DownloadStatusText = string.Empty;
        }
    }

    // IDownloadReporter: called from worker threads.
    public void Report(DownloadJob job, DownloadState state, double progress = 0, string? path = null, string? error = null)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            var song = job.Song;
            song.DownloadState = state;
            song.DownloadProgress = progress;
            if (path != null && state is DownloadState.Done or DownloadState.Skipped) song.DownloadedPath = path;
            if (error != null)
            {
                song.DownloadError = error switch
                {
                    "FFMPEG_MISSING" => L["DlErrFfmpeg"],
                    "STALLED" => L["DlErrStalled"],
                    "NO_STREAM" => L["DlErrNoStream"],
                    _ when error.StartsWith("RETRY:", StringComparison.Ordinal) => L.Format("DlRetryingFormat", error[6..]),
                    _ => error
                };
                if (error.StartsWith("RETRY:", StringComparison.Ordinal)) StatusMessage = $"{song.Query} — {song.DownloadError}";
            }
            if (state != DownloadState.Downloading) MarkDirty();
            UpdateDownloadSummary();
        });
    }

    private void UpdateDownloadSummary()
    {
        OnPropertyChanged(nameof(DownloadedCount));
        OnPropertyChanged(nameof(FailedDownloadCount));
        OnPropertyChanged(nameof(DownloadSetupText));
        OnPropertyChanged(nameof(CurrentDownloader));
        if (!IsDownloading || _jobSongs.Count == 0)
        {
            var picked = Songs.Count(s => s.SelectedUrl != null);
            DownloadFraction = picked == 0 ? 0 : (double)DownloadedCount / picked;
            if (!IsDownloading) DownloadStatusText = DownloadedCount > 0 ? L.Format("DlDoneCountFormat", DownloadedCount, picked) : string.Empty;
            return;
        }

        var active = _jobSongs;
        var finished = active.Count(s => s.DownloadState is DownloadState.Done or DownloadState.Sent or DownloadState.Skipped or DownloadState.Failed);
        var running = active.Where(s => s.DownloadState == DownloadState.Downloading).Sum(s => s.DownloadProgress);
        DownloadFraction = Math.Clamp((finished + running) / Math.Max(1, active.Count), 0, 1);
        DownloadStatusText = L.Format("DlProgressFormat", finished, active.Count);
    }
}
