namespace Phonexis.Services.Downloads;

/// <summary>
/// Cancels one download when it stops making progress. Every progress report calls <see cref="Touch"/>;
/// if nothing arrives for <c>timeout</c>, <see cref="Token"/> is cancelled and <see cref="Stalled"/> is set,
/// so the caller can retry instead of leaving the queue hanging.
/// </summary>
public sealed class StallGuard : IDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly Timer _timer;
    private readonly TimeSpan _timeout;
    private long _lastTouch = Environment.TickCount64;

    public StallGuard(TimeSpan timeout, CancellationToken outer)
    {
        _timeout = timeout;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(outer);
        var period = TimeSpan.FromMilliseconds(Math.Clamp(timeout.TotalMilliseconds / 6, 50, 5000));
        _timer = new Timer(_ => Check(), null, period, period);
    }

    public CancellationToken Token => _cts.Token;

    /// <summary>True when the guard (not the user) cancelled the download.</summary>
    public bool Stalled { get; private set; }

    public void Touch() => Interlocked.Exchange(ref _lastTouch, Environment.TickCount64);

    private void Check()
    {
        if (_cts.IsCancellationRequested) return;
        if (Environment.TickCount64 - Interlocked.Read(ref _lastTouch) < _timeout.TotalMilliseconds) return;
        Stalled = true;
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }
    }

    public void Dispose()
    {
        _timer.Dispose();
        _cts.Dispose();
    }
}
