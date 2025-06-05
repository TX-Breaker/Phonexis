using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Phonexis.Controls;
using Phonexis.Models;
using Phonexis.Services;
using Phonexis.Views; // Added using directive
// using Phonexis.Properties; // No longer needed
using CommunityToolkit.Mvvm.Messaging; // Assuming CommunityToolkit.Mvvm is used for messaging
using Phonexis.Messages; // Added for message class
using Phonexis.Helpers; // Added for LocalizationHelper

namespace Phonexis.ViewModels
{
    /// <summary>
    /// ViewModel principale dell'applicazione
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ILoggingService _loggingService;
        private readonly IDatabaseService _databaseService;
        private readonly IYouTubeApiService _youTubeApiService;
        private readonly IStateService _stateService;
        private readonly IAutoSaveService _autoSaveService;
        
        private Models.AppState _state = null!; // Initialized
        private List<string> _songTitles;
        private List<string> _noResultsLog;
        private object? _currentView = null; // Made nullable and initialized
        private string _lastSaveTime = string.Empty; // Initialized
        private string _statusMessage = "Ready"; // Added for Status Bar
        private SaveStatus _saveStatus = SaveStatus.Idle;
        private string _saveStatusMessage = "Pronto";
        private SearchFilters _currentFilters = new SearchFilters(); // Added for search filters
        private List<SearchResult> _allSearchResults = new List<SearchResult>(); // Store all results before filtering

        #region Localized UI Properties

        public string ApiCounterText
        {
            get
            {
                try
                {
                    // Calculate total calls today by summing counts for all keys
                    var today = DateTime.UtcNow.Date;
                    var countsToday = _databaseService.GetApiCallCounts(today);
                    int totalCallsToday = countsToday.Sum(kvp => kvp.Value);
                    
                    // Debug logging
                    _loggingService.Debug($"API Counter - Date: {today:yyyy-MM-dd}, Keys found: {countsToday.Count}, Total calls: {totalCallsToday}");
                    foreach (var kvp in countsToday)
                    {
                        _loggingService.Debug($"API Counter - Key ending in ...{kvp.Key.Substring(Math.Max(0, kvp.Key.Length - 4))}: {kvp.Value} calls");
                    }
                    
                    return LocalizationHelper.GetFormattedString("ApiCounterLabel", totalCallsToday);
                }
                catch (Exception ex)
                {
                    _loggingService.Error($"Error calculating API counter: {ex.Message}");
                    return LocalizationHelper.GetFormattedString("ApiCounterLabel", 0);
                }
            }
        }
        public string ClearCacheMenuItemHeader => LocalizationHelper.GetString("ClearCache");
        // Add other dynamic string properties here if needed in future

        #endregion

        #region State Properties
        
