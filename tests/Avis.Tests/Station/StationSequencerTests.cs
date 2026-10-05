using Avis.Configuration;
using Avis.Faults;
using Avis.IFactory;
using Avis.LightGuide;
using Avis.Station;
using Avis.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Avis.Tests.Station;

public sealed class StationSequencerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "avis-seq-" + Guid.NewGuid().ToString("N"));
    private readonly FakeMesClient _mes = new();
    private readonly FakeAlerts _alerts = new();
    private readonly FakeTimeProvider _time = new();
    private readonly CurrentOperatorState _operator;
    private readonly FaultLog _faults;
    private readonly StationStatus _status = new(new StationState());
    private readonly CounterStore _counters;
    private readonly SequenceOptions _sequence = new();
    private readonly StationSequencer _sequencer;

    private static readonly LightGuideCheckOptions Lj = new() { Name = "LJ", Kind = CheckKind.LJ, ResultVariable = "LJ" };
    private static readonly LightGuideCheckOptions Iv4 = new() { Name = "IV4", Kind = CheckKind.IV4, ResultVariable = "IV4" };

    public StationSequencerTests()
    {
        _operator = new CurrentOperatorState(_time);
        _faults = new FaultLog(new FaultLogOptions { Directory = Path.Combine(_dir, "faults") }, "S1", NullLogger<FaultLog>.Instance, _time);
        _counters = new CounterStore(Path.Combine(_dir, "data"), _time, NullLogger<CounterStore>.Instance);
        var ini = AviProjectConfig.Parse(new[]
        {
            "[RECIPE]", "PN-1001 = CVG300",
            "[VISUAL_ADD]", "CVG300 = https://va/model", "CVG300:2 = https://va/step2",
        });
        _mes.Wips["ASSET1"] = FakeMesClient.MakeWip(101, "ASSET1");
        _mes.Wips["ASSET2"] = FakeMesClient.MakeWip(102, "ASSET2");

        _sequencer = new StationSequencer(
            _mes, _operator, _alerts, _faults, _status, images: null, _counters,
            new StationOptions { OperatorDomain = "Jabil", OperatorScanWindowSeconds = 15 },
            new IFactoryOptions(),
            new JabilEyeCameraOptions { ResourceName = "RES-1" },
            _sequence, ini, NullLogger<StationSequencer>.Instance, _time);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    private Task Lg(LightGuideEvent e) => _sequencer.OnLightGuideEventAsync(e, CancellationToken.None);

    private static CheckEvaluated Result(LightGuideCheckOptions check, bool pass, string? value = null) =>
        new(check, pass, value ?? (pass ? "OK" : "NG"), new Dictionary<string, string?> { ["Height"] = "1.25" });

    private async Task StartPartAsync(string asset = "ASSET1", string badge = "4375789")
    {
        _sequencer.OnBadgeScanned(badge);
        await _sequencer.OnAssetCapturedAsync(asset, CancellationToken.None);
    }

    private IEnumerable<string> FaultCodes => _faults.Recent.Select(f => f.Code);

    [Fact]
    public async Task HappyPath_BadgeThenAsset_StartsWip_ChecksPass_CompletesWip()
    {
        await StartPartAsync();

        Assert.Contains("start 101 RES-1 Jabil\\4375789", _mes.Calls);
        Assert.Equal(StationPhase.InProcess, _status.Current.Phase);
        Assert.Equal("CVG300", _status.Current.Model);

        await Lg(new ProgramStarted("CVG300"));
        await Lg(new StepChanged("CVG300", 2, "Check housing"));
        Assert.Equal("https://va/step2", _status.Current.VisualAidUrl);

        await Lg(Result(Lj, pass: true));
        await Lg(Result(Iv4, pass: true));
        await Lg(new ProgramCompleted("CVG300"));

        Assert.Contains("complete 101 900 Jabil\\4375789", _mes.Calls);
        Assert.Equal(StationPhase.Completed, _status.Current.Phase);
        Assert.Equal(1, _status.Current.PassCount);
        Assert.Contains(_mes.Attributes, a => a.Name == "AVIS_LJ" && a.Value == "OK");
        Assert.Contains(_mes.Attributes, a => a.Name == "AVIS_LJ_Height" && a.Value == "1.25");
        Assert.Empty(_faults.Recent); // badge scanned before the part: nothing to report
    }

    [Fact]
    public async Task AssetBeforeBadge_WaitsForTheBadge_ThenStarts()
    {
        var capture = _sequencer.OnAssetCapturedAsync("ASSET1", CancellationToken.None);
        await Task.Delay(50);

        Assert.False(capture.IsCompleted);
        Assert.Equal(StationPhase.WaitingForBadge, _status.Current.Phase);
        Assert.Equal(FaultSeverity.Info, Assert.Single(_faults.Recent).Severity);
        Assert.DoesNotContain(_mes.Calls, c => c.StartsWith("start"));

        _sequencer.OnBadgeScanned("4045423");
        await capture.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Contains("start 101 RES-1 Jabil\\4045423", _mes.Calls);
    }

    [Fact]
    public async Task StaleBadge_IsNotUsed_ForANewPart()
    {
        _sequencer.OnBadgeScanned("4375789");
        _time.Advance(TimeSpan.FromSeconds(30));

        var capture = _sequencer.OnAssetCapturedAsync("ASSET1", CancellationToken.None);
        await Task.Delay(50);

        Assert.False(capture.IsCompleted);
        _sequencer.OnBadgeScanned("1111111");
        await capture.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("start 101 RES-1 Jabil\\1111111", _mes.Calls);
    }

    [Fact]
    public async Task NgResult_HighlightsRework_ThenPassAllowsCompletion()
    {
        await StartPartAsync();

        await Lg(Result(Lj, pass: false));
        Assert.Equal(StationPhase.ReworkRequired, _status.Current.Phase);
        Assert.True(_status.Current.MessageIsError);
        Assert.Contains("LJ-01", FaultCodes);
        Assert.Equal(1, _status.Current.ReworkCount);

        await Lg(Result(Lj, pass: true));
        Assert.Equal(StationPhase.InProcess, _status.Current.Phase);

        await Lg(new ProgramCompleted("CVG300"));
        Assert.Equal(StationPhase.Completed, _status.Current.Phase);
        Assert.Contains(_mes.Attributes, a => a.Name == "AVIS_LJ_Attempts" && a.Value == "2");
    }

    [Fact]
    public async Task FifthFailure_Escalates_AndThePartIsNotCompleted()
    {
        await StartPartAsync();

        for (var i = 0; i < 4; i++)
        {
            await Lg(Result(Iv4, pass: false));
            Assert.Equal(StationPhase.ReworkRequired, _status.Current.Phase);
        }
        await Lg(Result(Iv4, pass: false));

        Assert.Equal(StationPhase.EscalationRequired, _status.Current.Phase);
        Assert.Contains("ST-01", FaultCodes);
        Assert.Equal(FaultSeverity.Critical, _faults.Recent.First(f => f.Code == "ST-01").Severity);
        Assert.Equal(5, _faults.Recent.Count(f => f.Code == "IV-01"));

        // Even if the operator gets a pass afterwards, an escalated part is never auto-completed.
        await Lg(Result(Iv4, pass: true));
        await Lg(new ProgramCompleted("CVG300"));

        Assert.DoesNotContain(_mes.Calls, c => c.StartsWith("complete"));
        Assert.Contains("ST-03", FaultCodes);
        Assert.Equal(1, _status.Current.FailCount);
        Assert.Equal(0, _status.Current.PassCount);
    }

    [Fact]
    public async Task CompletionWithACheckStillNg_IsNotConfirmed()
    {
        await StartPartAsync();
        await Lg(Result(Lj, pass: false));

        await Lg(new ProgramCompleted("CVG300"));

        Assert.DoesNotContain(_mes.Calls, c => c.StartsWith("complete"));
        Assert.Equal(StationPhase.NotConfirmed, _status.Current.Phase);
        Assert.Contains("ST-03", FaultCodes);
    }

    [Fact]
    public async Task RequiredCheckNeverReported_IsNotConfirmed()
    {
        _sequence.RequiredChecks.Add("IV4");
        await StartPartAsync();
        await Lg(Result(Lj, pass: true));

        await Lg(new ProgramCompleted("CVG300"));

        Assert.DoesNotContain(_mes.Calls, c => c.StartsWith("complete"));
        Assert.Contains("ST-03", FaultCodes);
    }

    [Fact]
    public async Task UnknownAsset_RecordsWipNotFound_AndStartsNothing()
    {
        await StartPartAsync("NOPE");

        Assert.Contains("IF-02", FaultCodes);
        Assert.DoesNotContain(_mes.Calls, c => c.StartsWith("start"));
        Assert.Null(_sequencer.Session);
    }

    [Fact]
    public async Task CompletedWip_IsSkipped()
    {
        _mes.Wips["DONE"] = FakeMesClient.MakeWip(5, "DONE", status: "Completed");

        await StartPartAsync("DONE");

        Assert.DoesNotContain(_mes.Calls, c => c.StartsWith("start"));
    }

    [Fact]
    public async Task StartWipFailure_ShowsAlert_ThenRetriesAfterReset()
    {
        _mes.StartWipFailures.Enqueue(new IFactoryApiException("route error", 400));

        await StartPartAsync();

        var alert = Assert.Single(_alerts.Shown);
        Assert.Equal(FaultCode.StartWipRejected, alert.Code);
        Assert.Equal(2, _mes.Calls.Count(c => c.StartsWith("start")));
        Assert.NotNull(_sequencer.Session);
    }

    [Fact]
    public async Task LookupTimeout_IsTreatedAsUnreachable_NotAsShutdown()
    {
        _mes.LookupFailures.Enqueue(new TaskCanceledException("timeout"));

        await StartPartAsync();

        Assert.Equal(FaultCode.IFactoryUnreachable, Assert.Single(_alerts.Shown).Code);
        Assert.NotNull(_sequencer.Session);
    }

    [Fact]
    public async Task NewPartBeforeCompletion_AbandonsThePreviousPart()
    {
        await StartPartAsync("ASSET1");

        await StartPartAsync("ASSET2", "2222222");

        Assert.Contains("ST-02", FaultCodes);
        Assert.Equal("ASSET2", _sequencer.Session!.AssetId);
        Assert.Equal(1, _status.Current.FailCount);
    }

    [Fact]
    public async Task ResultWithoutActivePart_IsRecorded()
    {
        await Lg(Result(Lj, pass: true));

        Assert.Contains("LG-02", FaultCodes);
    }

    [Fact]
    public async Task ProgramAbort_IsRecorded_AndPartStaysOpenForARestart()
    {
        await StartPartAsync();

        await Lg(new ProgramAborted("CVG300"));
        Assert.Contains("LG-03", FaultCodes);
        Assert.Equal(StationPhase.NotConfirmed, _status.Current.Phase);

        await Lg(Result(Lj, pass: true));
        await Lg(new ProgramCompleted("CVG300"));
        Assert.Equal(StationPhase.Completed, _status.Current.Phase);
    }

    [Fact]
    public async Task AttributeFailure_IsRecorded_ButDoesNotBlockCompletion()
    {
        var mes = new ThrowingAttributeMes();
        mes.Wips["ASSET1"] = FakeMesClient.MakeWip(101, "ASSET1");
        var sequencer = new StationSequencer(
            mes, _operator, _alerts, _faults, _status, null, _counters,
            new StationOptions(), new IFactoryOptions(), new JabilEyeCameraOptions { ResourceName = "R" },
            _sequence, new AviProjectConfig(), NullLogger<StationSequencer>.Instance, _time);

        sequencer.OnBadgeScanned("1");
        await sequencer.OnAssetCapturedAsync("ASSET1", CancellationToken.None);
        await sequencer.OnLightGuideEventAsync(Result(Lj, pass: true), CancellationToken.None);
        await sequencer.OnLightGuideEventAsync(new ProgramCompleted("P"), CancellationToken.None);

        Assert.Contains("IF-05", FaultCodes);
        Assert.Contains(mes.Calls, c => c.StartsWith("complete"));
    }

    private sealed class ThrowingAttributeMes : FakeMesClient, IMesClient
    {
        Task IMesClient.AddWipAttributeAsync(long wipId, string name, string attributeType, string value, string? operatorId, CancellationToken ct) =>
            throw new IFactoryApiException("attribute not defined", 400);
    }
}
