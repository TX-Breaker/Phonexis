using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Phonexis.Models;
using Phonexis.Services;
using Phonexis.Services.Downloads;

namespace Phonexis.Views;

/// <summary>Rename &amp; archive: renames files downloaded by any app after their tracks.</summary>
public partial class ArchiveDialog : Window
{
    private readonly List<SongItem> _songs;
    private readonly ISettingsService _settings;
    private List<ArchiveMatch> _plan = new();
    private string _folder;

    public ArchiveDialog(List<SongItem> songs, ISettingsService settings, string? suggestedFolder = null)
    {
        InitializeComponent();
        _songs = songs;
        _settings = settings;
        _folder = settings.Settings.Download.HandoffDownloadFolder is { } f && Directory.Exists(f) ? f
                : suggestedFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        FolderText.Text = _folder;
        UndoButton.IsEnabled = ArchiveService.LatestLog() != null;
        SummaryText.Text = string.Empty;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = I18n.Instance["SelectFolder"], InitialDirectory = _folder };
        if (dialog.ShowDialog() != true) return;
        _folder = dialog.FolderName;
        FolderText.Text = _folder;
        _settings.Settings.Download.HandoffDownloadFolder = _folder;
        _settings.Save();
    }

    private void Analyze_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(_folder)) return;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            _plan = ArchiveService.Plan(_folder, _songs, _settings.Settings.Download, SubfoldersCheck.IsChecked == true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(ex.Message, I18n.Instance["Error"], MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        MatchesList.ItemsSource = _plan;
        EmptyText.Visibility = _plan.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var matched = _plan.Count(p => p.Song != null);
        SummaryText.Text = I18n.Instance.Format("ArchSummaryFormat", matched, _plan.Count - matched);
        ExecuteButton.IsEnabled = matched > 0;
    }

    private void Execute_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var moves = ArchiveService.Execute(_plan, matchAudioDates: _settings.Settings.Download.MatchAudioDates);
            MessageBox.Show(I18n.Instance.Format("ArchDoneFormat", moves.Count), Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(ex.Message, I18n.Instance["Error"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(I18n.Instance["ArchUndoConfirm"], Title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        var restored = ArchiveService.UndoLatest();
        foreach (var song in _songs.Where(s => s.DownloadedPath != null && !File.Exists(s.DownloadedPath)))
        {
            song.DownloadedPath = null;
            song.DownloadState = DownloadState.None;
        }
        UndoButton.IsEnabled = ArchiveService.LatestLog() != null;
        MessageBox.Show(I18n.Instance.Format("ArchUndoneFormat", restored), Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
