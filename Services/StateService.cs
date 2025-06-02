using System;
using System.IO;
using System.Text.Json;
using Phonexis.Helpers;
using Phonexis.Models;

namespace Phonexis.Services
{
    /// <summary>
    /// Service for managing application state
    /// </summary>
    public class StateService : IStateService
    {
        private readonly ILoggingService _loggingService;
        private string _stateFilePath;
        private readonly string _emergencyStateFilePath;
        private readonly JsonSerializerOptions _jsonOptions;
        private AppState _currentState;

        /// <summary>
        /// Constructor with required dependencies
        /// </summary>
        public StateService(ILoggingService loggingService)
        {
            _loggingService = loggingService;
            
            // Set up file paths using Config class
            _stateFilePath = Config.GetStateFilePath();
            _emergencyStateFilePath = Config.GetEmergencyStateFilePath();
            
            // Configure JSON serialization options
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            // Initialize current state
            _currentState = new AppState();
            
            _loggingService.Info("State service initialized");
        }
        
        /// <summary>
        /// Gets the current application state
        /// </summary>
        /// <returns>The current application state</returns>
        public AppState GetState()
        {
            // If current state is not initialized, load it
            if (_currentState == null)
            {
                _currentState = LoadState();
            }
            
            return _currentState;
        }

        /// <summary>
        /// Save the application state
        /// </summary>
        public bool SaveState(AppState state)
        {
            if (state == null)
            {
                _loggingService.Warning("Attempted to save null state");
                return false;
            }
            
            try
            {
                // Use standard appdata path for persistence
                _stateFilePath = Config.GetStateFilePath();
                
                string? directory = Path.GetDirectoryName(_stateFilePath); // Made nullable
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    _loggingService.Info($"Creating directory: {directory}");
                    Directory.CreateDirectory(directory);
                }
                
                // Set timestamp before saving
                state.LastSaveTimestamp = DateTime.Now;
                
                string json = JsonSerializer.Serialize(state, _jsonOptions);
                bool success = CompressionHelper.CompressStringToFile(json, _stateFilePath);
                
                // Verify file was created
                if (success && !File.Exists(_stateFilePath))
                {
                    _loggingService.Error($"Compression reported success but file not found: {_stateFilePath}");
                    return false;
                }
                _loggingService.Debug($"State file size: {new FileInfo(_stateFilePath).Length} bytes");
                
                if (success)
                {
                    _loggingService.Info($"Application state saved successfully to {_stateFilePath}");
                    _currentState = state; // Update current state
                    return true;
                }
                else
                {
                    _loggingService.Error("Failed to compress and save application state");
                    return SaveEmergencyState(state);
                }
            }
            catch (Exception ex)
            {
                _loggingService.Error($"Failed to save application state: {ex.Message}");
                
                // Try to save as emergency state if normal save fails
                return SaveEmergencyState(state);
            }
        }

        /// <summary>
        /// Load the application state
        /// </summary>
        public AppState LoadState()
        {
            try
            {
                // Check if the state file exists
                if (!StateFileExists())
                {
                    _loggingService.Info("No state file found, creating new application state");
                    return new AppState();
                }
                
                string json = CompressionHelper.DecompressFileToString(_stateFilePath);
                
                if (string.IsNullOrEmpty(json))
                {
                    _loggingService.Warning("Decompressed state was empty, creating new state");
                    return new AppState();
                }
                
                var state = JsonSerializer.Deserialize<AppState>(json, _jsonOptions);
                
                if (state == null)
                {
                    _loggingService.Warning("Deserialized state was null, creating new state");
                    return new AppState();
                }
                
                _loggingService.Info("Application state loaded successfully");
                _currentState = state; // Update current state
                return state;
            }
            catch (Exception ex)
            {
                _loggingService.Error($"Failed to load application state: {ex.Message}");
                
                // Try to load from emergency state file if available
                try
                {
                    if (File.Exists(_emergencyStateFilePath))
                    {
                        _loggingService.Info("Attempting to load from emergency state file");
                        string emergencyJson = CompressionHelper.DecompressFileToString(_emergencyStateFilePath);
                        
                        if (!string.IsNullOrEmpty(emergencyJson))
                        {
                            var emergencyState = JsonSerializer.Deserialize<AppState>(emergencyJson, _jsonOptions);
                            
                            if (emergencyState != null)
                            {
                                _loggingService.Info("Successfully loaded emergency state");
                                _currentState = emergencyState; // Update current state
                                return emergencyState;
                            }
                        }
                    }
                }
                catch (Exception emergencyEx)
                {
                    _loggingService.Error($"Failed to load emergency state: {emergencyEx.Message}");
                }
                
                // Return a new state as last resort
                _loggingService.Info("Creating new application state");
                _currentState = new AppState(); // Update current state
                return _currentState;
            }
        }

        /// <summary>
        /// Save the application state to an emergency file
        /// </summary>
        public bool SaveEmergencyState(AppState state)
        {
            if (state == null)
            {
                _loggingService.Warning("Attempted to save null emergency state");
                return false;
            }
            
            try
            {
                // Assicuriamoci che la directory esista
                string? directory = Path.GetDirectoryName(_emergencyStateFilePath); // Made nullable
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    _loggingService.Info($"Creating directory: {directory}");
                    Directory.CreateDirectory(directory);
                }
                
                string json = JsonSerializer.Serialize(state, _jsonOptions);
                bool success = CompressionHelper.CompressStringToFile(json, _emergencyStateFilePath);
                
                if (success)
                {
                    _loggingService.Info($"Emergency state saved successfully to {_emergencyStateFilePath}");
                    return true;
                }
                else
                {
                    _loggingService.Error("Failed to compress and save emergency state");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _loggingService.Error($"Failed to save emergency state: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if the state file exists
        /// </summary>
        public bool StateFileExists()
        {
            bool exists = File.Exists(_stateFilePath);
            if (exists)
            {
                _loggingService.Debug("State file exists");
            }
            else
            {
                _loggingService.Debug("State file does not exist");
            }
            return exists;
        }

        /// <summary>
        /// Delete the state file
        /// </summary>
        public bool DeleteStateFile()
        {
            try
            {
                // Delete main state file if it exists
                if (StateFileExists())
                {
                    File.Delete(_stateFilePath);
                    _loggingService.Info("State file deleted successfully");
                }
                
                // Also delete emergency state file if it exists
                if (File.Exists(_emergencyStateFilePath))
                {
                    File.Delete(_emergencyStateFilePath);
                    _loggingService.Info("Emergency state file deleted successfully");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.Error($"Failed to delete state file: {ex.Message}");
                return false;
            }
        }
    }
}
