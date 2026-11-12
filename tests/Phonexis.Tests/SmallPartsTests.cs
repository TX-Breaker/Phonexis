using Phonexis.Models;
using Phonexis.Services;
using Phonexis.Services.Previews;

namespace Phonexis.Tests;

public class YouTubeUrlTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?feature=share&v=dQw4w9WgXcQ&t=42", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=short", null)]
    [InlineData("https://vimeo.com/123456", null)]
    [InlineData("not a url", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TryGetVideoId_reads_common_link_shapes(string? url, string? expected) =>
        Assert.Equal(expected, YouTubeUrl.TryGetVideoId(url));
}

public class StoryboardSpecTests
{
    // Shape of a real spec (signatures shortened).
    private const string Spec =
        "https://i.ytimg.com/sb/dQw4w9WgXcQ/storyboard3_L$L/$N.jpg?sqp=abc|48#27#100#10#10#0#default#rs$A0" +
        "|80#45#108#10#10#2000#M$M#rs$A1|160#90#108#5#5#2000#M$M#rs$A2|320#180#108#3#3#2000#M$M#rs$A3";

    [Fact]
    public void ParseSpec_picks_level_two_and_spreads_frames_over_the_video()
    {
        var plan = StoryboardService.ParseSpec(Spec, 10)!;
        Assert.Equal(2, plan.Level);
        Assert.Equal((160, 90), (plan.FrameWidth, plan.FrameHeight));
        Assert.Equal(25, plan.FramesPerSheet);
        Assert.Equal(10, plan.Frames.Count);
        Assert.All(plan.Frames, f => Assert.InRange(f, 0, 107));
        Assert.Equal(plan.Frames.OrderBy(f => f), plan.Frames);
        Assert.True(plan.Frames[^1] > 90, "frames should reach the end of the video");
    }

    [Fact]
    public void ParseSpec_builds_signed_sheet_urls()
    {
        var plan = StoryboardService.ParseSpec(Spec)!;
        Assert.Equal("https://i.ytimg.com/sb/dQw4w9WgXcQ/storyboard3_L2/M3.jpg?sqp=abc&sigh=rs$A2", plan.SheetUrl(3));
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://x|1#2")]
    [InlineData("https://x|a#b#c#d#e#f#g#h|a#b#c#d#e#f#g#h")]
    public void ParseSpec_rejects_unusable_specs(string spec) => Assert.Null(StoryboardService.ParseSpec(spec));
}

public class SearchFiltersTests
{
    private static SearchResult R(int? seconds, long views, int daysAgo) => new()
    {
        DurationSeconds = seconds, ViewCount = views, PublishedAt = DateTimeOffset.Now.AddDays(-daysAgo)
    };

    [Fact]
    public void Duration_filter_keeps_unknown_durations()
    {
        var f = new SearchFilters { Duration = DurationFilter.Short };
        Assert.True(f.Passes(R(180, 0, 10)));
        Assert.False(f.Passes(R(600, 0, 10)));
        Assert.True(f.Passes(R(null, 0, 10)));
    }

    [Fact]
    public void Views_and_date_filters_combine()
    {
        var f = new SearchFilters { Views = ViewsFilter.High, Date = DateFilter.Older };
        Assert.True(f.Passes(R(200, 2_000_000, 800)));
        Assert.False(f.Passes(R(200, 2_000_000, 30)));
        Assert.False(f.Passes(R(200, 5_000, 800)));
    }
}