        /// <summary>
        /// Messaggio di stato corrente per la barra di stato
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Stato dell'indicatore di salvataggio
        /// </summary>
        public SaveStatus SaveStatus
        {
            get => _saveStatus;
            set
            {
                if (_saveStatus != value)
                {
                    _saveStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Messaggio dell'indicatore di salvataggio
        /// </summary>
        public string SaveStatusMessage
        {
            get => _saveStatusMessage;
            set
            {
                if (_saveStatusMessage != value)
                {
                    _saveStatusMessage = value;
                    OnPropertyChanged();
                }
            }
        }
        // Removed ApiCallCount property as it's now calculated in ApiCounterText

        /// <summary>
        /// Informazioni sulla chiave API in uso
        /// </summary>
        public string ApiKeyInfo
        {
            get
            {
                var apiKeys = _youTubeApiService.GetApiKeys();
                if (apiKeys.Count == 0)
                    return LocalizationHelper.GetString("NoApiKeysTitle");
                    
                return $"API Key {_youTubeApiService.GetCurrentKeyIndex() + 1} of {apiKeys.Count}"; // Keep simple for now, complex formatting later
            }
        }
        
        /// <summary>
        /// Informazioni sull'ultimo salvataggio
        /// </summary>
        public string LastSaveInfo
        {
            get
            {
                // Check if state and file path exist and timestamp is valid
                if (_state != null && !string.IsNullOrEmpty(_state.TxtFilePath) && _state.LastSaveTimestamp > DateTime.MinValue)
                {
                    string fileName = Path.GetFileName(_state.TxtFilePath);
                    string timestamp = _state.LastSaveTimestamp.ToString("g"); // General date/time pattern (short time)
                    int current = _state.CurrentSongIndex + 1; // Display 1-based index
                    int total = _songTitles?.Count ?? 0;
                    
                    // Use a new resource string for formatting
                    return LocalizationHelper.GetFormattedString("SavedStateInfoFormat", timestamp, fileName, current, total);
                }
                else
                {
                    return LocalizationHelper.GetString("NoSavedStateMessage");
                }
            }
        }

        /// <summary>
        /// Etichetta per il file di testo selezionato
        /// </summary>
        public string TxtFileLabel => string.IsNullOrEmpty(_state?.TxtFilePath) 
            ? LocalizationHelper.GetString("SelectFileLabelDefault")
            : LocalizationHelper.GetFormattedString("SelectFileLabelSelected", Path.GetFileName(_state.TxtFilePath));

        /// <summary>
        /// Indica se è possibile avviare la ricerca
        /// </summary>
        public bool CanStartSearch => !string.IsNullOrEmpty(_state?.TxtFilePath) && _songTitles?.Count > 0;
        
        /// <summary>
        /// Modalità test
        /// </summary>
        public bool TestMode
        {
            get => _state?.TestMode ?? false;
            set
            {
                if (_state != null && _state.TestMode != value)
                {
                    _state.TestMode = value;
                    OnPropertyChanged();
                    SaveState(false); // Save state when mode changes
                }
            }
        }
        
        /// <summary>
        /// Modalità solo cache
        /// </summary>
        public bool CacheOnlyMode
        {
            get => _state?.CacheOnlyMode ?? false;
            set
            {
                if (_state != null && _state.CacheOnlyMode != value)
                {
                    _state.CacheOnlyMode = value;
                    OnPropertyChanged();
                    SaveState(false); // Save state when mode changes
                }
            }
        }
        
        /// <summary>
        /// Valore corrente della barra di progresso
        /// </summary>
        public int ProgressValue => (_state?.CurrentSongIndex ?? 0) + 1;
        
        /// <summary>
        /// Valore massimo della barra di progresso
        /// </summary>
        public int ProgressMaximum => _songTitles?.Count ?? 0;
        
        /// <summary>
        /// Testo del contatore delle canzoni
        /// </summary>
        public string SongCounterText => $"{ProgressValue}/{ProgressMaximum}";
        
        /// <summary>
        /// Vista corrente
        /// </summary>
        public object? CurrentView
        {
            get => _currentView;
            set
            {
                _currentView = value;
                OnPropertyChanged();
            }
        }
        
        #endregion
        
        #region Comandi
        
        /// <summary>
        /// Comando per sfogliare il file di testo
        /// </summary>
        public ICommand BrowseTxtFileCommand { get; private set; } = null!; // Initialized

        /// <summary>
        /// Comando per avviare la ricerca
        /// </summary>
        public ICommand StartSearchCommand { get; private set; } = null!; // Initialized

        /// <summary>
        /// Comando per salvare lo stato
        /// </summary>
        public ICommand SaveStateCommand { get; private set; } = null!; // Initialized

        /// <summary>
        /// Comando per riprendere l'ultimo stato
        /// </summary>
        public ICommand ResumeStateCommand { get; private set; } = null!; // Initialized

        /// <summary>
        /// Comando per ricominciare
        /// </summary>
        public ICommand StartOverCommand { get; private set; } = null!; // Initialized
        
        /// <summary>
        /// Comando per pulire la cache
        /// </summary>
        public ICommand ClearCacheCommand { get; private set; } = null!; // Initialized
        public ICommand EditQueryCommand { get; private set; } = null!; // Initialized

        /// <summary>
        /// Comando per impostare la chiave API
        /// </summary>
        public ICommand SetApiKeyCommand { get; private set; } = null!; // Initialized

        /// <summary>
        /// Comando per aprire le impostazioni
        /// </summary>
        public ICommand OpenSettingsCommand { get; private set; } = null!; // Initialized
        
        /// <summary>
        /// Comando per aprire il recuperatore di nomi di file audio
        /// </summary>
        public ICommand OpenAudioFilenameRetrieverCommand { get; private set; } = null!; // Initialized
        
        #endregion
        
        /// <summary>
        /// Costruttore
        /// </summary>
        public MainViewModel(
            ILoggingService loggingService,
            IDatabaseService databaseService,
            IYouTubeApiService youTubeApiService,
            IStateService stateService,
            IAutoSaveService autoSaveService)
        {
            _loggingService = loggingService;
            _databaseService = databaseService;
            _youTubeApiService = youTubeApiService;
            _stateService = stateService;
            _autoSaveService = autoSaveService;
            
            // Inizializza le collezioni
            _songTitles = new List<string>();
            _noResultsLog = new List<string>();
            
            // Sottoscrivi all'evento di cambiamento del conteggio delle chiamate API
            _databaseService.ApiCallCountChanged += (sender, e) =>
            {
                _loggingService.Debug("API call count changed event received - updating UI counter");
                // Notifica il cambiamento del testo formattato del contatore API
                OnPropertyChanged(nameof(ApiCounterText)); // Notify the formatted text property
            };

            // Subscribe to language change messages (using CommunityToolkit.Mvvm Messenger)
            WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (r, m) => HandleLanguageChange());

            // Configura l'AutoSaveService
            ConfigureAutoSaveService();

            // Inizializza i comandi
            InitializeCommands();
        }
        
        /// <summary>
        /// Inizializza i comandi
        /// </summary>
        private void InitializeCommands()
        {
            BrowseTxtFileCommand = new RelayCommand(BrowseTxtFile);
            StartSearchCommand = new RelayCommand(StartSearch, () => CanStartSearch);
            SaveStateCommand = new RelayCommand(() => SaveState(true)); // Passa true per mostrare il messaggio di conferma
            ResumeStateCommand = new RelayCommand(ResumeLastState);
            StartOverCommand = new RelayCommand(StartOver);
            SetApiKeyCommand = new RelayCommand(SetApiKey);
            OpenSettingsCommand = new RelayCommand(OpenSettings); // Re-enabled
            OpenAudioFilenameRetrieverCommand = new RelayCommand(OpenAudioFilenameRetriever);
            EditQueryCommand = new RelayCommand(EditQuery, () => _state?.CurrentSongIndex < _songTitles?.Count);
            ClearCacheCommand = new RelayCommand(ClearCache); // Initialize command
        }
        
        /// <summary>
        /// Inizializza il ViewModel
        /// </summary>
        public void Initialize()
        {
            // Crea o carica lo stato
            if (_stateService.StateFileExists())
            {
                _state = _stateService.LoadState();
                _loggingService.Info("Stato caricato");
                
                // Carica i titoli delle canzoni se è stato selezionato un file
                if (!string.IsNullOrEmpty(_state.TxtFilePath))
                {
                    LoadSongTitles();
                }
            }
            else
            {
                _state = new Models.AppState();
                _loggingService.Info("Nuovo stato creato");
            }
            
            // Notifica le proprietà
            // Removed
            OnPropertyChanged(nameof(TxtFileLabel));
            OnPropertyChanged(nameof(CanStartSearch));
            OnPropertyChanged(nameof(TestMode));
            OnPropertyChanged(nameof(CacheOnlyMode));
            OnPropertyChanged(nameof(ProgressValue));
            OnPropertyChanged(nameof(ProgressMaximum));
            OnPropertyChanged(nameof(SongCounterText));
            OnPropertyChanged(nameof(LastSaveInfo)); // Notify LastSaveInfo
            StatusMessage = LocalizationHelper.GetString("StatusReady"); // Set initial status
            
            // Avvia l'AutoSaveService
            _autoSaveService.Start();
        }
        
        /// <summary>
        /// Sfoglia per selezionare un file di testo
        /// </summary>
        private void BrowseTxtFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt", // Filter might not need localization
                Title = LocalizationHelper.GetString("SelectFileLabelDefault"),
            };
            
            if (openFileDialog.ShowDialog() == true)
            {
                _state.TxtFilePath = openFileDialog.FileName;
                LoadSongTitles();
                SaveState(false); // Non mostrare il messaggio di conferma

                OnPropertyChanged(nameof(TxtFileLabel));
                OnPropertyChanged(nameof(CanStartSearch));
                StatusMessage = LocalizationHelper.GetFormattedString("StatusFileLoaded", Path.GetFileName(_state.TxtFilePath));
            }
        }
        
