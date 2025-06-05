using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Phonexis.Helpers;
using Phonexis.Models;

namespace Phonexis.Services
{
    /// <summary>
    /// Service implementation for YouTube API operations
    /// </summary>
    public class YouTubeApiService : IYouTubeApiService
    {
        private readonly ILoggingService _loggingService;
        private readonly IDatabaseService _databaseService;
        private readonly HttpClient _httpClient;
        private List<string> _apiKeys;
        private int _currentKeyIndex;
        
        // Constants
        private const string YOUTUBE_API_BASE_URL = "https://www.googleapis.com/youtube/v3/search";
        private const int MAX_DAILY_QUOTA_PER_KEY = 10000; // Standard YouTube API daily quota per key
        private readonly Random _random = new Random();

        /// <summary>
        /// Constructor with required dependencies
        /// </summary>
        public YouTubeApiService(ILoggingService loggingService, IDatabaseService databaseService)
        {
            _loggingService = loggingService;
            _databaseService = databaseService;
            _httpClient = new HttpClient();
            
            // Carica le chiavi API salvate
            _apiKeys = ApiKeyManager.LoadApiKeys();
            _currentKeyIndex = 0;
            
            _loggingService.Info($"YouTube API service initialized with {_apiKeys.Count} API keys");
        }

        /// <summary>
        /// Set the API keys to use for requests
        /// </summary>
        public void SetApiKeys(List<string> apiKeys)
        {
            if (apiKeys == null || !apiKeys.Any())
            {
                _loggingService.Warning("Attempted to set empty API keys list");
                return;
            }
            
            _apiKeys = apiKeys.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
            _currentKeyIndex = 0;
            
            // Salva le chiavi API in modo sicuro
            bool saved = ApiKeyManager.SaveApiKeys(_apiKeys);
            if (saved)
            {
                _loggingService.Info($"Set and saved {_apiKeys.Count} YouTube API keys");
            }
            else
            {
                _loggingService.Warning($"Set {_apiKeys.Count} YouTube API keys but failed to save them securely");
            }
        }

        /// <summary>
        /// Get the currently configured API keys
        /// </summary>
        public List<string> GetApiKeys()
        {
            return _apiKeys.ToList(); // Return a copy to prevent external modification
        }
        
        /// <summary>
        /// Get the index of the currently used API key
        /// </summary>
        public int GetCurrentKeyIndex()
        {
            return _currentKeyIndex;
        }

        /// <summary>
        /// Search for videos on YouTube
        /// </summary>
        public async Task<List<SearchResult>> SearchAsync(string query, int maxResults = 5)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _loggingService.Warning("Empty search query provided");
                return new List<SearchResult>();
            }
            
            // First check if we have results in the cache
            var cachedResults = _databaseService.GetSearchResults(query);
            if (cachedResults.Count > 0)
            {
                _loggingService.Debug($"Using cached results for query: '{query}'");
                // Imposta la proprietà IsFromCache per indicare che i risultati provengono dalla cache
                foreach (var result in cachedResults)
                {
                    result.IsFromCache = true;
                    result.IsDummy = false;
                }
                return cachedResults.Take(maxResults).ToList();
            }
            
            // Check if we have API keys available
            if (_apiKeys.Count == 0)
            {
                _loggingService.Warning("No YouTube API keys configured, using dummy results");
                return GenerateDummyResults(query, maxResults);
            }
            
            // Check the API quota (This logic needs significant rework for multi-API keys)
            // int apiCallCount = _databaseService.GetApiCallCount(); // TODO: Remove/replace global counter
            // if (apiCallCount >= MAX_DAILY_QUOTA) // TODO: Check quota for the *current* key
            // {
            //     _loggingService.Warning("YouTube API daily quota potentially exceeded for current key.");
            //     // TODO: Implement proper key rotation or error handling instead of dummy results
            //     // return GenerateDummyResults(query, maxResults); // REMOVED DUMMY FALLBACK
            //     // For now, let the request proceed and handle potential quota errors in ExecuteYouTubeSearchAsync
            // }
            
