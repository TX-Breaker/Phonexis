namespace Phonexis.Services
{
    /// <summary>
    /// Interfaccia per il servizio di logging
    /// </summary>
    public interface ILoggingService
    {
        /// <summary>
        /// Inizializza il servizio di logging
        /// </summary>
        void Initialize();
        
        /// <summary>
        /// Registra un messaggio di debug
        /// </summary>
        /// <param name="message">Il messaggio da registrare</param>
        void Debug(string message);
        
        /// <summary>
        /// Registra un messaggio informativo
        /// </summary>
        /// <param name="message">Il messaggio da registrare</param>
        void Info(string message);
        
        /// <summary>
        /// Registra un messaggio di avviso
        /// </summary>
        /// <param name="message">Il messaggio da registrare</param>
        void Warning(string message);
        
        /// <summary>
        /// Registra un messaggio di errore
        /// </summary>
        /// <param name="message">Il messaggio da registrare</param>
        void Error(string message);
        
        /// <summary>
        /// Registra un messaggio di errore critico
        /// </summary>
        /// <param name="message">Il messaggio da registrare</param>
        void Critical(string message);
    }
}
