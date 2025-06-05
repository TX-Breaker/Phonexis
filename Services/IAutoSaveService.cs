using System;
using System.Threading.Tasks;
using Phonexis.Models;

namespace Phonexis.Services
{
    public interface IAutoSaveService
    {
        /// <summary>
        /// Avvia il servizio di auto-save
        /// </summary>
        void Start();
        
        /// <summary>
        /// Ferma il servizio di auto-save
        /// </summary>
        void Stop();
        
        /// <summary>
        /// Forza un salvataggio immediato
        /// </summary>
        Task SaveNowAsync();
        
        /// <summary>
        /// Indica se il servizio è attivo
        /// </summary>
        bool IsRunning { get; }
        
        /// <summary>
        /// Ultimo salvataggio effettuato
        /// </summary>
        DateTime? LastSaveTime { get; }
        
        /// <summary>
        /// Evento scatenato quando viene effettuato un auto-save
        /// </summary>
        event EventHandler<AutoSaveEventArgs>? AutoSaveCompleted;
        
        /// <summary>
        /// Evento scatenato quando si verifica un errore durante l'auto-save
        /// </summary>
        event EventHandler<AutoSaveErrorEventArgs>? AutoSaveError;
        
        /// <summary>
        /// Configura l'intervallo di auto-save (default: 30 secondi)
        /// </summary>
        /// <param name="intervalSeconds">Intervallo in secondi</param>
        void SetAutoSaveInterval(int intervalSeconds);
        
        /// <summary>
        /// Abilita/disabilita il backup automatico prima delle operazioni critiche
        /// </summary>
        /// <param name="enabled">True per abilitare</param>
        void SetBackupBeforeCriticalOperations(bool enabled);
        
        /// <summary>
        /// Crea un backup prima di un'operazione critica
        /// </summary>
        /// <param name="operationName">Nome dell'operazione</param>
        Task CreateBackupAsync(string operationName);
        
        /// <summary>
        /// Recupera automaticamente l'ultimo stato salvato
        /// </summary>
        Task<bool> RecoverLastStateAsync();
        
        /// <summary>
        /// Pulisce i backup vecchi (mantiene solo gli ultimi N)
        /// </summary>
        /// <param name="keepCount">Numero di backup da mantenere</param>
        Task CleanupOldBackupsAsync(int keepCount = 10);
        
        /// <summary>
        /// Evento scatenato quando inizia il salvataggio
        /// </summary>
        event EventHandler? SavingStarted;
        
        /// <summary>
        /// Evento scatenato quando il salvataggio è completato
        /// </summary>
        event EventHandler? SavingCompleted;
        
        /// <summary>
        /// Evento scatenato quando il salvataggio fallisce
        /// </summary>
        event EventHandler<string>? SavingFailed;
        
        /// <summary>
        /// Imposta il provider per ottenere lo stato corrente
        /// </summary>
        /// <param name="stateProvider">Funzione che restituisce lo stato corrente</param>
        void SetStateProvider(Func<AppState> stateProvider);
        
        /// <summary>
        /// Notifica che lo stato è cambiato
        /// </summary>
        void NotifyStateChanged();
    }
    
    public class AutoSaveEventArgs : EventArgs
    {
        public DateTime SaveTime { get; set; }
        public string? SavePath { get; set; }
        public bool IsBackup { get; set; }
        public string? OperationName { get; set; }
    }
    
    public class AutoSaveErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
        public string? OperationName { get; set; }
        public DateTime ErrorTime { get; set; }
        
        public AutoSaveErrorEventArgs(Exception exception, string? operationName = null)
        {
            Exception = exception;
            OperationName = operationName;
            ErrorTime = DateTime.Now;
        }
    }
}