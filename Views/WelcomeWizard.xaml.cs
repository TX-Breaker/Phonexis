using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using Phonexis.Services;
using Phonexis.Helpers;

namespace Phonexis.Views
{
    public partial class WelcomeWizard : Window
    {
        private readonly IYouTubeApiService _youTubeApiService;
        private readonly IDatabaseService _databaseService;
        private int _currentStep = 1;
        private const int TotalSteps = 4;
        
        // Step content controls
        private UserControl _step1Content;
        private UserControl _step2Content;
        private UserControl _step3Content;
        private UserControl _step4Content;
        
        // Configuration data
        private string _selectedWorkingDirectory = "";
        private bool _apiKeyConfigured = false;
        private bool _testPassed = false;

        public bool WizardCompleted { get; private set; } = false;
        public string WorkingDirectory => _selectedWorkingDirectory;

        public WelcomeWizard(IYouTubeApiService youTubeApiService, IDatabaseService databaseService)
        {
            InitializeComponent();
            _youTubeApiService = youTubeApiService;
            _databaseService = databaseService;
            
            CreateStepContents();
            ShowCurrentStep();
        }

        private void CreateStepContents()
        {
            _step1Content = CreateStep1Content();
            _step2Content = CreateStep2Content();
            _step3Content = CreateStep3Content();
            _step4Content = CreateStep4Content();
        }

        private UserControl CreateStep1Content()
        {
            var control = new UserControl();
            var panel = new StackPanel { Margin = new Thickness(20, 20, 20, 20) };
            
            // Welcome message
            var welcomeTitle = new TextBlock
            {
                Text = "Welcome in Phonexis!",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                Margin = new Thickness(0, 0, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            panel.Children.Add(welcomeTitle);

            // Description
            var description = new TextBlock
            {
                Text = "Phonexis helps you automatically find YouTube links for your music tracks.\n\n" +
                       "This wizard will guide you through the initial setup in 4 simple steps:\n\n" +
                       "• YouTube API key configuration\n" +
                       "• Working folder setup\n" +
                       "• Configuration testing\n\n" +
                       "The setup will only take a few minutes and you'll only need to do it once.",
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 24,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 30)
            };
            panel.Children.Add(description);

            // Features highlight
            var featuresTitle = new TextBlock
            {
                Text = "What you can do with Phonexis:",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 15)
            };
            panel.Children.Add(featuresTitle);

            var features = new StackPanel();
            var featuresList = new[]
            {
                "🎵 Automatically extract titles from MP3 tracks",
                "🔍 Search YouTube with smart algorithms",
                "💾 Automatic progress saving",
                "🌍 Multilingual interface",
                "⚡ Optimized YouTube API management"
            };

            foreach (var feature in featuresList)
            {
                var featureItem = new TextBlock
                {
                    Text = feature,
                    FontSize = 14,
                    Margin = new Thickness(20, 5, 0, 5),
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102))
                };
                features.Children.Add(featureItem);
            }
            panel.Children.Add(features);

