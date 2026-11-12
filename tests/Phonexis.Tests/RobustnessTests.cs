using Phonexis.Models;
using Phonexis.Services.Downloads;

namespace Phonexis.Tests;

public class FileDatesTests
{
    private static readonly DateTime Created = new(2015, 3, 14, 9, 26, 53, DateTimeKind.Utc);
    private static readonly DateTime Modified = new(2019, 11, 2, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Video_gets_the_audio_files_dates()
    {
        using var temp = new TempFolder();
        var audio = temp.File("Song.flac");
        File.SetCreationTimeUtc(audio, Created);
        File.SetLastWriteTimeUtc(audio, Modified);
        var video = temp.File(@"video\Song.mp4");

        Assert.True(FileDates.CopyFromAudio(audio, video));
        Assert.Equal(Created, File.GetCreationTimeUtc(video));
        Assert.Equal(Modified, File.GetLastWriteTimeUtc(video));
    }

    [Fact]
    public void Missing_audio_leaves_the_video_alone()
    {
        using var temp = new TempFolder();
        var video = temp.File("Song.mp4");
        var before = File.GetLastWriteTimeUtc(video);
        Assert.False(FileDates.CopyFromAudio(Path.Combine(temp.Path, "missing.flac"), video));
        Assert.False(FileDates.CopyFromAudio(null, video));
        Assert.Equal(before, File.GetLastWriteTimeUtc(video));
    }

    [Fact]
    public void Archive_applies_the_dates_when_asked()
    {
        using var temp = new TempFolder();
        var audio = temp.File(@"Music\Bicep - Glue.flac");
        File.SetCreationTimeUtc(audio, Created);
        File.SetLastWriteTimeUtc(audio, Modified);
        temp.File(@"Downloads\clip [x1x2x3x4x5x].mp4");
        var song = new SongItem
        {
            OriginalTitle = "Bicep - Glue", Query = "Bicep - Glue", SelectedUrl = "u", SelectedTitle = "Bicep - Glue",
            SelectedVideoId = "x1x2x3x4x5x", SourceAudioPath = audio
        };

        var plan = ArchiveService.Plan(Path.Combine(temp.Path, "Downloads"), new[] { song },
            new DownloadOptions { Destination = VideoDestination.SameFolder }, false);
        ArchiveService.Execute(plan, Path.Combine(temp.Path, "logs"), matchAudioDates: true);

        var target = Path.Combine(temp.Path, "Music", "Bicep - Glue.mp4");
        Assert.Equal(Modified, File.GetLastWriteTimeUtc(target));
        Assert.Equal(Created, File.GetCreationTimeUtc(target));
    }
}

public class StallGuardTests
{
    [Fact]
    public async Task Silence_cancels_and_marks_stalled()
    {
        using var guard = new StallGuard(TimeSpan.FromMilliseconds(200), CancellationToken.None);
        await Task.Delay(700);
        Assert.True(guard.Stalled);
        Assert.True(guard.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task Steady_progress_keeps_it_alive()
    {
        using var guard = new StallGuard(TimeSpan.FromMilliseconds(300), CancellationToken.None);
        for (var i = 0; i < 12; i++)
        {
            guard.Touch();
            await Task.Delay(50);
        }
        Assert.False(guard.Stalled);
        Assert.False(guard.Token.IsCancellationRequested);
    }

    [Fact]
    public void User_stop_cancels_without_counting_as_a_stall()
    {
        using var outer = new CancellationTokenSource();
        using var guard = new StallGuard(TimeSpan.FromSeconds(30), outer.Token);
        outer.Cancel();
        Assert.True(guard.Token.IsCancellationRequested);
        Assert.False(guard.Stalled);
    }
}
