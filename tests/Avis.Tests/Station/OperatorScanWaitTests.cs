using Avis.Station;
using Avis.Tests.Fakes;
using Xunit;

namespace Avis.Tests.Station;

public class OperatorScanWaitTests
{
    [Fact]
    public async Task WaitForNextScan_CompletesWithTheNextScan_AndConsumesIt()
    {
        var state = new CurrentOperatorState(new FakeTimeProvider());

        var wait = state.WaitForNextScanAsync(CancellationToken.None);
        Assert.False(wait.IsCompleted);

        state.SetOperator("4375789");

        Assert.Equal("4375789", await wait.WaitAsync(TimeSpan.FromSeconds(5)));
        // Consumed by the waiting part - can't also be attributed to the next one.
        Assert.Null(state.ConsumeRecentScan(TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task WaitForNextScan_CanBeCancelled_AndLaterScansAreStoredNormally()
    {
        var state = new CurrentOperatorState(new FakeTimeProvider());
        using var cts = new CancellationTokenSource();

        var wait = state.WaitForNextScanAsync(cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        state.SetOperator("1");
        Assert.Equal("1", state.ConsumeRecentScan(TimeSpan.FromMinutes(1)));
    }
}
