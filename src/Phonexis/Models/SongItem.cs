using CommunityToolkit.Mvvm.ComponentModel;

namespace Phonexis.Models;

public enum SongStatus
{
    Pending,
    Done,
    Skipped,
    NoResults
}

/// <summary>One entry of the search queue (one line of the loaded text file).</summary>
public partial class SongItem : ObservableObject
{
    public int Index { get; init; }

    /// <summary>Original line from the file, never modified.</summary>
    public string OriginalTitle { get; init; } = string.Empty;

    /// <summary>Effective search query (user can edit it).</summary>
    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private SongStatus _status = SongStatus.Pending;

    /// <summary>True while this song is the one being worked on (not persisted).</summary>
    [ObservableProperty]
    private bool _isCurrent;

    [ObservableProperty]
    private string? _selectedUrl;

    [ObservableProperty]
    private string? _selectedTitle;

    /// <summary>Full path of the audio file this line came from (folder scan only); names the video.</summary>
    public string? SourceAudioPath { get; set; }

    public string? SelectedVideoId { get; set; }
    public int? SelectedDurationSeconds { get; set; }

    [ObservableProperty]
    private DownloadState _downloadState;

    /// <summary>0..1 while downloading.</summary>
    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string? _downloadedPath;

    [ObservableProperty]
    private string? _downloadError;

    public int DisplayNumber => Index + 1;
}
