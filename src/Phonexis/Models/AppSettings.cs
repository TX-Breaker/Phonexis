namespace Phonexis.Models;

public enum AppTheme { System, Light, Dark }

/// <summary>User preferences persisted to settings.json.</summary>
public class AppSettings
{
    public string Language { get; set; } = "en";
    public AppTheme Theme { get; set; } = AppTheme.System;
    public bool TestMode { get; set; }
    public bool CacheOnlyMode { get; set; }
    public int MaxResults { get; set; } = 5;

    /// <summary>Free search first saves API quota; the API steps in only when free search fails.</summary>
    public SearchEngineMode SearchMode { get; set; } = SearchEngineMode.FreeThenApi;

    // Window placement (null = let Windows decide). Nullable instead of NaN:
    // System.Text.Json cannot serialize NaN and would break settings persistence.
    public double WindowWidth { get; set; } = 1240;
    public double WindowHeight { get; set; } = 800;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool WindowMaximized { get; set; }

    public bool FirstRunCompleted { get; set; }

    /// <summary>Highlight the most likely result for each song (Enter picks it).</summary>
    public bool AutoPreselect { get; set; } = true;

    public DownloadOptions Download { get; set; } = new();
}
