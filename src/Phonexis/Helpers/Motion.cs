using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Phonexis.Helpers;

public enum Entrance
{
    None,
    Fade,
    Rise,
    Scale
}

/// <summary>
/// UI-only motion, driven by attached properties so XAML stays declarative.
/// Every animation is skipped when Windows "Show animations" is turned off.
/// </summary>
public static class Motion
{
    // Strong ease-out for entrances and feedback, strong ease-in-out for on-screen movement.
    private static readonly KeySpline EaseOut = new(0.23, 1, 0.32, 1);
    private static readonly KeySpline EaseInOut = new(0.77, 0, 0.175, 1);

    private const int StaggerStepMs = 30;
    private const int StaggerMaxItems = 8;

    public static bool IsEnabled => SystemParameters.ClientAreaAnimation;

    // ===================== Entrance =====================

    public static readonly DependencyProperty EntranceProperty = DependencyProperty.RegisterAttached(
        "Entrance", typeof(Entrance), typeof(Motion), new PropertyMetadata(Entrance.None, OnEntranceChanged));

    public static Entrance GetEntrance(DependencyObject d) => (Entrance)d.GetValue(EntranceProperty);
    public static void SetEntrance(DependencyObject d, Entrance value) => d.SetValue(EntranceProperty, value);

    /// <summary>Extra delay before the entrance, in milliseconds.</summary>
    public static readonly DependencyProperty DelayProperty = DependencyProperty.RegisterAttached(
        "Delay", typeof(int), typeof(Motion), new PropertyMetadata(0));

    public static int GetDelay(DependencyObject d) => (int)d.GetValue(DelayProperty);
    public static void SetDelay(DependencyObject d, int value) => d.SetValue(DelayProperty, value);

    /// <summary>Adds a per-item delay based on the element's index inside its ItemsControl.</summary>
    public static readonly DependencyProperty StaggerProperty = DependencyProperty.RegisterAttached(
        "Stagger", typeof(bool), typeof(Motion), new PropertyMetadata(false));

    public static bool GetStagger(DependencyObject d) => (bool)d.GetValue(StaggerProperty);
    public static void SetStagger(DependencyObject d, bool value) => d.SetValue(StaggerProperty, value);

    private static readonly DependencyProperty LastPlayedProperty = DependencyProperty.RegisterAttached(
        "LastPlayed", typeof(long), typeof(Motion), new PropertyMetadata(0L));

