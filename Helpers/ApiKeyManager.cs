using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Phonexis.Helpers
{
    /// <summary>
    /// Gestisce il salvataggio e il caricamento delle chiavi API
    /// </summary>
    public static class ApiKeyManager
    {
        /// <summary>
        /// Salva le chiavi API in un file
        /// </summary>
        /// <param name="apiKeys">Lista delle chiavi API da salvare</param>
        /// <returns>True se il salvataggio è riuscito, altrimenti False</returns>
        public static bool SaveApiKeys(List<string> apiKeys)
        {
            try
            {
                // Crea la directory se non esiste
                string? directory = Path.GetDirectoryName(Config.GetSecureApiKeysFilePath()); // Made nullable
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Converti le chiavi API in una stringa separata da virgole
                string apiKeysString = string.Join(",", apiKeys);
                
                // Salva le chiavi API nel file
                File.WriteAllText(Config.GetSecureApiKeysFilePath(), apiKeysString);
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        /// <summary>
        /// Carica le chiavi API dal file
        /// </summary>
        /// <returns>Lista delle chiavi API caricate, o una lista vuota in caso di errore</returns>
        public static List<string> LoadApiKeys()
        {
            try
            {
                // Verifica se il file esiste
                if (!File.Exists(Config.GetSecureApiKeysFilePath()))
                {
                    return new List<string>();
                }
                
                // Leggi le chiavi API dal file
                string apiKeysString = File.ReadAllText(Config.GetSecureApiKeysFilePath());
                List<string> apiKeys = new List<string>(apiKeysString.Split(','));
                
                // Filtra le chiavi API vuote
                return apiKeys.FindAll(key => !string.IsNullOrWhiteSpace(key));
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }
    }
}
