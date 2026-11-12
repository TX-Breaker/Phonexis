using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Phonexis.Helpers;
using Phonexis.Services;
using Phonexis.ViewModels;

namespace Phonexis;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settings;

    public MainWindow(MainViewModel viewModel, ISettingsService settings)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settings = settings;
        DataContext = viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        PreviewKeyDown += Window_PreviewKeyDown;
        RestorePlacement();
        ContentRendered += (_, _) =>
        {
            if (_viewModel.NeedsOnboarding) Dispatcher.BeginInvoke(_viewModel.OpenOnboarding);
        };
    }

    private void RestorePlacement()
    {
        var s = _settings.Settings;
        if (s.WindowWidth >= 400) Width = s.WindowWidth;
        if (s.WindowHeight >= 300) Height = s.WindowHeight;
        if (s.WindowLeft is double left && s.WindowTop is double top &&
            left > SystemParameters.VirtualScreenLeft - 100 &&
            top > SystemParameters.VirtualScreenTop - 100 &&
            left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
            top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
        if (s.WindowMaximized) WindowState = WindowState.Maximized;
    }

    // Keep the queue and the results in step with the song being worked on.
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.CurrentSong)) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (_viewModel.CurrentSong is { } song) QueueList.ScrollIntoView(song);
            ResultsScroll.ScrollToTop();
            if (SongHeader.IsVisible) Motion.PlayEntrance(SongHeader);
        });
    }

    // ===================== Keyboard shortcuts =====================
    // Letters and digits without modifiers cannot be KeyGestures in WPF, so one handler maps every key.
    // Typing in a text box or using an open drop-down always wins over a shortcut.

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBoxBase or PasswordBox) return;
        // Only an open drop-down owns the keys. A closed one keeps focus after a mouse pick
        // (filters, search engine) and must not switch the shortcuts off.
        if (Keyboard.FocusedElement is ComboBox { IsDropDownOpen: true } or ComboBoxItem) return;

        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None && Keyboard.FocusedElement is not ButtonBase)
        {
            if (_viewModel.PickRecommended()) e.Handled = true;
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var action = Keyboard.Modifiers switch
        {
            ModifierKeys.Control => key switch
            {
                Key.O => MainViewModel.ShortcutAction.LoadFile,
                Key.S => MainViewModel.ShortcutAction.SaveSession,
                Key.E => MainViewModel.ShortcutAction.Export,
                Key.D => MainViewModel.ShortcutAction.DownloadVideos,
                _ => (MainViewModel.ShortcutAction?)null
            },
            ModifierKeys.Control | ModifierKeys.Shift => key switch
            {
                Key.S => MainViewModel.ShortcutAction.SaveList,
                _ => (MainViewModel.ShortcutAction?)null
            },
            ModifierKeys.Shift => key switch
            {
                >= Key.D1 and <= Key.D9 or >= Key.NumPad1 and <= Key.NumPad9 => MainViewModel.ShortcutAction.Preview,
                _ => (MainViewModel.ShortcutAction?)null
            },
            ModifierKeys.None => key switch
            {
                >= Key.D1 and <= Key.D9 or >= Key.NumPad1 and <= Key.NumPad9 => MainViewModel.ShortcutAction.Pick,
                Key.S or Key.Right => MainViewModel.ShortcutAction.Skip,
                Key.B or Key.Left => MainViewModel.ShortcutAction.Back,
                Key.E => MainViewModel.ShortcutAction.EditQuery,
                Key.R or Key.F5 => MainViewModel.ShortcutAction.SearchAgain,
                Key.O => MainViewModel.ShortcutAction.OpenFirst,
                Key.P => MainViewModel.ShortcutAction.Preview,
                Key.F1 => MainViewModel.ShortcutAction.Guide,
                _ => null
            },
            _ => null
        };
        if (action is not { } a) return;

        var number = key is >= Key.D1 and <= Key.D9 ? key - Key.D0
                   : key is >= Key.NumPad1 and <= Key.NumPad9 ? key - Key.NumPad0
                   : a == MainViewModel.ShortcutAction.Preview ? 1
                   : 0;
        if (_viewModel.RunShortcut(a, number)) e.Handled = true;
    }

    // Compact result cards when the results column gets narrow.
    private void ResultsScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < 620;
        if (Resources["Result.ThumbWidth"] is double current && current == (compact ? 128.0 : 176.0)) return;
        Resources["Result.ThumbWidth"] = compact ? 128.0 : 176.0;
        Resources["Result.ThumbHeight"] = compact ? 72.0 : 99.0;
        Resources["Result.SelectMinWidth"] = compact ? 0.0 : 112.0;
        Resources["Result.DescVisibility"] = compact ? Visibility.Collapsed : Visibility.Visible;
        Resources["Result.ViewsColumn"] = new GridLength(compact ? 66 : 78);
        Resources["Result.DateColumn"] = new GridLength(compact ? 0 : 80);
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        var s = _settings.Settings;
        s.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            s.WindowWidth = Width;
            s.WindowHeight = Height;
            s.WindowLeft = Left;
            s.WindowTop = Top;
        }
        _viewModel.OnAppClosing();
    }

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (GetDroppedTxt(e) != null) DropOverlay.Visibility = Visibility.Visible;
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        // DragLeave bubbles up from every child crossed; hide only when the pointer leaves the window.
        var p = e.GetPosition(this);
        if (p.X <= 0 || p.Y <= 0 || p.X >= ActualWidth || p.Y >= ActualHeight)
            DropOverlay.Visibility = Visibility.Collapsed;
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDroppedTxt(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        var path = GetDroppedTxt(e);
        if (path != null) _viewModel.LoadFileFromPath(path);
    }

    private static string? GetDroppedTxt(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return null;
        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        return files?.FirstOrDefault(f =>
            (Path.GetExtension(f).Equals(".txt", StringComparison.OrdinalIgnoreCase) || ListFileService.IsListFile(f)) && File.Exists(f));
    }
}
