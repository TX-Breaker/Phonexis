using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using Phonexis.Models;

namespace Phonexis.Services
{
    public class AutoSaveService : IAutoSaveService, IDisposable
    {
        private readonly IStateService _stateService;
        private readonly ILoggingService _loggingService;
        private Timer? _autoSaveTimer;
        private bool _isRunning = false;
        private int _autoSaveIntervalSeconds = 30;
        private bool _backupBeforeCriticalOperations = true;
        private readonly string _backupDirectory;
        private readonly SemaphoreSlim _saveSemaphore = new(1, 1);
        
        public bool IsRunning => _isRunning;
        public DateTime? LastSaveTime { get; private set; }
        
        public event EventHandler<AutoSaveEventArgs>? AutoSaveCompleted;
        public event EventHandler<AutoSaveErrorEventArgs>? AutoSaveError;
        
        // Nuovi eventi per l'integrazione con il MainViewModel
        public event EventHandler? SavingStarted;
        public event EventHandler? SavingCompleted;
        public event EventHandler<string>? SavingFailed;
        
        // Provider per ottenere lo stato corrente
        private Func<AppState>? _stateProvider;
        
        public AutoSaveService(IStateService stateService, ILoggingService loggingService)
        {
            _stateService = stateService;
            _loggingService = loggingService;
            
            // Crea la directory per i backup
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _backupDirectory = Path.Combine(appDataPath, "Phonexis", "Backups");
            Directory.CreateDirectory(_backupDirectory);
        }
        
        public void Start()
        {
            if (_isRunning) return;
            
            _loggingService.Info($"Avvio servizio auto-save con intervallo di {_autoSaveIntervalSeconds} secondi");
            
            _autoSaveTimer = new Timer(
                AutoSaveCallback, 
                null, 
                TimeSpan.FromSeconds(_autoSaveIntervalSeconds), 
                TimeSpan.FromSeconds(_autoSaveIntervalSeconds));
                
            _isRunning = true;
        }
        
        public void Stop()
        {
            if (!_isRunning) return;
            
            _loggingService.Info("Arresto servizio auto-save");
            
            _autoSaveTimer?.Dispose();
            _autoSaveTimer = null;
            _isRunning = false;
        }
        
        public async Task SaveNowAsync()
        {
            await PerformSaveAsync(false, "Manual Save");
        }
        
        public void SetAutoSaveInterval(int intervalSeconds)
        {
            if (intervalSeconds < 10)
                throw new ArgumentException("L'intervallo di auto-save non può essere inferiore a 10 secondi");
                
            _autoSaveIntervalSeconds = intervalSeconds;
            
            if (_isRunning)
            {
                // Riavvia il timer con il nuovo intervallo
                Stop();
                Start();
            }
            
            _loggingService.Info($"Intervallo auto-save impostato a {intervalSeconds} secondi");
        }
        
        public void SetBackupBeforeCriticalOperations(bool enabled)
        {
            _backupBeforeCriticalOperations = enabled;
            _loggingService.Info($"Backup prima delle operazioni critiche: {(enabled ? "abilitato" : "disabilitato")}");
        }
        
        public async Task CreateBackupAsync(string operationName)
        {
            if (!_backupBeforeCriticalOperations) return;
            
            await PerformSaveAsync(true, operationName);
        }
        
