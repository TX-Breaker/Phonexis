namespace Phonexis.Models;

/// <summary>Persisted snapshot of a search session (resume support).</summary>
public class SessionState
{
    public int Version { get; set; } = 2;

    /// <summary>"phonexis-list" in .phonexis files; absent in the autosaved session.</summary>
    public string? Format { get; set; }
    public string? SourceFilePath { get; set; }
    public int CurrentIndex { get; set; }
    public DateTime SavedAt { get; set; }
    public List<SessionSong> Songs { get; set; } = new();
}

public class SessionSong
{
    public string OriginalTitle { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public SongStatus Status { get; set; }
    public string? SelectedUrl { get; set; }
    public string? SelectedTitle { get; set; }

    // V3 additions (absent in V2 sessions, which still load).
    public string? SourceAudioPath { get; set; }
    public string? SelectedVideoId { get; set; }
    public int? SelectedDurationSeconds { get; set; }
    public string? DownloadedPath { get; set; }
}
