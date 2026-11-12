using System.IO;
using Phonexis.Models;
using Phonexis.Services.Downloads;

namespace Phonexis.ViewModels;

/// <summary>Preselection support: the audio file's duration feeds ResultRanker; Enter picks the recommendation.</summary>
public partial class MainViewModel
{
    private readonly Dictionary<string, double?> _audioDurations = new(StringComparer.OrdinalIgnoreCase);

    public SearchResult? RecommendedResult => Results.FirstOrDefault(r => r.IsRecommended);

    private async Task<double?> GetAudioSecondsAsync(SongItem song)
    {
        if (song.SourceAudioPath is not { } path || !File.Exists(path)) return null;
        if (_audioDurations.TryGetValue(path, out var known)) return known;

        var ffprobe = ToolLocator.FindFfprobe(ToolLocator.FindFfmpeg(_settings.Settings.Download.FfmpegPath));
        var seconds = ffprobe == null ? null : await Task.Run(() => ToolLocator.ProbeDurationSeconds(ffprobe, path));
        _audioDurations[path] = seconds;
        return seconds;
    }

    public bool PickRecommended() =>
        IsResultsVisible && !IsSearching && RecommendedResult is { } r && Execute(SelectVideoCommand, r);
}
