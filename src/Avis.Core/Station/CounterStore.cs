using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Avis.Station;

public record DailyCounters(DateOnly Date, int Pass, int Fail, int Rework);

/// <summary>
/// Today's PASS / FAIL / rework counters, persisted to {directory}\counters-yyyyMMdd.json
/// so they survive an app restart, and reset automatically at midnight.
/// </summary>
public class CounterStore
{
    private readonly string _directory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CounterStore> _logger;
    private readonly object _lock = new();
    private DailyCounters _counters;

    public CounterStore(string directory, TimeProvider timeProvider, ILogger<CounterStore> logger)
    {
        _directory = directory;
        _timeProvider = timeProvider;
        _logger = logger;
        _counters = Load(Today);
    }

    private DateOnly Today => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    public DailyCounters Current
    {
        get
        {
            lock (_lock)
            {
                RollOverIfNewDay();
                return _counters;
            }
        }
    }

    public DailyCounters AddPass() => Change(c => c with { Pass = c.Pass + 1 });
    public DailyCounters AddFail() => Change(c => c with { Fail = c.Fail + 1 });
    public DailyCounters AddRework() => Change(c => c with { Rework = c.Rework + 1 });

    private DailyCounters Change(Func<DailyCounters, DailyCounters> change)
    {
        lock (_lock)
        {
            RollOverIfNewDay();
            _counters = change(_counters);
            Save(_counters);
            return _counters;
        }
    }

    private void RollOverIfNewDay()
    {
        if (_counters.Date != Today)
        {
            _counters = new DailyCounters(Today, 0, 0, 0);
        }
    }

    private string PathFor(DateOnly date) => Path.Combine(_directory, $"counters-{date:yyyyMMdd}.json");

    private DailyCounters Load(DateOnly date)
    {
        try
        {
            var path = PathFor(date);
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<DailyCounters>(File.ReadAllText(path));
                if (loaded is not null && loaded.Date == date)
                {
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read today's counters - starting from zero");
        }
        return new DailyCounters(date, 0, 0, 0);
    }

    private void Save(DailyCounters counters)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(PathFor(counters.Date), JsonSerializer.Serialize(counters));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save counters");
        }
    }
}