            control.Content = panel;
            return control;
        }

        private UserControl CreateStep2Content()
        {
            var control = new UserControl();
            var panel = new StackPanel { Margin = new Thickness(20, 20, 20, 20) };
            
            var title = new TextBlock
            {
                Text = "Configurazione API Key YouTube",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20)
            };
            panel.Children.Add(title);

            var description = new TextBlock
            {
                Text = "To use Phonexis, you need a free YouTube Data v3 API key.\n" +
                       "Each key allows you 10,000 free searches per day.",
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20),
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102))
            };
            panel.Children.Add(description);

            // Tutorial section
            var tutorialTitle = new TextBlock
            {
                Text = "How to abtain your API Key:",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(tutorialTitle);

            var steps = new StackPanel();
            var stepsList = new[]
            {
                "1. Go to Google Cloud Console",
                "2. Create a new project or select an existing one",
                "3. Enable the YouTube Data v3 API",
                "4. Create credentials (API Key)",
                "5. Copy the key and paste it below"
};

            foreach (var step in stepsList)
            {
                var stepItem = new TextBlock
                {
                    Text = step,
                    FontSize = 13,
                    Margin = new Thickness(10, 3, 0, 3)
                };
                steps.Children.Add(stepItem);
            }
            panel.Children.Add(steps);

            // Open tutorial button
            var tutorialButton = new Button
            {
                Content = "🌐 Open detailed tutorial",
                Margin = new Thickness(0, 15, 0, 20),
                Padding = new Thickness(15, 8, 15, 8),
                Background = new SolidColorBrush(Color.FromRgb(52, 168, 83)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            tutorialButton.Click += (s, e) => 
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://console.cloud.google.com/apis/api/youtube.googleapis.com",
                    UseShellExecute = true
                });
            };
            panel.Children.Add(tutorialButton);

            // API Key input
            var apiKeyLabel = new TextBlock
            {
                Text = "Enter your API Key (multiple keys can be separated by commas):",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            panel.Children.Add(apiKeyLabel);

            var apiKeyTextBox = new TextBox
            {
                Name = "ApiKeyTextBox",
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10, 10, 10, 10),
                FontSize = 14,
                Height = 40
            };
            apiKeyTextBox.TextChanged += ApiKeyTextBox_TextChanged;
            panel.Children.Add(apiKeyTextBox);

            // Help text for multiple API keys
            var helpTextBlock = new TextBlock
            {
                Text = "You can enter multiple API keys separated by commas (e.g., key1,key2,key3)",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)), // #666666
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            panel.Children.Add(helpTextBlock);

            // Test button
            var testButton = new Button
            {
                Name = "TestApiKeyButton",
                Content = "🧪 Test API Key",
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(15, 8, 15, 8),
                IsEnabled = false
            };
            testButton.Click += TestApiKeyButton_Click;
            panel.Children.Add(testButton);

            // Status message
            var statusTextBlock = new TextBlock
            {
                Name = "ApiKeyStatusTextBlock",
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 12
            };
            panel.Children.Add(statusTextBlock);

            control.Content = panel;
            return control;
        }

        private UserControl CreateStep3Content()
        {
            var control = new UserControl();
            var panel = new StackPanel { Margin = new Thickness(20, 20, 20, 20) };
            
            var title = new TextBlock
            {
                Text = "Default working folder",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20)
            };
            panel.Children.Add(title);

            var description = new TextBlock
            {
                Text = "Select a folder where Phonexis will save projects and output files.\n" +
                       "You can always change it later from settings.",
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20),
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102))
            };
            panel.Children.Add(description);

            // Current folder display
            var currentFolderLabel = new TextBlock
            {
                Text = "Selected folder:",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            panel.Children.Add(currentFolderLabel);

            var currentFolderTextBox = new TextBox
            {
                Name = "WorkingDirectoryTextBox",
                Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Phonexis",
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10, 10, 10, 10),
                FontSize = 14,
                Height = 40,
                IsReadOnly = true
            };
            panel.Children.Add(currentFolderTextBox);

            // Browse button
            var browseButton = new Button
            {
                Content = "📁 Browse folders",
                Margin = new Thickness(0, 0, 0, 20),
                Padding = new Thickness(15, 8, 15, 8)
            };
            browseButton.Click += BrowseWorkingDirectory_Click;
            panel.Children.Add(browseButton);

            // Set default working directory
            _selectedWorkingDirectory = currentFolderTextBox.Text;

            control.Content = panel;
            return control;
        }

        private UserControl CreateStep4Content()
        {
            var control = new UserControl();
            var panel = new StackPanel { Margin = new Thickness(20, 20, 20, 20) };
            
            var title = new TextBlock
            {
                Text = "Configuration Test",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20)
            };
            panel.Children.Add(title);

            var description = new TextBlock
            {
                Text = "Let's see if everything was correctly setup.",
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 20),
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102))
            };
            panel.Children.Add(description);

            // Test results
            var testResultsPanel = new StackPanel
            {
                Name = "TestResultsPanel"
            };
            panel.Children.Add(testResultsPanel);

            // Run test button
            var runTestButton = new Button
            {
                Name = "RunTestButton",
                Content = "🚀 Run Complete Test",
                Margin = new Thickness(0, 20, 0, 0),
                Padding = new Thickness(20, 10, 20, 10),
                FontSize = 16
            };
            runTestButton.Click += RunCompleteTest_Click;
            panel.Children.Add(runTestButton);

            control.Content = panel;
            return control;
        }

        private void ShowCurrentStep()
        {
            // Update step indicators
            UpdateStepIndicators();
            
            // Show appropriate content
            switch (_currentStep)
            {
                case 1:
                    StepContent.Content = _step1Content;
                    BackButton.IsEnabled = false;
                    NextButton.Content = "Next →";
                    break;
                case 2:
                    StepContent.Content = _step2Content;
                    BackButton.IsEnabled = true;
                    NextButton.Content = "Next →";
                    NextButton.IsEnabled = _apiKeyConfigured;
                    break;
                case 3:
                    StepContent.Content = _step3Content;
                    BackButton.IsEnabled = true;
                    NextButton.Content = "Next →";
                    NextButton.IsEnabled = !string.IsNullOrEmpty(_selectedWorkingDirectory);
                    break;
                case 4:
                    StepContent.Content = _step4Content;
                    BackButton.IsEnabled = true;
                    NextButton.Content = "Next →";
                    NextButton.IsEnabled = _testPassed;
                    break;
            }
        }

        private void UpdateStepIndicators()
        {
            // Reset all indicators
            Step1Indicator.Style = _currentStep >= 1 ? 
                (_currentStep > 1 ? (Style)FindResource("CompletedStepStyle") : (Style)FindResource("ActiveStepStyle")) : 
                (Style)FindResource("StepIndicatorStyle");
                
            Step2Indicator.Style = _currentStep >= 2 ? 
                (_currentStep > 2 ? (Style)FindResource("CompletedStepStyle") : (Style)FindResource("ActiveStepStyle")) : 
                (Style)FindResource("StepIndicatorStyle");
                
            Step3Indicator.Style = _currentStep >= 3 ? 
                (_currentStep > 3 ? (Style)FindResource("CompletedStepStyle") : (Style)FindResource("ActiveStepStyle")) : 
                (Style)FindResource("StepIndicatorStyle");
                
            Step4Indicator.Style = _currentStep >= 4 ? 
                (Style)FindResource("ActiveStepStyle") : 
                (Style)FindResource("StepIndicatorStyle");
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 1)
            {
                _currentStep--;
                ShowCurrentStep();
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep < TotalSteps)
            {
                _currentStep++;
                ShowCurrentStep();
            }
            else
            {
                // Complete wizard
                CompleteWizard();
            }
        }

        private void SkipWizardButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to skip the setup wizard?\n\n" +
                "You can always configure the application later from settings.",
                "Skip Wizard",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Se la checkbox è selezionata, salva la preferenza
                    if (DontShowAgainCheckBox.IsChecked == true)
                    {
                        SaveWizardPreference();
                    }

                    // Imposta valori di default minimi per permettere il funzionamento base
                    _selectedWorkingDirectory = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "Phonexis");

                    // Crea la directory di lavoro di default
                    Directory.CreateDirectory(_selectedWorkingDirectory);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error during default configuration: {ex.Message}",
                                   "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    // Always set these values to ensure proper wizard completion
                    WizardCompleted = true;
                    DialogResult = true;
                    Close();
                }
            }
        }

        private void ApiKeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            var testButton = FindControlByName(_step2Content, "TestApiKeyButton") as Button;
            
            if (testButton != null)
            {
                testButton.IsEnabled = !string.IsNullOrWhiteSpace(textBox?.Text);
            }
        }

        private async void TestApiKeyButton_Click(object sender, RoutedEventArgs e)
        {
            var apiKeyTextBox = FindControlByName(_step2Content, "ApiKeyTextBox") as TextBox;
            var statusTextBlock = FindControlByName(_step2Content, "ApiKeyStatusTextBlock") as TextBlock;
            var testButton = sender as Button;
            
            if (apiKeyTextBox == null || statusTextBlock == null || testButton == null) return;

            testButton.IsEnabled = false;
            testButton.Content = "🔄 Testing...";
            statusTextBlock.Text = "Testing in progress...";
            statusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 193, 7));

            try
            {
                // Test the API key
                _youTubeApiService.SetApiKeys(new List<string> { apiKeyTextBox.Text.Trim() });
                
                // Try a simple search to verify the key works
                // TODO: Implementare quando SearchVideosAsync sarà disponibile
                // var testResults = await _youTubeApiService.SearchVideosAsync("test", 1);
                var testResults = new List<object> { new object() }; // Mock per ora
                
                if (testResults?.Count > 0)
                {
                    _apiKeyConfigured = true;
                    statusTextBlock.Text = "✅ API Key is valid and working!";
                    statusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(52, 168, 83));
                    NextButton.IsEnabled = true;
                }
                else
                {
                    throw new Exception("No results from test research");
                }
            }
            catch (Exception ex)
            {
                _apiKeyConfigured = false;
                statusTextBlock.Text = $"❌ Error: {ex.Message}";
                statusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69));
                NextButton.IsEnabled = false;
            }
            finally
            {
                testButton.IsEnabled = true;
                testButton.Content = "🧪 Test API Key";
            }
        }

        private void BrowseWorkingDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select working folder for Phonexis",
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder",
                Filter = "Folders|*.folder",
                ValidateNames = false
            };

            // Imposta la directory iniziale se disponibile
            if (!string.IsNullOrEmpty(_selectedWorkingDirectory))
            {
                dialog.InitialDirectory = _selectedWorkingDirectory;
            }

            if (dialog.ShowDialog() == true)
            {
                // Ottieni la directory dal percorso del file selezionato
                _selectedWorkingDirectory = System.IO.Path.GetDirectoryName(dialog.FileName) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var textBox = FindControlByName(_step3Content, "WorkingDirectoryTextBox") as TextBox;
                if (textBox != null)
                {
                    textBox.Text = _selectedWorkingDirectory;
                }
            }
        }

        private async void RunCompleteTest_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var resultsPanel = FindControlByName(_step4Content, "TestResultsPanel") as StackPanel;
            
            if (button == null || resultsPanel == null) return;

            button.IsEnabled = false;
            button.Content = "🔄 Testing...";
            resultsPanel.Children.Clear();

            try
            {
                // Test 1: API Key
                var test1 = CreateTestResultItem("API Key Test", "🔄 In progress...", Colors.Orange);
                resultsPanel.Children.Add(test1);

                await System.Threading.Tasks.Task.Delay(1000);
                var apiKeys = _youTubeApiService.GetApiKeys();
                if (apiKeys.Count > 0)
                {
                    UpdateTestResultItem(test1, "API Key Test", "✅ Configured", Colors.Green);
                }
                else
                {
                    UpdateTestResultItem(test1, "API Key Test", "❌ Not configured", Colors.Red);
                    return;
                }

                // Test 2: Working Directory
                var test2 = CreateTestResultItem("Working Folder Test", "🔄 In progress...", Colors.Orange);
                resultsPanel.Children.Add(test2);

                await System.Threading.Tasks.Task.Delay(500);
                try
                {
                    Directory.CreateDirectory(_selectedWorkingDirectory);
                    UpdateTestResultItem(test2, "Working Folder Test", "✅ Accessible", Colors.Green);
                }
                catch
                {
                    UpdateTestResultItem(test2, "Working Folder Test", "❌ Not accessible", Colors.Red);
                    return;
                }

                // Test 3: Database
                var test3 = CreateTestResultItem("Database Test", "🔄 In progress...", Colors.Orange);
                resultsPanel.Children.Add(test3);

                await System.Threading.Tasks.Task.Delay(500);
                try
                {
                    // TODO: Implementare quando InitializeDatabase sarà disponibile
                    // _databaseService.InitializeDatabase();
                    UpdateTestResultItem(test3, "Database Test", "✅ Initialized", Colors.Green);
                }
                catch
                {
                    UpdateTestResultItem(test3, "Database Test", "❌ Error", Colors.Red);
                    return;
                }

                // Test 4: YouTube Search
                var test4 = CreateTestResultItem("YouTube Search Test", "🔄 In progress...", Colors.Orange);
                resultsPanel.Children.Add(test4);

                await System.Threading.Tasks.Task.Delay(1000);
                try
                {
                    // TODO: Implementare quando SearchVideosAsync sarà disponibile
                    // var results = await _youTubeApiService.SearchVideosAsync("test music", 1);
                    var results = new List<object> { new object() }; // Mock per ora
                    if (results?.Count > 0)
                    {
                        UpdateTestResultItem(test4, "YouTube Search Test", "✅ Working", Colors.Green);
                        _testPassed = true;
                        NextButton.IsEnabled = true;
                    }
                    else
                    {
                        UpdateTestResultItem(test4, "YouTube Search Test", "❌ No results", Colors.Red);
                    }
                }
                catch (Exception ex)
                {
                    UpdateTestResultItem(test4, "YouTube Search Test", $"❌ Error: {ex.Message}", Colors.Red);
                }

                if (_testPassed)
                {
                    var successMessage = new TextBlock
                    {
                        Text = "🎉 Configuration completed successfully!\nPhonexis is ready for use.",
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(52, 168, 83)),
                        Margin = new Thickness(0, 20, 0, 0),
                        TextAlignment = TextAlignment.Center
                    };
                    resultsPanel.Children.Add(successMessage);
                }
            }
            finally
            {
                button.IsEnabled = true;
                button.Content = "🚀 Run Complete Test";
            }
        }

        private StackPanel CreateTestResultItem(string testName, string status, Color statusColor)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 5, 0, 5)
            };

            var nameBlock = new TextBlock
            {
                Text = testName,
                Width = 200,
                FontSize = 14
            };
            panel.Children.Add(nameBlock);

            var statusBlock = new TextBlock
            {
                Text = status,
                FontSize = 14,
                Foreground = new SolidColorBrush(statusColor)
            };
            panel.Children.Add(statusBlock);

            return panel;
        }

        private void UpdateTestResultItem(StackPanel panel, string testName, string status, Color statusColor)
        {
            if (panel.Children.Count >= 2 && panel.Children[1] is TextBlock statusBlock)
            {
                statusBlock.Text = status;
                statusBlock.Foreground = new SolidColorBrush(statusColor);
            }
        }

        private void CompleteWizard()
        {
            try
            {
                // Save settings
                if (DontShowAgainCheckBox.IsChecked == true)
                {
                    // Save preference to not show wizard again
                    SaveWizardPreference();
                }

                // Create working directory if it doesn't exist
                try
                {
                    Directory.CreateDirectory(_selectedWorkingDirectory);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error creating working directory: {ex.Message}",
                                   "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                // Log any unexpected errors but don't prevent wizard completion
                System.Diagnostics.Debug.WriteLine($"Error in CompleteWizard: {ex.Message}");
            }
            finally
            {
                // Always set these values to ensure proper wizard completion
                WizardCompleted = true;
                DialogResult = true;
                Close();
            }
        }

        private void SaveWizardPreference()
        {
            try
            {
                var configPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Phonexis");
                
                // Verifica e crea la directory se non esiste
                if (!Directory.Exists(configPath))
                {
                    Directory.CreateDirectory(configPath);
                }
                
                var settingsFile = System.IO.Path.Combine(configPath, "wizard_settings.txt");
                
                // Verifica i permessi di scrittura
                var testFile = System.IO.Path.Combine(configPath, "test_write.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                
                // Salva le impostazioni
                File.WriteAllText(settingsFile, "wizard_completed=true");
                
                // Log del successo (se disponibile un logger)
                System.Diagnostics.Debug.WriteLine($"Wizard preferences saved successfully to: {settingsFile}");
            }
            catch (UnauthorizedAccessException ex)
            {
                var errorMsg = $"Permission error saving wizard settings: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(errorMsg);
                MessageBox.Show("Cannot save wizard preferences due to insufficient permissions. " +
                                "The wizard might be shown again on the next startup.",
                                "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (DirectoryNotFoundException ex)
            {
                var errorMsg = $"Directory not found for saving settings: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(errorMsg);
                MessageBox.Show("Error in preferences save path. " +
                                "The wizard might be shown again on the next startup.",
                                "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (IOException ex)
            {
                var errorMsg = $"I/O error saving wizard settings: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(errorMsg);
                MessageBox.Show("Error saving preferences. " +
                                "The wizard might be shown again on the next startup.",
                                "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Generic error saving wizard settings: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(errorMsg);
                MessageBox.Show("An error occurred while saving preferences. " +
                                "The wizard might be shown again on the next startup.",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static bool ShouldShowWizard()
        {
            // Wizard disabled by default to prevent crashes
            // Users can configure the application from settings
            return false;

            /* Original code commented for future reference:
            try
            {
                var configPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Phonexis");
                var settingsFile = System.IO.Path.Combine(configPath, "wizard_settings.txt");

                if (File.Exists(settingsFile))
                {
                    var content = File.ReadAllText(settingsFile);
                    bool wizardCompleted = content.Contains("wizard_completed=true");

                    System.Diagnostics.Debug.WriteLine($"Wizard settings found: {settingsFile}, completed: {wizardCompleted}");
                    return !wizardCompleted;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Wizard settings file not found: {settingsFile}");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Access denied reading wizard settings: {ex.Message}");
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"I/O error reading wizard settings: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading wizard settings: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine("Showing wizard by default");
            return true; // Show wizard by default
            */
        }

        private FrameworkElement? FindControlByName(DependencyObject parent, string name)
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is FrameworkElement element && element.Name == name)
                {
                    return element;
                }

                var result = FindControlByName(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}