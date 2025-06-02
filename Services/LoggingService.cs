using System;
using System.IO;
using System.Text;

namespace Phonexis.Services
{
    /// <summary>
    /// Implementazione del servizio di logging
    /// </summary>
    public class LoggingService : ILoggingService, IDisposable
    {
        private readonly object _lockObject = new object();
        private StreamWriter? _logWriter = null; // Made nullable
        private bool _consoleLogging = true;
        private bool _disposed = false;

        /// <summary>
        /// Inizializza il servizio di logging
        /// </summary>
        public void Initialize()
        {
            try
            {
                // Assicura che la directory esista
                string logFilePath = Config.GetLogFilePath();
                string? logDirectory = Path.GetDirectoryName(logFilePath); // Made nullable

                // Check if logDirectory is not null before using it
                if (!string.IsNullOrEmpty(logDirectory))
                {
                    if (!Directory.Exists(logDirectory))
                    {
                        Directory.CreateDirectory(logDirectory);
                    }
                }
                else
                {
                    // Handle the case where the directory path is invalid or null
                    Console.WriteLine($"Errore: Impossibile determinare la directory per il file di log: {logFilePath}");
                    // Optionally throw an exception or return early
                    return;
                }
                
                // Apre il file di log in modalità append
                _logWriter = new StreamWriter(logFilePath, true, Encoding.UTF8)
                {
                    AutoFlush = true
                };
                
                // Scrive l'intestazione del log
                string header = $"=== Log inizializzato il {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===";
                _logWriter.WriteLine(header);
                
                if (_consoleLogging)
                {
                    Console.WriteLine(header);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore nell'inizializzazione del log: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Registra un messaggio di debug
        /// </summary>
        public void Debug(string message)
        {
            Log("DEBUG", message);
        }
        
        /// <summary>
        /// Registra un messaggio informativo
        /// </summary>
        public void Info(string message)
        {
            Log("INFO", message);
        }
        
        /// <summary>
        /// Registra un messaggio di avviso
        /// </summary>
        public void Warning(string message)
        {
            Log("WARNING", message);
        }
        
        /// <summary>
        /// Registra un messaggio di errore
        /// </summary>
        public void Error(string message)
        {
            Log("ERROR", message);
        }
        
        /// <summary>
        /// Registra un messaggio di errore critico
        /// </summary>
        public void Critical(string message)
        {
            Log("CRITICAL", message);
        }
        
        /// <summary>
        /// Metodo interno per registrare un messaggio con un livello specifico
        /// </summary>
        private void Log(string level, string message)
        {
            if (_logWriter == null)
                return;
                
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logMessage = $"{timestamp} [{level}] {message}";
            
            lock (_lockObject)
            {
                _logWriter.WriteLine(logMessage);
                
                if (_consoleLogging)
                {
                    Console.WriteLine(logMessage);
                }
            }
        }
        
        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Dispose pattern implementation
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    if (_logWriter != null)
                    {
                        try
                        {
                            lock (_lockObject)
                            {
                                _logWriter.Flush();
                                _logWriter.Close();
                                _logWriter.Dispose();
                                _logWriter = null;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error closing log file: {ex.Message}");
                        }
                    }
                }
                
                _disposed = true;
            }
        }
        
        /// <summary>
        /// Finalizer
        /// </summary>
        ~LoggingService()
        {
            Dispose(false);
        }
    }
}
