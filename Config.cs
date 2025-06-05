using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace Phonexis
{
    /// <summary>
    /// Classe di configurazione centralizzata per l'applicazione Phonexis
    /// </summary>
    public static class Config
    {
        private static IConfiguration? _configuration;
        private static readonly object _lock = new();
        
        /// <summary>
        /// Inizializza la configurazione dell'applicazione
        /// </summary>
        public static void Initialize(IConfiguration configuration)
        {
            lock (_lock)
            {
                _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            }
        }
        
        /// <summary>
        /// Ottiene la configurazione corrente
        /// </summary>
        private static IConfiguration Configuration
        {
            get
            {
                if (_configuration == null)
                {
                    throw new InvalidOperationException("Configuration not initialized. Call Config.Initialize() first.");
                }
                return _configuration;
            }
        }
        
        #region Application Settings
        
        public static string AppName => Configuration["AppSettings:Name"] ?? "Phonexis";
        public static string AppVersion => Configuration["AppSettings:Version"] ?? "1.0.0";
        public static string OrganizationName => Configuration["AppSettings:Organization"] ?? "TX-Breaker";
        
        #endregion
        
        #region YouTube API Settings
        
        public static string YouTubeApiBaseUrl => Configuration["YouTube:ApiBaseUrl"] ?? "https://www.googleapis.com/youtube/v3/search";
        public static int MaxApiCallsPerDay => Configuration.GetValue<int>("YouTube:MaxDailyQuotaPerKey", 10000);
        public static int DefaultMaxResults => Configuration.GetValue<int>("YouTube:DefaultMaxResults", 5);
        public static int RequestTimeoutSeconds => Configuration.GetValue<int>("YouTube:RequestTimeoutSeconds", 30);
        
        #endregion
        
        #region Database Settings
        
        public static int MaxCacheSize => Configuration.GetValue<int>("Database:MaxCacheSize", 500);
        public static int ConnectionTimeoutSeconds => Configuration.GetValue<int>("Database:ConnectionTimeoutSeconds", 30);
        public static int CommandTimeoutSeconds => Configuration.GetValue<int>("Database:CommandTimeoutSeconds", 60);
        
        #endregion
        
        #region AutoSave Settings
        
        public static int AutoSaveIntervalSeconds => Configuration.GetValue<int>("AutoSave:IntervalSeconds", 30);
        public static int MaxBackupCount => Configuration.GetValue<int>("AutoSave:MaxBackupCount", 10);
        public static bool BackupBeforeCriticalOperations => Configuration.GetValue<bool>("AutoSave:BackupBeforeCriticalOperations", true);
        
        #endregion
        
        #region UI Settings
        
        public static string DefaultLanguage => Configuration["UI:DefaultLanguage"] ?? "en";
        public static string[] SupportedLanguages => Configuration.GetSection("UI:SupportedLanguages").Get<string[]>() ?? new[] { "en", "it", "fr", "es" };
        public static bool AutoSaveVisualFeedback => Configuration.GetValue<bool>("UI:AutoSaveVisualFeedback", true);
        public static int SearchResultsPerPage => Configuration.GetValue<int>("UI:SearchResultsPerPage", 10);
        
        #endregion
        
        #region Security Settings
        
        public static string ApiKeyEncryptionSalt => Configuration["Security:ApiKeyEncryption:Salt"] ?? "PhonexisSecureSalt2024";
        public static int KeyDerivationIterations => Configuration.GetValue<int>("Security:ApiKeyEncryption:KeyDerivationIterations", 10000);
        
        #endregion
        
        #region File Paths
        
        private static readonly Lazy<string> _appDataPath = new(() =>
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppName);
            
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            
            return path;
        });
        
        public static string AppDataPath => _appDataPath.Value;
        
        public static string GetStateFilePath() => Path.Combine(AppDataPath, "session_state.json.gz");
        public static string GetEmergencyStateFilePath() => Path.Combine(AppDataPath, "emergency_session_state.json.gz");
        public static string GetNoResultsLogFilePath() => Path.Combine(AppDataPath, "no_results_log.txt");
        public static string GetDatabaseFilePath() => Path.Combine(AppDataPath, "youtube_cache.db");
        public static string GetSecureApiKeysFilePath() => Path.Combine(AppDataPath, "api_keys.env");
        public static string GetLogFilePath() => Path.Combine(AppDataPath, "logs", "phonexis.log");
        
        public static string GetBackupDirectory() => Path.Combine(AppDataPath, "backups");
        public static string GetTempDirectory() => Path.Combine(AppDataPath, "temp");
        
        #endregion
        
        #region Validation
        
        /// <summary>
        /// Valida la configurazione corrente
        /// </summary>
        public static void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(AppName))
                throw new InvalidOperationException("App name cannot be null or empty");
                
            if (string.IsNullOrWhiteSpace(YouTubeApiBaseUrl))
                throw new InvalidOperationException("YouTube API base URL cannot be null or empty");
                
            if (MaxApiCallsPerDay <= 0)
                throw new InvalidOperationException("Max API calls per day must be greater than 0");
                
            if (AutoSaveIntervalSeconds <= 0)
                throw new InvalidOperationException("AutoSave interval must be greater than 0");
        }
        
        #endregion
    }
}
