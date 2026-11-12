using Phonexis.Models;

namespace Phonexis.Services.Search;

/// <summary>One way of turning a query into YouTube results.</summary>
public interface ISearchProvider
{
    SearchEngine Engine { get; }
    Task<List<SearchResult>> SearchAsync(string query, int maxResults, CancellationToken ct);
}

public record EngineFailure(SearchEngine Engine, Exception Error);

/// <summary>Results of a search plus how they were obtained.</summary>
public class SearchOutcome
{
    public List<SearchResult> Results { get; init; } = new();

    /// <summary>Engine that produced the results; null when they came from the local cache.</summary>
    public SearchEngine? Engine { get; init; }

    /// <summary>Engines tried before <see cref="Engine"/> that failed (empty when the first one answered).</summary>
    public IReadOnlyList<EngineFailure> Failures { get; init; } = Array.Empty<EngineFailure>();
}

/// <summary>Thrown when every engine in the chosen order failed.</summary>
public class SearchFailedException : Exception
{
    public IReadOnlyList<EngineFailure> Failures { get; }

    public SearchFailedException(IReadOnlyList<EngineFailure> failures)
        : base("All search engines failed: " + string.Join("; ", failures.Select(f => $"{f.Engine}: {f.Error.Message}")),
               failures.Count > 0 ? failures[^1].Error : null)
    {
        Failures = failures;
    }
}

/// <summary>
/// Runs the engines in the user's order and falls back to the next one when an engine throws.
/// An empty result list is an answer, not a failure: falling back on it would burn API quota on
/// songs YouTube simply does not have. After <see cref="FailureThreshold"/> failures in a row an
/// engine is moved to the back of the order for <see cref="Cooldown"/>, so a broken free search
/// does not slow down every song while the API is answering.
/// </summary>
public class SearchChain
{
    public const int FailureThreshold = 3;
    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    private readonly Dictionary<SearchEngine, ISearchProvider> _providers;
    private readonly ILogService? _log;
    private readonly Func<DateTime> _clock;
    private readonly Dictionary<SearchEngine, int> _consecutiveFailures = new();
    private readonly Dictionary<SearchEngine, DateTime> _coolingUntil = new();
    private readonly object _gate = new();

    public SearchChain(IEnumerable<ISearchProvider> providers, ILogService? log = null, Func<DateTime>? clock = null)
    {
        _providers = providers.ToDictionary(p => p.Engine);
        _log = log;
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    public bool IsCoolingDown(SearchEngine engine)
    {
        lock (_gate)
            return _coolingUntil.TryGetValue(engine, out var until) && until > _clock();
    }

    public async Task<SearchOutcome> RunAsync(string query, int maxResults, IReadOnlyList<SearchEngine> order, CancellationToken ct)
    {
        var failures = new List<EngineFailure>();
        foreach (var engine in Arrange(order))
        {
            if (!_providers.TryGetValue(engine, out var provider)) continue;
            ct.ThrowIfCancellationRequested();
            try
            {
                var results = await provider.SearchAsync(query, maxResults, ct);
                RecordSuccess(engine);
                return new SearchOutcome { Results = results, Engine = engine, Failures = failures };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log?.Warn($"{engine} search failed for '{query}': {ex.Message}");
                RecordFailure(engine);
                failures.Add(new EngineFailure(engine, ex));
            }
        }
        throw new SearchFailedException(failures);
    }

    /// <summary>The user's order with cooling-down engines moved to the back (never dropped).</summary>
    private List<SearchEngine> Arrange(IReadOnlyList<SearchEngine> order)
    {
        var ready = order.Where(e => !IsCoolingDown(e)).ToList();
        ready.AddRange(order.Where(IsCoolingDown));
        return ready;
    }

    private void RecordSuccess(SearchEngine engine)
    {
        lock (_gate)
        {
            _consecutiveFailures[engine] = 0;
            _coolingUntil.Remove(engine);
        }
    }

    private void RecordFailure(SearchEngine engine)
    {
        lock (_gate)
        {
            var n = _consecutiveFailures.GetValueOrDefault(engine) + 1;
            _consecutiveFailures[engine] = n;
            if (n >= FailureThreshold)
            {
                _coolingUntil[engine] = _clock() + Cooldown;
                _log?.Warn($"{engine} search failed {n} times in a row, deprioritised for {Cooldown.TotalMinutes:0} min");
            }
        }
    }
}
