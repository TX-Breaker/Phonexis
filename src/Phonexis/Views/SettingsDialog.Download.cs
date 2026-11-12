using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Phonexis.Models;
using Phonexis.Services;
using Phonexis.Services.Downloads;

namespace Phonexis.Views;

/// <summary>Settings › Download: downloader choice, naming/destination, quality and tool paths.</summary>
public partial class SettingsDialog
{
    private static readonly int[] Qualities = { 360, 480, 720, 1080 };

    private DownloadOptions Dl => _settings.Settings.Download;

    private void LoadDownloadValues()
    {
        var current = _downloaders.Get(Dl.Downloader);
        DownloaderLevel? lastLevel = null;
        foreach (var d in _downloaders.All)
        {
            if (lastLevel != d.Level)
            {
                DownloaderCombo.Items.Add(new ComboBoxItem
                {
                    Content = I18n.Instance[d.Level == DownloaderLevel.Direct ? "DlLevelDirect" : "DlLevelHandoff"],
                    IsEnabled = false,
                    FontWeight = FontWeights.SemiBold
                });
                lastLevel = d.Level;
            }
            var cost = d.Cost.Length == 0 ? string.Empty : $"  ·  {I18n.Instance["DlCost_" + d.Cost]}";
            var item = new ComboBoxItem { Content = d.DisplayName + cost, Tag = d.Id };
            DownloaderCombo.Items.Add(item);
            if (d == current) DownloaderCombo.SelectedItem = item;
        }

        DestSameRadio.IsChecked = Dl.Destination == VideoDestination.SameFolder;
        DestSubRadio.IsChecked = Dl.Destination == VideoDestination.VideoSubfolder;
        DestCustomRadio.IsChecked = Dl.Destination == VideoDestination.CustomFolder;
        SubfolderBox.Text = Dl.SubfolderName;
        MatchDatesCheck.IsChecked = Dl.MatchAudioDates;
        CustomFolderText.Text = VideoNaming.FallbackRoot(Dl);

        foreach (var (policy, key) in new[]
                 {
                     (ExistingFilePolicy.Skip, "DlExistingSkip"),
                     (ExistingFilePolicy.AddSuffix, "DlExistingSuffix"),
                     (ExistingFilePolicy.Overwrite, "DlExistingOverwrite")
                 })
        {
            var item = new ComboBoxItem { Content = I18n.Instance[key], Tag = policy };
            ExistingCombo.Items.Add(item);
            if (Dl.ExistingFiles == policy) ExistingCombo.SelectedItem = item;
        }

        foreach (var q in Qualities)
        {
            var item = new ComboBoxItem { Content = q + "p", Tag = q };
            QualityCombo.Items.Add(item);
            if (Dl.MaxQuality == q) QualityCombo.SelectedItem = item;
        }
        if (QualityCombo.SelectedItem == null) QualityCombo.SelectedIndex = Qualities.Length - 1;

        for (var n = 1; n <= 4; n++)
        {
            var item = new ComboBoxItem { Content = n.ToString(), Tag = n };
            ParallelCombo.Items.Add(item);
            if (Dl.ParallelDownloads == n) ParallelCombo.SelectedItem = item;
        }

        BuildToolRows();
        UpdateDownloaderHint();
    }

    private void SaveDownload()
    {
        if (_loading) return;
        _settings.Save();
    }

