using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Phonexis.Services.Previews;

/// <summary>
/// Hover frames for a YouTube video. Uses the storyboard YouTube itself shows when scrubbing (a few sprite
/// sheets of small frames), read from the public watch page; falls back to the three still frames
/// (hq1/hq2/hq3) every video has. Results are cached for the session.
/// </summary>
public static class StoryboardService
{
    private const int FramesToShow = 10;
    private const string SpecMarker = "\"playerStoryboardSpecRenderer\":{\"spec\":\"";

    private static readonly HttpClient Http = CreateClient();
    private static readonly Dictionary<string, Task<IReadOnlyList<BitmapSource>>> Cache = new();
    private static readonly object Gate = new();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36");
        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        return http;
    }

    /// <summary>Frames evenly spread over the video (empty when nothing could be loaded).</summary>
    public static Task<IReadOnlyList<BitmapSource>> GetFramesAsync(string videoId)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(videoId, out var cached)) return cached;
            if (Cache.Count > 60) Cache.Clear();
            var task = LoadAsync(videoId);
            Cache[videoId] = task;
            return task;
        }
    }

    private static async Task<IReadOnlyList<BitmapSource>> LoadAsync(string videoId)
    {
        try
        {
            var frames = await LoadStoryboardAsync(videoId);
            if (frames.Count > 0) return frames;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException or NotSupportedException or IOException)
        {
            // Fall through to the still frames.
        }

        var stills = new List<BitmapSource>();
        foreach (var name in new[] { "hq1", "hq2", "hq3" })
        {
            try
            {
                stills.Add(Decode(await Http.GetByteArrayAsync($"https://i.ytimg.com/vi/{videoId}/{name}.jpg")));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException)
            {
                // Missing still: skip it.
            }
        }
        return stills;
    }

    /// <summary>What to fetch for a storyboard: frame size, grid, the frames to show and each sheet's URL.</summary>
    public sealed record StoryboardPlan(int Level, int FrameWidth, int FrameHeight, int Columns, int Rows,
                                        IReadOnlyList<int> Frames, Func<int, string> SheetUrl)
    {
        public int FramesPerSheet => Columns * Rows;
    }

    /// <summary>
    /// Parses YouTube's storyboard spec: "baseUrl|level0|level1|…", each level "w#h#count#cols#rows#interval#name#sigh".
    /// Uses level 2 (160×90) when present. Returns null for a spec it cannot use.
    /// </summary>
    public static StoryboardPlan? ParseSpec(string spec, int framesToShow = FramesToShow)
    {
        var parts = spec.Split('|');
        if (parts.Length < 3) return null;
        var level = Math.Min(2, parts.Length - 2);
        var f = parts[level + 1].Split('#');
        if (f.Length < 8) return null;
        if (!int.TryParse(f[0], out var w) || !int.TryParse(f[1], out var h) || !int.TryParse(f[2], out var count) ||
            !int.TryParse(f[3], out var cols) || !int.TryParse(f[4], out var rows)) return null;
        if (w <= 0 || h <= 0 || count <= 0 || cols <= 0 || rows <= 0 || framesToShow <= 0) return null;

        string baseUrl = parts[0], name = f[6], sigh = f[7];
        var picks = Enumerable.Range(0, framesToShow)
            .Select(i => Math.Min(count - 1, (int)((i + 0.5) * count / framesToShow)))
            .Distinct()
            .ToList();
        return new StoryboardPlan(level, w, h, cols, rows, picks,
            sheet => baseUrl.Replace("$L", level.ToString()).Replace("$N", name.Replace("$M", sheet.ToString())) + "&sigh=" + sigh);
    }

    private static async Task<IReadOnlyList<BitmapSource>> LoadStoryboardAsync(string videoId)
    {
        var spec = await ReadSpecAsync(videoId);
        if (spec == null || ParseSpec(spec) is not { } plan) return Array.Empty<BitmapSource>();

        var sheets = new Dictionary<int, BitmapSource>();
        foreach (var sheetIndex in plan.Frames.Select(p => p / plan.FramesPerSheet).Distinct())
            sheets[sheetIndex] = Decode(await Http.GetByteArrayAsync(plan.SheetUrl(sheetIndex)));

        var frames = new List<BitmapSource>();
        foreach (var p in plan.Frames)
        {
            var sheet = sheets[p / plan.FramesPerSheet];
            var cell = p % plan.FramesPerSheet;
            var x = (cell % plan.Columns) * plan.FrameWidth;
            var y = (cell / plan.Columns) * plan.FrameHeight;
            if (x + plan.FrameWidth > sheet.PixelWidth || y + plan.FrameHeight > sheet.PixelHeight) continue;
            var crop = new CroppedBitmap(sheet, new Int32Rect(x, y, plan.FrameWidth, plan.FrameHeight));
            crop.Freeze();
            frames.Add(crop);
        }
        return frames;
    }

    /// <summary>Streams the watch page and stops as soon as the storyboard spec has been read.</summary>
    private static async Task<string?> ReadSpecAsync(string videoId)
    {
        using var response = await Http.GetAsync($"https://www.youtube.com/watch?v={videoId}&hl=en",
            HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var buffer = new char[32768];
        var window = new StringBuilder();
        int read;
        while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            window.Append(buffer, 0, read);
            var text = window.ToString();
            var start = text.IndexOf(SpecMarker, StringComparison.Ordinal);
            if (start >= 0)
            {
                var from = start + SpecMarker.Length;
                var end = text.IndexOf('"', from);
                if (end > from) return text[from..end].Replace("\\u0026", "&").Replace("\\/", "/");
            }
            else if (window.Length > SpecMarker.Length * 4)
            {
                window.Remove(0, window.Length - SpecMarker.Length * 4); // keep a tail in case the marker spans reads
            }
        }
        return null;
    }

    private static BitmapSource Decode(byte[] bytes)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = new MemoryStream(bytes);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
