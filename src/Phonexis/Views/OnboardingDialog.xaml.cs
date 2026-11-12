using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Phonexis.Models;
using Phonexis.Services;
using Phonexis.Services.Downloads;

namespace Phonexis.Views;

/// <summary>What the user asked for when closing the guide on its last step.</summary>
public enum OnboardingNext
{
    None,
    LoadTxt,
    FromFolder
}

/// <summary>First-run guide: what Phonexis does, API key, video downloads, first list.</summary>
public partial class OnboardingDialog : Window
{
    private readonly ISettingsService _settings;
    private readonly IYouTubeService _youtube;
    private readonly DownloaderRegistry _downloaders;
    private int _step = 1;

    public OnboardingNext Next { get; private set; }

    public OnboardingDialog(ISettingsService settings, IYouTubeService youtube, DownloaderRegistry downloaders)
    {
        InitializeComponent();
        _settings = settings;
        _youtube = youtube;
        _downloaders = downloaders;

        var t = I18n.Instance;
        FlowList.ItemsSource = new[]
        {
            Tuple.Create("", t["ObFlow1"]),
            Tuple.Create("", t["ObFlow2"]),
            Tuple.Create("", t["ObFlow3"]),
            Tuple.Create("", t["ObFlow4"])
        };

        TestModeCheck.IsChecked = settings.Settings.TestMode;
        foreach (var mode in SearchEngineModes.All)
            SearchModeCombo.Items.Add(new ComboBoxItem { Content = t[SearchEngineModes.I18nKey(mode)], Tag = mode });
        SearchModeCombo.SelectedIndex = Math.Max(0, Array.IndexOf(SearchEngineModes.All, settings.Settings.SearchMode));
        RefreshKeys();

        var current = downloaders.Get(settings.Settings.Download.Downloader);
        foreach (var d in downloaders.All)
        {
            var found = d.Id == "builtin" || d.Locate(settings.Settings.Download) != null;
            var item = new ComboBoxItem
            {
                Content = d.DisplayName + (found && d.Id != "builtin" ? $"  ·  {I18n.Instance["DlInstalled"]}" : string.Empty),
                Tag = d.Id
            };
            DownloaderCombo.Items.Add(item);
            if (d == current) DownloaderCombo.SelectedItem = item;
        }
        var dest = settings.Settings.Download.Destination;
        DestSameRadio.IsChecked = dest == VideoDestination.SameFolder;
        DestSubRadio.IsChecked = dest == VideoDestination.VideoSubfolder;
        DestCustomRadio.IsChecked = dest == VideoDestination.CustomFolder;

        ShowStep();
    }

    private void ShowStep()
    {
        Step1.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4.Visibility = _step == 4 ? Visibility.Visible : Visibility.Collapsed;

        var numbers = new[] { Step1No, Step2No, Step3No, Step4No };
        for (var i = 0; i < numbers.Length; i++)
        {
            numbers[i].SetResourceReference(TextBlock.ForegroundProperty, i + 1 == _step ? "Brush.ObiInk" : "Brush.ObiInkMuted");
            numbers[i].Opacity = i + 1 == _step ? 1 : 0.6;
        }

        BackButton.Visibility = _step > 1 ? Visibility.Visible : Visibility.Hidden;
        SkipButton.Visibility = _step < 4 ? Visibility.Visible : Visibility.Hidden;
        NextButton.Content = I18n.Instance[_step == 4 ? "ObDone" : "ObNext"];
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_step == 4) { Finish(); return; }
        _step++;
        ShowStep();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_step <= 1) return;
        _step--;
        ShowStep();
    }

    private void Finish_Click(object sender, RoutedEventArgs e) => Finish();

    private void LoadTxt_Click(object sender, RoutedEventArgs e)
    {
        Next = OnboardingNext.LoadTxt;
        Finish();
    }

    private void FromFolder_Click(object sender, RoutedEventArgs e)
    {
        Next = OnboardingNext.FromFolder;
        Finish();
    }

    private void Finish()
    {
        var s = _settings.Settings;
        s.TestMode = TestModeCheck.IsChecked == true;
        if (SearchModeCombo.SelectedItem is ComboBoxItem { Tag: SearchEngineMode mode }) s.SearchMode = mode;
        s.Download.Destination = DestSameRadio.IsChecked == true ? VideoDestination.SameFolder
                               : DestCustomRadio.IsChecked == true ? VideoDestination.CustomFolder
                               : VideoDestination.VideoSubfolder;
        s.FirstRunCompleted = true;
        _settings.Save();
        DialogResult = true;
    }

    private void AddKey_Click(object sender, RoutedEventArgs e)
    {
        var keys = KeyBox.Text.Split(new[] { ',', '\n', '\r', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(k => k.Trim()).Where(k => k.Length > 0).ToList();
        if (keys.Count == 0) return;
        _youtube.SetApiKeys(_youtube.ApiKeys.Concat(keys).Distinct());
        KeyBox.Clear();
        RefreshKeys();
    }

    private void RefreshKeys() =>
        KeysStatus.Text = I18n.Instance.Format("KeysCountFormat", _youtube.ApiKeys.Count);

    private void KeyHelp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = AppInfo.ApiKeyHelpUrl, UseShellExecute = true });
        }
        catch
        {
            // non-fatal
        }
    }

    private void Downloader_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (DownloaderCombo.SelectedItem is not ComboBoxItem { Tag: string id }) return;
        _settings.Settings.Download.Downloader = id;
        DownloaderHint.Text = I18n.Instance[_downloaders.Get(id).HowItWorksKey];
    }
}
