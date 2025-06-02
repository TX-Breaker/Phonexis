using System;

namespace Phonexis.Models
{
    /// <summary>
    /// Rappresenta un risultato di ricerca da YouTube
    /// </summary>
    public class SearchResult
    {
        /// <summary>
        /// ID del video
        /// </summary>
        public string? VideoId { get; set; } // Made nullable

        /// <summary>
        /// Titolo del video
        /// </summary>
        public string? Title { get; set; } // Made nullable

        /// <summary>
        /// Descrizione del video
        /// </summary>
        public string? Description { get; set; } // Made nullable

        /// <summary>
        /// Nome del canale che ha caricato il video
        /// </summary>
        public string? ChannelTitle { get; set; } // Made nullable

        /// <summary>
        /// URL della miniatura del video
        /// </summary>
        public string? ThumbnailUrl { get; set; } // Made nullable
        
        /// <summary>
        /// Data di pubblicazione del video
        /// </summary>
        public DateTime PublishedAt { get; set; }
        
        /// <summary>
        /// Numero di visualizzazioni del video
        /// </summary>
        public int ViewCount { get; set; }
        
        /// <summary>
        /// Durata del video in formato "HH:MM:SS"
        /// </summary>
        public string? Duration { get; set; } // Made nullable

        /// <summary>
        /// Indica se il risultato proviene dalla cache
        /// </summary>
        public bool IsFromCache { get; set; }
        
        /// <summary>
        /// Indica se il risultato è stato generato come dummy
        /// </summary>
        public bool IsDummy { get; set; }
        
        /// <summary>
        /// URL completo del video su YouTube
        /// </summary>
        public string VideoUrl => $"https://www.youtube.com/watch?v={VideoId}";
        
        /// <summary>
        /// Restituisce una descrizione della fonte del risultato
        /// </summary>
        public string SourceDescription =>
            IsDummy ? Helpers.LocalizationHelper.GetString("SourceTestLabel") :
            IsFromCache ? Helpers.LocalizationHelper.GetString("SourceCacheLabel") :
            Helpers.LocalizationHelper.GetString("SourceApiLabel");
    }
}
