using System.Windows;
using System.Windows.Input;

namespace Phonexis.Helpers;

/// <summary>
/// Wires the custom title bar: caption button commands, and the padding a WindowChrome
/// window needs when maximized so its edges don't slide off-screen.
/// </summary>
public static class WindowChromeHelper
{
    public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached(
        "Enable", typeof(bool), typeof(WindowChromeHelper), new PropertyMetadata(false, OnEnableChanged));

    public static bool GetEnable(DependencyObject d) => (bool)d.GetValue(EnableProperty);
    public static void SetEnable(DependencyObject d, bool value) => d.SetValue(EnableProperty, value);

    private static readonly DependencyProperty NormalBorderProperty = DependencyProperty.RegisterAttached(
        "NormalBorder", typeof(Thickness), typeof(WindowChromeHelper), new PropertyMetadata(new Thickness()));

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window w || e.NewValue is not true) return;

        w.CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => SystemCommands.CloseWindow(w)));
        w.CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, (_, _) => SystemCommands.MinimizeWindow(w)));
        w.CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, (_, _) => SystemCommands.MaximizeWindow(w)));
        w.CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, (_, _) => SystemCommands.RestoreWindow(w)));

        w.SourceInitialized += (_, _) =>
        {
            w.SetValue(NormalBorderProperty, w.BorderThickness);
            UpdateBorder(w);
        };
        w.StateChanged += (_, _) => UpdateBorder(w);

        // A dialog dims its owner while open, if the owner provides a "DialogScrim" element.
        w.Loaded += (_, _) => SetOwnerScrim(w, true);
        w.Closed += (_, _) => SetOwnerScrim(w, false);
    }

    private static void SetOwnerScrim(Window w, bool visible)
    {
        if (w.Owner?.FindName("DialogScrim") is not FrameworkElement scrim) return;
        scrim.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void UpdateBorder(Window w)
    {
        if (w.WindowState == WindowState.Maximized)
        {
            var frame = SystemParameters.WindowResizeBorderThickness;
            w.BorderThickness = new Thickness(frame.Left + 4, frame.Top + 4, frame.Right + 4, frame.Bottom + 4);
        }
        else
        {
            w.BorderThickness = (Thickness)w.GetValue(NormalBorderProperty);
        }
    }
}
