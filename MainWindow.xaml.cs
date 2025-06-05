using System;
using System.Windows;
using Phonexis.Helpers; // Added for LocalizationHelper
using Phonexis.Services;
using Phonexis.ViewModels;
using Phonexis.Models; // Added for SearchFilters

namespace Phonexis
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly ILoggingService _loggingService;

        public MainWindow(
            MainViewModel viewModel,
            ILoggingService loggingService)
        {
            InitializeComponent();
            
            _viewModel = viewModel;
            _loggingService = loggingService;
            
            // Imposta il DataContext
            DataContext = viewModel;
            
            // Registra gli eventi
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            AboutMenuItem.Click += AboutMenuItem_Click; // Re-enabled

            // Set localized strings for static elements
            SetLocalizedStrings();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Inizializza il ViewModel
                _viewModel.Initialize();
                _loggingService.Info("MainWindow loaded");
            }
            catch (Exception ex)
            {
                _loggingService.Error($"Error initializing MainWindow: {ex.Message}");
                MessageBox.Show($"Error initializing application: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Updates all localized strings in the UI
        /// </summary>
        public void UpdateLocalizedStrings()
        {
            SetLocalizedStrings();
        }

        /// <summary>
        /// Sets all localized strings in the UI
        /// </summary>
        private void SetLocalizedStrings()
        {
            this.Title = LocalizationHelper.GetString("WindowTitle");
            
            // Localize Buttons
            BrowseButton.Content = LocalizationHelper.GetString("BrowseButton");
            CreateTxtButton.Content = LocalizationHelper.GetString("CreateTXT");
            StartSearchButton.Content = LocalizationHelper.GetString("StartButton");

            // Localize Menu Items
            FileMenu.Header = LocalizationHelper.GetString("FileMenu");
            SaveStateMenuItem.Header = LocalizationHelper.GetString("SaveStateMenuItem");
            ResumeStateMenuItem.Header = LocalizationHelper.GetString("ResumeStateMenuItem");
            StartOverMenuItem.Header = LocalizationHelper.GetString("StartOverMenuItem");
            ExitMenuItem.Header = LocalizationHelper.GetString("ExitMenuItem");
            
            ToolsMenu.Header = LocalizationHelper.GetString("ToolsMenu");
            EditQueryMenuItem.Header = LocalizationHelper.GetString("EditQueryMenuItem");
            SettingsMenuItem.Header = LocalizationHelper.GetString("SettingsMenuItem");
            
            HelpMenu.Header = LocalizationHelper.GetString("HelpMenu");
            AboutMenuItem.Header = LocalizationHelper.GetString("AboutMenuItem");
            
            // Localize Checkboxes
            CacheOnlyModeCheckBox.Content = LocalizationHelper.GetString("CacheOnlyModeCheckbox");
            
            // Localize Footer
            CopyrightTextBlock.Text = LocalizationHelper.GetString("CopyrightNotice");
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e) // Made sender nullable
        {
            try
            {
                // Salva lo stato prima di chiudere (senza mostrare dialog di errore)
                _viewModel.SaveState(showConfirmation: false, showErrorDialog: false);
                _loggingService.Info("MainWindow closing, state saved");
            }
            catch (Exception ex)
            {
                _loggingService.Error($"Error saving state on closing: {ex.Message}");
            }
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Save state before exiting (senza mostrare dialog di errore)
                _viewModel.SaveState(showConfirmation: false, showErrorDialog: false);
                _loggingService.Info("Application exiting via menu, state saved");
            }
            catch (Exception ex)
            {
                _loggingService.Error($"Error saving state on exit: {ex.Message}");
            }
            
            Application.Current.Shutdown();
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new Views.AboutWindow();
            aboutWindow.Owner = this; // Set the owner for centering
            aboutWindow.ShowDialog();
        }

        /// <summary>
        /// Gestore per il cambio dei filtri di ricerca
        /// </summary>
        private async void SearchFiltersPanel_FiltersChanged(object sender, SearchFilters filters)
        {
            try
            {
                // Applica i filtri al ViewModel
                await _viewModel.ApplySearchFilters(filters);
                _loggingService.Info($"Search filters applied: Duration={filters.Duration}, Date={filters.Date}, Quality={filters.Quality}");
            }
            catch (Exception ex)
            {
                _loggingService.Error($"Error applying search filters: {ex.Message}");
            }
        }
    }
}
