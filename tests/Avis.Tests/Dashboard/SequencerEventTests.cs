using Avis.Configuration;
using Avis.Dashboard;
using Avis.Faults;
using Avis.IFactory;
using Avis.LightGuide;
using Avis.Station;
using Avis.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Avis.Tests.Dashboard;

/// <summary>What the station sends to the dashboard at each point of the flow chart.</summary>
public sealed class SequencerEventTests : IDisposable
{
    private sealed class RecordingSink : IStationEventSink
    {
        public List<StationEvent> Events { get; } = new();
        public void Emit(StationEvent e) => Events.Add(e);
    }

    private sealed class FakeVisualAids : IVisualAidResolver
    {
        public string? Resolve(string? material, string? model, string? program, int? step) =>
            material == "PN-1001" && step == 3 ? "C:/va-cache/doc/html/step-3.html" : null;

        public string? ProductFor(string? material, string? model, string? program) =>
            material == "PN-1001" ? "CVG300 Housing" : null;
    }

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "avis-seqev-" + Guid.NewGuid().ToString("N"));
    private readonly FakeMesClient _mes = new();
    private readonly FakeTimeProvider _time = new();
    private readonly StationStatus _status = new(new StationState());
    private readonly RecordingSink _sink = new();
    private readonly StationSequencer _sequencer;

    private static readonly LightGuideCheckOptions Lj = new() { Name = "LJ", Kind = CheckKind.LJ, ResultVariable = "LJ" };

    public SequencerEventTests()
    {
        var ini = AviProjectConfig.Parse(new[]
        {
            "[RECIPE]", "PN-1001 = CVG300",
            "[VISUAL_ADD]", "CVG300 = https://va/model", "CVG300:2 = https://va/step2",
        });
        _mes.Wips["ASSET1"] = FakeMesClient.MakeWip(101, "ASSET1");
        _mes.Wips["ASSET2"] = FakeMesClient.MakeWip(102, "ASSET2");

        _sequencer = new StationSequencer(
            _mes, new CurrentOperatorState(_time), new FakeAlerts(),
            new FaultLog(new FaultLogOptions { Directory = Path.Combine(_dir, "faults") }, "S1", NullLogger<FaultLog>.Instance, _time),
            _status, images: null,
            new CounterStore(Path.Combine(_dir, "data"), _time, NullLogger<CounterStore>.Instance),
            new StationOptions { OperatorDomain = "Jabil", OperatorScanWindowSeconds = 15 },
            new IFactoryOptions(),
            new JabilEyeCameraOptions { ResourceName = "RES-1" },
            new SequenceOptions(), ini, NullLogger<StationSequencer>.Instance, _time,
            new FakeVisualAids(), _sink);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    private Task Lg(LightGuideEvent e) => _sequencer.OnLightGuideEventAsync(e, CancellationToken.None);

    private static CheckEvaluated Result(bool pass) =>
        new(Lj, pass, pass ? "OK" : "NG", new Dictionary<string, string?> { ["Height"] = "1.25" });

    private async Task StartPartAsync(string asset = "ASSET1")
    {
        _sequencer.OnBadgeScanned("4375789");
        await _sequencer.OnAssetCapturedAsync(asset, CancellationToken.None);
    }

    [Fact]
    public async Task HappyPath_EmitsTheUnitTimeline_AllWithOneUnitId()
    {
        await StartPartAsync();
        await Lg(new ProgramStarted("CVG300"));
        await Lg(new StepChanged("CVG300", 3, "Fit cover"));
        await Lg(Result(pass: false));
        await Lg(Result(pass: true));
        await Lg(new ProgramCompleted("CVG300"));

        Assert.Equal(
            new[] { "unit_started", "step_changed", "check_result", "check_result", "unit_confirmed" },
            _sink.Events.Select(e => e.Type));

        var unitId = _sink.Events[0].UnitId;
        Assert.False(string.IsNullOrEmpty(unitId));
        Assert.All(_sink.Events, e => Assert.Equal(unitId, e.UnitId));
        Assert.All(_sink.Events, e => Assert.Equal("ASSET1", e.AssetId));
        Assert.All(_sink.Events, e => Assert.Equal("4375789", e.Ntid));

        var started = _sink.Events[0];
        Assert.Equal(101, started.WipId);
        Assert.Equal("PN-1001", started.Material);
        Assert.Equal("CVG300 Housing", started.Product); // product name from the dashboard's VA map

        var step = _sink.Events[1];
        Assert.Equal(3, step.Step);
        Assert.Equal("Fit cover", step.StepComment);

        var ng = _sink.Events[2];
        Assert.Equal("LJ", ng.Check);
        Assert.Equal("NG", ng.Result);
        Assert.Equal(1, ng.Attempt);
        Assert.Equal(1, ng.Failures);
        Assert.Equal("1.25", ng.Measurements!["Height"]);
        Assert.Equal("OK", _sink.Events[3].Result);
        Assert.Equal(2, _sink.Events[3].Attempt);

        Assert.Equal(unitId, _sequencer.UnitIdFor("asset1"));
        Assert.Null(_sequencer.UnitIdFor("OTHER"));
    }

    [Fact]
    public async Task DashboardVa_WinsOverTheIni_IniStillUsedForUnmappedSteps()
    {
        await StartPartAsync();
        await Lg(new StepChanged("CVG300", 3, null));
        Assert.Equal("C:/va-cache/doc/html/step-3.html", _status.Current.VisualAidUrl);

        await Lg(new StepChanged("CVG300", 2, null));
        Assert.Equal("https://va/step2", _status.Current.VisualAidUrl);
    }

    [Fact]
    public async Task FifthFailure_EmitsEscalated_ThenNotConfirmed()
    {
        await StartPartAsync();
        for (var i = 0; i < 5; i++)
        {
            await Lg(Result(pass: false));
        }
        await Lg(new ProgramCompleted("CVG300"));

        var escalated = Assert.Single(_sink.Events, e => e.Type == "escalated");
        Assert.Equal("LJ", escalated.Check);
        Assert.Equal(5, escalated.Failures);
        Assert.Equal("unit_not_confirmed", _sink.Events[^1].Type);
        Assert.DoesNotContain(_sink.Events, e => e.Type == "unit_confirmed");
    }

    [Fact]
    public async Task NextPartBeforeCompletion_AbandonsThePreviousUnit()
    {
        await StartPartAsync("ASSET1");
        var first = _sink.Events[0].UnitId;
        await StartPartAsync("ASSET2");

        var abandoned = Assert.Single(_sink.Events, e => e.Type == "unit_abandoned");
        Assert.Equal(first, abandoned.UnitId);
        var second = _sink.Events.Last(e => e.Type == "unit_started");
        Assert.Equal("ASSET2", second.AssetId);
        Assert.NotEqual(first, second.UnitId);
    }

    [Fact]
    public async Task ASinkThatThrows_NeverStopsTheStation()
    {
        var throwing = new StationSequencer(
            _mes, new CurrentOperatorState(_time), new FakeAlerts(),
            new FaultLog(new FaultLogOptions { Directory = Path.Combine(_dir, "faults2") }, "S1", NullLogger<FaultLog>.Instance, _time),
            _status, images: null,
            new CounterStore(Path.Combine(_dir, "data2"), _time, NullLogger<CounterStore>.Instance),
            new StationOptions { OperatorDomain = "Jabil", OperatorScanWindowSeconds = 15 },
            new IFactoryOptions(), new JabilEyeCameraOptions { ResourceName = "RES-1" },
            new SequenceOptions(), AviProjectConfig.Parse(Array.Empty<string>()), NullLogger<StationSequencer>.Instance, _time,
            NoVisualAids.Instance, new ThrowingSink());

        throwing.OnBadgeScanned("4375789");
        await throwing.OnAssetCapturedAsync("ASSET1", CancellationToken.None);
        await throwing.OnLightGuideEventAsync(Result(pass: true), CancellationToken.None);
        await throwing.OnLightGuideEventAsync(new ProgramCompleted("X"), CancellationToken.None);

        Assert.Equal(StationPhase.Completed, _status.Current.Phase);
    }

    private sealed class ThrowingSink : IStationEventSink
    {
        public void Emit(StationEvent e) => throw new IOException("disk full");
    }
}
