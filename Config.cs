using System;
using System.IO;

namespace Phonexis
{
    public static class Config
    {
        // Informazioni sull'applicazione
        public const string AppName = "Phonexis";
        public const string AppVersion = "1.0.0";
        public const string OrganizationName = "Phonexis";
        
        // URL per la ricerca su YouTube
        public const string YouTubeSearchUrl = "https://www.googleapis.com/youtube/v3/search";
        
        // Limiti API
        public const int MaxApiCallsPerDay = 10000;
        public const int MaxCacheSize = 500;
        
        // Percorsi dei file
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Phonexis");
        
        public static string GetStateFilePath()
        {
            return Path.Combine(AppDataPath, "session_state.json.gz");
        }
        
        public static string GetEmergencyStateFilePath()
        {
            return Path.Combine(AppDataPath, "emergency_session_state.json.gz");
        }
        
        public static string GetNoResultsLogFilePath()
        {
            return Path.Combine(AppDataPath, "no_results_log.txt");
        }
        
        public static string GetDatabaseFilePath()
        {
            return Path.Combine(AppDataPath, "youtube_cache.db");
        }
        
        public static string GetSecureApiKeysFilePath()
        {
            return Path.Combine(AppDataPath, "api_keys.env");
        }
        
        public static string GetLogFilePath()
        {
            return Path.Combine(AppDataPath, "youtube_link_chooser.log");
        }
        
        // Sicurezza
        public const string ApiKeyHashSalt = "your_salt_here";
    }
}
