using System;

namespace Phonexis.Models
{
    /// <summary>
    /// Enum per i filtri di durata
    /// </summary>
    public enum DurationFilter
    {
        Any,
        Short,    // < 4 minuti
        Medium,   // 4-20 minuti
        Long      // > 20 minuti
    }

    /// <summary>
    /// Enum per i filtri di data
    /// </summary>
    public enum DateFilter
    {
        Any,
        LastHour,
        Today,
        ThisWeek,
        ThisMonth,
        ThisYear
    }

    /// <summary>
    /// Enum per i filtri di qualità (basato su visualizzazioni)
    /// </summary>
    public enum QualityFilter
    {
        Any,
        High,     // > 100K visualizzazioni
        Medium,   // 10K-100K visualizzazioni
        Low       // < 10K visualizzazioni
    }

    /// <summary>
    /// Classe che contiene tutti i filtri di ricerca
    /// </summary>
    public class SearchFilters
    {
        public DurationFilter Duration { get; set; } = DurationFilter.Any;
        public DateFilter Date { get; set; } = DateFilter.Any;
        public QualityFilter Quality { get; set; } = QualityFilter.Any;

        /// <summary>
        /// Verifica se un SearchResult passa tutti i filtri
        /// </summary>
        public bool PassesFilters(SearchResult result)
        {
            return PassesDurationFilter(result) && 
                   PassesDateFilter(result) && 
                   PassesQualityFilter(result);
        }

        private bool PassesDurationFilter(SearchResult result)
        {
            if (Duration == DurationFilter.Any || string.IsNullOrEmpty(result.Duration))
                return true;

            var durationMinutes = ParseDurationToMinutes(result.Duration);
            
            return Duration switch
            {
                DurationFilter.Short => durationMinutes < 4,
                DurationFilter.Medium => durationMinutes >= 4 && durationMinutes <= 20,
                DurationFilter.Long => durationMinutes > 20,
                _ => true
            };
        }

        private bool PassesDateFilter(SearchResult result)
        {
            if (Date == DateFilter.Any)
                return true;

            var now = DateTime.Now;
            var publishDate = result.PublishedAt;

            return Date switch
            {
                DateFilter.LastHour => (now - publishDate).TotalHours <= 1,
                DateFilter.Today => publishDate.Date == now.Date,
                DateFilter.ThisWeek => (now - publishDate).TotalDays <= 7,
                DateFilter.ThisMonth => publishDate.Month == now.Month && publishDate.Year == now.Year,
                DateFilter.ThisYear => publishDate.Year == now.Year,
                _ => true
            };
        }

        private bool PassesQualityFilter(SearchResult result)
        {
            if (Quality == QualityFilter.Any)
                return true;

            return Quality switch
            {
                QualityFilter.High => result.ViewCount > 100000,
                QualityFilter.Medium => result.ViewCount >= 10000 && result.ViewCount <= 100000,
                QualityFilter.Low => result.ViewCount < 10000,
                _ => true
            };
        }

        /// <summary>
        /// Converte una stringa durata (formato YouTube) in minuti
        /// </summary>
        private static double ParseDurationToMinutes(string duration)
        {
            try
            {
                // Formato YouTube: PT#M#S o PT#H#M#S
                if (duration.StartsWith("PT"))
                {
                    duration = duration.Substring(2); // Rimuovi "PT"
                    
                    var hours = 0;
                    var minutes = 0;
                    var seconds = 0;

                    if (duration.Contains("H"))
                    {
                        var hIndex = duration.IndexOf("H");
                        hours = int.Parse(duration.Substring(0, hIndex));
                        duration = duration.Substring(hIndex + 1);
                    }

                    if (duration.Contains("M"))
                    {
                        var mIndex = duration.IndexOf("M");
                        minutes = int.Parse(duration.Substring(0, mIndex));
                        duration = duration.Substring(mIndex + 1);
                    }

                    if (duration.Contains("S"))
                    {
                        var sIndex = duration.IndexOf("S");
                        seconds = int.Parse(duration.Substring(0, sIndex));
                    }

                    return hours * 60 + minutes + seconds / 60.0;
                }
                
                // Formato alternativo: HH:MM:SS o MM:SS
                var parts = duration.Split(':');
                if (parts.Length == 3)
                {
                    return int.Parse(parts[0]) * 60 + int.Parse(parts[1]) + int.Parse(parts[2]) / 60.0;
                }
                else if (parts.Length == 2)
                {
                    return int.Parse(parts[0]) + int.Parse(parts[1]) / 60.0;
                }
            }
            catch
            {
                // Se il parsing fallisce, considera come durata media
                return 10;
            }

            return 0;
        }
    }
}