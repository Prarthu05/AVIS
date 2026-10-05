using System.Collections.Immutable;

namespace Avis.Station;

public enum StationPhase
{
    Idle,
    WaitingForBadge,
    StartingWip,
    InProcess,
    ReworkRequired,
    EscalationRequired,
    Completing,
    Completed,
    NotConfirmed,
}

public enum ConnectionState
{
    Unknown,
    Ok,
    Error,
}

public record CheckState(string Name, bool Passed, string Value, int Attempts, int Failures, DateTimeOffset At, string? ImagePath);

/// <summary>Immutable snapshot of everything the station screen shows.</summary>
public record StationState
{
    public string Environment { get; init; } = "";
    public string StationName { get; init; } = "";
    public string ResourceName { get; init; } = "";

    public StationPhase Phase { get; init; } = StationPhase.Idle;
    public string Message { get; init; } = "Waiting for part";
    public bool MessageIsError { get; init; }

    public string? OperatorId { get; init; }
    public string? AssetId { get; init; }
    public long? WipId { get; init; }
    public string? Material { get; init; }
    public string? Model { get; init; }

    public string? Program { get; init; }
    public int? Step { get; init; }
    public string? StepComment { get; init; }
    public string? VisualAidUrl { get; init; }

    public ImmutableList<CheckState> Checks { get; init; } = ImmutableList<CheckState>.Empty;

    public int PassCount { get; init; }
    public int FailCount { get; init; }
    public int ReworkCount { get; init; }

    public ConnectionState Scanner { get; init; }
    public ConnectionState Camera { get; init; }
    public ConnectionState IFactory { get; init; }
    public ConnectionState LightGuide { get; init; }

    /// <summary>PASS / (PASS + FAIL) in percent, or null before the first finished part.</summary>
    public double? YieldPercent => PassCount + FailCount == 0 ? null : 100.0 * PassCount / (PassCount + FailCount);
}

/// <summary>Thread-safe holder of the current StationState; raises Changed after every update.</summary>
public class StationStatus
{
    private readonly object _lock = new();
    private StationState _state;

    public StationStatus(StationState initial)
    {
        _state = initial;
    }

    public event Action<StationState>? Changed;

    public StationState Current
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    public StationState Update(Func<StationState, StationState> change)
    {
        StationState updated;
        lock (_lock)
        {
            updated = _state = change(_state);
        }
        Changed?.Invoke(updated);
        return updated;
    }
}