            try
            {
                _loggingService.Info($"Searching YouTube for: '{query}'");
                var results = await ExecuteYouTubeSearchAsync(query, maxResults);
                
                // Cache the results
                if (results.Count > 0)
                {
                    _databaseService.CacheSearchResults(query, results);
                    _loggingService.Debug($"Cached {results.Count} results for query: '{query}'");
                }
                
                return results;
            }
            catch (Exception ex)
            {
                _loggingService.Error($"Error searching YouTube: {ex.Message}");
                // return GenerateDummyResults(query, maxResults); // REMOVED DUMMY FALLBACK
                // Propagate the error or return an empty list to indicate failure
                // Depending on desired behavior, could show error message to user in ViewModel
                return new List<SearchResult>(); // Return empty list on error for now
            }
        }

        /// <summary>
        /// Generate dummy results for testing or when API is unavailable
        /// </summary>
        public List<SearchResult> GenerateDummyResults(string query, int count = 5)
        {
            _loggingService.Debug($"Generating {count} dummy results for query: '{query}'");
            
            var results = new List<SearchResult>();
            string[] channelNames = { "TechChannel", "GamerSpot", "TutorialHub", "MusicVibes", "NewsNetwork" };
            
            for (int i = 0; i < count; i++)
            {
                var result = new SearchResult
                {
                    VideoId = GenerateRandomVideoId(),
                    Title = $"[DUMMY] {query} - Result {i + 1}",
                    Description = $"This is a dummy search result for the query '{query}'. Generated automatically for testing purposes.",
                    ChannelTitle = channelNames[_random.Next(channelNames.Length)],
                    ThumbnailUrl = $"https://via.placeholder.com/{320 + i * 10}x{180 + i * 5}",
                    PublishedAt = DateTime.Now.AddDays(-_random.Next(1, 365)),
                    ViewCount = _random.Next(100, 1000000),
                    Duration = $"{_random.Next(0, 10)}:{_random.Next(10, 59)}:{_random.Next(10, 59)}",
                    IsDummy = true,
                    IsFromCache = false
                };
                
                results.Add(result);
            }
            
            return results;
        }
        
        /// <summary>
        /// Execute a search against the YouTube Data API, handling key rotation.
        /// </summary>
        private async Task<List<SearchResult>> ExecuteYouTubeSearchAsync(string query, int maxResults)
        {
            if (_apiKeys == null || _apiKeys.Count == 0)
            {
                _loggingService.Error("ExecuteYouTubeSearchAsync called with no API keys available.");
                throw new InvalidOperationException("No API keys available");
            }

            int initialKeyIndex = _currentKeyIndex;
            int attempts = 0;

            while (attempts < _apiKeys.Count)
            {
                string currentApiKey = _apiKeys[_currentKeyIndex];
                _loggingService.Debug($"Attempting YouTube search with API key index: {_currentKeyIndex}");
                var today = DateTime.UtcNow.Date; // Use UTC date for consistency

                // Check if key is marked as exhausted for today
                if (_databaseService.IsApiKeyExhausted(currentApiKey, today))
                {
                    _loggingService.Warning($"API key index {_currentKeyIndex} is marked as exhausted for today. Rotating.");
                    _currentKeyIndex = (_currentKeyIndex + 1) % _apiKeys.Count;
                    attempts++;
                    if (attempts >= _apiKeys.Count) // Check if we've tried all keys
                    {
                         _loggingService.Error("All API keys are marked as exhausted for today.");
                         break; // Exit loop
                    }
                    continue; // Try next key
                }

                // Check if key has reached the quota limit for today
                int currentKeyQuotaUsed = _databaseService.GetApiCallCount(currentApiKey, today);
                if (currentKeyQuotaUsed >= MAX_DAILY_QUOTA_PER_KEY)
                {
                    _loggingService.Warning($"Quota ({currentKeyQuotaUsed}/{MAX_DAILY_QUOTA_PER_KEY}) met or exceeded for API key index: {_currentKeyIndex}. Marking as exhausted and rotating.");
                    _databaseService.MarkApiKeyExhausted(currentApiKey, today); // Mark it
                    _currentKeyIndex = (_currentKeyIndex + 1) % _apiKeys.Count;
                    attempts++;
                     if (attempts >= _apiKeys.Count) // Check if we've tried all keys
                    {
                         _loggingService.Error("All API keys have met or exceeded their quota for today.");
                         break; // Exit loop
                    }
                    continue; // Try next key
                }

                // Key seems usable, proceed with the request
                try
                {
                    // Build the request URL
                    var uriBuilder = new UriBuilder(YOUTUBE_API_BASE_URL);
                    var queryParams = HttpUtility.ParseQueryString(string.Empty);
                    queryParams["key"] = currentApiKey;
                    queryParams["part"] = "snippet";
                    queryParams["maxResults"] = maxResults.ToString();
                    queryParams["q"] = query;
                    queryParams["type"] = "video";
                    uriBuilder.Query = queryParams.ToString();

                    // Make the request
                    HttpResponseMessage response = await _httpClient.GetAsync(uriBuilder.Uri);

                    // REMOVED global counter increment
                    // Increment per-key counter happens *after* successful processing or based on response type

                    if (response.IsSuccessStatusCode)
                    {
                        // Process the successful response
                        string responseContent = await response.Content.ReadAsStringAsync();
                        var results = ParseYouTubeResponse(responseContent);
                        _loggingService.Info($"Successfully found {results.Count} results using API key index: {_currentKeyIndex}");
                        // Increment per-key counter here after success
                        _databaseService.IncrementApiCallCount(currentApiKey, today);
                        return results; // Success, return results
                    }

                    // Handle API errors - Increment count even on errors, as the call was made
                    _databaseService.IncrementApiCallCount(currentApiKey, today); // Increment even on failure
                    string errorContent = await response.Content.ReadAsStringAsync();
                    _loggingService.Error($"YouTube API error with key index {_currentKeyIndex}: {response.StatusCode} - {errorContent}");

                    // Check for quota/auth errors to trigger rotation
                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || // Often quota exceeded
                        response.StatusCode == System.Net.HttpStatusCode.Unauthorized) // Invalid key
                    {
                        _loggingService.Warning($"Quota or auth error for API key index: {_currentKeyIndex}. Marking as exhausted and rotating.");
                        _databaseService.MarkApiKeyExhausted(currentApiKey, today); // Mark as exhausted
                    }
                    else
                    {
                        // For other errors (e.g., server errors), maybe don't rotate immediately?
                        // Or maybe still rotate, assuming the key might be temporarily bad.
                        // For now, treat other errors as potentially key-related and rotate.
                         _loggingService.Warning($"Non-quota/auth error for API key index: {_currentKeyIndex}. Rotating anyway.");
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    // Network errors, etc.
                    _loggingService.Error($"HTTP request exception with key index {_currentKeyIndex}: {httpEx.Message}. Rotating.");
                    // Rotate on network errors too, maybe the endpoint is region-specific?
                }
                catch (Exception ex)
                {
                    // Catch other unexpected errors during the request phase
                    _loggingService.Error($"Unexpected exception during API request with key index {_currentKeyIndex}: {ex.Message}. Rotating.");
                }

                // If we reached here, the current key failed. Rotate to the next one.
                _currentKeyIndex = (_currentKeyIndex + 1) % _apiKeys.Count;
                attempts++;

                // Check if we've tried all keys
                if (attempts < _apiKeys.Count && _currentKeyIndex == initialKeyIndex)
                {
                    // This should ideally not happen if attempts < _apiKeys.Count, but as a safeguard...
                     _loggingService.Error("Key rotation logic error: Cycled through all keys unexpectedly.");
                     break; // Exit loop to prevent infinite loop
                }
                 else if (attempts >= _apiKeys.Count) // Check if we have completed a full cycle
                {
                     _loggingService.Error("All API keys failed or exhausted.");
                     break; // Exit loop after trying all keys
                }
                // Otherwise, loop continues with the next key index
            }

            // If the loop finishes without returning results, all keys failed.
            throw new InvalidOperationException("Failed to retrieve results from YouTube API after trying all available keys.");
        }
        
        /// <summary>
        /// Parse the JSON response from YouTube Data API
        /// </summary>
        private List<SearchResult> ParseYouTubeResponse(string responseContent)
        {
            var results = new List<SearchResult>();
            
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(responseContent))
                {
                    JsonElement root = doc.RootElement;
                    
                    if (root.TryGetProperty("items", out JsonElement items))
                    {
                        foreach (JsonElement item in items.EnumerateArray())
                        {
                            // Extract the video ID
                            string videoId = "";
                            if (item.TryGetProperty("id", out JsonElement id) && 
                                id.TryGetProperty("videoId", out JsonElement videoIdElement))
                            {
                                videoId = videoIdElement.GetString() ?? "";
                            }
                            
                            if (string.IsNullOrEmpty(videoId))
                                continue;
                                
                            // Extract snippet information
                            if (item.TryGetProperty("snippet", out JsonElement snippet))
                            {
                                var result = new SearchResult
                                {
                                    VideoId = videoId,
                                    Title = GetJsonPropertyString(snippet, "title"),
                                    Description = GetJsonPropertyString(snippet, "description"),
                                    ChannelTitle = GetJsonPropertyString(snippet, "channelTitle"),
                                    PublishedAt = GetPublishedDate(snippet),
                                    ThumbnailUrl = GetThumbnailUrl(snippet),
                                    ViewCount = 0, // This requires a separate API call
                                    Duration = "00:00:00", // This requires a separate API call
                                    IsFromCache = false,
                                    IsDummy = false
                                };
                                
                                results.Add(result);
                            }
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _loggingService.Error($"Error parsing YouTube response: {ex.Message}");
                throw;
            }
            
            return results;
        }
        
        /// <summary>
        /// Helper method to safely get string property from JSON element
        /// </summary>
        private string GetJsonPropertyString(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out JsonElement property))
            {
                return property.GetString() ?? "";
            }
            return "";
        }
        
        /// <summary>
        /// Extract published date from snippet
        /// </summary>
        private DateTime GetPublishedDate(JsonElement snippet)
        {
            if (snippet.TryGetProperty("publishedAt", out JsonElement publishedAt))
            {
                string? dateString = publishedAt.GetString();
                if (dateString != null && DateTime.TryParse(dateString, out DateTime result))
                {
                    return result;
                }
            }
            return DateTime.Now;
        }
        
        /// <summary>
        /// Extract the best available thumbnail URL
        /// </summary>
        private string GetThumbnailUrl(JsonElement snippet)
        {
            if (snippet.TryGetProperty("thumbnails", out JsonElement thumbnails))
            {
                // Try to get high quality thumbnail first, then fall back to medium and default
                string[] qualityLevels = { "high", "medium", "default" };
                
                foreach (string quality in qualityLevels)
                {
                    if (thumbnails.TryGetProperty(quality, out JsonElement thumbnail) && 
                        thumbnail.TryGetProperty("url", out JsonElement url))
                    {
                        return url.GetString() ?? "";
                    }
                }
            }
            
            return "";
        }
        
        /// <summary>
        /// Generate a random YouTube-like video ID for dummy results
        /// </summary>
        private string GenerateRandomVideoId()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
            char[] id = new char[11]; // YouTube IDs are 11 characters
            
            for (int i = 0; i < 11; i++)
            {
                id[i] = chars[_random.Next(chars.Length)];
            }
            
            return new string(id);
        }
    }
}
