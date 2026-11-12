using Phonexis.Models;
using Phonexis.Services.Downloads;

namespace Phonexis.Tests;

public class VideoNamingTests
{
    [Theory]
    [InlineData("AC/DC: Back In Black?  *live*", "AC DC Back In Black live")]
    [InlineData("  trailing dots...  ", "trailing dots")]
    [InlineData("<>:\"/\\|?*", "video")]
    [InlineData("Bicep - Glue", "Bicep - Glue")]
    public void SafeFileName_removes_characters_Windows_rejects(string input, string expected) =>
        Assert.Equal(expected, VideoNaming.SafeFileName(input));

    [Fact]
    public void SafeFileName_caps_length()
    {
        var name = VideoNaming.SafeFileName(new string('a', 400));
        Assert.True(name.Length <= 180);
    }

    [Fact]
    public void BaseName_uses_the_audio_file_name_when_known()
    {
        var song = new SongItem { OriginalTitle = "line from txt", SourceAudioPath = @"D:\Music\Bicep - Glue.flac" };
        Assert.Equal("Bicep - Glue", VideoNaming.BaseName(song));
    }

    [Fact]
    public void BaseName_falls_back_to_the_list_line()
    {
        var song = new SongItem { OriginalTitle = "Daft Punk: One More Time" };
        Assert.Equal("Daft Punk One More Time", VideoNaming.BaseName(song));
    }

    [Theory]
    [InlineData(VideoDestination.SameFolder, @"D:\Music")]
    [InlineData(VideoDestination.VideoSubfolder, @"D:\Music\video")]
    [InlineData(VideoDestination.CustomFolder, @"E:\Videos")]
    public void TargetFolder_follows_the_destination(VideoDestination destination, string expected)
    {
        var song = new SongItem { OriginalTitle = "x", SourceAudioPath = @"D:\Music\Bicep - Glue.flac" };
        var options = new DownloadOptions { Destination = destination, CustomFolder = @"E:\Videos", SubfolderName = "video" };
        Assert.Equal(expected, VideoNaming.TargetFolder(song, options));
    }

    [Fact]
    public void TargetFolder_without_audio_file_uses_the_custom_folder()
    {
        var song = new SongItem { OriginalTitle = "x" };
        var options = new DownloadOptions { Destination = VideoDestination.SameFolder, CustomFolder = @"E:\Videos" };
        Assert.Equal(@"E:\Videos", VideoNaming.TargetFolder(song, options));
    }

    [Fact]
    public void ResolveTarget_applies_the_existing_file_policy()
    {
        using var temp = new TempFolder();
        var existing = temp.File("Song.mp4");

        Assert.Null(VideoNaming.ResolveTarget(temp.Path, "Song", ".mp4", ExistingFilePolicy.Skip));
        Assert.Equal(existing, VideoNaming.ResolveTarget(temp.Path, "Song", ".mp4", ExistingFilePolicy.Overwrite));
        Assert.Equal(Path.Combine(temp.Path, "Song (2).mp4"), VideoNaming.ResolveTarget(temp.Path, "Song", ".mp4", ExistingFilePolicy.AddSuffix));
        Assert.Equal(Path.Combine(temp.Path, "Other.mp4"), VideoNaming.ResolveTarget(temp.Path, "Other", ".mp4", ExistingFilePolicy.Skip));
    }
}