        /// <summary>
        /// Carica i titoli delle canzoni dal file di testo
        /// </summary>
        private void LoadSongTitles()
        {
            if (string.IsNullOrEmpty(_state.TxtFilePath))
                return;
                
            try
            {
                _songTitles = new List<string>(File.ReadAllLines(_state.TxtFilePath));
                _loggingService.Debug($"Caricati {_songTitles.Count} titoli di canzoni");
                
                OnPropertyChanged(nameof(ProgressMaximum));
                OnPropertyChanged(nameof(SongCounterText));
            }
            catch (Exception ex)
            {
                _loggingService.Error($"Errore nel caricamento dei titoli delle canzoni: {ex.Message}");
                MessageBox.Show($"Failed to load the file: {ex.Message}", LocalizationHelper.GetString("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Avvia la ricerca su YouTube
        /// </summary>
        private async void StartSearch()
        {
            StatusMessage = LocalizationHelper.GetString("StatusSearching"); // Set searching status
            
            if (_songTitles.Count == 0)
            {
                MessageBox.Show(LocalizationHelper.GetString("NoSongsMessage"), LocalizationHelper.GetString("NoSongsTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Verifica se ci sono chiavi API impostate
            var apiKeys = _youTubeApiService.GetApiKeys();
            if (apiKeys.Count == 0 && !TestMode)
            {
                MessageBoxResult result = MessageBox.Show(
                    LocalizationHelper.GetString("NoApiKeysMessage"),
                    LocalizationHelper.GetString("NoApiKeysTitle"),
                    MessageBoxButton.YesNo, // Buttons might need localization later if standard ones aren't sufficient
                    MessageBoxImage.Question);
                    
                if (result == MessageBoxResult.Yes)
                {
                    SetApiKey();
                    
                    // Verifica nuovamente se sono state impostate chiavi API
                    apiKeys = _youTubeApiService.GetApiKeys();
                    if (apiKeys.Count == 0 && !TestMode)
                    {
                        MessageBox.Show(
                            LocalizationHelper.GetString("NoApiKeysError"),
                            LocalizationHelper.GetString("ErrorTitle"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
            
            // Crea un controllo per visualizzare i risultati
            var resultsPanel = new StackPanel();
            resultsPanel.Name = "ResultsPanel"; // Nome per identificare il pannello nei filtri
            
            // Inizia dalla canzone corrente
            for (int i = _state.CurrentSongIndex; i < _songTitles.Count; i++)
            {
                _state.CurrentSongIndex = i;
                
                // Aggiorna l'interfaccia
                OnPropertyChanged(nameof(ProgressValue));
                OnPropertyChanged(nameof(SongCounterText));
                
                // Ottieni il titolo della canzone
                string songTitle = _songTitles[i];
                
                // Crea un pannello per il titolo e i pulsanti di controllo
                var headerPanel = new Grid();
                headerPanel.Margin = new Thickness(0, 10, 0, 15);
                
                // Definisci le colonne del grid
                headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                
                // Crea un TextBlock per il titolo
                var titleBlock = new TextBlock
                {
                    Text = $"Searching for: {songTitle}",
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(titleBlock, 0);
                headerPanel.Children.Add(titleBlock);
                
                // Crea un pannello per i pulsanti di controllo
                var controlButtonsPanel = new StackPanel { Orientation = Orientation.Horizontal };
                Grid.SetColumn(controlButtonsPanel, 1);

                // Pulsante per modificare la query
                var editQueryButton = new Button
                {
                    Content = LocalizationHelper.GetString("EditQueryButton"),
                    Command = EditQueryCommand, // Use existing command
                    Margin = new Thickness(0, 0, 10, 0),
                    Padding = new Thickness(10, 5, 10, 5),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6D00")) // Orange
                };
                controlButtonsPanel.Children.Add(editQueryButton);

                // Pulsante per aggiornare i risultati
                var refreshButton = new Button
                {
                    Content = LocalizationHelper.GetString("RefreshResultsButton"),
                    Margin = new Thickness(0, 0, 10, 0),
                    Padding = new Thickness(10, 5, 10, 5),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3")) // Blue
                };
                refreshButton.Click += (sender, e) =>
                {
                    // Ricarica i risultati per la canzone corrente
                    StartSearch();
                };
                controlButtonsPanel.Children.Add(refreshButton);

                // Pulsante per tornare alla canzone precedente
                if (_state.CurrentSongIndex > 0)
                {
                    var goBackButton = new Button
                    {
                        Content = LocalizationHelper.GetString("GoBackButton"),
                        Margin = new Thickness(0, 0, 10, 0),
                        Padding = new Thickness(10, 5, 10, 5),
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4B400")) // Yellow
                    };
                    goBackButton.Click += (sender, e) =>
                    {
                        // Torna alla canzone precedente
                        _state.CurrentSongIndex--;
                        SaveState(false);
                        StartSearch();
                    };
                    controlButtonsPanel.Children.Add(goBackButton);
                }

                // Pulsante per saltare questa canzone (ora in fondo e rosso)
                var skipButton = new Button
                {
                    Content = LocalizationHelper.GetString("SkipSongButton"),
                    Margin = new Thickness(0, 0, 0, 0), // No right margin needed if it's the last button
                    Padding = new Thickness(10, 5, 10, 5),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DB4437")) // Red
                };
                skipButton.Click += (sender, e) =>
                {
                    // Passa alla canzone successiva
                    _state.CurrentSongIndex++;
                    // Salva lo stato
                    SaveState(false); // Non mostrare il messaggio di conferma

                    // Continua la ricerca
                    StartSearch();
                };
                controlButtonsPanel.Children.Add(skipButton);

                headerPanel.Children.Add(controlButtonsPanel);
                resultsPanel.Children.Add(headerPanel);
                
                // Imposta la vista corrente
                CurrentView = resultsPanel;
                
                try // Outer try for general UI updates or unexpected issues
                {
                    try // Inner try specifically for the search operation
                    {
                        // Cerca su YouTube
                        List<SearchResult> results;
                        if (TestMode)
                        {
                            results = _youTubeApiService.GenerateDummyResults(songTitle, 5);
                        }
                        else if (CacheOnlyMode)
                        {
                            results = _databaseService.GetSearchResults(songTitle);
                            // Assicuriamoci che i risultati dalla cache siano correttamente etichettati
                            foreach (var result in results)
                            {
                                result.IsFromCache = true;
                                result.IsDummy = false;
                            }
                        }
                        else
                        {
                            // *** API Call happens here ***
                            results = await _youTubeApiService.SearchAsync(songTitle, 5);
                        }
                        
                        if (results.Count == 0)
                        {
                            // Nessun risultato trovato
                            var noResultsBlock = new TextBlock
                            {
                                Text = LocalizationHelper.GetString("NoResultsFound"),
                                Foreground = Brushes.Red,
                                Margin = new Thickness(10, 5, 0, 10)
                            };
                            resultsPanel.Children.Add(noResultsBlock);
                            
                            // Aggiungi al log dei risultati mancanti
                            _noResultsLog.Add(songTitle);
                        }
                        else
                        {
                            // Memorizza tutti i risultati per il filtraggio
                            StoreAllResults(results);
                            
                            // Applica i filtri correnti
                            var filteredResults = results.Where(result => _currentFilters.PassesFilters(result)).ToList();
                            
                            // Crea un controllo per ogni risultato filtrato
                            foreach (var result in filteredResults)
                            {
                                // Crea un controllo personalizzato per il risultato
                                var videoResultControl = new VideoResultControl();
                                
                                // Imposta il risultato di ricerca
                                await videoResultControl.SetSearchResult(result);
                                
                                // Aggiungi il gestore dell'evento VideoSelected
                                videoResultControl.VideoSelected += (sender, selectedResult) =>
                                {
                                    // Aggiungi il link selezionato
                                    _state.SelectedLinks.Add(selectedResult.VideoUrl);
                                    
                                    // Passa alla canzone successiva
                                    _state.CurrentSongIndex++;
                                    // Salva lo stato
                                    SaveState(false); // Non mostrare il messaggio di conferma
    
                                    
                                    // Continua la ricerca
                                    StartSearch();
                                };
                                
                                // Aggiungi il controllo al pannello dei risultati
                                resultsPanel.Children.Add(videoResultControl);
                            }
                        }
                    }
                    catch (InvalidOperationException apiEx) // Catch specific exception for API key failure
                    {
                         _loggingService.Error($"API Error for query '{songTitle}': {apiEx.Message}");
                         MessageBox.Show(
                             LocalizationHelper.GetString("AllApiKeysFailedMessage"),
                             LocalizationHelper.GetString("AllApiKeysFailedTitle"),
                             MessageBoxButton.OK,
                             MessageBoxImage.Error);
                         // Stop the entire search process if all keys fail
                         StatusMessage = LocalizationHelper.GetString("AllApiKeysFailedTitle"); // Update status
                         CurrentView = null; // Clear the view
                         return; // Exit StartSearch method
                    }
                    catch (Exception ex) // Catch general errors during search/view creation
                    {
                        _loggingService.Error($"Error processing query '{songTitle}': {ex.Message}");
                        MessageBox.Show(
                            LocalizationHelper.GetFormattedString("SearchErrorMessage", ex.Message),
                            LocalizationHelper.GetString("SearchErrorTitle"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        
                        // Log as no result and skip to the next song
                        _noResultsLog.Add(songTitle);
                        CurrentView = null; // Clear the view before next iteration
                        continue; // Continue to the next song in the loop
                    }

                    // Crea un pannello per i pulsanti di azione (Moved outside inner try-catch)
                    var actionButtonsPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 10, 0, 20),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    
                    // Pulsante per caricare più risultati
                    var loadMoreButton = new Button
                    {
                        Content = LocalizationHelper.GetString("LoadMoreButton"),
                        Margin = new Thickness(0, 0, 10, 0),
                        Padding = new Thickness(10, 5, 10, 5),
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4285F4"))
                    };
                    loadMoreButton.Click += async (sender, e) =>
                    {
                        // Carica altri 5 risultati
                        List<SearchResult> moreResults;
                        if (TestMode)
                        {
                            moreResults = _youTubeApiService.GenerateDummyResults(songTitle, 5);
                        }
                        else if (CacheOnlyMode)
                        {
                            moreResults = _databaseService.GetSearchResults(songTitle);
                            foreach (var result in moreResults)
                            {
                                result.IsFromCache = true;
                                result.IsDummy = false;
                            }
                        }
                        else
                        {
                            // Aggiorna il contatore API prima della chiamata
                            
                            
                            // Esegui la chiamata API
                            moreResults = await _youTubeApiService.SearchAsync(songTitle, 5);
                            
                            // Aggiorna il contatore API dopo la chiamata
                            
                            
                            // Aggiorna l'interfaccia utente immediatamente
                            await Application.Current.Dispatcher.InvokeAsync(() => {
                                
                            });
                        }
                        
                        // Aggiungi i nuovi risultati
                        foreach (var result in moreResults)
                        {
                            var videoResultControl = new VideoResultControl();
                            await videoResultControl.SetSearchResult(result);
                            
                            videoResultControl.VideoSelected += (s, selectedResult) =>
                            {
                                _state.SelectedLinks.Add(selectedResult.VideoUrl);
                                // Save state BEFORE incrementing index
                                SaveState(false);
                                _state.CurrentSongIndex++;
                                StartSearch();
                            };
                            
                            resultsPanel.Children.Add(videoResultControl);
                        }
                    };
                    actionButtonsPanel.Children.Add(loadMoreButton);

                    // Pulsante per saltare questa canzone (spostato sopra nel controlButtonsPanel)
                    // var skipSongButton = new Button ... (code removed)

                    resultsPanel.Children.Add(actionButtonsPanel);
                    // Salva lo stato
                    SaveState(false); // Non mostrare il messaggio di conferma

                    
                    // Interrompi il ciclo dopo aver mostrato i risultati per la canzone corrente
                    break;
                }
                catch (Exception ex)
                {
                    _loggingService.Error($"Errore nella ricerca: {ex.Message}");
                    
                    var errorBlock = new TextBlock
                    {
                        Text = $"Error: {ex.Message}",
                        Foreground = Brushes.Red,
                        Margin = new Thickness(10, 5, 0, 10)
                    };
                    resultsPanel.Children.Add(errorBlock);
                }
            }
            
            // Verifica se abbiamo completato tutte le canzoni
            if (_state.CurrentSongIndex >= _songTitles.Count)
            {
                StatusMessage = LocalizationHelper.GetString("StatusSearchComplete"); // Set complete status
                
                // Automatically save links on completion
                SaveLinksToFile();
                
                MessageBox.Show(
                    LocalizationHelper.GetString("SearchCompleteMessage"),
                    LocalizationHelper.GetString("SearchCompleteTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                    
                // Mostra un riepilogo
                var summaryPanel = new StackPanel();
                summaryPanel.Children.Add(new TextBlock
                {
                    Text = LocalizationHelper.GetString("SummaryTitle"),
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 10, 0, 10)
                });
                
                summaryPanel.Children.Add(new TextBlock
                {
                    Text = LocalizationHelper.GetFormattedString("SummaryTotalSongs", _songTitles.Count),
                    Margin = new Thickness(0, 5, 0, 0)
                });
                
                summaryPanel.Children.Add(new TextBlock
                {
                    Text = LocalizationHelper.GetFormattedString("SummarySelectedLinks", _state.SelectedLinks.Count),
                    Margin = new Thickness(0, 5, 0, 0)
                });
                
                summaryPanel.Children.Add(new TextBlock
                {
                    Text = LocalizationHelper.GetFormattedString("SummaryNoResults", _noResultsLog.Count),
                    Margin = new Thickness(0, 5, 0, 10)
                });
                
                // Pulsante per salvare i link
                var saveLinksButton = new Button
                {
                    Content = LocalizationHelper.GetString("SaveLinksButton"),
                    Margin = new Thickness(0, 10, 0, 10)
                };
                saveLinksButton.Click += (sender, e) => SaveLinksToFile();
                summaryPanel.Children.Add(saveLinksButton);
                
                // Pulsante per ricominciare
                var startOverButton = new Button
                {
                    Content = LocalizationHelper.GetString("RestartButton"),
                    Margin = new Thickness(0, 10, 0, 10)
                };
                startOverButton.Click += (sender, e) => StartOver();
                summaryPanel.Children.Add(startOverButton);
                
                CurrentView = summaryPanel;
            }
        }
        
        /// <summary>
        /// Salva i link selezionati in un file
        /// </summary>
        private void SaveLinksToFile()
        {
            if (_state.SelectedLinks.Count == 0)
            {
                MessageBox.Show(LocalizationHelper.GetString("NoLinksToSaveMessage"), LocalizationHelper.GetString("NoLinksToSaveTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt",
                Title = LocalizationHelper.GetString("SaveLinksDialogTitle"),
                FileName = "youtube_links.txt" // Filename likely doesn't need localization
            };
            
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllLines(saveFileDialog.FileName, _state.SelectedLinks);
                    MessageBox.Show(
                        LocalizationHelper.GetFormattedString("SaveLinksSuccessMessage", _state.SelectedLinks.Count, saveFileDialog.FileName),
                        LocalizationHelper.GetString("SaveLinksSuccessTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    _loggingService.Error($"Errore nel salvataggio dei link: {ex.Message}");
                    MessageBox.Show(
                        LocalizationHelper.GetFormattedString("SaveLinksError", ex.Message),
                        LocalizationHelper.GetString("ErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
        
        /// <summary>
        /// Salva lo stato dell'applicazione
        /// </summary>
        /// <param name="showConfirmation">Indica se mostrare un messaggio di conferma all'utente</param>
        /// <param name="showErrorDialog">Indica se mostrare il dialog di errore in caso di fallimento</param>
        public void SaveState(bool showConfirmation = false, bool showErrorDialog = true)
        {
            if (_stateService.SaveState(_state))
            {
                _loggingService.Info("Stato salvato");
                StatusMessage = LocalizationHelper.GetString("StatusStateSaved"); // Set status
                OnPropertyChanged(nameof(LastSaveInfo)); // Notify LastSaveInfo
                
                // Notifica l'AutoSaveService che lo stato è cambiato
                _autoSaveService.NotifyStateChanged();
                
                // Mostra un messaggio di conferma all'utente solo se richiesto
                if (showConfirmation)
                {
                    MessageBox.Show(
                        LocalizationHelper.GetString("StateSavedMessage"),
                        LocalizationHelper.GetString("StateSavedTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            else
            {
                _loggingService.Error("Errore nel salvataggio dello stato");
                if (showErrorDialog)
                {
                    MessageBox.Show(
                        LocalizationHelper.GetString("StateSaveError"),
                        LocalizationHelper.GetString("ErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
        
        /// <summary>
        /// Riprende l'ultimo stato salvato
        /// </summary>
        private void ResumeLastState()
        {
            if (_stateService.StateFileExists())
            {
                StatusMessage = LocalizationHelper.GetString("StatusResuming"); // Set status
                _state = _stateService.LoadState();
                
                if (!string.IsNullOrEmpty(_state.TxtFilePath))
                {
                    // Carica i titoli delle canzoni dal file
                    LoadSongTitles();
                    
                    // Aggiorna l'interfaccia utente
                    OnPropertyChanged(nameof(TxtFileLabel));
                    OnPropertyChanged(nameof(CanStartSearch));
                    OnPropertyChanged(nameof(TestMode));
                    OnPropertyChanged(nameof(CacheOnlyMode));
                    OnPropertyChanged(nameof(ProgressValue));
                    OnPropertyChanged(nameof(SongCounterText));
                    
                    // Mostra un messaggio di conferma all'utente
                    MessageBox.Show(
                        LocalizationHelper.GetFormattedString("StateRestoredMessage",
                                      Path.GetFileName(_state.TxtFilePath),
                                      _state.CurrentSongIndex + 1,
                                      _songTitles.Count,
                                      _state.SelectedLinks.Count),
                        LocalizationHelper.GetString("StateRestoredTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    
                    // Continua la ricerca dalla canzone corrente
                    StartSearch();
                }
                else
                {
                    MessageBox.Show(
                        LocalizationHelper.GetString("StateRestoreInvalidMessage"),
                        LocalizationHelper.GetString("StateRestoreErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                OnPropertyChanged(nameof(LastSaveInfo)); // Notify LastSaveInfo
            }
            else
            {
                MessageBox.Show(
                    LocalizationHelper.GetString("NoSavedStateMessage"),
                    LocalizationHelper.GetString("NoSavedStateTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        
        /// <summary>
        /// Ricomincia da capo
        /// </summary>
        private void StartOver()
        {
            MessageBoxResult result = MessageBox.Show(
                LocalizationHelper.GetString("StartOverMessage"),
                LocalizationHelper.GetString("StartOverTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                // Resetta lo stato in memoria senza eliminare il file di stato salvato
                _state = new Models.AppState();
                _songTitles.Clear();
                _noResultsLog.Clear();
                
                // Resetta l'interfaccia utente
                CurrentView = null;
                StatusMessage = LocalizationHelper.GetString("StatusReady"); // Reset status
                
                // Aggiorna l'interfaccia
                OnPropertyChanged(nameof(TxtFileLabel));
                OnPropertyChanged(nameof(CanStartSearch));
                OnPropertyChanged(nameof(TestMode));
                OnPropertyChanged(nameof(CacheOnlyMode));
                OnPropertyChanged(nameof(ProgressValue));
                OnPropertyChanged(nameof(ProgressMaximum));
                OnPropertyChanged(nameof(SongCounterText));
                OnPropertyChanged(nameof(LastSaveInfo)); // Notify LastSaveInfo
                
                MessageBox.Show(LocalizationHelper.GetString("StartOverConfirmMessage"), LocalizationHelper.GetString("StartOverConfirmTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                _loggingService.Info("Applicazione reimpostata dall'utente (stato salvato mantenuto)");
            }
        }
        
        /// <summary>
        /// Pulisce la cache del database
        /// </summary>
        private void ClearCache()
        {
            MessageBoxResult result = MessageBox.Show(
                LocalizationHelper.GetString("ClearCacheConfirmMessage"),
                LocalizationHelper.GetString("ClearCacheConfirmTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                bool success = _databaseService.ClearCache();
                
                if (success)
                {
                    MessageBox.Show(
                        LocalizationHelper.GetString("ClearCacheSuccessMessage"),
                        LocalizationHelper.GetString("ClearCacheSuccessTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                        
                    // Aggiorna il conteggio delle chiamate API (potrebbe essere resettato)
                    
                }
                else
                {
                    MessageBox.Show(
                        LocalizationHelper.GetString("ClearCacheErrorMessage"),
                        LocalizationHelper.GetString("ErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
        
        /// <summary>
        /// Apre la finestra delle impostazioni
        /// </summary>
        private void OpenSettings()
        {
            try
            {
                // Create and show the settings dialog
                var settingsDialog = new Views.SettingsDialog(
                    _youTubeApiService,
                    _databaseService,
                    _stateService);
                
                settingsDialog.Owner = Application.Current.MainWindow;
                
                if (settingsDialog.ShowDialog() == true)
                {
                    // Update UI if settings were changed
                    OnPropertyChanged(nameof(TestMode));
                    OnPropertyChanged(nameof(CacheOnlyMode));
                    OnPropertyChanged(nameof(ApiKeyInfo));
                    
                    // Update localized strings if language was changed
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.UpdateLocalizedStrings();
                    }
                    
                    _loggingService.Info("Settings updated");
                }
            }
            catch (Exception ex)
            {
                _loggingService.Error($"Error opening settings: {ex.Message}");
                MessageBox.Show(
                    LocalizationHelper.GetFormattedString("ErrorOpeningSettings", ex.Message),
                    LocalizationHelper.GetString("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Apre la vista per il recupero dei nomi di file audio
        /// </summary>
        private void OpenAudioFilenameRetriever()
        {
            // Crea la vista passando il riferimento a questo ViewModel
            var audioFilenameRetrieverView = new Views.AudioFilenameRetrieverView(this);
            
            // Imposta la vista come vista corrente
            CurrentView = audioFilenameRetrieverView;
            
            _loggingService.Info("Vista recuperatore nomi file audio aperta");
        }
        
        /// <summary>
        /// Imposta la chiave API
        /// </summary>
        private void SetApiKey()
        {
            // Crea una finestra di dialogo personalizzata
            Window dialog = new Window
            {
                Title = LocalizationHelper.GetString("SetApiKeyDialogTitle"),
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };
            
            // Crea il layout
            StackPanel panel = new StackPanel { Margin = new Thickness(10) };
            
            // Aggiungi un'etichetta
            panel.Children.Add(new TextBlock
            {
                Text = LocalizationHelper.GetString("SetApiKeyDialogLabel"),
                Margin = new Thickness(0, 0, 0, 10)
            });
            
            // Aggiungi una casella di testo
            TextBox textBox = new TextBox
            {
                Text = string.Join(",", _youTubeApiService.GetApiKeys()),
                Margin = new Thickness(0, 0, 0, 20),
                Height = 25
            };
            panel.Children.Add(textBox);
            
            // Aggiungi i pulsanti
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            
            Button okButton = new Button
            {
                Content = LocalizationHelper.GetString("OkButton"),
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            okButton.Click += (s, e) => { dialog.DialogResult = true; };
            
            Button cancelButton = new Button
            {
                Content = LocalizationHelper.GetString("CancelButton"),
                Width = 80,
                Height = 30,
                IsCancel = true
            };
            
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            panel.Children.Add(buttonPanel);
            
            // Imposta il contenuto della finestra
            dialog.Content = panel;
            
            // Mostra la finestra di dialogo
            bool? result = dialog.ShowDialog();
            
            // Elabora il risultato
            if (result == true)
            {
                string input = textBox.Text;
                
                if (!string.IsNullOrWhiteSpace(input))
                {
                    List<string> apiKeys = input.Split(',')
                        .Select(k => k.Trim())
                        .Where(k => !string.IsNullOrWhiteSpace(k))
                        .ToList();
                        
                    if (apiKeys.Count > 0)
                    {
                        _youTubeApiService.SetApiKeys(apiKeys);
                        _loggingService.Info($"Impostate {apiKeys.Count} chiavi API");
                        MessageBox.Show(LocalizationHelper.GetFormattedString("ApiKeySetSuccessMessage", apiKeys.Count), LocalizationHelper.GetString("ApiKeySetSuccessTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(LocalizationHelper.GetString("ApiKeySetErrorMessage"), LocalizationHelper.GetString("ApiKeySetErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }
        #region INotifyPropertyChanged
        
        public event PropertyChangedEventHandler? PropertyChanged; // Already nullable, kept for context

        /// <summary>
        /// Modifica la query corrente
        /// </summary>
        private void EditQuery()
        {
            if (_state?.CurrentSongIndex >= 0 && _state.CurrentSongIndex < _songTitles?.Count)
            {
                var dialog = new EditQueryDialog(_songTitles[_state.CurrentSongIndex]);
                if (dialog.ShowDialog() == true)
                {
                    _songTitles[_state.CurrentSongIndex] = dialog.EditedQuery;
                    _loggingService.Info($"Query modificata: {dialog.EditedQuery}");
                    
                    // Riavvia la ricerca dalla canzone modificata
                    StartSearch();
                }
            }
        }
        
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
private void HandleLanguageChange()
        {
            // Raise PropertyChanged for all properties that depend on localized strings
            OnPropertyChanged(nameof(ApiCounterText));
            OnPropertyChanged(nameof(ClearCacheMenuItemHeader));
            OnPropertyChanged(nameof(TxtFileLabel));
            OnPropertyChanged(nameof(LastSaveInfo));
            OnPropertyChanged(nameof(ApiKeyInfo));
            OnPropertyChanged(nameof(StatusMessage)); // Assuming status messages might be localized too
            // Add any other relevant properties here
        }

        /// <summary>
        /// Configura l'AutoSaveService e sottoscrive agli eventi
        /// </summary>
        private void ConfigureAutoSaveService()
        {
            // Sottoscrivi agli eventi dell'AutoSaveService
            _autoSaveService.SavingStarted += OnAutoSavingStarted;
            _autoSaveService.SavingCompleted += OnAutoSavingCompleted;
            _autoSaveService.SavingFailed += OnAutoSavingFailed;

            // Configura il callback per ottenere lo stato corrente
            _autoSaveService.SetStateProvider(() => _state);
        }

        /// <summary>
        /// Gestisce l'inizio del salvataggio automatico
        /// </summary>
        private void OnAutoSavingStarted(object? sender, EventArgs e)
        {
            SaveStatus = Controls.SaveStatus.Saving;
            SaveStatusMessage = "Salvataggio automatico in corso...";
        }

        /// <summary>
        /// Gestisce il completamento del salvataggio automatico
        /// </summary>
        private void OnAutoSavingCompleted(object? sender, EventArgs e)
        {
            SaveStatus = Controls.SaveStatus.Saved;
            SaveStatusMessage = $"Salvato automaticamente: {DateTime.Now:HH:mm:ss}";
            OnPropertyChanged(nameof(LastSaveInfo)); // Aggiorna le informazioni di salvataggio
        }

        /// <summary>
        /// Gestisce l'errore nel salvataggio automatico
        /// </summary>
        private void OnAutoSavingFailed(object? sender, string errorMessage)
        {
            SaveStatus = Controls.SaveStatus.Error;
            SaveStatusMessage = $"Errore salvataggio: {errorMessage}";
            _loggingService.Error($"Errore nel salvataggio automatico: {errorMessage}");
        }

        #endregion

        #region Search Filters

        /// <summary>
        /// Applica i filtri di ricerca ai risultati
        /// </summary>
        public async Task ApplySearchFilters(SearchFilters filters)
        {
            _currentFilters = filters ?? new SearchFilters();
            
            // Se abbiamo risultati memorizzati, riapplica i filtri
            if (_allSearchResults.Any())
            {
                await ApplyFiltersToCurrentResults();
            }
        }

        /// <summary>
        /// Applica i filtri ai risultati correnti e aggiorna la vista
        /// </summary>
        private async Task ApplyFiltersToCurrentResults()
        {
            try
            {
                // Filtra i risultati
                var filteredResults = _allSearchResults.Where(result => _currentFilters.PassesFilters(result)).ToList();
                
                // Aggiorna la vista con i risultati filtrati
                await UpdateResultsView(filteredResults);
                
                _loggingService.Info($"Applied filters: {filteredResults.Count} of {_allSearchResults.Count} results shown");
            }
            catch (Exception ex)
            {
                _loggingService.Error($"Error applying filters: {ex.Message}");
            }
        }

        /// <summary>
        /// Aggiorna la vista con i risultati filtrati
        /// </summary>
        private async Task UpdateResultsView(List<SearchResult> filteredResults)
        {
            if (CurrentView is StackPanel currentPanel)
            {
                // Trova il pannello dei risultati
                var resultsPanel = currentPanel.Children.OfType<StackPanel>()
                    .FirstOrDefault(sp => sp.Name == "ResultsPanel");
                
                if (resultsPanel != null)
                {
                    // Rimuovi tutti i controlli dei risultati esistenti
                    var controlsToRemove = resultsPanel.Children.OfType<VideoResultControl>().ToList();
                    foreach (var control in controlsToRemove)
                    {
                        resultsPanel.Children.Remove(control);
                    }
                    
                    // Aggiungi i risultati filtrati
                    foreach (var result in filteredResults)
                    {
                        var videoResultControl = new VideoResultControl();
                        
                        // Imposta il risultato di ricerca
                        await videoResultControl.SetSearchResult(result);
                        
                        videoResultControl.VideoSelected += (sender, selectedResult) =>
                        {
                            // Aggiungi il link selezionato (usa la logica esistente)
                            _state.SelectedLinks.Add(selectedResult.VideoUrl);
                            _state.CurrentSongIndex++;
                            SaveState(false);
                            
                            if (_state.CurrentSongIndex < _songTitles.Count)
                            {
                                StartSearch();
                            }
                            else
                            {
                                // Mostra il riepilogo (codice inline dal metodo StartSearch)
                                StatusMessage = LocalizationHelper.GetString("StatusSearchComplete");
                                SaveLinksToFile();
                                MessageBox.Show(
                                    LocalizationHelper.GetString("SearchCompleteMessage"),
                                    LocalizationHelper.GetString("SearchCompleteTitle"),
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            }
                        };
                        resultsPanel.Children.Add(videoResultControl);
                    }
                    
                    // Aggiorna il contatore se presente
                    var counterBlock = currentPanel.Children.OfType<TextBlock>()
                        .FirstOrDefault(tb => tb.Text.Contains("risultati"));
                    
                    if (counterBlock != null)
                    {
                        counterBlock.Text = $"Trovati {filteredResults.Count} risultati";
                    }
                }
            }
        }

        /// <summary>
        /// Memorizza tutti i risultati per il filtraggio
        /// </summary>
        private void StoreAllResults(List<SearchResult> results)
        {
            _allSearchResults = new List<SearchResult>(results);
        }

        #endregion
    }

    /// <summary>
    /// Implementazione semplice di ICommand
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;
        
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        
        public event EventHandler? CanExecuteChanged // Made nullable
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) // Parameter made nullable
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object? parameter) // Parameter made nullable
        {
            _execute();
        }
    }
}
