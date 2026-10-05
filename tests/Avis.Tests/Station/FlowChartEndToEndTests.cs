using Avis.Configuration;
using Avis.Faults;
using Avis.LightGuide;
using Avis.Station;
using Avis.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Avis.Tests.Station;

/// <summary>
/// The AVIS flow chart end to end, through the real LightGuide change detection
/// (LightGuideMonitor) and the real sequence - only iFactory is faked. Each
/// snapshot is what one LightGuide Web API poll would return.
/// </summary>
public sealed class FlowChartEndToEndTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "avis-e2e-" + Guid.NewGuid().ToString("N"));
    private readonly FakeMesClient _mes = new();
    private readonly FakeTimeProvider _time = new();
    private readonly StationStatus _status = new(new StationState());
    private readonly FaultLog _faults;
    private readonly StationSequencer _sequencer;
    private readonly LightGuideMonitor _monitor;
    private readonly Dictionary<string, string?> _lg = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WI_Name"] = "", ["StepNumberMain"] = "0", ["WI_Running"] = "False", ["WI_Complete"] = "False",
        ["LGSState"] = "File Browser", ["LJ_R"] = "", ["LJ_N"] = "0", ["IV_R"] = "", ["IV_N"] = "0",
    };

    public FlowChartEndToEndTests()
    {
        var options = new LightGuideOptions
        {
            Checks =
            {
                new LightGuideCheckOptions { Name = "LJ", Kind = CheckKind.LJ, ResultVariable = "LJ_R", CounterVariable = "LJ_N" },
                new LightGuideCheckOptions { Name = "IV4", Kind = CheckKind.IV4, ResultVariable = "IV_R", CounterVariable = "IV_N" },
            },
        };
        _monitor = new LightGuideMonitor(options);
        _faults = new FaultLog(new FaultLogOptions { Directory = _dir }, "S", NullLogger<FaultLog>.Instance, _time);
        _mes.Wips["HN-1"] = FakeMesClient.MakeWip(7, "HN-1", "PN-1");
        _sequencer = new StationSequencer(
            _mes, new CurrentOperatorState(_time), new FakeAlerts(), _faults, _status, null,
            new CounterStore(_dir, _time, NullLogger<CounterStore>.Instance),
            new StationOptions(), new IFactoryOptions(), new JabilEyeCameraOptions { ResourceName = "R" },
            new SequenceOptions { MaxFailedAttempts = 5 },
            AviProjectConfig.Parse(new[] { "[RECIPE]", "PN-1 = M1", "[VISUAL_ADD]", "M1:2 = https://va/2" }),
            NullLogger<StationSequencer>.Instance, _time);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    private async Task PollAsync()
    {
        foreach (var e in _monitor.Process(new Dictionary<string, string?>(_lg)))
        {
            await _sequencer.OnLightGuideEventAsync(e, CancellationToken.None);
        }
    }

    private async Task PostAsync(string result, string counter, string value)
    {
        _lg[result] = value;
        _lg[counter] = (int.Parse(_lg[counter]!) + 1).ToString();
        await PollAsync();
    }

    [Fact]
    public async Task HnArrives_Ids_LightGuide_Rework_Pass_Complete_NextStation()
    {
        await PollAsync(); // baseline

        // Operator scans ID + sensor detects HN -> both IDs to iFactory -> WIP started.
        _sequencer.OnBadgeScanned("4375789");
        await _sequencer.OnAssetCapturedAsync("HN-1", CancellationToken.None);
        Assert.Contains(_mes.Calls, c => c.StartsWith("start 7"));

        // iFactory triggers LightGuide, which runs the program and reports steps.
        _lg["WI_Name"] = "M1"; _lg["StepNumberMain"] = "1"; _lg["WI_Running"] = "True"; _lg["LGSState"] = "Program Running";
        await PollAsync();
        _lg["StepNumberMain"] = "2";
        await PollAsync();
        Assert.Equal("https://va/2", _status.Current.VisualAidUrl);

        // LJ check: NG twice (rework, highlighted to the operator), then OK.
        await PostAsync("LJ_R", "LJ_N", "NG");
        Assert.Equal(StationPhase.ReworkRequired, _status.Current.Phase);
        await PostAsync("LJ_R", "LJ_N", "NG");
        await PostAsync("LJ_R", "LJ_N", "OK");
        await PostAsync("IV_R", "IV_N", "OK");
        Assert.Equal(StationPhase.InProcess, _status.Current.Phase);

        // Program ends -> no errors -> station data to iFactory -> next station.
        _lg["WI_Running"] = "False"; _lg["WI_Complete"] = "True"; _lg["LGSState"] = "Program Ended";
        await PollAsync();

        Assert.Contains(_mes.Calls, c => c.StartsWith("complete 7"));
        Assert.Equal(StationPhase.Completed, _status.Current.Phase);
        Assert.Equal(2, _status.Current.ReworkCount);
        Assert.Equal(2, _faults.Recent.Count(f => f.Code == "LJ-01"));
    }

    [Fact]
    public async Task FifthFailedRework_Escalates_AndNothingIsCompleted()
    {
        await PollAsync();
        _sequencer.OnBadgeScanned("4375789");
        await _sequencer.OnAssetCapturedAsync("HN-1", CancellationToken.None);
        _lg["WI_Name"] = "M1"; _lg["StepNumberMain"] = "3"; _lg["WI_Running"] = "True";
        await PollAsync();

        for (var i = 0; i < 5; i++)
        {
            await PostAsync("IV_R", "IV_N", "NG");
        }
        _lg["WI_Running"] = "False"; _lg["WI_Complete"] = "True";
        await PollAsync();

        Assert.Equal(StationPhase.EscalationRequired, _status.Current.Phase);
        Assert.Contains(_faults.Recent, f => f.Code == "ST-01" && f.Severity == FaultSeverity.Critical);
        Assert.DoesNotContain(_mes.Calls, c => c.StartsWith("complete"));
    }
}