    private void Downloader_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (DownloaderCombo.SelectedItem is not ComboBoxItem { Tag: string id }) return;
        Dl.Downloader = id;
        SaveDownload();
        UpdateDownloaderHint();
    }

    private void UpdateDownloaderHint()
    {
        var d = _downloaders.Get(Dl.Downloader);
        var located = d.Locate(Dl);
        var status = d.Id == "builtin" ? string.Empty
            : located != null ? "  " + I18n.Instance.Format("DlFoundFormat", located)
            : "  " + I18n.Instance["DlNotFound"];
        DownloaderHint.Text = I18n.Instance[d.HowItWorksKey] + status;
        QualityCard.Visibility = d.Level == DownloaderLevel.Direct && d.Id is "builtin" or "ytdlp"
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Destination_Changed(object sender, RoutedEventArgs e)
    {
        Dl.Destination = DestSameRadio.IsChecked == true ? VideoDestination.SameFolder
                       : DestCustomRadio.IsChecked == true ? VideoDestination.CustomFolder
                       : VideoDestination.VideoSubfolder;
        if (SubfolderBox != null) SubfolderBox.IsEnabled = Dl.Destination == VideoDestination.VideoSubfolder;
        SaveDownload();
    }

    private void MatchDates_Changed(object sender, RoutedEventArgs e)
    {
        Dl.MatchAudioDates = MatchDatesCheck.IsChecked == true;
        SaveDownload();
    }

    private void Subfolder_LostFocus(object sender, RoutedEventArgs e)
    {
        var name = VideoNaming.SafeFileName(SubfolderBox.Text.Trim());
        SubfolderBox.Text = name;
        Dl.SubfolderName = name;
        SaveDownload();
    }

    private void CustomFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = I18n.Instance["SelectFolder"] };
        if (dialog.ShowDialog() != true) return;
        Dl.CustomFolder = dialog.FolderName;
        CustomFolderText.Text = dialog.FolderName;
        SaveDownload();
    }

    private void Existing_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ExistingCombo.SelectedItem is not ComboBoxItem { Tag: ExistingFilePolicy policy }) return;
        Dl.ExistingFiles = policy;
        SaveDownload();
    }

    private void Quality_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (QualityCombo.SelectedItem is not ComboBoxItem { Tag: int q }) return;
        Dl.MaxQuality = q;
        SaveDownload();
    }

    private void Parallel_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ParallelCombo.SelectedItem is not ComboBoxItem { Tag: int n }) return;
        Dl.ParallelDownloads = n;
        SaveDownload();
    }

    // ---- Tool paths ----

    private sealed record ToolRow(string LabelKey, Func<string?> Locate, Action<string> Set, bool Folder, string? Filter);

    private void BuildToolRows()
    {
        while (ToolsPanel.Children.Count > 1) ToolsPanel.Children.RemoveAt(1);

        var rows = new[]
        {
            new ToolRow("DlToolFfmpeg", () => ToolLocator.FindFfmpeg(Dl.FfmpegPath), v => Dl.FfmpegPath = v, false, "ffmpeg.exe|ffmpeg.exe"),
            new ToolRow("DlToolYtDlp", () => _downloaders.Get("ytdlp").Locate(Dl), v => Dl.YtDlpPath = v, false, "yt-dlp.exe|yt-dlp.exe"),
            new ToolRow("DlToolJd", () => _downloaders.Get("jdownloader").Locate(Dl), v => Dl.JDownloaderFolderWatch = v, true, null)
        };

        foreach (var row in rows)
        {
            ToolsPanel.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 10, 0, 0) });
            var grid = new Grid { Margin = new Thickness(18, 0, 18, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock { Text = I18n.Instance[row.LabelKey], VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
            label.SetResourceReference(ForegroundProperty, "Brush.Text");
            grid.Children.Add(label);

            var found = row.Locate();
            var value = new TextBlock
            {
                Text = found ?? I18n.Instance["DlNotFound"],
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 10, 0),
                ToolTip = found
            };
            value.SetResourceReference(StyleProperty, "Text.Secondary");
            value.TextWrapping = TextWrapping.NoWrap;
            if (found == null) value.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextTertiary");
            Grid.SetColumn(value, 1);
            grid.Children.Add(value);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            var browse = new Button { Content = I18n.Instance["DlBrowse"], Padding = new Thickness(12, 0, 12, 0) };
            browse.Click += (_, _) =>
            {
                string? picked = null;
                if (row.Folder)
                {
                    var fd = new OpenFolderDialog { Title = I18n.Instance[row.LabelKey] };
                    if (fd.ShowDialog() == true) picked = fd.FolderName;
                }
                else
                {
                    var od = new OpenFileDialog { Filter = row.Filter, Title = I18n.Instance[row.LabelKey] };
                    if (od.ShowDialog() == true) picked = od.FileName;
                }
                if (picked == null) return;
                row.Set(picked);
                SaveDownload();
                BuildToolRows();
                UpdateDownloaderHint();
            };
            buttons.Children.Add(browse);

            if (row.LabelKey == "DlToolFfmpeg" && found == null)
            {
                var get = new Button { Content = I18n.Instance["DlGetFfmpeg"], Margin = new Thickness(6, 0, 0, 0), Tag = "" };
                get.SetResourceReference(StyleProperty, "Button.Primary");
                get.Click += async (_, _) =>
                {
                    var ok = MessageBox.Show(I18n.Instance["DlFfmpegAsk"], Title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (ok != MessageBoxResult.Yes) return;
                    get.IsEnabled = false;
                    try
                    {
                        var progress = new Progress<double>(p => get.Content = $"{p:P0}");
                        Dl.FfmpegPath = await ToolLocator.DownloadFfmpegAsync(progress, CancellationToken.None);
                        SaveDownload();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(I18n.Instance.Format("DlFfmpegFailedFormat", ex.Message), Title,
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    BuildToolRows();
                };
                buttons.Children.Add(get);
            }
            Grid.SetColumn(buttons, 2);
            grid.Children.Add(buttons);
            ToolsPanel.Children.Add(grid);
        }
        ToolsPanel.Children.Add(new Border { Height = 14 });
    }
}
