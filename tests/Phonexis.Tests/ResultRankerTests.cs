using Phonexis.Models;
using Phonexis.Services;

namespace Phonexis.Tests;

public class ResultRankerTests
{
    private static SearchResult R(string id, string title, string channel, int seconds, long views) =>
        new() { VideoId = id, Title = title, ChannelTitle = channel, DurationSeconds = seconds, ViewCount = views };

    private static SongItem Song(string line, string? audioPath = null) =>
        new() { OriginalTitle = line, Query = line, SourceAudioPath = audioPath };

    [Fact]
    public void Official_video_with_matching_name_and_duration_wins()
    {
        var results = new List<SearchResult>
        {
            R("A", "Rick Astley - Never Gonna Give You Up (Official Video) (4K Remaster)", "Rick Astley", 213, 1_700_000_000),
            R("B", "Rick Astley - Never Gonna Give You Up (Live at Glastonbury 2023)", "Glastonbury Festival", 236, 4_000_000),
            R("C", "Never Gonna Give You Up - Karaoke Version", "Sing King", 214, 900_000),
            R("D", "Never Gonna Give You Up", "Rick Astley - Topic", 213, 52_000_000),
            R("E", "rick astley - never gonna give you up (slowed + reverb)", "lofi vibes", 262, 3_000_000)
        };

        ResultRanker.Rank(Song("Rick Astley - Never Gonna Give You Up", @"C:\M\Rick Astley - Never Gonna Give You Up.flac"), results, 213.4);

        Assert.Equal("A", Assert.Single(results, r => r.IsRecommended).VideoId);
        var byScore = results.OrderByDescending(r => r.MatchScore).Select(r => r.VideoId).ToList();
        Assert.True(byScore.IndexOf("C") > byScore.IndexOf("D"), "karaoke must rank below the Topic upload");
        Assert.True(byScore.IndexOf("E") > byScore.IndexOf("B"), "slowed + reverb must rank below live");
    }

    [Fact]
    public void Unrequested_versions_are_penalised_but_requested_ones_are_not()
    {
        var live = R("L", "Bicep - Glue (Live)", "Ninja Tune", 300, 2_000_000);
        var official = R("O", "Bicep - Glue (Official Video)", "BICEP", 269, 30_000_000);

        ResultRanker.Rank(Song("Bicep - Glue"), new[] { live, official }, null);
        Assert.True(official.IsRecommended);

        ResultRanker.Rank(Song("Bicep - Glue live"), new[] { live, official }, null);
        Assert.DoesNotContain("live", live.MatchReasons, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Duration_far_from_the_audio_loses_to_a_close_one()
    {
        var far = R("F", "Artist - Song", "Artist", 600, 5_000_000);
        var close = R("C", "Artist - Song", "Artist", 181, 5_000_000);
        ResultRanker.Rank(Song("Artist - Song"), new[] { far, close }, 180);
        Assert.True(close.IsRecommended);
        Assert.False(far.IsRecommended);
    }

    [Fact]
    public void Exact_ties_recommend_nothing()
    {
        var a = R("A", "Artist - Song", "Someone", 200, 1000);
        var b = R("B", "Artist - Song", "Someone", 200, 1000);
        ResultRanker.Rank(Song("Artist - Song"), new[] { a, b }, null);
        Assert.DoesNotContain(new[] { a, b }, r => r.IsRecommended);
    }

    [Fact]
    public void Weak_matches_recommend_nothing()
    {
        var unrelated = R("X", "Cooking pasta at home", "Chef", 600, 1000);
        ResultRanker.Rank(Song("Bicep - Glue"), new[] { unrelated }, null);
        Assert.False(unrelated.IsRecommended);
    }

    [Fact]
    public void Ranking_resets_previous_marks_and_handles_empty_lists()
    {
        var r = R("A", "Artist - Song (Official Video)", "Artist", 200, 1_000_000);
        ResultRanker.Rank(Song("Artist - Song"), new[] { r }, null);
        Assert.True(r.IsRecommended);
        ResultRanker.Rank(Song("Something else entirely"), new[] { r }, null);
        Assert.False(r.IsRecommended);
        ResultRanker.Rank(Song("x"), Array.Empty<SearchResult>(), null);
    }
}
