using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Phonexis.Models;
using Phonexis.Services;

namespace Phonexis.ViewModels;

public enum MainViewState
{
    Welcome,
    Results,
    Complete
}

public enum SaveIndicatorState
{
    Idle,
    Saving,
    Saved,
    Error
}

public partial class MainViewModel : ObservableObject
{
    private readonly ILogService _log;
    private readonly IYouTubeService _youtube;
    private readonly ICacheService _cache;
    private readonly ISessionService _session;
    private readonly ISettingsService _settings;
    private readonly IThemeService _theme;
    private readonly Services.Downloads.DownloaderRegistry _downloaders;

    private readonly List<SearchResult> _allResults = new();
    private readonly DispatcherTimer _autosaveTimer;
    private CancellationTokenSource? _searchCts;
    private bool _dirty;
    private int _lastFetchCount;

    public ObservableCollection<SongItem> Songs { get; } = new();
    public ObservableCollection<SearchResult> Results { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWelcomeVisible), nameof(IsResultsVisible), nameof(IsCompleteVisible))]
    private MainViewState _viewState = MainViewState.Welcome;

    [ObservableProperty] private SongItem? _currentSong;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _sourceFileName = string.Empty;
    [ObservableProperty] private int _currentIndex = -1;
    [ObservableProperty] private int _hiddenByFilters;
    [ObservableProperty] private string _searchError = string.Empty;

    [ObservableProperty] private SaveIndicatorState _saveIndicator = SaveIndicatorState.Idle;
    [ObservableProperty] private string _saveIndicatorText = string.Empty;

    [ObservableProperty] private string _quotaText = string.Empty;
    [ObservableProperty] private string _searchesLeftText = string.Empty;

    [ObservableProperty] private bool _showResumeBanner;
    [ObservableProperty] private string _resumeBannerText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoApiKeyBanner))]
    private bool _hasApiKeys;

    // Filters (index-bound to combo boxes)
    [ObservableProperty] private int _filterDurationIndex;
    [ObservableProperty] private int _filterDateIndex;
    [ObservableProperty] private int _filterViewsIndex;

    public bool IsWelcomeVisible => ViewState == MainViewState.Welcome;
    public bool IsResultsVisible => ViewState == MainViewState.Results;
    public bool IsCompleteVisible => ViewState == MainViewState.Complete;
    // Only "API only" depends on a key; every other mode can search for free.
    public bool ShowNoApiKeyBanner => !HasApiKeys && !_settings.Settings.TestMode &&
                                      _settings.Settings.SearchMode == SearchEngineMode.ApiOnly;

    public bool HasResults => Results.Count > 0;
    public bool ShowNoResultsCard => !IsSearching && Results.Count == 0 && SearchError.Length == 0;
    public string CurrentQueryHeader => CurrentSong == null ? string.Empty : L.Format("ResultsForFormat", CurrentSong.Query);
    public string HiddenByFiltersText => HiddenByFilters > 0 ? L.Format("HiddenByFiltersFormat", HiddenByFilters) : string.Empty;

    public string SummarySongsText => L.Format("SummarySongsFormat", Songs.Count);
    public string SummaryPickedText => L.Format("SummaryPickedFormat", DoneCount);
    public string SummarySkippedText => L.Format("SummarySkippedFormat", SkippedCount);
    public string SummaryNoResultsText => L.Format("SummaryNoResultsFormat", NoResultsCount);

    public bool HasSongs => Songs.Count > 0;
    public int DoneCount => Songs.Count(s => s.Status == SongStatus.Done);
    public int SkippedCount => Songs.Count(s => s.Status == SongStatus.Skipped);
    public int NoResultsCount => Songs.Count(s => s.Status == SongStatus.NoResults);
    public int ProcessedCount => Songs.Count(s => s.Status != SongStatus.Pending);
    public string ProgressText => I18n.Instance.Format("ProgressFormat", ProcessedCount, Songs.Count);
    public string SongsLoadedText => I18n.Instance.Format("SongsLoadedFormat", Songs.Count);
    public double ProgressFraction => Songs.Count == 0 ? 0 : (double)ProcessedCount / Songs.Count;

    public I18n L => I18n.Instance;

    public MainViewModel(
        ILogService log,
        IYouTubeService youtube,
        ICacheService cache,
        ISessionService session,
        ISettingsService settings,
        IThemeService theme,
        Services.Downloads.DownloaderRegistry downloaders)
    {
        _downloaders = downloaders;
        _log = log;
        _youtube = youtube;
        _cache = cache;
        _session = session;
        _settings = settings;
        _theme = theme;

        _cache.UsageChanged += (_, _) =>
            Application.Current?.Dispatcher.BeginInvoke(UpdateQuotaDisplay);

        Results.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(ShowNoResultsCard));
        };

        I18n.Instance.PropertyChanged += (_, _) => RefreshLocalizedTexts();

        _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _autosaveTimer.Tick += (_, _) => AutosaveTick();
        _autosaveTimer.Start();

        HasApiKeys = _youtube.ApiKeys.Count > 0;
        StatusMessage = L["StatusReady"];
        UpdateQuotaDisplay();
        CheckForPreviousSession();
    }

    // ===================== Session bootstrap =====================

    private void CheckForPreviousSession()
    {
        var state = _session.SessionExists ? _session.Load() : null;
        if (state == null || state.Songs.Count == 0) return;
        var done = state.Songs.Count(s => s.Status != SongStatus.Pending);
        var name = string.IsNullOrEmpty(state.SourceFilePath) ? "—" : Path.GetFileName(state.SourceFilePath);
        ResumeBannerText = L.Format("ResumeBannerFormat", name, done, state.Songs.Count);
        ShowResumeBanner = true;
    }

    [RelayCommand]
    private void Resume()
    {
        var state = _session.Load();
        if (state == null) { ShowResumeBanner = false; return; }
        ApplyState(state, state.SourceFilePath);
        _log.Info($"Session resumed: {Songs.Count} songs");
    }

    /// <summary>Restores a saved session or a .phonexis list into the queue and continues where it left off.</summary>
    private void ApplyState(SessionState state, string? sourcePath)
    {
        _searchCts?.Cancel();
        Results.Clear();
        _allResults.Clear();
        if (CurrentSong != null) CurrentSong.IsCurrent = false;
        CurrentSong = null;
        CurrentIndex = -1;
        Songs.Clear();
        for (var i = 0; i < state.Songs.Count; i++)
        {
            var s = state.Songs[i];
            Songs.Add(new SongItem
            {
                Index = i,
                OriginalTitle = s.OriginalTitle,
                Query = s.Query,
                Status = s.Status,
                SelectedUrl = s.SelectedUrl,
                SelectedTitle = s.SelectedTitle,
                SourceAudioPath = s.SourceAudioPath,
                SelectedVideoId = s.SelectedVideoId,
                SelectedDurationSeconds = s.SelectedDurationSeconds,
                DownloadedPath = s.DownloadedPath,
                DownloadState = s.DownloadedPath != null && File.Exists(s.DownloadedPath) ? DownloadState.Done : DownloadState.None
            });
        }
        _sourceFilePath = sourcePath;
        SourceFileName = string.IsNullOrEmpty(sourcePath) ? string.Empty : Path.GetFileName(sourcePath);
        ShowResumeBanner = false;
        NotifyQueueChanged();

        var target = FindNextPending(Math.Max(0, state.CurrentIndex) - 1) ?? state.CurrentIndex;
        if (Songs.All(s => s.Status != SongStatus.Pending))
        {
            ViewState = MainViewState.Complete;
        }
        else
        {
            _ = GoToSongAsync(Math.Min(Math.Max(0, target), Songs.Count - 1));
        }
    }

    [RelayCommand]
    private void DismissResume() => ShowResumeBanner = false;

    // ===================== File loading =====================

    [RelayCommand]
    private void LoadFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = $"{L["ListFilesFilter"]}|*.phonexis;*.txt|{L["ListFilterPhonexis"]}|*.phonexis|{L["ListFilterTxt"]}|*.txt|*.*|*.*",
            Title = L["LoadTxt"]
        };
        if (dialog.ShowDialog() == true)
            LoadFileFromPath(dialog.FileName);
    }

    public void LoadFileFromPath(string path)
    {
        if (ListFileService.IsListFile(path))
        {
            LoadListFile(path);
            return;
        }
        try
        {
            var lines = File.ReadAllLines(path)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            if (lines.Count == 0)
            {
                MessageBox.Show(L["NoSongsInFile"], L["Warning"], MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            LoadQueue(lines, path);
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to load file '{path}'", ex);
            MessageBox.Show(L.Format("FileLoadErrorFormat", ex.Message), L["Error"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Replaces the queue with the given titles (from file or extractor).</summary>
    public void LoadQueue(IReadOnlyList<string> titles, string? sourcePath, IReadOnlyList<string>? audioPaths = null)
    {
        _searchCts?.Cancel();
        Songs.Clear();
        for (var i = 0; i < titles.Count; i++)
        {
            Songs.Add(new SongItem
            {
                Index = i,
                OriginalTitle = titles[i],
                Query = titles[i],
                SourceAudioPath = audioPaths != null && i < audioPaths.Count ? audioPaths[i] : null
            });
        }
        SourceFileName = sourcePath == null ? string.Empty : Path.GetFileName(sourcePath);
        CurrentIndex = -1;
        CurrentSong = null;
        Results.Clear();
        _allResults.Clear();
        ViewState = MainViewState.Welcome;
        ShowResumeBanner = false;
        NotifyQueueChanged();
        MarkDirty();
        SaveSessionInternal(sourcePath);
        StatusMessage = L.Format("StatusFileLoadedFormat", SourceFileName);
        _log.Info($"Loaded {titles.Count} titles" + (sourcePath != null ? $" from {sourcePath}" : ""));
    }

    // ===================== Navigation & search =====================

    [RelayCommand(CanExecute = nameof(HasSongs))]
    private async Task StartAsync()
    {
        var target = FindNextPending(-1) ?? 0;
        await GoToSongAsync(target);
    }

    [RelayCommand]
    private async Task JumpToSongAsync(SongItem? song)
    {
        if (song == null) return;
        await GoToSongAsync(song.Index);
    }

    [RelayCommand]
    private async Task SkipAsync()
    {
        if (CurrentSong == null) return;
        if (CurrentSong.SelectedUrl == null)
            CurrentSong.Status = SongStatus.Skipped;
        SaveSessionInternal(null);
        await AdvanceAsync();
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        if (CurrentIndex > 0)
            await GoToSongAsync(CurrentIndex - 1);
    }

    [RelayCommand]
    private async Task SelectVideoAsync(SearchResult? result)
    {
        if (result == null || CurrentSong == null) return;
        CurrentSong.SelectedUrl = result.VideoUrl;
        CurrentSong.SelectedTitle = result.Title;
        CurrentSong.SelectedVideoId = result.VideoId;
        CurrentSong.SelectedDurationSeconds = result.DurationSeconds;
        CurrentSong.DownloadState = DownloadState.None;
        CurrentSong.Status = SongStatus.Done;
        NotifyQueueChanged();
        SaveSessionInternal(null); // a picked link is too valuable to wait for the autosave tick
        await AdvanceAsync();
    }

    private async Task AdvanceAsync()
    {
        var next = FindNextPending(CurrentIndex);
        if (next == null)
        {
            FinishSession();
            return;
        }
        await GoToSongAsync(next.Value);
    }

    private int? FindNextPending(int afterIndex)
    {
        // First pending strictly after the given index, then wrap to the first pending anywhere.
        for (var i = afterIndex + 1; i < Songs.Count; i++)
            if (Songs[i].Status == SongStatus.Pending) return i;
        for (var i = 0; i <= afterIndex && i < Songs.Count; i++)
            if (Songs[i].Status == SongStatus.Pending) return i;
        return null;
    }

    private async Task GoToSongAsync(int index)
    {
        if (index < 0 || index >= Songs.Count) return;

        if (CurrentSong != null) CurrentSong.IsCurrent = false;
        CurrentIndex = index;
        CurrentSong = Songs[index];
        CurrentSong.IsCurrent = true;
        ViewState = MainViewState.Results;
        await SearchCurrentAsync(bypassCache: false);
    }

    private async Task SearchCurrentAsync(bool bypassCache, int? overrideCount = null, SearchEngineMode? modeOverride = null)
    {
        if (CurrentSong == null) return;

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;
        var song = CurrentSong;
        var count = overrideCount ?? Math.Clamp(_settings.Settings.MaxResults, 1, 25);

        IsSearching = true;
        SearchError = string.Empty;
        AlternativeEngine = null;
        LastEngine = null;
        ClearSearchErrorDetails();
        StatusMessage = L["StatusSearching"];
        string? engineNotice = null;
        Results.Clear();
        _allResults.Clear();
        HiddenByFilters = 0;

        try
        {
            List<SearchResult> results;
            if (_settings.Settings.TestMode)
            {
                await Task.Delay(250, ct); // small delay so the UI flow is visible
                results = _youtube.GenerateDummyResults(song.Query, count);
            }
            else if (_settings.Settings.CacheOnlyMode)
            {
                results = _cache.GetCachedResults(song.Query) ?? new List<SearchResult>();
            }
            else
            {
                var mode = modeOverride ?? _settings.Settings.SearchMode;
                var outcome = await _youtube.SearchAsync(song.Query, count, mode, bypassCache, ct);
                results = outcome.Results;
                LastEngine = outcome.Engine;
                if (outcome.Engine is { } used && outcome.Failures.Count > 0)
                {
                    engineNotice = L.Format("FallbackUsedFormat", EngineName(outcome.Failures[0].Engine), EngineName(used));
                    _log.Info($"Search '{song.Query}' answered by {used} after {outcome.Failures.Count} failure(s)");
                }
            }

            if (ct.IsCancellationRequested || CurrentSong != song) return;

            _lastFetchCount = count;
            _allResults.AddRange(results);
            if (_settings.Settings.AutoPreselect)
            {
                var audioSeconds = await GetAudioSecondsAsync(song);
                if (ct.IsCancellationRequested || CurrentSong != song) return;
                ResultRanker.Rank(song, _allResults, audioSeconds);
            }
            ApplyFilters();

            if (results.Count == 0 && song.SelectedUrl == null)
                song.Status = SongStatus.NoResults;

            StatusMessage = engineNotice ?? L["StatusReady"];
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Services.Search.SearchFailedException ex)
        {
            SearchError = DescribeSearchFailure(ex);
            AlternativeEngine = PickAlternative(modeOverride ?? _settings.Settings.SearchMode);
            StatusMessage = L["StatusReady"];
        }
        catch (Exception ex)
        {
            _log.Error($"Search failed for '{song.Query}'", ex);
            SearchError = ex is System.Net.Http.HttpRequestException
                ? L["NetworkError"]
                : L.Format("SearchErrorFormat", ex.Message);
            StatusMessage = L["StatusReady"];
        }
        finally
        {
            if (CurrentSong == song) IsSearching = false;
            OnPropertyChanged(nameof(SearchModeTooltip));
            UpdateQuotaDisplay();
            NotifyQueueChanged();
        }
    }

    [RelayCommand]
    private async Task RefreshSearchAsync() => await SearchCurrentAsync(bypassCache: true);

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        // The search endpoint has no cheap pagination without storing page tokens;
        // re-query with a larger window (superset of current results).
        var newCount = Math.Min(_lastFetchCount + 5, 25);
        await SearchCurrentAsync(bypassCache: true, overrideCount: newCount);
    }

    [RelayCommand]
    private void EditQuery()
    {
        if (CurrentSong == null) return;
        var dialog = new Views.EditQueryDialog(CurrentSong.Query) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() == true && dialog.EditedQuery.Trim().Length > 0)
        {
            CurrentSong.Query = dialog.EditedQuery.Trim();
            OnPropertyChanged(nameof(CurrentQueryHeader));
            MarkDirty();
            _ = SearchCurrentAsync(bypassCache: false);
        }
    }

    private void FinishSession()
    {
        if (CurrentSong != null) CurrentSong.IsCurrent = false;
        CurrentSong = null;
        ViewState = MainViewState.Complete;
        StatusMessage = L["StatusComplete"];
        SaveSessionInternal(null);
        NotifyQueueChanged();
    }

    // ===================== Property change hooks =====================

    partial void OnIsSearchingChanged(bool value) => OnPropertyChanged(nameof(ShowNoResultsCard));
    partial void OnSearchErrorChanged(string value) => OnPropertyChanged(nameof(ShowNoResultsCard));
    partial void OnCurrentSongChanged(SongItem? value) => OnPropertyChanged(nameof(CurrentQueryHeader));
    partial void OnHiddenByFiltersChanged(int value) => OnPropertyChanged(nameof(HiddenByFiltersText));

    // ===================== Filters =====================

    partial void OnFilterDurationIndexChanged(int value) => ApplyFilters();
    partial void OnFilterDateIndexChanged(int value) => ApplyFilters();
    partial void OnFilterViewsIndexChanged(int value) => ApplyFilters();

    private void ApplyFilters()
    {
        var filters = new SearchFilters
        {
            Duration = (DurationFilter)Math.Max(0, FilterDurationIndex),
            Date = (DateFilter)Math.Max(0, FilterDateIndex),
            Views = (ViewsFilter)Math.Max(0, FilterViewsIndex)
        };

        Results.Clear();
        foreach (var r in _allResults.Where(filters.Passes))
            Results.Add(r);
        HiddenByFilters = _allResults.Count - Results.Count;
    }

    // ===================== Result actions =====================

    [RelayCommand]
    private void OpenVideo(SearchResult? result)
    {
        if (result == null) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = result.VideoUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _log.Error("Failed to open browser", ex);
        }
    }

    [RelayCommand]
    private void CopyLink(SearchResult? result)
    {
        if (result == null) return;
        try
        {
            Clipboard.SetText(result.VideoUrl);
            StatusMessage = L["LinkCopied"];
        }
        catch (Exception ex)
        {
            _log.Error("Clipboard write failed", ex);
        }
    }

    // ===================== Export =====================

    [RelayCommand]
    private void Export()
    {
        var picked = Songs.Where(s => s.SelectedUrl != null).ToList();
        if (picked.Count == 0)
        {
            MessageBox.Show(L["NothingToExport"], L["Warning"], MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = L["ExportDialogTitle"],
            FileName = "phonexis_links",
            Filter = $"{L["ExportTxt"]}|*.txt|{L["ExportCsv"]}|*.csv|{L["ExportM3u"]}|*.m3u"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var content = dialog.FilterIndex switch
            {
                2 => BuildCsv(picked),
                3 => BuildM3u(picked),
                _ => string.Join(Environment.NewLine, picked.Select(s => s.SelectedUrl))
            };
            File.WriteAllText(dialog.FileName, content, new UTF8Encoding(false));
            StatusMessage = L.Format("ExportSuccessFormat", picked.Count, Path.GetFileName(dialog.FileName));
            _log.Info($"Exported {picked.Count} links to {dialog.FileName}");
        }
        catch (Exception ex)
        {
            _log.Error("Export failed", ex);
            MessageBox.Show(ex.Message, L["Error"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string BuildCsv(IEnumerable<SongItem> songs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Original Title,Video Title,URL");
        foreach (var s in songs)
            sb.AppendLine($"{Csv(s.OriginalTitle)},{Csv(s.SelectedTitle ?? "")},{Csv(s.SelectedUrl ?? "")}");
        return sb.ToString();

        static string Csv(string v) => $"\"{v.Replace("\"", "\"\"")}\"";
    }

    private static string BuildM3u(IEnumerable<SongItem> songs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#EXTM3U");
        foreach (var s in songs)
        {
            sb.AppendLine($"#EXTINF:-1,{s.SelectedTitle ?? s.OriginalTitle}");
            sb.AppendLine(s.SelectedUrl);
        }
        return sb.ToString();
    }

    // ===================== Session persistence =====================

    private string? _sourceFilePath;

    private void SaveSessionInternal(string? sourcePath)
    {
        if (sourcePath != null) _sourceFilePath = sourcePath;
        if (Songs.Count == 0) return;

        var state = BuildState();

        if (_session.Save(state))
        {
            SaveIndicator = SaveIndicatorState.Saved;
            SaveIndicatorText = L.Format("AutosaveSavedFormat", DateTime.Now.ToString("HH:mm:ss"));
            _dirty = false;
        }
        else
        {
            SaveIndicator = SaveIndicatorState.Error;
            SaveIndicatorText = L["AutosaveError"];
        }
    }

    [RelayCommand]
    private void SaveSessionNow()
    {
        SaveSessionInternal(null);
        StatusMessage = L["SessionSaved"];
    }

    [RelayCommand]
    private void NewSession()
    {
        if (Songs.Count > 0)
        {
            var confirm = MessageBox.Show(L["NewSessionConfirm"], L["Confirm"],
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
        }

        _searchCts?.Cancel();
        Songs.Clear();
        Results.Clear();
        _allResults.Clear();
        CurrentSong = null;
        CurrentIndex = -1;
        SourceFileName = string.Empty;
        _sourceFilePath = null;
        ViewState = MainViewState.Welcome;
        _session.Delete();
        NotifyQueueChanged();
        StatusMessage = L["StatusReady"];
    }

    private void MarkDirty() => _dirty = true;

    private void AutosaveTick()
    {
        if (!_dirty || Songs.Count == 0) return;
        SaveIndicator = SaveIndicatorState.Saving;
        SaveIndicatorText = L["AutosaveSaving"];
        SaveSessionInternal(null);
    }

    /// <summary>Called by the window on close.</summary>
    public void OnAppClosing()
    {
        if (_dirty && Songs.Count > 0) SaveSessionInternal(null);
        _settings.Save();
    }

    // ===================== Settings / theme / dialogs =====================

    [RelayCommand]
    private void OpenSettings()
    {
        var dialog = new Views.SettingsDialog(_settings, _youtube, _cache, _theme, _downloaders)
        {
            Owner = Application.Current.MainWindow
        };
        dialog.ShowDialog();
        HasApiKeys = _youtube.ApiKeys.Count > 0;
        OnPropertyChanged(nameof(ShowNoApiKeyBanner));
        NotifySearchModeChanged();
        UpdateQuotaDisplay();
        UpdateDownloadSummary();
    }

    [RelayCommand]
    private void OpenAbout()
    {
        new Views.AboutDialog { Owner = Application.Current.MainWindow }.ShowDialog();
    }

    [RelayCommand]
    private void OpenExtractor()
    {
        var dialog = new Views.FilenameExtractorDialog { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() == true && dialog.ResultTitles.Count > 0 && dialog.UseAsQueue)
        {
            LoadQueue(dialog.ResultTitles, dialog.SavedFilePath, dialog.ResultPaths);
        }
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        var next = _theme.IsDarkActive ? AppTheme.Light : AppTheme.Dark;
        _theme.Apply(next);
        _settings.Settings.Theme = next;
        _settings.Save();
    }

    // ===================== Helpers =====================

    private void UpdateQuotaDisplay()
    {
        QuotaText = L.Format("QuotaPillFormat", _cache.GetTotalUnitsToday());
        SearchesLeftText = L.Format("SearchesLeftFormat", _youtube.EstimateSearchesLeftToday());
    }

    private void NotifyQueueChanged()
    {
        OnPropertyChanged(nameof(HasSongs));
        OnPropertyChanged(nameof(DoneCount));
        OnPropertyChanged(nameof(SkippedCount));
        OnPropertyChanged(nameof(NoResultsCount));
        OnPropertyChanged(nameof(ProcessedCount));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(SongsLoadedText));
        OnPropertyChanged(nameof(ProgressFraction));
        OnPropertyChanged(nameof(SummarySongsText));
        OnPropertyChanged(nameof(SummaryPickedText));
        OnPropertyChanged(nameof(SummarySkippedText));
        OnPropertyChanged(nameof(SummaryNoResultsText));
        StartCommand.NotifyCanExecuteChanged();
        SaveListAsCommand.NotifyCanExecuteChanged();
        UpdateDownloadSummary();
    }

    private void RefreshLocalizedTexts()
    {
        StatusMessage = L["StatusReady"];
        UpdateQuotaDisplay();
        NotifyQueueChanged();
        OnPropertyChanged(nameof(SearchModeOptions)); // before the index, so the picker keeps its selection
        NotifySearchModeChanged();
        OnPropertyChanged(nameof(RetryWithAlternativeText));
        OnPropertyChanged(nameof(PreferAlternativeText));
    }
}
