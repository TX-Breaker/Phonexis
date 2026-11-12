using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Phonexis.Models;

namespace Phonexis.Services.Downloads;

/// <summary>One downloaded file and where Rename &amp; archive proposes to put it.</summary>
public sealed class ArchiveMatch
{
    public required string SourceFile { get; init; }
    public SongItem? Song { get; init; }
    public string? TargetPath { get; set; }

    /// <summary>"id", "title" or "none".</summary>
    public required string MatchedBy { get; init; }
    public double Score { get; init; }
    public bool Include { get; set; }

    public string SourceName => Path.GetFileName(SourceFile);
    public string TargetName => TargetPath == null ? string.Empty : Path.GetFileName(TargetPath);
    public string TargetFolder => TargetPath == null ? string.Empty : Path.GetDirectoryName(TargetPath) ?? string.Empty;
    public string SongLabel => Song == null ? string.Empty : $"{Song.DisplayNumber:000}  {Song.OriginalTitle}";
}

public sealed record ArchiveMove(string From, string To);

/// <summary>
/// Matches files saved by any downloader to picked songs (by video ID in the file name, otherwise by
/// title similarity confirmed with duration), then moves and renames them after the track. Every run
/// writes a log so it can be undone.
/// </summary>
public static partial class ArchiveService
{
    private static readonly string[] VideoExtensions = { ".mp4", ".mkv", ".webm", ".mov", ".m4v", ".avi", ".flv", ".3gp" };

    public static string LogFolder => Path.Combine(AppPaths.Root, "archive-logs");

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+")]
    private static partial Regex NonWord();

    public static List<ArchiveMatch> Plan(string folder, IEnumerable<SongItem> songs, DownloadOptions options, bool includeSubfolders)
    {
        var picked = songs.Where(s => s.SelectedUrl != null).ToList();
        var files = Directory.EnumerateFiles(folder, "*.*",
                includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(f => VideoExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var ffprobe = ToolLocator.FindFfprobe(ToolLocator.FindFfmpeg(options.FfmpegPath));
        var claimed = new HashSet<SongItem>();
        var results = new List<ArchiveMatch>();

        // Pass 1: video ID in the file name is unambiguous.
        var remaining = new List<string>();
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var song = picked.FirstOrDefault(s => !claimed.Contains(s) && s.SelectedVideoId is { Length: 11 } id &&
                                                  name.Contains(id, StringComparison.Ordinal));
            if (song == null) { remaining.Add(file); continue; }
            claimed.Add(song);
            results.Add(Build(file, song, "id", 1, options));
        }

        // Pass 2: best title match, highest score first, duration as a veto when ffprobe is available.
        var candidates = new List<(string File, SongItem Song, double Score)>();
        foreach (var file in remaining)
        {
            var fileTokens = Tokens(Path.GetFileNameWithoutExtension(file));
            foreach (var song in picked.Where(s => !claimed.Contains(s)))
            {
                var score = Math.Max(Similarity(fileTokens, Tokens(song.SelectedTitle ?? "")),
                                     Similarity(fileTokens, Tokens(song.OriginalTitle)) * 0.9);
                if (score >= 0.5) candidates.Add((file, song, score));
            }
        }

        var usedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var durations = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (file, song, score) in candidates.OrderByDescending(c => c.Score))
        {
            if (usedFiles.Contains(file) || claimed.Contains(song)) continue;
            if (ffprobe != null && song.SelectedDurationSeconds is int expected && score < 0.95)
            {
                if (!durations.TryGetValue(file, out var actual))
                    durations[file] = actual = ToolLocator.ProbeDurationSeconds(ffprobe, file);
                if (actual is double seconds && Math.Abs(seconds - expected) > 5) continue;
            }
            usedFiles.Add(file);
            claimed.Add(song);
            results.Add(Build(file, song, "title", score, options));
        }

        foreach (var file in remaining.Where(f => !usedFiles.Contains(f)))
            results.Add(new ArchiveMatch { SourceFile = file, MatchedBy = "none", Include = false });

        return results.OrderBy(r => r.Song?.Index ?? int.MaxValue).ThenBy(r => r.SourceName).ToList();
    }

