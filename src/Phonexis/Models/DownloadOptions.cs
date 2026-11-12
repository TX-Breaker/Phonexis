namespace Phonexis.Models;

/// <summary>Where a downloaded video lands, relative to its track.</summary>
public enum VideoDestination
{
    /// <summary>Next to the audio file (or in the custom folder when the track has no audio file).</summary>
    SameFolder,
    /// <summary>A "video" subfolder next to the audio file.</summary>
    VideoSubfolder,
    /// <summary>One folder chosen by the user for everything.</summary>
    CustomFolder
}

/// <summary>What to do when the target video file already exists.</summary>
public enum ExistingFilePolicy
{
    Skip,
    Overwrite,
    AddSuffix
}

/// <summary>Per-song download state shown in the queue and in the export preview.</summary>
public enum DownloadState
{
    None,
    Queued,
    Downloading,
    Done,
    Sent,
    Skipped,
    Failed
}

/// <summary>Download preferences persisted inside <see cref="AppSettings"/>.</summary>
public class DownloadOptions
{
    /// <summary>Id of the selected downloader (see DownloaderRegistry).</summary>
    public string Downloader { get; set; } = "builtin";
    public VideoDestination Destination { get; set; } = VideoDestination.VideoSubfolder;
    public string? CustomFolder { get; set; }
    public string SubfolderName { get; set; } = "video";
    public ExistingFilePolicy ExistingFiles { get; set; } = ExistingFilePolicy.Skip;

    /// <summary>Highest video height to download (360, 720, 1080).</summary>
    public int MaxQuality { get; set; } = 1080;
    public int ParallelDownloads { get; set; } = 2;

    /// <summary>Give each video the creation/modification dates of its audio file.</summary>
    public bool MatchAudioDates { get; set; }

    /// <summary>A download with no progress for this long is restarted (the queue never hangs on one video).</summary>
    public int StallSeconds { get; set; } = 90;

    /// <summary>Extra attempts after a stall or a network error.</summary>
    public int Retries { get; set; } = 2;

    /// <summary>Optional explicit paths; empty = auto-detect.</summary>
    public string? FfmpegPath { get; set; }
    public string? YtDlpPath { get; set; }
    public string? JDownloaderFolderWatch { get; set; }

    /// <summary>Folder where a "link handoff" app saves its files, used by Rename &amp; archive.</summary>
    public string? HandoffDownloadFolder { get; set; }
}
