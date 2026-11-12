using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phonexis.Models;
using Phonexis.Services;
using Phonexis.Services.Search;

namespace Phonexis.ViewModels;

/// <summary>Search engine choice: free search, the API, or both in the user's order.</summary>
public partial class MainViewModel
{
    /// <summary>Engine the user can switch to after a failed search; null when there is none to offer.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAlternativeEngine), nameof(RetryWithAlternativeText), nameof(PreferAlternativeText))]
    private SearchEngine? _alternativeEngine;

    public bool HasAlternativeEngine => AlternativeEngine != null;

    /// <summary>Engine that answered the current search; null for cache, test mode or a failure.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTryApiOnEmpty))]
    private SearchEngine? _lastEngine;

    /// <summary>
    /// Free search answered with nothing. Empty answers never fall back on their own (no quota burnt on
    /// songs YouTube does not have), so the no-results card offers the API as a deliberate second try.
    /// </summary>
    public bool CanTryApiOnEmpty => LastEngine == SearchEngine.Free && HasApiKeys;
    public string TryApiText => L.Format("RetryWithFormat", EngineName(SearchEngine.Api));

    [RelayCommand]
    private Task TryApiAsync() => SearchCurrentAsync(bypassCache: true, modeOverride: SearchEngineMode.ApiOnly);

    /// <summary>One line per failed engine under the error title: engine name, then why.</summary>
    [ObservableProperty] private IReadOnlyList<EngineFailureLine> _searchErrorDetails = Array.Empty<EngineFailureLine>();
    [ObservableProperty] private string _searchErrorHint = string.Empty;
    public string RetryWithAlternativeText =>
        AlternativeEngine is { } e ? L.Format("RetryWithFormat", EngineName(e)) : string.Empty;
    public string PreferAlternativeText =>
        AlternativeEngine is { } e ? L.Format("AlwaysPreferFormat", EngineName(e)) : string.Empty;

    public SearchEngineMode SearchMode => _settings.Settings.SearchMode;

    /// <summary>Localized labels for the title-bar picker, in <see cref="SearchEngineModes.All"/> order.</summary>
    public IReadOnlyList<string> SearchModeOptions =>
        SearchEngineModes.All.Select(m => L[SearchEngineModes.I18nKey(m)]).ToList();

    public int SearchModeIndex
    {
        get => Array.IndexOf(SearchEngineModes.All, SearchMode);
        set
        {
            if (value >= 0 && value < SearchEngineModes.All.Length)
                SetSearchMode(SearchEngineModes.All[value].ToString());
        }
    }

    /// <summary>API quota is meaningless when the API is never used.</summary>
    public bool ShowQuotaPill => SearchMode != SearchEngineMode.FreeOnly;

    public string SearchModeTooltip
    {
        get
        {
            // The pill may be trimmed to an ellipsis on narrow windows: the tooltip always names the mode.
            var tip = L["SearchEngineLabel"] + ": " + L[SearchEngineModes.I18nKey(SearchMode)] + "\n" + L["SearchModeTooltip"];
            foreach (var engine in SearchEngineModes.Order(SearchMode).Where(_youtube.IsCoolingDown))
                tip += "\n" + L.Format("EngineCoolingFormat", EngineName(engine));
            return tip;
        }
    }

    [RelayCommand]
    private void SetSearchMode(string? mode)
    {
        if (!Enum.TryParse<SearchEngineMode>(mode, out var parsed) || parsed == SearchMode) return;
        _settings.Settings.SearchMode = parsed;
        _settings.Save();
        _log.Info($"Search mode set to {parsed}");
        NotifySearchModeChanged();
    }

    [RelayCommand]
    private async Task RetryWithAlternativeAsync()
    {
        if (AlternativeEngine is not { } engine) return;
        var once = engine == SearchEngine.Api ? SearchEngineMode.ApiOnly : SearchEngineMode.FreeOnly;
        await SearchCurrentAsync(bypassCache: true, modeOverride: once);
    }

    [RelayCommand]
    private async Task PreferAlternativeAsync()
    {
        if (AlternativeEngine is not { } engine) return;
        SetSearchMode(SearchEngineModes.Preferring(engine).ToString());
        await SearchCurrentAsync(bypassCache: true);
    }

    private void NotifySearchModeChanged()
    {
        OnPropertyChanged(nameof(SearchMode));
        OnPropertyChanged(nameof(SearchModeIndex));
        OnPropertyChanged(nameof(SearchModeTooltip));
        OnPropertyChanged(nameof(ShowQuotaPill));
        OnPropertyChanged(nameof(CanTryApiOnEmpty));
        OnPropertyChanged(nameof(TryApiText));
        OnPropertyChanged(nameof(ShowNoApiKeyBanner));
    }

    private string EngineName(SearchEngine engine) => L[SearchEngineModes.I18nKey(engine)];

    /// <summary>Fills the per-engine lines and the hint; returns the card title.</summary>
    private string DescribeSearchFailure(SearchFailedException ex)
    {
        SearchErrorDetails = ex.Failures
            .Select(f => new EngineFailureLine(EngineName(f.Engine), DescribeReason(f.Error)))
            .ToList();
        if (!HasApiKeys && ex.Failures.All(f => f.Engine == SearchEngine.Free))
            SearchErrorHint = L["AddKeyForBackupHint"];
        else if (HasApiKeys && ex.Failures.Any(f => f.Engine == SearchEngine.Api && f.Error is ApiQuotaException))
            SearchErrorHint = L["AllKeysFailedHint"];
        return L["SearchAllFailed"];
    }

    private void ClearSearchErrorDetails()
    {
        SearchErrorDetails = Array.Empty<EngineFailureLine>();
        SearchErrorHint = string.Empty;
    }

    private string DescribeReason(Exception error) => error switch
    {
        ApiQuotaException when !HasApiKeys => L["ReasonNoKeys"],
        ApiQuotaException => L["ReasonQuota"],
        HttpRequestException => L["ReasonNetwork"],
        TimeoutException => L["ReasonTimeout"],
        _ => error.Message
    };

    /// <summary>
    /// The engine left out of the mode that just failed, if it can work: the API only with a key.
    /// Modes that already use both engines have nothing left to offer.
    /// </summary>
    private SearchEngine? PickAlternative(SearchEngineMode mode)
    {
        var tried = SearchEngineModes.Order(mode);
        foreach (var engine in new[] { SearchEngine.Free, SearchEngine.Api })
        {
            if (tried.Contains(engine)) continue;
            if (engine == SearchEngine.Api && !HasApiKeys) continue;
            return engine;
        }
        return null;
    }
}

public record EngineFailureLine(string Engine, string Reason);
