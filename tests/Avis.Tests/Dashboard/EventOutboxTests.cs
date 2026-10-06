using Avis.Dashboard;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Avis.Tests.Dashboard;

public sealed class EventOutboxTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "avis-outbox-" + Guid.NewGuid().ToString("N"));
    private string PathName => Path.Combine(_dir, "outbox.jsonl");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    private EventOutbox New(int max = 1000) => new(PathName, max, NullLogger<EventOutbox>.Instance);

    [Fact]
    public void Events_SurviveARestart()
    {
        var outbox = New();
        outbox.Emit(new StationEvent { Type = StationEvent.Types.UnitStarted, AssetId = "A1", UnitId = "u1" });
        outbox.Emit(new StationEvent { Type = StationEvent.Types.UnitConfirmed, AssetId = "A1", UnitId = "u1" });

        var reloaded = New();

        Assert.Equal(2, reloaded.Count);
        var first = reloaded.Peek(10)[0];
        Assert.Equal("unit_started", first.Type);
        Assert.Equal("A1", first.AssetId);
    }

    [Fact]
    public void Acknowledge_RemovesOnlyWhatWasSent()
    {
        var outbox = New();
        outbox.Emit(new StationEvent { Type = "a" });
        outbox.Emit(new StationEvent { Type = "b" });
        var batch = outbox.Peek(1);

        outbox.Emit(new StationEvent { Type = "c" }); // arrives while the batch is in flight
        outbox.Acknowledge(batch);

        Assert.Equal(new[] { "b", "c" }, outbox.Peek(10).Select(e => e.Type));
        Assert.Equal(new[] { "b", "c" }, New().Peek(10).Select(e => e.Type));
    }

    [Fact]
    public void BeyondTheLimit_OldestAreDropped()
    {
        var outbox = New(max: 100);
        for (var i = 0; i < 105; i++)
        {
            outbox.Emit(new StationEvent { Type = "t", Value = i.ToString() });
        }

        Assert.Equal(100, outbox.Count);
        Assert.Equal("5", outbox.Peek(1)[0].Value);
        Assert.Equal(100, New(max: 100).Count);
    }

    [Fact]
    public void TornLastLine_IsSkipped()
    {
        New().Emit(new StationEvent { Type = "ok" });
        File.AppendAllText(PathName, "{\"type\":\"half");

        Assert.Equal(1, New().Count);
    }

    [Fact]
    public void NullFields_AreNotWritten()
    {
        New().Emit(new StationEvent { Type = "x" });
        var line = File.ReadAllLines(PathName).Single();
        Assert.DoesNotContain("assetId", line);
        Assert.Contains("\"type\":\"x\"", line);
    }
}
