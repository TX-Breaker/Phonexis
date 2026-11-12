using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Phonexis.Models;
using Phonexis.Services;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace Phonexis.Views;

/// <summary>
/// Plays a result inside Phonexis. Streams are resolved with YoutubeExplode and played by a local page in
/// WebView2 (video + audio kept in sync), which also works for videos whose owners disable embedding;
/// when no stream can be resolved it falls back to YouTube's embedded player.
/// </summary>
public partial class PreviewDialog : Window
{
    private const string HostName = "phonexis.preview";
    private readonly SearchResult _result;
    private readonly CancellationTokenSource _cts = new();

    public PreviewDialog(SearchResult result)
    {
        InitializeComponent();
        _result = result;
        VideoTitle.Text = result.Title;
        VideoMeta.Text = string.Join("  ·  ", new[] { result.ChannelTitle, result.DurationText, result.PublishedText }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        Loaded += async (_, _) => await LoadAsync();
        Closed += (_, _) =>
        {
            _cts.Cancel();
            Player.Dispose(); // stops playback immediately
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
    }

    public static string PlayerFolder => Path.Combine(AppPaths.Root, "preview");

    private async Task LoadAsync()
    {
        try
        {
            // The user opened the preview on purpose: let it start with sound.
            var options = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(AppPaths.Root, "webview2"), options);
            await Player.EnsureCoreWebView2Async(env);
            Directory.CreateDirectory(PlayerFolder);
            Player.CoreWebView2.SetVirtualHostNameToFolderMapping(HostName, PlayerFolder, CoreWebView2HostResourceAccessKind.Allow);
            Player.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Player.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Player.CoreWebView2.NavigationCompleted += (_, _) => LoadingPanel.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or COMException)
        {
            LoadingText.Text = I18n.Instance["PvNoRuntime"];
            return;
        }

        string html;
        try
        {
            html = await BuildStreamPageAsync(_cts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            html = BuildEmbedPage();
        }
        if (_cts.IsCancellationRequested) return;

        var file = $"player-{Guid.NewGuid():N}.html";
        foreach (var old in Directory.EnumerateFiles(PlayerFolder, "player-*.html")) TryDelete(old);
        await File.WriteAllTextAsync(Path.Combine(PlayerFolder, file), html);
        Player.CoreWebView2.Navigate($"https://{HostName}/{file}");
    }

    private async Task<string> BuildStreamPageAsync(CancellationToken ct)
    {
        using var youtube = new YoutubeClient();
        var manifest = await youtube.Videos.Streams.GetManifestAsync(_result.VideoId, ct);

        var muxed = manifest.GetMuxedStreams().OrderByDescending(s => s.VideoQuality.MaxHeight).FirstOrDefault();
        var video = manifest.GetVideoOnlyStreams()
            .Where(s => s.VideoQuality.MaxHeight <= 720)
            .OrderByDescending(s => s.VideoQuality.MaxHeight)
            .ThenBy(s => s.Container == Container.Mp4 ? 0 : 1)
            .FirstOrDefault();
        var audio = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();

        if (video != null && audio != null && (muxed == null || video.VideoQuality.MaxHeight > muxed.VideoQuality.MaxHeight))
            return PlayerPage(video.Url, audio.Url);
        if (muxed != null)
            return PlayerPage(muxed.Url, null);
        throw new InvalidOperationException("No playable stream");
    }

    /// <summary>Native video controls; a hidden audio element follows the video (play, pause, seek, rate, volume).</summary>
    private static string PlayerPage(string videoUrl, string? audioUrl)
    {
        var v = WebUtility.HtmlEncode(videoUrl);
        var audioTag = audioUrl == null ? string.Empty : $"<audio id=\"a\" src=\"{WebUtility.HtmlEncode(audioUrl)}\" preload=\"auto\"></audio>";
        return $$"""
            <!doctype html><html><head><meta charset="utf-8">
            <style>html,body{margin:0;height:100%;background:#000;overflow:hidden}video{width:100%;height:100%;object-fit:contain;background:#000}</style>
            </head><body>
            <video id="v" src="{{v}}" controls autoplay {{(audioUrl == null ? "" : "muted")}} playsinline></video>
            {{audioTag}}
            <script>
            const v = document.getElementById('v'), a = document.getElementById('a');
            if (a) {
              const sync = () => { if (Math.abs(a.currentTime - v.currentTime) > 0.25) a.currentTime = v.currentTime; };
              v.addEventListener('play', () => { sync(); a.play(); });
              v.addEventListener('pause', () => a.pause());
              v.addEventListener('waiting', () => a.pause());
              v.addEventListener('playing', () => { sync(); a.play(); });
              v.addEventListener('seeked', sync);
              v.addEventListener('ratechange', () => a.playbackRate = v.playbackRate);
              // The video stays muted: its volume slider drives the audio element and its mute button toggles it.
              let guard = false;
              v.addEventListener('volumechange', () => {
                if (guard) return;
                a.volume = v.volume;
                if (!v.muted) { a.muted = !a.muted; guard = true; v.muted = true; guard = false; }
              });
              setInterval(sync, 1000);
            }
            </script></body></html>
            """;
    }

    private string BuildEmbedPage() => $$"""
        <!doctype html><html><head><meta charset="utf-8">
        <style>html,body{margin:0;height:100%;background:#000;overflow:hidden}iframe{border:0;width:100%;height:100%}</style>
        </head><body>
        <iframe src="https://www.youtube-nocookie.com/embed/{{WebUtility.UrlEncode(_result.VideoId)}}?autoplay=1&rel=0"
                allow="autoplay; encrypted-media; picture-in-picture" allowfullscreen></iframe>
        </body></html>
        """;

    private void Select_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = _result.VideoUrl, UseShellExecute = true });
        }
        catch
        {
            // non-fatal
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
