using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq; // Added for LINQ extension methods
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.Messaging; // Added for messaging
using Phonexis.Messages; // Added for message class
using Phonexis.Helpers;
using Phonexis.Services;

namespace Phonexis.Views
{
    /// <summary>
    /// Logica di interazione per SettingsDialog.xaml
    /// </summary>
    public partial class SettingsDialog : Window
    {
        private readonly IYouTubeApiService _youTubeApiService;
        private readonly IDatabaseService _databaseService;
        private readonly IStateService _stateService;
        
        private string _selectedLanguage = "en";
        private bool _testMode;
        private bool _cacheOnlyMode; // Keep this for compatibility with AppState
        private string _apiKey = string.Empty;
        
        // UI Controls
        private RadioButton _italianRadioButton;
        private RadioButton _englishRadioButton;
        private RadioButton _frenchRadioButton;
        private RadioButton _spanishRadioButton;
        private CheckBox _testModeCheckBox;
        private TextBox _apiKeyTextBox;
        // private TextBlock _apiCallCountTextBlock; // Replaced by _apiStatsListPanel
        private StackPanel _apiStatsListPanel; // Panel to hold per-key stats
        private TextBlock _versionTextBlock;
        
        public string SelectedLanguage => _selectedLanguage;
        public bool TestMode => _testMode;
        public bool CacheOnlyMode => _cacheOnlyMode;
        
        public SettingsDialog(
            IYouTubeApiService youTubeApiService,
            IDatabaseService databaseService,
            IStateService stateService)
        {
            _youTubeApiService = youTubeApiService;
            _databaseService = databaseService;
            _stateService = stateService;
            
            // Configure window
            Title = LocalizationHelper.GetString("Settings");
            Width = 550;
            Height = 450;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            
            // Create UI
            CreateUI();
            
            // Load settings
            LoadCurrentSettings();
        }
        
        private void CreateUI()
        {
            // Main grid
            Grid mainGrid = new Grid();
            mainGrid.Margin = new Thickness(20);
            
            // Define rows
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            // Title
            TextBlock titleBlock = new TextBlock();
            titleBlock.Text = LocalizationHelper.GetString("Settings");
            titleBlock.FontSize = 24;
            titleBlock.FontWeight = FontWeights.Bold;
            titleBlock.Margin = new Thickness(0, 0, 0, 20);
            Grid.SetRow(titleBlock, 0);
            mainGrid.Children.Add(titleBlock);
            
            // Tab control
            TabControl tabControl = new TabControl();
            tabControl.Margin = new Thickness(0, 0, 0, 20);
            Grid.SetRow(tabControl, 1);
            
            // General tab
            TabItem generalTab = new TabItem();
            generalTab.Header = LocalizationHelper.GetString("General");
            
            ScrollViewer generalScrollViewer = new ScrollViewer();
            generalScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            
            StackPanel generalPanel = new StackPanel();
            generalPanel.Margin = new Thickness(10);
            
            // Language group
            GroupBox languageGroup = new GroupBox();
            languageGroup.Header = LocalizationHelper.GetString("Language");
            languageGroup.Margin = new Thickness(0, 0, 0, 10);
            
            StackPanel languagePanel = new StackPanel();
            languagePanel.Margin = new Thickness(10);
            
            _italianRadioButton = new RadioButton();
            _italianRadioButton.Content = "Italiano";
            _italianRadioButton.Margin = new Thickness(0, 5, 0, 0);
            _italianRadioButton.Checked += LanguageRadioButton_Checked;
            languagePanel.Children.Add(_italianRadioButton);
            
            _englishRadioButton = new RadioButton();
            _englishRadioButton.Content = "English";
            _englishRadioButton.Margin = new Thickness(0, 5, 0, 0);
            _englishRadioButton.Checked += LanguageRadioButton_Checked;
            languagePanel.Children.Add(_englishRadioButton);
            
            _frenchRadioButton = new RadioButton();
            _frenchRadioButton.Content = "Français";
            _frenchRadioButton.Margin = new Thickness(0, 5, 0, 0);
            _frenchRadioButton.Checked += LanguageRadioButton_Checked;
            languagePanel.Children.Add(_frenchRadioButton);
            
            _spanishRadioButton = new RadioButton();
            _spanishRadioButton.Content = "Español";
            _spanishRadioButton.Margin = new Thickness(0, 5, 0, 0);
            _spanishRadioButton.Checked += LanguageRadioButton_Checked;
            languagePanel.Children.Add(_spanishRadioButton);
            
            languageGroup.Content = languagePanel;
            generalPanel.Children.Add(languageGroup);
            
            // Test Mode group
            GroupBox testModeGroup = new GroupBox();
            testModeGroup.Header = LocalizationHelper.GetString("TestMode");
            testModeGroup.Margin = new Thickness(0, 10, 0, 10);
            
            StackPanel testModePanel = new StackPanel();
            testModePanel.Margin = new Thickness(10);
            
            _testModeCheckBox = new CheckBox();
            _testModeCheckBox.Content = LocalizationHelper.GetString("TestMode");
            _testModeCheckBox.Margin = new Thickness(0, 5, 0, 0);
            testModePanel.Children.Add(_testModeCheckBox);
            
            TextBlock testModeDescription = new TextBlock();
            testModeDescription.Text = LocalizationHelper.GetString("TestModeDescription");
            testModeDescription.TextWrapping = TextWrapping.Wrap;
            testModeDescription.Margin = new Thickness(20, 0, 0, 5);
            testModeDescription.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85));
            testModePanel.Children.Add(testModeDescription);
            
