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
                _loggingService.Info("Tentativo di recupero dell'ultimo stato salvato");
                
                // Cerca il file di stato più recente
                var stateFiles = Directory.GetFiles(_backupDirectory, "state_*.json")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();
                
                if (!stateFiles.Any())
                {
                    _loggingService.Info("Nessun file di stato trovato per il recupero");
                    return false;
                }
                
                var latestStateFile = stateFiles.First();
                
                // Verifica che il file non sia troppo vecchio (max 24 ore)
                if (DateTime.Now - latestStateFile.LastWriteTime > TimeSpan.FromHours(24))
                {
                    _loggingService.Info("Il file di stato più recente è troppo vecchio per il recupero automatico");
                    return false;
                }
                
                // Tenta il recupero
                var stateJson = await File.ReadAllTextAsync(latestStateFile.FullName);
                var state = JsonSerializer.Deserialize<AppState>(stateJson);
                
                if (state != null)
                {
                    // TODO: Implementare il ripristino quando il metodo sarà disponibile nell'IStateService
                    _loggingService.Info($"Stato recuperato con successo da: {latestStateFile.Name}");
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
            // Usa il semaforo per evitare salvataggi concorrenti
            if (!await _saveSemaphore.WaitAsync(100))
            {
                _loggingService.Debug("Salvataggio saltato: operazione già in corso");
                return;
            }
            
            try
            {
                // Scatena l'evento di inizio salvataggio
                SavingStarted?.Invoke(this, EventArgs.Empty);
                
                // Usa il state provider se disponibile, altrimenti usa il servizio di stato
                var currentState = _stateProvider?.Invoke();
                if (currentState == null)
                {
                    _loggingService.Warning("Nessun state provider configurato e stato non disponibile");
                    return;
                }
                
                // Non salvare se non ci sono dati significativi
                if (string.IsNullOrEmpty(currentState.TxtFilePath))
                {
                    return;
                }
                
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = isBackup 
                    ? $"backup_{operationName.Replace(" ", "_")}_{timestamp}.json"
                    : $"state_{timestamp}.json";
                    
                var filePath = Path.Combine(_backupDirectory, fileName);
                
                // Serializza lo stato
                var stateJson = JsonSerializer.Serialize(currentState, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                await File.WriteAllTextAsync(filePath, stateJson);
                
                LastSaveTime = DateTime.Now;
                
                _loggingService.Debug($"Stato salvato: {fileName}");
                
                // Notifica il completamento
                AutoSaveCompleted?.Invoke(this, new AutoSaveEventArgs
                {
                    SaveTime = LastSaveTime.Value,
                    SavePath = filePath,
                    IsBackup = isBackup,
                    OperationName = operationName
                });
                
                // Scatena l'evento di completamento per il MainViewModel
                SavingCompleted?.Invoke(this, EventArgs.Empty);
                
                // Pulizia periodica (ogni 10 salvataggi)
                if (!isBackup && DateTime.Now.Minute % 10 == 0)
                {
                    _ = Task.Run(() => CleanupOldBackupsAsync());
                }
            }
            catch (Exception ex)
            {
                _loggingService.Error($"Errore durante il salvataggio ({operationName}): {ex.Message}");
                AutoSaveError?.Invoke(this, new AutoSaveErrorEventArgs(ex, operationName));
                
                // Scatena l'evento di errore per il MainViewModel
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
            // Questo metodo può essere usato per forzare un salvataggio quando lo stato cambia
            if (_isRunning)
            {
                _ = Task.Run(() => PerformSaveAsync(false, "State Changed"));
            }
        }
        
        public void Dispose()
        {
            Stop();
            _saveSemaphore?.Dispose();
        }
    }
}