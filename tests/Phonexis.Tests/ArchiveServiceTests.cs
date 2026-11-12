using Phonexis.Models;
using Phonexis.Services.Downloads;

namespace Phonexis.Tests;

public class ArchiveServiceTests
{
    // Durations are left unknown so no test depends on ffprobe being installed.
    private static SongItem Picked(int index, string line, string videoTitle, string id, string audioPath) => new()
    {
        Index = index, OriginalTitle = line, Query = line, SelectedUrl = "https://www.youtube.com/watch?v=" + id,
        SelectedTitle = videoTitle, SelectedVideoId = id, SourceAudioPath = audioPath
    };

    [Fact]
    public void Plan_matches_by_video_id_then_by_title_and_flags_strangers()
    {
        using var temp = new TempFolder();
        var music = Path.Combine(temp.Path, "Music");
        var downloads = Path.Combine(temp.Path, "Downloads");
        var songs = new[]
        {
            Picked(0, "Bicep - Glue", "Bicep - Glue (Official Video)", "x1x2x3x4x5x", Path.Combine(music, "Bicep - Glue.flac")),
            Picked(1, "Moderat - A New Error", "Moderat - A New Error (Official Video)", "AAAAAAAAAAA", Path.Combine(music, "Moderat - A New Error.mp3"))
        };
        temp.File(@"Downloads\Some title [x1x2x3x4x5x].mp4");
        temp.File(@"Downloads\Moderat - A New Error (Official Video).mp4");
        temp.File(@"Downloads\holiday clip.mp4");
        temp.File(@"Downloads\notes.txt");

        var plan = ArchiveService.Plan(downloads, songs, new DownloadOptions { Destination = VideoDestination.VideoSubfolder }, false);

        Assert.Equal(3, plan.Count);
        var byId = Assert.Single(plan, p => p.MatchedBy == "id");
        Assert.Same(songs[0], byId.Song);
        Assert.Equal(Path.Combine(music, "video", "Bicep - Glue.mp4"), byId.TargetPath);

        var byTitle = Assert.Single(plan, p => p.MatchedBy == "title");
        Assert.Same(songs[1], byTitle.Song);
        Assert.Equal(Path.Combine(music, "video", "Moderat - A New Error.mp4"), byTitle.TargetPath);

        var stranger = Assert.Single(plan, p => p.MatchedBy == "none");
        Assert.False(stranger.Include);
    }

    [Fact]
    public void Execute_moves_files_and_undo_puts_them_back()
    {
        using var temp = new TempFolder();
        var logs = Path.Combine(temp.Path, "logs");
        var song = Picked(0, "Bicep - Glue", "Bicep - Glue", "x1x2x3x4x5x", Path.Combine(temp.Path, "Music", "Bicep - Glue.flac"));
        var source = temp.File(@"Downloads\clip [x1x2x3x4x5x].mp4", "video");

        var plan = ArchiveService.Plan(Path.Combine(temp.Path, "Downloads"), new[] { song },
            new DownloadOptions { Destination = VideoDestination.SameFolder }, false);
        var moves = ArchiveService.Execute(plan, logs);

        var target = Path.Combine(temp.Path, "Music", "Bicep - Glue.mp4");
        Assert.Single(moves);
        Assert.True(File.Exists(target));
        Assert.False(File.Exists(source));
        Assert.Equal(DownloadState.Done, song.DownloadState);
        Assert.Equal(target, song.DownloadedPath);
        Assert.NotNull(ArchiveService.LatestLog(logs));

        Assert.Equal(1, ArchiveService.UndoLatest(logs));
        Assert.True(File.Exists(source));
        Assert.False(File.Exists(target));
        Assert.Null(ArchiveService.LatestLog(logs)); // the undone log is no longer "latest"
    }

    [Fact]
    public void Existing_target_is_skipped_under_the_skip_policy()
    {
        using var temp = new TempFolder();
        var song = Picked(0, "Bicep - Glue", "Bicep - Glue", "x1x2x3x4x5x", Path.Combine(temp.Path, "Music", "Bicep - Glue.flac"));
        temp.File(@"Music\Bicep - Glue.mp4", "already there");
        temp.File(@"Downloads\clip [x1x2x3x4x5x].mp4");

        var plan = ArchiveService.Plan(Path.Combine(temp.Path, "Downloads"), new[] { song },
            new DownloadOptions { Destination = VideoDestination.SameFolder, ExistingFiles = ExistingFilePolicy.Skip }, false);

        var match = Assert.Single(plan);
        Assert.Null(match.TargetPath);
        Assert.False(match.Include);
    }
}
