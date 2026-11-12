using Phonexis.Models;
using Phonexis.Services;

namespace Phonexis.Tests;

public class ListFileServiceTests
{
    [Fact]
    public void Save_and_load_keep_everything_a_txt_loses()
    {
        using var temp = new TempFolder();
        var path = Path.Combine(temp.Path, "set.phonexis");
        var state = new SessionState
        {
            CurrentIndex = 1,
            Songs =
            {
                new SessionSong
                {
                    OriginalTitle = "Bicep - Glue", Query = "Bicep Glue official", Status = SongStatus.Done,
                    SelectedUrl = "https://www.youtube.com/watch?v=abcdefghijk", SelectedTitle = "Bicep - Glue (Official Video)",
                    SourceAudioPath = @"D:\Music\Bicep - Glue.flac", SelectedVideoId = "abcdefghijk", SelectedDurationSeconds = 269
                },
                new SessionSong { OriginalTitle = "Moderat - A New Error", Query = "Moderat - A New Error", Status = SongStatus.Skipped }
            }
        };

        ListFileService.Save(path, state);
        var loaded = ListFileService.Load(path);

        Assert.Equal(ListFileService.FormatName, loaded.Format);
        Assert.Equal(1, loaded.CurrentIndex);
        Assert.Equal(2, loaded.Songs.Count);
        var first = loaded.Songs[0];
        Assert.Equal("Bicep Glue official", first.Query);
        Assert.Equal(SongStatus.Done, first.Status);
        Assert.Equal(@"D:\Music\Bicep - Glue.flac", first.SourceAudioPath);
        Assert.Equal("abcdefghijk", first.SelectedVideoId);
        Assert.Equal(269, first.SelectedDurationSeconds);
        Assert.Equal(SongStatus.Skipped, loaded.Songs[1].Status);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Saved_file_is_readable_json_with_enum_names()
    {
        using var temp = new TempFolder();
        var path = Path.Combine(temp.Path, "a.phonexis");
        ListFileService.Save(path, ListFileService.FromTitles(new[] { "One" }, null));
        var text = File.ReadAllText(path);
        Assert.Contains("\"Format\": \"phonexis-list\"", text);
        Assert.Contains("\"Status\": \"Pending\"", text);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"Songs\": []}")]
    [InlineData("{\"Songs\": [{\"OriginalTitle\": \"\"}]}")]
    public void Load_rejects_files_that_are_not_lists(string content)
    {
        using var temp = new TempFolder();
        var path = temp.File("bad.phonexis", content);
        Assert.Throws<InvalidDataException>(() => ListFileService.Load(path));
    }

    [Fact]
    public void Load_fills_a_missing_query_with_the_title()
    {
        using var temp = new TempFolder();
        var path = temp.File("old.phonexis", "{\"Songs\": [{\"OriginalTitle\": \"Song A\"}]}");
        Assert.Equal("Song A", ListFileService.Load(path).Songs[0].Query);
    }

    [Fact]
    public void FromTitles_pairs_titles_with_audio_paths()
    {
        var state = ListFileService.FromTitles(new[] { "A", "B" }, new[] { @"C:\a.mp3" });
        Assert.Equal(@"C:\a.mp3", state.Songs[0].SourceAudioPath);
        Assert.Null(state.Songs[1].SourceAudioPath);
        Assert.All(state.Songs, s => Assert.Equal(SongStatus.Pending, s.Status));
    }

    [Theory]
    [InlineData(@"C:\x\set.phonexis", true)]
    [InlineData(@"C:\x\SET.PHONEXIS", true)]
    [InlineData(@"C:\x\set.txt", false)]
    public void IsListFile_checks_the_extension(string path, bool expected) =>
        Assert.Equal(expected, ListFileService.IsListFile(path));
}
