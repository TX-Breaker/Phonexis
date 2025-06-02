using System;
using System.Collections.Generic;
using Phonexis.Models;

namespace Phonexis.Services
{
    /// <summary>
    /// Interfaccia per il servizio di database
    /// </summary>
    public interface IDatabaseService
    {
        /// <summary>
        /// Evento che viene sollevato quando il conteggio delle chiamate API cambia
        /// </summary>
        event EventHandler ApiCallCountChanged;
        /// <summary>
        /// Inizializza il database
        /// </summary>
        void Initialize();
        
        // --- Monitoraggio Utilizzo API per Chiave ---

        /// <summary>
        /// Incrementa il contatore delle chiamate API per una specifica chiave API in una data data.
        /// </summary>
        /// <param name="apiKey">La chiave API utilizzata.</param>
        /// <param name="date">La data della chiamata API (tipicamente data UTC).</param>
        void IncrementApiCallCount(string apiKey, DateTime date);

        /// <summary>
        /// Ottiene il numero di chiamate API effettuate con una specifica chiave API in una data data.
        /// </summary>
        /// <param name="apiKey">La chiave API.</param>
        /// <param name="date">La data (tipicamente data UTC).</param>
        /// <returns>Il conteggio delle chiamate per quella chiave in quella data.</returns>
        int GetApiCallCount(string apiKey, DateTime date);

        /// <summary>
        /// Ottiene i conteggi delle chiamate API per tutte le chiavi note in una data data.
        /// </summary>
        /// <param name="date">La data (tipicamente data UTC).</param>
        /// <returns>Un dizionario che mappa le chiavi API ai loro conteggi di chiamate per la data.</returns>
        Dictionary<string, int> GetApiCallCounts(DateTime date);

        /// <summary>
        /// Contrassegna una specifica chiave API come esaurita per una data data.
        /// </summary>
        /// <param name="apiKey">La chiave API da contrassegnare.</param>
        /// <param name="date">La data (tipicamente data UTC).</param>
        void MarkApiKeyExhausted(string apiKey, DateTime date);

        /// <summary>
        /// Verifica se una specifica chiave API è contrassegnata come esaurita per una data data.
        /// </summary>
        /// <param name="apiKey">La chiave API.</param>
        /// <param name="date">La data (tipicamente data UTC).</param>
        /// <returns>True se la chiave è contrassegnata come esaurita, altrimenti False.</returns>
        bool IsApiKeyExhausted(string apiKey, DateTime date);

        /// <summary>
        /// Ottiene lo stato di esaurimento per tutte le chiavi note in una data data.
        /// </summary>
        /// <param name="date">La data (tipicamente data UTC).</param>
        /// <returns>Un dizionario che mappa le chiavi API al loro stato di esaurimento (true se esaurita).</returns>
        Dictionary<string, bool> GetApiKeyExhaustionStatus(DateTime date);

        // --- Gestione Cache ---

        /// <summary>
        /// Memorizza nella cache una miniatura
        /// </summary>
        /// <param name="url">L'URL della miniatura</param>
        /// <param name="imageData">I dati binari dell'immagine</param>
        void CacheThumbnail(string url, byte[] imageData);
        
        /// <summary>
        /// Ottiene una miniatura dalla cache
        /// </summary>
        /// <param name="url">L'URL della miniatura</param>
        /// <returns>I dati binari dell'immagine, o null se non trovata</returns>
        byte[] GetCachedThumbnail(string url);
        
        /// <summary>
        /// Memorizza nella cache i risultati di una ricerca
        /// </summary>
        /// <param name="query">La query di ricerca</param>
        /// <param name="results">I risultati della ricerca</param>
        void CacheSearchResults(string query, List<SearchResult> results);
        
        /// <summary>
        /// Ottiene i risultati di una ricerca dalla cache
        /// </summary>
        /// <param name="query">La query di ricerca</param>
        /// <returns>I risultati della ricerca, o una lista vuota se non trovati</returns>
        List<SearchResult> GetSearchResults(string query);
        
        /// <summary>
        /// Normalizza una query di ricerca
        /// </summary>
        /// <param name="query">La query da normalizzare</param>
        /// <returns>La query normalizzata</returns>
        string NormalizeQuery(string query);
        
        /// <summary>
        /// Cancella tutti i dati nella cache
        /// </summary>
        /// <returns>True se l'operazione è riuscita, altrimenti False</returns>
        bool ClearCache();
    }
}