        public async Task<bool> RecoverLastStateAsync()
        {
            try
            {
                var backupFiles = Directory.GetFiles(_backupDirectory, "*.json")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();
                
                if (!backupFiles.Any())
                {
                    _loggingService.Info("Nessun backup disponibile per il recupero");
                    return false;
                }
                
                var latestBackup = backupFiles.First();
                var jsonContent = await File.ReadAllTextAsync(latestBackup.FullName);
                var state = JsonSerializer.Deserialize<AppState>(jsonContent);
                
                if (state != null)
                {
                    _stateService.SaveState(state);
                    _loggingService.Info($"Stato recuperato dal backup: {latestBackup.Name}");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _loggingService.Error($"Errore durante il recupero dello stato: {ex.Message}");
                AutoSaveError?.Invoke(this, new AutoSaveErrorEventArgs(ex, "Recovery"));
                return false;
            }
        }
        
        public async Task CleanupOldBackupsAsync(int keepCount = 10)
        {
            try
            {
                await Task.Run(() =>
                {
                    var backupFiles = Directory.GetFiles(_backupDirectory, "*.json")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.LastWriteTime)
                        .ToList();
                    
                    var filesToDelete = backupFiles.Skip(keepCount).ToList();
                    
                    foreach (var file in filesToDelete)
                    {
                        try
                        {
                            file.Delete();
                            _loggingService.Debug($"Eliminato backup vecchio: {file.Name}");
                        }
                        catch (Exception ex)
                        {
                            _loggingService.Warning($"Impossibile eliminare il backup {file.Name}: {ex.Message}");
                        }
                    }
                    
                    if (filesToDelete.Any())
                    {
                        _loggingService.Info($"Pulizia backup completata: eliminati {filesToDelete.Count} file");
                    }
                });
            }
            catch (Exception ex)
            {
                _loggingService.Error($"Errore durante la pulizia dei backup: {ex.Message}");
            }
        }
        
        private async void AutoSaveCallback(object? state)
        {
            await PerformSaveAsync(false, "Auto Save");
        }
        
        private async Task PerformSaveAsync(bool isBackup, string operationName)
        {
            // Usa semaforo per evitare salvataggi concorrenti
            if (!await _saveSemaphore.WaitAsync(100))
            {
                _loggingService.Debug("Salvataggio saltato: operazione già in corso");
                return;
            }
            
            try
            {
                // Notifica inizio salvataggio
                SavingStarted?.Invoke(this, EventArgs.Empty);
                
                // Ottieni lo stato corrente
                var currentState = _stateProvider?.Invoke();
                if (currentState == null)
                {
                    _loggingService.Warning("Nessun state provider configurato e stato non disponibile");
                    return;
                }
                
                // Aggiorna timestamp
                currentState.LastSaveTimestamp = DateTime.Now;
                
                // Serializza lo stato
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                var jsonContent = JsonSerializer.Serialize(currentState, jsonOptions);
                
                // Determina il nome del file
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = isBackup 
                    ? $"backup_{operationName}_{timestamp}.json"
                    : $"autosave_{timestamp}.json";
                
                var filePath = Path.Combine(_backupDirectory, fileName);
                
                // Salva il file
                await File.WriteAllTextAsync(filePath, jsonContent);
                
                // Salva anche nello StateService se non è un backup
                if (!isBackup)
                {
                    _stateService.SaveState(currentState);
                }
                
                LastSaveTime = DateTime.Now;
                
                _loggingService.Debug($"Stato salvato: {fileName}");
                
                // Notifica completamento
                AutoSaveCompleted?.Invoke(this, new AutoSaveEventArgs
                {
                    SaveTime = LastSaveTime.Value,
                    SavePath = filePath,
                    IsBackup = isBackup,
                    OperationName = operationName
                });
                
                SavingCompleted?.Invoke(this, EventArgs.Empty);
                
                // Pulizia backup vecchi (solo per auto-save, non per backup manuali)
                if (!isBackup)
                {
                    _ = Task.Run(() => CleanupOldBackupsAsync());
                }
            }
            catch (Exception ex)
            {
                _loggingService.Error($"Errore durante il salvataggio ({operationName}): {ex.Message}");
                AutoSaveError?.Invoke(this, new AutoSaveErrorEventArgs(ex, operationName));
                
                // Notifica errore
                SavingFailed?.Invoke(this, ex.Message);
            }
            finally
            {
                _saveSemaphore.Release();
            }
        }
        
        public void SetStateProvider(Func<AppState> stateProvider)
        {
            _stateProvider = stateProvider;
        }
        
        public void NotifyStateChanged()
        {
            // Forza un salvataggio se il servizio è attivo
            if (_isRunning)
            {
                _ = Task.Run(() => SaveNowAsync());
            }
        }
        
        public void Dispose()
        {
            Stop();
            _saveSemaphore?.Dispose();
        }
    }
}