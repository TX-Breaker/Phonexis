using System;
using System.Collections.Generic;

namespace Phonexis.Models
{
    /// <summary>
    /// Rappresenta lo stato dell'applicazione che può essere salvato e ripristinato
    /// </summary>
    public class AppState
    {
        /// <summary>
        /// Percorso del file di testo contenente i titoli delle canzoni
        /// </summary>
        public string TxtFilePath { get; set; } = string.Empty; // Initialized
        
        /// <summary>
        /// Lista dei link selezionati
        /// </summary>
        public List<string> SelectedLinks { get; set; } = new List<string>();
        
        /// <summary>
        /// Indica se la modalità test è attiva
        /// </summary>
        public bool TestMode { get; set; }
        
        /// <summary>
        /// Indica se la modalità "solo cache" è attiva
        /// </summary>
        public bool CacheOnlyMode { get; set; }
        
        /// <summary>
        /// Indice della canzone corrente
        /// </summary>
        public int CurrentSongIndex { get; set; }
        
        /// <summary>
        /// Timestamp dell'ultimo salvataggio
        /// </summary>
        public DateTime LastSaveTimestamp { get; set; }
    }
}