    private static void OnEntranceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe) return;
        fe.IsVisibleChanged -= Entrance_IsVisibleChanged;
        fe.Loaded -= Entrance_Loaded;
        if ((Entrance)e.NewValue == Entrance.None) return;
        fe.IsVisibleChanged += Entrance_IsVisibleChanged;
        fe.Loaded += Entrance_Loaded;
    }

    private static void Entrance_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { IsVisible: true } fe) PlayEntrance(fe);
    }

    private static void Entrance_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && sender is FrameworkElement fe) PlayEntrance(fe);
    }

    public static void PlayEntrance(FrameworkElement fe)
    {
        if (!IsEnabled) return;

        // Loaded and IsVisibleChanged can both fire for the same appearance.
        var now = Environment.TickCount64;
        if (now - (long)fe.GetValue(LastPlayedProperty) < 120) return;
        fe.SetValue(LastPlayedProperty, now);

        var delay = GetDelay(fe);
        if (GetStagger(fe)) delay += Math.Min(IndexInItemsControl(fe), StaggerMaxItems) * StaggerStepMs;

        var (scale, translate) = EnsureTransforms(fe);
        switch (GetEntrance(fe))
        {
            case Entrance.Fade:
                Run(fe, UIElement.OpacityProperty, 0, 1, delay, 180, EaseOut);
                break;
            case Entrance.Rise:
                Run(fe, UIElement.OpacityProperty, 0, 1, delay, 220, EaseOut);
                Run(translate, TranslateTransform.YProperty, 10, 0, delay, 260, EaseOut);
                break;
            case Entrance.Scale:
                fe.RenderTransformOrigin = new Point(0.5, 0.5);
                Run(fe, UIElement.OpacityProperty, 0, 1, delay, 180, EaseOut);
                Run(scale, ScaleTransform.ScaleXProperty, 0.97, 1, delay, 220, EaseOut);
                Run(scale, ScaleTransform.ScaleYProperty, 0.97, 1, delay, 220, EaseOut);
                break;
        }
    }

    private static int IndexInItemsControl(FrameworkElement fe)
    {
        var container = ItemsControl.ContainerFromElement(null, fe);
        if (container == null) return 0;
        var owner = ItemsControl.ItemsControlFromItemContainer(container);
        return owner == null ? 0 : Math.Max(0, owner.ItemContainerGenerator.IndexFromContainer(container));
    }

    // ===================== Press feedback =====================

    public static readonly DependencyProperty PressProperty = DependencyProperty.RegisterAttached(
        "Press", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnPressChanged));

    public static bool GetPress(DependencyObject d) => (bool)d.GetValue(PressProperty);
    public static void SetPress(DependencyObject d, bool value) => d.SetValue(PressProperty, value);

    private static void OnPressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement el) return;
        el.PreviewMouseLeftButtonDown -= Press_Down;
        el.PreviewMouseLeftButtonUp -= Press_Up;
        el.MouseLeave -= Press_Leave;
        if (e.NewValue is not true) return;
        el.PreviewMouseLeftButtonDown += Press_Down;
        el.PreviewMouseLeftButtonUp += Press_Up;
        el.MouseLeave += Press_Leave;
    }

    private static void Press_Down(object sender, MouseButtonEventArgs e)
    {
        if (!IsEnabled || sender is not FrameworkElement { IsEnabled: true } fe) return;
        fe.RenderTransformOrigin = new Point(0.5, 0.5);
        var (scale, _) = EnsureTransforms(fe);
        To(scale, ScaleTransform.ScaleXProperty, 0.97, 100, EaseOut);
        To(scale, ScaleTransform.ScaleYProperty, 0.97, 100, EaseOut);
    }

    private static void Press_Up(object sender, MouseButtonEventArgs e) => Release(sender);
    private static void Press_Leave(object sender, MouseEventArgs e) => Release(sender);

    private static void Release(object sender)
    {
        if (sender is not FrameworkElement fe || !HasOwnTransforms(fe)) return;
        var (scale, _) = EnsureTransforms(fe);
        To(scale, ScaleTransform.ScaleXProperty, 1, 160, EaseOut);
        To(scale, ScaleTransform.ScaleYProperty, 1, 160, EaseOut);
    }

    // ===================== Vertical fill (the obi rail) =====================

    /// <summary>
    /// Fraction 0..1. The element is a full-height band that slides up from the bottom of its
    /// (clipped) parent: 0 = hidden below, 1 = fully risen.
    /// </summary>
    public static readonly DependencyProperty FillProperty = DependencyProperty.RegisterAttached(
        "Fill", typeof(double), typeof(Motion), new PropertyMetadata(double.NaN, OnFillChanged));

    public static double GetFill(DependencyObject d) => (double)d.GetValue(FillProperty);
    public static void SetFill(DependencyObject d, double value) => d.SetValue(FillProperty, value);

    private static void OnFillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe) return;
        fe.SizeChanged -= Fill_SizeChanged;
        fe.SizeChanged += Fill_SizeChanged;
        ApplyFill(fe, animate: fe.IsLoaded);
    }

    private static void Fill_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is FrameworkElement fe) ApplyFill(fe, animate: false);
    }

    private static void ApplyFill(FrameworkElement fe, bool animate)
    {
        var (_, translate) = EnsureTransforms(fe);
        var value = GetFill(fe);
        var fraction = double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);
        var target = (1 - fraction) * fe.ActualHeight;
        if (animate && IsEnabled)
            To(translate, TranslateTransform.YProperty, target, 420, EaseInOut);
        else
        {
            translate.BeginAnimation(TranslateTransform.YProperty, null);
            translate.Y = target;
        }
    }

    // ===================== Skeleton shimmer =====================

    public static readonly DependencyProperty ShimmerProperty = DependencyProperty.RegisterAttached(
        "Shimmer", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnShimmerChanged));

    public static bool GetShimmer(DependencyObject d) => (bool)d.GetValue(ShimmerProperty);
    public static void SetShimmer(DependencyObject d, bool value) => d.SetValue(ShimmerProperty, value);

    private static void OnShimmerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Border border) return;
        border.IsVisibleChanged -= Shimmer_IsVisibleChanged;
        if (e.NewValue is not true) return;
        border.IsVisibleChanged += Shimmer_IsVisibleChanged;
    }

    private static void Shimmer_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not Border border) return;
        if (e.NewValue is not true || !IsEnabled)
        {
            if (border.Background is LinearGradientBrush { RelativeTransform: TranslateTransform old })
                old.BeginAnimation(TranslateTransform.XProperty, null);
            border.Background = Brushes.Transparent;
            return;
        }

        var highlight = border.TryFindResource("Color.Shimmer") is Color c ? c : Color.FromArgb(0x60, 255, 255, 255);
        var clear = Color.FromArgb(0, highlight.R, highlight.G, highlight.B);
        var slide = new TranslateTransform(-1, 0);
        border.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            RelativeTransform = slide,
            GradientStops =
            {
                new GradientStop(clear, 0.25),
                new GradientStop(highlight, 0.5),
                new GradientStop(clear, 0.75)
            }
        };
        slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-1, 1, TimeSpan.FromMilliseconds(1400))
        {
            RepeatBehavior = RepeatBehavior.Forever
        });
    }

    // ===================== Plumbing =====================

    private static bool HasOwnTransforms(UIElement el) => el.RenderTransform is TransformGroup { Children.Count: >= 2 } g
                                                          && g.Children[^2] is ScaleTransform && g.Children[^1] is TranslateTransform;

    private static (ScaleTransform scale, TranslateTransform translate) EnsureTransforms(UIElement el)
    {
        if (HasOwnTransforms(el))
        {
            var g = (TransformGroup)el.RenderTransform;
            return ((ScaleTransform)g.Children[^2], (TranslateTransform)g.Children[^1]);
        }

        var group = new TransformGroup();
        if (el.RenderTransform is { } existing && !existing.Value.IsIdentity) group.Children.Add(existing);
        var scale = new ScaleTransform();
        var translate = new TranslateTransform();
        group.Children.Add(scale);
        group.Children.Add(translate);
        el.RenderTransform = group;
        return (scale, translate);
    }

    /// <summary>Holds <paramref name="from"/> through the delay, then eases to <paramref name="to"/>.</summary>
    private static DoubleAnimationUsingKeyFrames Keys(double from, double to, int delayMs, int durationMs, KeySpline spline)
    {
        var anim = new DoubleAnimationUsingKeyFrames();
        anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        if (delayMs > 0)
            anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delayMs))));
        anim.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delayMs + durationMs)), spline));
        return anim;
    }

    /// <summary>One-shot entrance: animates, then hands the property back to its base value.</summary>
    private static void Run(IAnimatable target, DependencyProperty property, double from, double to,
                            int delayMs, int durationMs, KeySpline spline)
    {
        var anim = Keys(from, to, delayMs, durationMs, spline);
        anim.Completed += (_, _) => target.BeginAnimation(property, null);
        target.BeginAnimation(property, anim);
    }

    /// <summary>Retargetable transition from the current value; interrupting it is safe.</summary>
    private static void To(IAnimatable target, DependencyProperty property, double to, int durationMs, KeySpline spline)
    {
        var anim = new DoubleAnimationUsingKeyFrames();
        anim.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs)), spline));
        target.BeginAnimation(property, anim, HandoffBehavior.SnapshotAndReplace);
    }
}
