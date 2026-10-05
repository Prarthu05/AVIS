using Avis.Station;
using Avis.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Avis.Tests.Station;

public sealed class CounterStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "avis-counters-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    [Fact]
    public void Counters_SurviveARestart_AndResetOnANewDay()
    {
        var time = new FakeTimeProvider();
        var store = new CounterStore(_dir, time, NullLogger<CounterStore>.Instance);
        store.AddPass();
        store.AddPass();
        store.AddFail();
        store.AddRework();

        var reloaded = new CounterStore(_dir, time, NullLogger<CounterStore>.Instance);
        Assert.Equal(2, reloaded.Current.Pass);
        Assert.Equal(1, reloaded.Current.Fail);
        Assert.Equal(1, reloaded.Current.Rework);

        time.Advance(TimeSpan.FromDays(1));
        Assert.Equal(0, reloaded.Current.Pass);
        Assert.Equal(1, reloaded.AddPass().Pass);
    }

    [Fact]
    public void Yield_IsPassOverFinishedParts()
    {
        var state = new StationState { PassCount = 3, FailCount = 1 };

        Assert.Equal(75, state.YieldPercent);
        Assert.Null(new StationState().YieldPercent);
    }
}
