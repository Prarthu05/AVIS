using Avis.Station;
using Xunit;

namespace Avis.Tests.Station;

public class CurrentOperatorStateTests
{
    private sealed class FakeTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    [Fact]
    public void EmployeeIdIsNullBeforeAnyScan()
    {
        var state = new CurrentOperatorState(new FakeTimeProvider());

        Assert.Null(state.EmployeeId);
    }

    [Fact]
    public void SetOperatorMakesEmployeeIdAvailable()
    {
        var state = new CurrentOperatorState(new FakeTimeProvider());

        state.SetOperator("4375789");

        Assert.Equal("4375789", state.EmployeeId);
    }

    [Fact]
    public void SetOperatorTwiceKeepsTheMostRecentScan()
    {
        var state = new CurrentOperatorState(new FakeTimeProvider());

        state.SetOperator("4375789");
        state.SetOperator("4045423");

        Assert.Equal("4045423", state.EmployeeId);
    }

    [Fact]
    public void ConsumeRecentScan_NoScanYet_ReturnsNull()
    {
        var state = new CurrentOperatorState(new FakeTimeProvider());

        Assert.Null(state.ConsumeRecentScan(TimeSpan.FromSeconds(15)));
    }

    [Fact]
    public void ConsumeRecentScan_WithinWindow_ReturnsEmployeeId()
    {
        var time = new FakeTimeProvider();
        var state = new CurrentOperatorState(time);
        state.SetOperator("4375789");

        time.UtcNow += TimeSpan.FromSeconds(5);

        Assert.Equal("4375789", state.ConsumeRecentScan(TimeSpan.FromSeconds(15)));
    }

    [Fact]
    public void ConsumeRecentScan_OutsideWindow_ReturnsNull()
    {
        var time = new FakeTimeProvider();
        var state = new CurrentOperatorState(time);
        state.SetOperator("4375789");

        time.UtcNow += TimeSpan.FromSeconds(20);

        Assert.Null(state.ConsumeRecentScan(TimeSpan.FromSeconds(15)));
    }

    [Fact]
    public void ConsumeRecentScan_ClearsTheScanEvenWhenExpired()
    {
        var time = new FakeTimeProvider();
        var state = new CurrentOperatorState(time);
        state.SetOperator("4375789");
        time.UtcNow += TimeSpan.FromSeconds(20);

        state.ConsumeRecentScan(TimeSpan.FromSeconds(15));

        Assert.Null(state.EmployeeId);
    }

    [Fact]
    public void ConsumeRecentScan_CannotBeReusedForASecondPart()
    {
        var time = new FakeTimeProvider();
        var state = new CurrentOperatorState(time);
        state.SetOperator("4375789");

        var first = state.ConsumeRecentScan(TimeSpan.FromSeconds(15));
        var second = state.ConsumeRecentScan(TimeSpan.FromSeconds(15));

        Assert.Equal("4375789", first);
        Assert.Null(second);
    }

    [Fact]
    public void ConsumeRecentScan_ANewScanAfterConsumingCanBeUsedAgain()
    {
        var time = new FakeTimeProvider();
        var state = new CurrentOperatorState(time);
        state.SetOperator("4375789");
        state.ConsumeRecentScan(TimeSpan.FromSeconds(15));

        state.SetOperator("4045423");

        Assert.Equal("4045423", state.ConsumeRecentScan(TimeSpan.FromSeconds(15)));
    }
}
