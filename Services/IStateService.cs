using Phonexis.Models;

namespace Phonexis.Services
{
    /// <summary>
    /// Interfaccia per il servizio di gestione dello stato dell'applicazione
    /// </summary>
    public interface IStateService
    {
        /// <summary>
        /// Salva lo stato dell'applicazione
        /// </summary>
        /// <param name="state">Lo stato da salvare</param>
        /// <returns>True se il salvataggio è riuscito, altrimenti False</returns>
        bool SaveState(AppState state);
        
        /// <summary>
        /// Carica lo stato dell'applicazione
        /// </summary>
        /// <returns>Lo stato caricato, o un nuovo stato se il caricamento fallisce</returns>
        AppState LoadState();
        
        /// <summary>
        /// Salva lo stato dell'applicazione in un file di emergenza
        /// </summary>
        /// <param name="state">Lo stato da salvare</param>
        /// <returns>True se il salvataggio è riuscito, altrimenti False</returns>
        bool SaveEmergencyState(AppState state);
        
        /// <summary>
        /// Verifica se esiste un file di stato
        /// </summary>
        /// <returns>True se il file di stato esiste, altrimenti False</returns>
        bool StateFileExists();
        
        /// <summary>
        /// Elimina il file di stato
        /// </summary>
        /// <returns>True se l'eliminazione è riuscita, altrimenti False</returns>
        bool DeleteStateFile();
        
        /// <summary>
        /// Ottiene lo stato corrente dell'applicazione
        /// </summary>
        /// <returns>Lo stato corrente dell'applicazione</returns>
        AppState GetState();
    }
}
