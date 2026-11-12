using System.IO;
using System.Text;
using Phonexis.Models;

namespace Phonexis.Services.Downloads;

/// <summary>
/// Names a video after its track: "Bicep - Glue.flac" becomes "Bicep - Glue.mp4",
/// placed next to the audio, in a "video" subfolder, or in a custom folder.
/// </summary>
public static class VideoNaming
{
    private static readonly HashSet<char> Invalid = new(Path.GetInvalidFileNameChars());

    /// <summary>Folder used when a track has no audio file (lists loaded from a TXT).</summary>
    public static string FallbackRoot(DownloadOptions options) =>
        !string.IsNullOrWhiteSpace(options.CustomFolder)
            ? options.CustomFolder!
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Phonexis");

    public static string BaseName(SongItem song) =>
        song.SourceAudioPath != null
            ? Path.GetFileNameWithoutExtension(song.SourceAudioPath)
            : SafeFileName(song.OriginalTitle);

    public static string TargetFolder(SongItem song, DownloadOptions options)
    {
        var audioDir = song.SourceAudioPath != null ? Path.GetDirectoryName(song.SourceAudioPath) : null;
        var subfolder = string.IsNullOrWhiteSpace(options.SubfolderName) ? "video" : SafeFileName(options.SubfolderName);

        return options.Destination switch
        {
            VideoDestination.SameFolder => audioDir ?? FallbackRoot(options),
            VideoDestination.VideoSubfolder => Path.Combine(audioDir ?? FallbackRoot(options), subfolder),
            _ => FallbackRoot(options)
        };
    }

    /// <summary>
    /// Applies the existing-file policy. Returns null when the file exists and must be skipped.
    /// </summary>
    public static string? ResolveTarget(string folder, string baseName, string extension, ExistingFilePolicy policy)
    {
        var path = Path.Combine(folder, baseName + extension);
        if (!File.Exists(path) || policy == ExistingFilePolicy.Overwrite) return path;
        if (policy == ExistingFilePolicy.Skip) return null;

        for (var n = 2; n < 1000; n++)
        {
            var candidate = Path.Combine(folder, $"{baseName} ({n}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public static string SafeFileName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(Invalid.Contains(c) || char.IsControl(c) ? ' ' : c);

        var cleaned = string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim(' ', '.');
        if (cleaned.Length > 180) cleaned = cleaned[..180].TrimEnd(' ', '.');
        return cleaned.Length == 0 ? "video" : cleaned;
    }
}
