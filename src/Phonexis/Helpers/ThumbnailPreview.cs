using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Phonexis.Services.Previews;

namespace Phonexis.Helpers;

/// <summary>
/// YouTube-style hover preview: after a short hover the thumbnail plays frames from across the video,
/// with a thin progress line. Attach to the thumbnail's Grid with the video ID.
/// </summary>
public static class ThumbnailPreview
{
    private const int HoverDelayMs = 350;
    private const int FrameMs = 550;

    public static readonly DependencyProperty VideoIdProperty = DependencyProperty.RegisterAttached(
        "VideoId", typeof(string), typeof(ThumbnailPreview), new PropertyMetadata(null, OnVideoIdChanged));

    public static string? GetVideoId(DependencyObject d) => (string?)d.GetValue(VideoIdProperty);
    public static void SetVideoId(DependencyObject d, string? value) => d.SetValue(VideoIdProperty, value);

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(PreviewState), typeof(ThumbnailPreview));

    private sealed class PreviewState
    {
        public required Image Frame { get; init; }
        public required Border Progress { get; init; }
        public required ScaleTransform ProgressScale { get; init; }
        public DispatcherTimer? Timer;
        public IReadOnlyList<BitmapSource> Frames = Array.Empty<BitmapSource>();
        public int Index;
        public int Generation;
    }

    private static void OnVideoIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Grid grid) return;
        grid.MouseEnter -= OnEnter;
        grid.MouseLeave -= OnLeave;
        grid.SizeChanged -= OnSizeChanged;
        Stop(grid);
        if (e.NewValue is not string { Length: > 0 }) return;
        grid.MouseEnter += OnEnter;
        grid.MouseLeave += OnLeave;
        grid.SizeChanged += OnSizeChanged;
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Keep frames inside the thumbnail's rounded corners.
        if (sender is Grid grid)
            grid.Clip = new RectangleGeometry(new Rect(e.NewSize), 6, 6);
    }

    private static PreviewState EnsureState(Grid grid)
    {
        if (grid.GetValue(StateProperty) is PreviewState existing) return existing;

        var frame = new Image { Stretch = Stretch.UniformToFill, Opacity = 0, IsHitTestVisible = false };
        RenderOptions.SetBitmapScalingMode(frame, BitmapScalingMode.HighQuality);
        var scale = new ScaleTransform(0, 1);
        var progress = new Border
        {
            Height = 3,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RenderTransform = scale,
            RenderTransformOrigin = new Point(0, 0.5),
            Opacity = 0,
            IsHitTestVisible = false
        };
        progress.SetResourceReference(Border.BackgroundProperty, "Brush.Brand");

        // Frames go above the static thumbnail but under the duration and key badges.
        grid.Children.Insert(Math.Min(2, grid.Children.Count), frame);
        grid.Children.Add(progress);
        grid.Clip ??= new RectangleGeometry(new Rect(new Size(grid.ActualWidth, grid.ActualHeight)), 6, 6);

        var state = new PreviewState { Frame = frame, Progress = progress, ProgressScale = scale };
        grid.SetValue(StateProperty, state);
        return state;
    }

    private static async void OnEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Grid grid || GetVideoId(grid) is not { } id) return;
        var state = EnsureState(grid);
        var generation = ++state.Generation;

        await Task.Delay(HoverDelayMs);
        if (generation != state.Generation || !grid.IsMouseOver) return; // passed by without stopping

        var frames = await StoryboardService.GetFramesAsync(id);
        if (generation != state.Generation || !grid.IsMouseOver || frames.Count == 0) return;

        state.Frames = frames;
        state.Index = 0;
        ShowFrame(state, animate: false);
        Fade(state.Frame, 1, 160);
        Fade(state.Progress, 1, 160);

        state.Timer?.Stop();
        state.Timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FrameMs) };
        state.Timer.Tick += (_, _) =>
        {
            state.Index = (state.Index + 1) % state.Frames.Count;
            ShowFrame(state, animate: true);
        };
        state.Timer.Start();
    }

    private static void OnLeave(object sender, MouseEventArgs e)
    {
        if (sender is Grid grid) Stop(grid);
    }

    private static void Stop(Grid grid)
    {
        if (grid.GetValue(StateProperty) is not PreviewState state) return;
        state.Generation++;
        state.Timer?.Stop();
        state.Timer = null;
        Fade(state.Frame, 0, 180);
        Fade(state.Progress, 0, 180);
    }

    private static void ShowFrame(PreviewState state, bool animate)
    {
        state.Frame.Source = state.Frames[state.Index];
        var target = (state.Index + 1) / (double)state.Frames.Count;
        if (animate && Motion.IsEnabled)
        {
            state.ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(target, TimeSpan.FromMilliseconds(FrameMs - 50)));
        }
        else
        {
            state.ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            state.ProgressScale.ScaleX = target;
        }
    }

    private static void Fade(UIElement element, double to, int ms)
    {
        if (!Motion.IsEnabled)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = to;
            return;
        }
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }
}
