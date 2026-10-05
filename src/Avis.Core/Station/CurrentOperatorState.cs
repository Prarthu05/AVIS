namespace Avis.Station;

/// <summary>
/// The employee ID of whichever operator most recently badge-scanned in at this
/// station, held in memory so it can be attributed (via IFactoryClient's
/// operatorId parameter / the x-operator-override header) to whatever iFactory
/// action that operator's work triggers next - e.g. the eventual Start WIP call
/// once camera integration captures an Asset ID. There's no dedicated "operator
/// logged in" iFactory endpoint for this: ChangeQueue, the one place the header
/// shows up in the API reference we have, operates on a specific WIP that
/// doesn't exist yet at badge-scan time, so attribution happens by carrying the
/// operator forward to whichever WIP-level call comes next instead.
///
/// Each part needs its own fresh scan: a scan is only valid for a limited time
/// window (see ConsumeRecentScan) and is cleared once read that way, so it can
/// never silently carry over and get attributed to a second, unrelated part.
/// </summary>
public class CurrentOperatorState
{
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();
    private string? _employeeId;
    private DateTimeOffset _scannedAt;
    private TaskCompletionSource<string>? _nextScanWaiter;

    public CurrentOperatorState(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>The most recently scanned employee ID, regardless of age. For attribution, use ConsumeRecentScan instead.</summary>
    public string? EmployeeId
    {
        get
        {
            lock (_lock)
            {
                return _employeeId;
            }
        }
    }

    public void SetOperator(string employeeId)
    {
        TaskCompletionSource<string>? waiter;
        lock (_lock)
        {
            waiter = _nextScanWaiter;
            _nextScanWaiter = null;
            if (waiter is null)
            {
                _employeeId = employeeId;
                _scannedAt = _timeProvider.GetUtcNow();
            }
        }
        // A part already waiting for its badge consumes this scan directly, so it
        // can't also be attributed to the next part.
        waiter?.TrySetResult(employeeId);
    }

    /// <summary>
    /// Completes with the next badge scan (which is consumed, exactly like
    /// ConsumeRecentScan). Used when a part arrives before its operator has
    /// scanned - the badge and the Asset ID can arrive in either order.
    /// </summary>
    public Task<string> WaitForNextScanAsync(CancellationToken ct)
    {
        TaskCompletionSource<string> waiter;
        lock (_lock)
        {
            waiter = _nextScanWaiter ??= new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        if (ct.CanBeCanceled)
        {
            var registration = ct.Register(() =>
            {
                lock (_lock)
                {
                    if (_nextScanWaiter == waiter)
                    {
                        _nextScanWaiter = null;
                    }
                }
                waiter.TrySetCanceled(ct);
            });
            _ = waiter.Task.ContinueWith(_ => registration.Dispose(), TaskScheduler.Default);
        }
        return waiter.Task;
    }

    /// <summary>
    /// Returns the scanned employee ID only if the scan happened within the last
    /// <paramref name="maxAge"/> - and, either way, clears the stored scan so it
    /// can never be reused for a second, later part. Each badge scan is meant to
    /// cover exactly the one part captured next, not carried forward indefinitely.
    /// </summary>
    public string? ConsumeRecentScan(TimeSpan maxAge)
    {
        lock (_lock)
        {
            var employeeId = _employeeId;
            var scannedAt = _scannedAt;
            _employeeId = null;

            if (employeeId is null)
            {
                return null;
            }

            return _timeProvider.GetUtcNow() - scannedAt <= maxAge ? employeeId : null;
        }
    }
}