    private static ArchiveMatch Build(string file, SongItem song, string by, double score, DownloadOptions options)
    {
        var folder = VideoNaming.TargetFolder(song, options);
        var ext = Path.GetExtension(file).ToLowerInvariant();
        var desired = Path.Combine(folder, VideoNaming.BaseName(song) + ext);
        var target = string.Equals(desired, file, StringComparison.OrdinalIgnoreCase)
            ? desired
            : VideoNaming.ResolveTarget(folder, VideoNaming.BaseName(song), ext, options.ExistingFiles);
        return new ArchiveMatch
        {
            SourceFile = file,
            Song = song,
            TargetPath = target,
            MatchedBy = by,
            Score = score,
            Include = target != null
        };
    }

    /// <summary>Moves the included files; returns the moves done and writes the undo log.</summary>
    public static List<ArchiveMove> Execute(IEnumerable<ArchiveMatch> plan, string? logFolder = null, bool matchAudioDates = false)
    {
        var moves = new List<ArchiveMove>();
        foreach (var m in plan.Where(m => m.Include && m.TargetPath != null && m.Song != null))
        {
            if (string.Equals(m.SourceFile, m.TargetPath, StringComparison.OrdinalIgnoreCase))
            {
                if (matchAudioDates) FileDates.CopyFromAudio(m.Song!.SourceAudioPath, m.TargetPath!);
                m.Song!.DownloadedPath = m.TargetPath;
                m.Song.DownloadState = DownloadState.Done;
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(m.TargetPath!)!);
            File.Move(m.SourceFile, m.TargetPath!, overwrite: true);
            moves.Add(new ArchiveMove(m.SourceFile, m.TargetPath!));
            if (matchAudioDates) FileDates.CopyFromAudio(m.Song!.SourceAudioPath, m.TargetPath!);
            m.Song!.DownloadedPath = m.TargetPath;
            m.Song.DownloadState = DownloadState.Done;
        }

        if (moves.Count > 0)
        {
            Directory.CreateDirectory(logFolder ?? LogFolder);
            File.WriteAllText(Path.Combine(logFolder ?? LogFolder, $"archive-{DateTime.Now:yyyyMMdd-HHmmss}.json"),
                JsonSerializer.Serialize(moves, new JsonSerializerOptions { WriteIndented = true }));
        }
        return moves;
    }

    public static string? LatestLog(string? logFolder = null)
    {
        var folder = logFolder ?? LogFolder;
        return Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "archive-*.json").OrderByDescending(f => f).FirstOrDefault()
            : null;
    }

    /// <summary>Moves files of the latest run back; returns how many were restored.</summary>
    public static int UndoLatest(string? logFolder = null)
    {
        var log = LatestLog(logFolder);
        if (log == null) return 0;
        var moves = JsonSerializer.Deserialize<List<ArchiveMove>>(File.ReadAllText(log)) ?? new();
        var restored = 0;
        foreach (var move in moves.AsEnumerable().Reverse())
        {
            if (!File.Exists(move.To) || File.Exists(move.From)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(move.From)!);
            File.Move(move.To, move.From);
            restored++;
        }
        File.Move(log, log + ".undone", overwrite: true);
        return restored;
    }

    private static HashSet<string> Tokens(string text)
    {
        var normalized = new string(text.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return NonWord().Split(normalized.ToLowerInvariant())
            .Where(t => t.Length > 1 && t is not ("official" or "video" or "audio" or "hd" or "hq" or "lyrics" or "mp4"))
            .ToHashSet();
    }

    private static double Similarity(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var common = a.Count(b.Contains);
        return (double)common / Math.Min(a.Count, b.Count) * 0.7 + (double)common / a.Union(b).Count() * 0.3;
    }
}
