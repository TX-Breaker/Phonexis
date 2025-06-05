using System;
using System.Reflection;
using System.Resources;
using System.Globalization;

namespace Phonexis.Helpers
{
    /// <summary>
    /// Helper class to retrieve localized strings using ResourceManager.
    /// </summary>
    public static class LocalizationHelper
    {
        private static readonly ResourceManager _resourceManager;
        private static CultureInfo _currentCulture = new CultureInfo("en"); // Set English as default
        
        /// <summary>
        /// Gets the current language code (e.g., "en", "it", "fr", "es").
        /// </summary>
        public static string CurrentLanguage => _currentCulture.TwoLetterISOLanguageName;

        static LocalizationHelper()
        {
            // Base name should match the root namespace + folder + filename (without .resx)
            _resourceManager = new ResourceManager("Phonexis.Properties.Strings", Assembly.GetExecutingAssembly());
        }
        
        /// <summary>
        /// Sets the current language for the application.
        /// </summary>
        /// <param name="languageCode">The two-letter ISO language code (e.g., "en", "it", "fr", "es").</param>
        public static void SetLanguage(string languageCode)
        {
            try
            {
                _currentCulture = new CultureInfo(languageCode);
                CultureInfo.CurrentUICulture = _currentCulture;
                CultureInfo.CurrentCulture = _currentCulture;
                
                // Notify the application that the language has changed
                // This could be implemented with an event if needed
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting language to '{languageCode}': {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the localized string for the specified key.
        /// </summary>
        /// <param name="key">The key of the string resource.</param>
        /// <returns>The localized string, or the key itself if not found.</returns>
        public static string GetString(string key)
        {
            try
            {
                // Use the current culture set by SetLanguage
                string? value = _resourceManager.GetString(key, _currentCulture);
                return value ?? key; // Return key if resource is null
            }
            catch (Exception ex) // Catch potential exceptions during resource lookup
            {
                // Log the error (consider injecting a logger if available)
                Console.WriteLine($"Error getting resource string for key '{key}': {ex.Message}");
                return key; // Return the key as a fallback
            }
        }

        /// <summary>
        /// Gets the localized string for the specified key and formats it.
        /// </summary>
        /// <param name="key">The key of the string resource.</param>
        /// <param name="args">Arguments for formatting.</param>
        /// <returns>The formatted localized string, or the key itself if not found.</returns>
        public static string GetFormattedString(string key, params object[] args)
        {
            string format = GetString(key);
            if (format == key) // If the key itself was returned due to an error or missing resource
            {
                return key;
            }
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"Error formatting resource string for key '{key}': {ex.Message}");
                return key; // Return the key as a fallback on formatting error
            }
        }
    }
}
