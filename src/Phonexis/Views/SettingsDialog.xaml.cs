using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Phonexis.Models;
using Phonexis.Services;

namespace Phonexis.Views;

public partial class SettingsDialog : Window
{
    private readonly ISettingsService _settings;
    private readonly IYouTubeService _youtube;
    private readonly ICacheService _cache;
    private readonly IThemeService _theme;
    private readonly Services.Downloads.DownloaderRegistry _downloaders;
    private bool _loading = true;

    public SettingsDialog(ISettingsService settings, IYouTubeService youtube, ICacheService cache, IThemeService theme,
                          Services.Downloads.DownloaderRegistry downloaders)
    {
        InitializeComponent();
        _downloaders = downloaders;
        _settings = settings;
        _youtube = youtube;
        _cache = cache;
        _theme = theme;
        LoadValues();
        _loading = false;
    }

    private void LoadValues()
    {
        var s = _settings.Settings;

        foreach (var (code, name) in I18n.Languages)
            LanguageCombo.Items.Add(new ComboBoxItem { Content = name, Tag = code });
        LanguageCombo.SelectedIndex = Math.Max(0,
            I18n.Languages.ToList().FindIndex(l => l.Code == s.Language));

        ThemeSystemRadio.IsChecked = s.Theme == AppTheme.System;
        ThemeLightRadio.IsChecked = s.Theme == AppTheme.Light;
        ThemeDarkRadio.IsChecked = s.Theme == AppTheme.Dark;

        foreach (var n in new[] { 3, 5, 8, 10, 15, 20, 25 })
            MaxResultsCombo.Items.Add(new ComboBoxItem { Content = n.ToString(), Tag = n });
        var idx = new[] { 3, 5, 8, 10, 15, 20, 25 }.ToList().IndexOf(s.MaxResults);
        MaxResultsCombo.SelectedIndex = idx >= 0 ? idx : 1;

        foreach (var mode in SearchEngineModes.All)
            SearchModeCombo.Items.Add(new ComboBoxItem { Content = I18n.Instance[SearchEngineModes.I18nKey(mode)], Tag = mode });
        SearchModeCombo.SelectedIndex = Math.Max(0, Array.IndexOf(SearchEngineModes.All, s.SearchMode));

        TestModeCheck.IsChecked = s.TestMode;
        CacheOnlyCheck.IsChecked = s.CacheOnlyMode;
        PreselectCheck.IsChecked = s.AutoPreselect;

        RefreshKeysList();
        RefreshCacheStats();
        LoadDownloadValues();
    }

    // ---- Tabs ----

    private void Tab_Changed(object sender, RoutedEventArgs e)
    {
        if (PanelGeneral == null || PanelKeys == null || PanelCache == null || PanelDownload == null) return;
        PanelDownload.Visibility = TabDownload.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelGeneral.Visibility = TabGeneral.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelKeys.Visibility = TabKeys.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelCache.Visibility = TabCache.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- General ----

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageCombo.SelectedItem is not ComboBoxItem item) return;
        var code = (string)item.Tag;
        _settings.Settings.Language = code;
        _settings.Save();
        I18n.Instance.SetLanguage(code);
    }

    private void Theme_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var theme = ThemeDarkRadio.IsChecked == true ? AppTheme.Dark
                  : ThemeLightRadio.IsChecked == true ? AppTheme.Light
                  : AppTheme.System;
        _settings.Settings.Theme = theme;
        _settings.Save();
        _theme.Apply(theme);
    }

    private void MaxResults_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || MaxResultsCombo.SelectedItem is not ComboBoxItem item) return;
        _settings.Settings.MaxResults = (int)item.Tag;
        _settings.Save();
    }

    private void SearchMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || SearchModeCombo.SelectedItem is not ComboBoxItem item) return;
        _settings.Settings.SearchMode = (SearchEngineMode)item.Tag;
        _settings.Save();
    }

    private void Modes_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.Settings.TestMode = TestModeCheck.IsChecked == true;
        _settings.Settings.CacheOnlyMode = CacheOnlyCheck.IsChecked == true;
        _settings.Settings.AutoPreselect = PreselectCheck.IsChecked == true;
        _settings.Save();
    }

    // ---- API keys ----

    private void RefreshKeysList()
    {
        KeysList.Items.Clear();
        foreach (var key in _youtube.ApiKeys)
        {
            var masked = key.Length > 10 ? $"{key[..6]}…{key[^4..]}" : key;

            var grid = new Grid { Margin = new Thickness(10, 2, 2, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new TextBlock { Text = "", Margin = new Thickness(0, 1, 10, 0) };
            icon.SetResourceReference(StyleProperty, "Text.Icon");
            icon.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextTertiary");
            grid.Children.Add(icon);

            var text = new TextBlock
            {
                Text = masked,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new System.Windows.Media.FontFamily("Consolas")
            };
            text.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Text");
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            var remove = new Button
            {
                Content = "",
                Tag = key
            };
            remove.SetResourceReference(StyleProperty, "Button.Icon");
            remove.SetResourceReference(ForegroundProperty, "Brush.Danger");
            remove.Click += RemoveKey_Click;
            Grid.SetColumn(remove, 2);
            grid.Children.Add(remove);

            KeysList.Items.Add(new ListBoxItem { Content = grid, IsHitTestVisible = true, Focusable = false });
        }
        KeysCountText.Text = I18n.Instance.Format("KeysCountFormat", _youtube.ApiKeys.Count);
    }

    private void AddKey_Click(object sender, RoutedEventArgs e) => AddKeyFromBox();

    private void NewKeyBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AddKeyFromBox();
    }

    private void AddKeyFromBox()
    {
        var raw = NewKeyBox.Text.Trim();
        if (raw.Length == 0) return;
        // Allow pasting several keys separated by commas or newlines.
        var newKeys = raw.Split(new[] { ',', '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(k => k.Trim())
                         .Where(k => k.Length > 0);
        var keys = _youtube.ApiKeys.ToList();
        keys.AddRange(newKeys);
        _youtube.SetApiKeys(keys);
        NewKeyBox.Clear();
        RefreshKeysList();
    }

    private void RemoveKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string key }) return;
        var keys = _youtube.ApiKeys.Where(k => k != key).ToList();
        _youtube.SetApiKeys(keys);
        RefreshKeysList();
    }

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

    // ---- Cache ----

    private void RefreshCacheStats()
    {
        CacheStatsText.Text = I18n.Instance.Format("CacheStatsFormat", _cache.GetCachedQueryCount());
        UsageTodayText.Text = I18n.Instance.Format("UsageTodayFormat", _cache.GetTotalUnitsToday());
    }

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(I18n.Instance["ClearCacheConfirm"], I18n.Instance["Confirm"],
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;
        _cache.ClearSearchCache();
        RefreshCacheStats();
        MessageBox.Show(I18n.Instance["ClearCacheDone"], I18n.Instance["SettingsTitle"],
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