            testModeGroup.Content = testModePanel;
            generalPanel.Children.Add(testModeGroup);
            
            // Cache group
            GroupBox cacheGroup = new GroupBox();
            cacheGroup.Header = "Cache";
            cacheGroup.Margin = new Thickness(0, 10, 0, 0);
            
            StackPanel cachePanel = new StackPanel();
            cachePanel.Margin = new Thickness(10);
            
            Button clearCacheButton = new Button();
            clearCacheButton.Content = LocalizationHelper.GetString("ClearCache");
            clearCacheButton.Margin = new Thickness(0, 5, 0, 5);
            clearCacheButton.Click += ClearCacheButton_Click;
            cachePanel.Children.Add(clearCacheButton);
            
            TextBlock cacheDescription = new TextBlock();
            cacheDescription.Text = LocalizationHelper.GetString("ClearCacheExplanation");
            cacheDescription.TextWrapping = TextWrapping.Wrap;
            cacheDescription.Margin = new Thickness(0, 5, 0, 0);
            cacheDescription.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85));
            cachePanel.Children.Add(cacheDescription);
            
            cacheGroup.Content = cachePanel;
            generalPanel.Children.Add(cacheGroup);
            
            generalScrollViewer.Content = generalPanel;
            generalTab.Content = generalScrollViewer;
            tabControl.Items.Add(generalTab);
            
            // Api tab
            TabItem apiTab = new TabItem();
            apiTab.Header = "Api";
            
            ScrollViewer apiScrollViewer = new ScrollViewer();
            apiScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            
            StackPanel apiPanel = new StackPanel();
            apiPanel.Margin = new Thickness(10);
            
            // Api Key group
            GroupBox apiKeyGroup = new GroupBox();
            apiKeyGroup.Header = LocalizationHelper.GetString("YouTubeApiKey");
            apiKeyGroup.Margin = new Thickness(0, 0, 0, 10);
            
            StackPanel apiKeyPanel = new StackPanel();
            apiKeyPanel.Margin = new Thickness(10);
            
            TextBlock apiKeyLabel = new TextBlock();
            apiKeyLabel.Text = LocalizationHelper.GetString("CurrentApiKey");
            apiKeyLabel.Margin = new Thickness(0, 5, 0, 0);
            apiKeyPanel.Children.Add(apiKeyLabel);
            
            _apiKeyTextBox = new TextBox();
            _apiKeyTextBox.Margin = new Thickness(0, 5, 0, 5);
            _apiKeyTextBox.IsReadOnly = true;
            apiKeyPanel.Children.Add(_apiKeyTextBox);
            
            Button changeApiKeyButton = new Button();
            changeApiKeyButton.Content = LocalizationHelper.GetString("ChangeApiKey");
            changeApiKeyButton.Margin = new Thickness(0, 5, 0, 0);
            changeApiKeyButton.Click += ChangeApiKeyButton_Click;
            apiKeyPanel.Children.Add(changeApiKeyButton);
            
            TextBlock apiKeyDescription1 = new TextBlock();
            apiKeyDescription1.Text = LocalizationHelper.GetString("ApiKeyDescription1");
            apiKeyDescription1.TextWrapping = TextWrapping.Wrap;
            apiKeyDescription1.Margin = new Thickness(0, 5, 0, 0);
            apiKeyDescription1.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85));
            apiKeyPanel.Children.Add(apiKeyDescription1);
            
            TextBlock apiKeyDescription2 = new TextBlock();
            apiKeyDescription2.Text = LocalizationHelper.GetString("ApiKeyDescription2");
            apiKeyDescription2.TextWrapping = TextWrapping.Wrap;
            apiKeyDescription2.Margin = new Thickness(0, 5, 0, 0);
            apiKeyDescription2.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85));
            apiKeyPanel.Children.Add(apiKeyDescription2);
            
            apiKeyGroup.Content = apiKeyPanel;
            apiPanel.Children.Add(apiKeyGroup);
            
            // Api Stats group
            GroupBox apiStatsGroup = new GroupBox();
            apiStatsGroup.Header = LocalizationHelper.GetString("ApiStats");
            apiStatsGroup.Margin = new Thickness(0, 10, 0, 0);
            
            StackPanel apiStatsPanel = new StackPanel(); // This outer panel remains
            apiStatsPanel.Margin = new Thickness(10);

            // Create a dedicated panel to list the stats for each key
            _apiStatsListPanel = new StackPanel();
            _apiStatsListPanel.Margin = new Thickness(0, 5, 0, 5);
            apiStatsPanel.Children.Add(_apiStatsListPanel); // Add the list panel

            // Keep the general description
            TextBlock apiStatsDescription = new TextBlock();
            apiStatsDescription.Text = LocalizationHelper.GetString("ApiLimitDescription");
            apiStatsDescription.TextWrapping = TextWrapping.Wrap;
            apiStatsDescription.Margin = new Thickness(0, 5, 0, 0);
            apiStatsDescription.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85));
            apiStatsPanel.Children.Add(apiStatsDescription);
            
            apiStatsGroup.Content = apiStatsPanel;
            apiPanel.Children.Add(apiStatsGroup);
            
            apiScrollViewer.Content = apiPanel;
            apiTab.Content = apiScrollViewer;
            tabControl.Items.Add(apiTab);
            
            // Info tab
            TabItem infoTab = new TabItem();
            infoTab.Header = "Info";
            
            ScrollViewer infoScrollViewer = new ScrollViewer();
            infoScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            
            StackPanel infoPanel = new StackPanel();
            infoPanel.Margin = new Thickness(10);
            
            TextBlock appTitleBlock = new TextBlock();
            appTitleBlock.Text = "Phonexis - TXT to YT Multisearch";
            appTitleBlock.FontSize = 18;
            appTitleBlock.FontWeight = FontWeights.Bold;
            appTitleBlock.Margin = new Thickness(0, 0, 0, 10);
            infoPanel.Children.Add(appTitleBlock);
            
            TextBlock appDescriptionBlock = new TextBlock();
            appDescriptionBlock.Text = LocalizationHelper.GetString("AppDescription");
            appDescriptionBlock.TextWrapping = TextWrapping.Wrap;
            appDescriptionBlock.Margin = new Thickness(0, 0, 0, 10);
            infoPanel.Children.Add(appDescriptionBlock);
            
            _versionTextBlock = new TextBlock();
            _versionTextBlock.Margin = new Thickness(0, 0, 0, 10);
            infoPanel.Children.Add(_versionTextBlock);
            
            TextBlock copyrightBlock = new TextBlock();
            copyrightBlock.Text = LocalizationHelper.GetString("CopyrightNotice");
            copyrightBlock.Margin = new Thickness(0, 0, 0, 10);
            infoPanel.Children.Add(copyrightBlock);
            
            TextBlock techTitleBlock = new TextBlock();
            techTitleBlock.Text = LocalizationHelper.GetString("TechnologiesUsed");
            techTitleBlock.FontWeight = FontWeights.Bold;
            techTitleBlock.Margin = new Thickness(0, 10, 0, 5);
            infoPanel.Children.Add(techTitleBlock);
            
            TextBlock tech1Block = new TextBlock();
            tech1Block.Text = "• .NET 6.0";
            tech1Block.Margin = new Thickness(10, 0, 0, 0);
            infoPanel.Children.Add(tech1Block);
            
            TextBlock tech2Block = new TextBlock();
            tech2Block.Text = "• WPF";
            tech2Block.Margin = new Thickness(10, 0, 0, 0);
            infoPanel.Children.Add(tech2Block);
            
            TextBlock tech3Block = new TextBlock();
            tech3Block.Text = "• SQLite";
            tech3Block.Margin = new Thickness(10, 0, 0, 0);
            infoPanel.Children.Add(tech3Block);
            
            TextBlock tech4Block = new TextBlock();
            tech4Block.Text = "• YouTube Data API v3";
            tech4Block.Margin = new Thickness(10, 0, 0, 0);
            infoPanel.Children.Add(tech4Block);
            
            infoScrollViewer.Content = infoPanel;
            infoTab.Content = infoScrollViewer;
            tabControl.Items.Add(infoTab);
            
            mainGrid.Children.Add(tabControl);
            
            // Buttons
            StackPanel buttonPanel = new StackPanel();
            buttonPanel.Orientation = Orientation.Horizontal;
            buttonPanel.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetRow(buttonPanel, 2);
            
            Button okButton = new Button();
            okButton.Content = "OK";
            okButton.Width = 80;
            okButton.Margin = new Thickness(0, 0, 10, 0);
            okButton.Click += OkButton_Click;
            okButton.IsDefault = true;
            buttonPanel.Children.Add(okButton);
            
            Button cancelButton = new Button();
            cancelButton.Content = LocalizationHelper.GetString("Cancel");
            cancelButton.Width = 80;
            cancelButton.Click += CancelButton_Click;
            cancelButton.IsCancel = true;
            buttonPanel.Children.Add(cancelButton);
            
            mainGrid.Children.Add(buttonPanel);
            
            Content = mainGrid;
        }
        
        private void LoadCurrentSettings()
        {
            // Load current language
            _selectedLanguage = LocalizationHelper.CurrentLanguage;
            switch (_selectedLanguage)
            {
                case "it":
                    _italianRadioButton.IsChecked = true;
                    break;
                case "en":
                    _englishRadioButton.IsChecked = true;
                    break;
                case "fr":
                    _frenchRadioButton.IsChecked = true;
                    break;
                case "es":
                    _spanishRadioButton.IsChecked = true;
                    break;
                default:
                    _englishRadioButton.IsChecked = true;
                    break;
            }
            
            // Load test mode
            var appState = _stateService.GetState();
            _testMode = appState.TestMode;
            _cacheOnlyMode = appState.CacheOnlyMode; // Still load it, but don't show UI for it
            
            _testModeCheckBox.IsChecked = _testMode;
            
            // Load Api key
            _apiKey = _youTubeApiService.GetApiKeys().Count > 0 ?
                _youTubeApiService.GetApiKeys()[0] :
                LocalizationHelper.GetString("NoApiKeySet");
            
            // Mask Api key for security
            _apiKeyTextBox.Text = _apiKey.Length > 8 ?
                $"{_apiKey.Substring(0, 4)}...{_apiKey.Substring(_apiKey.Length - 4)}" :
                LocalizationHelper.GetString("NoApiKeySet");
            
            // Set version
            _versionTextBlock.Text = $"{LocalizationHelper.GetString("Version")}: {GetType().Assembly.GetName().Version}";
            // Load and display per-key Api stats
            LoadApiStats();
        }

        private void LoadApiStats()
        {
            _apiStatsListPanel.Children.Clear(); // Clear previous stats

            var keys = _youTubeApiService.GetApiKeys();
            if (keys.Count == 0)
            {
                _apiStatsListPanel.Children.Add(new TextBlock { Text = LocalizationHelper.GetString("NoApiKeySet"), FontStyle = FontStyles.Italic });
                return;
            }

            var today = DateTime.UtcNow.Date;
            var counts = _databaseService.GetApiCallCounts(today);
            var statuses = _databaseService.GetApiKeyExhaustionStatus(today);
            const int quotaPerKey = 10000; // TODO: Make this configurable or get from YouTubeApiService constant

            for(int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                string shortKey = key.Length > 4 ? key.Substring(key.Length - 4) : key;
                
                counts.TryGetValue(key, out int count);
                statuses.TryGetValue(key, out bool isExhausted);

                string statusText = isExhausted
                    ? LocalizationHelper.GetString("StatusExhausted")
                    : LocalizationHelper.GetString("StatusActive");

                // Add visual cue if this is the currently active key
                string activeIndicator = (i == _youTubeApiService.GetCurrentKeyIndex()) ? " -> " : "    ";

                string formattedText = string.Format(
                    LocalizationHelper.GetString("ApiKeyCountFormat"),
                    shortKey,          // {0} Last 4 digits
                    statusText,        // {1} Status (Active/Exhausted)
                    count,             // {2} Current Count
                    quotaPerKey        // {3} Quota Limit
                );

                var textBlock = new TextBlock
                {
                    Text = activeIndicator + formattedText,
                    Margin = new Thickness(0, 2, 0, 2),
                    Foreground = isExhausted ? Brushes.Red : SystemColors.ControlTextBrush // Highlight exhausted keys
                };
                _apiStatsListPanel.Children.Add(textBlock);
            }
        }
        
        
        private void LanguageRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.IsChecked == true)
            {
                if (radioButton == _italianRadioButton)
                    _selectedLanguage = "it";
                else if (radioButton == _englishRadioButton)
                    _selectedLanguage = "en";
                else if (radioButton == _frenchRadioButton)
                    _selectedLanguage = "fr";
                else if (radioButton == _spanishRadioButton)
                    _selectedLanguage = "es";
            }
        }
        
        private void ClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                LocalizationHelper.GetString("ClearCacheConfirmation"),
                LocalizationHelper.GetString("Confirmation"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                _databaseService.ClearCache();
                MessageBox.Show(
                    LocalizationHelper.GetString("CacheCleared"),
                    LocalizationHelper.GetString("Information"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void ResetEverythingButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                LocalizationHelper.GetString("ResetEverythingConfirmation"),
                LocalizationHelper.GetString("ResetEverythingTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
                
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Get all application data paths
                    var appDataPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Phonexis");
                    var roamingPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Phonexis");
                    
                    // Delete LocalApplicationData folder (contains database, cache, states)
                    if (System.IO.Directory.Exists(appDataPath))
                    {
                        System.IO.Directory.Delete(appDataPath, true);
                    }
                    
                    // Delete ApplicationData folder (contains wizard preferences)
                    if (System.IO.Directory.Exists(roamingPath))
                    {
                        System.IO.Directory.Delete(roamingPath, true);
                    }
                    
                    MessageBox.Show(
                        LocalizationHelper.GetString("ResetEverythingSuccess"),
                        LocalizationHelper.GetString("ResetEverythingTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                        
                    // Close the application
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        LocalizationHelper.GetFormattedString("ResetEverythingError", ex.Message),
                        LocalizationHelper.GetString("ResetEverythingTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
        
        private void ChangeApiKeyButton_Click(object sender, RoutedEventArgs e)
        {
            // Get current keys and join them for initial display
            var currentKeys = _youTubeApiService.GetApiKeys();
            string initialKeysText = string.Join(",", currentKeys); // Join with comma, no space

            var dialog = new InputDialog(
                LocalizationHelper.GetString("YouTubeApiKey"), // Title
                LocalizationHelper.GetString("ApiKeyHelpText"), // Help text
                initialKeysText); // Initial value (comma-separated)
                
            if (dialog.ShowDialog() == true)
            {
                // Parse the comma-separated input
                var newKeys = dialog.ResponseText
                                    .Split(',')
                                    .Select(k => k.Trim()) // Trim whitespace
                                    .Where(k => !string.IsNullOrEmpty(k)) // Remove empty entries
                                    .ToList();

                _youTubeApiService.SetApiKeys(newKeys); // Set the list of keys
                
                // Update display text based on number of keys
                if (newKeys.Count == 0)
                {
                     _apiKeyTextBox.Text = LocalizationHelper.GetString("NoApiKeySet");
                }
                else if (newKeys.Count == 1)
                {
                    string singleKey = newKeys[0];
                     _apiKeyTextBox.Text = singleKey.Length > 8 ?
                        $"{singleKey.Substring(0, 4)}...{singleKey.Substring(singleKey.Length - 4)}" :
                        singleKey; // Show short keys directly
                }
                else
                {
                     _apiKeyTextBox.Text = LocalizationHelper.GetString("MultipleKeysSet");
                }
                _apiKey = string.Join(",", newKeys); // Update internal field if needed, though maybe not necessary
            }
        }
        
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Save settings
            LocalizationHelper.SetLanguage(_selectedLanguage);
            WeakReferenceMessenger.Default.Send(new LanguageChangedMessage()); // Notify language change
            
            // Update test mode
            _testMode = _testModeCheckBox.IsChecked ?? false;
            
            var appState = _stateService.GetState();
            appState.TestMode = _testMode;
            // Don't update CacheOnlyMode from settings as it's controlled from the main UI
            _stateService.SaveState(appState);
            
            DialogResult = true;
            Close();
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        // Helper per trovare i controlli figli di un tipo specifico
        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
    }
    
    // Classe di dialogo per l'input di testo
    public class InputDialog : Window
    {
        private TextBox _textBox;
        
        public string ResponseText { get; private set; } = string.Empty;
        
        public InputDialog(string question, string title, string defaultValue = "")
        {
            Title = title;
            Width = 400;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            
            Grid grid = new Grid();
            grid.Margin = new Thickness(10);
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            
            TextBlock textBlock = new TextBlock
            {
                Text = question,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(textBlock, 0);
            grid.Children.Add(textBlock);
            
            _textBox = new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(_textBox, 1);
            grid.Children.Add(_textBox);
            
            StackPanel stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(stackPanel, 2);
            
            Button okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 25,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            okButton.Click += OkButton_Click;
            
            Button cancelButton = new Button
            {
                Content = LocalizationHelper.GetString("Cancel"),
                Width = 80,
                Height = 25,
                IsCancel = true
            };
            cancelButton.Click += CancelButton_Click;
            
            stackPanel.Children.Add(okButton);
            stackPanel.Children.Add(cancelButton);
            grid.Children.Add(stackPanel);
            
            Content = grid;
        }
        
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ResponseText = _textBox.Text;
            DialogResult = true;
            Close();
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
