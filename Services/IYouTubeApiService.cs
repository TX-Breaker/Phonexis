using System.Collections.Generic;
using System.Threading.Tasks;
using Phonexis.Models;

namespace Phonexis.Services
{
    /// <summary>
    /// Interfaccia per il servizio API di YouTube
    /// </summary>
    public interface IYouTubeApiService
    {
        /// <summary>
        /// Imposta le chiavi API da utilizzare per le richieste
        /// </summary>
        /// <param name="apiKeys">La lista delle chiavi API</param>
        void SetApiKeys(List<string> apiKeys);
        
        /// <summary>
        /// Ottiene le chiavi API attualmente impostate
        /// </summary>
        /// <returns>La lista delle chiavi API</returns>
        List<string> GetApiKeys();
        
        /// <summary>
        /// Ottiene l'indice della chiave API attualmente in uso
        /// </summary>
        /// <returns>L'indice della chiave API corrente</returns>
        int GetCurrentKeyIndex();
        
        /// <summary>
        /// Cerca video su YouTube in base a una query
        /// </summary>
        /// <param name="query">La query di ricerca</param>
        /// <param name="maxResults">Il numero massimo di risultati da restituire</param>
        /// <returns>Una lista di risultati di ricerca</returns>
        Task<List<SearchResult>> SearchAsync(string query, int maxResults = 5);
        
        /// <summary>
        /// Genera risultati fittizi per la modalità test
        /// </summary>
        /// <param name="query">La query di ricerca</param>
        /// <param name="count">Il numero di risultati da generare</param>
        /// <returns>Una lista di risultati di ricerca fittizi</returns>
        List<SearchResult> GenerateDummyResults(string query, int count = 5);
    }
}
