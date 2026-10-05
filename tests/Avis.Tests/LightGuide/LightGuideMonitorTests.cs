using Avis.LightGuide;
using Xunit;

namespace Avis.Tests.LightGuide;

public class LightGuideMonitorTests
{
    private static LightGuideOptions Options(bool withCounter = true) => new()
    {
        CompletionGracePolls = 2,
        Checks =
        {
            new LightGuideCheckOptions
            {
                Name = "LJ",
                Kind = CheckKind.LJ,
                ResultVariable = "LJ_Result",
                CounterVariable = withCounter ? "LJ_Count" : null,
                MeasurementVariables = { "LJ_Height" },
            },
        },
    };

    private static Dictionary<string, string?> Snapshot(
        string program = "", int? step = null, bool running = false, bool complete = false,
        string? state = null, string? lj = null, string? ljCount = null, string? height = null) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["WI_Name"] = program,
        ["StepNumberMain"] = step?.ToString(),
        ["StepCommentMain"] = step is null ? null : $"Step {step}",
        ["WI_Running"] = running ? "True" : "False",
        ["WI_Complete"] = complete ? "True" : "False",
        ["LGSState"] = state,
        ["LJ_Result"] = lj,
        ["LJ_Count"] = ljCount,
        ["LJ_Height"] = height,
    };

    [Fact]
    public void VariableNames_IncludeStatusAndCheckVariables()
    {
        var monitor = new LightGuideMonitor(Options());

        Assert.Contains("WI_Name", monitor.VariableNames);
        Assert.Contains("StepNumberMain", monitor.VariableNames);
        Assert.Contains("LJ_Result", monitor.VariableNames);
        Assert.Contains("LJ_Count", monitor.VariableNames);
        Assert.Contains("LJ_Height", monitor.VariableNames);
    }

    [Fact]
    public void FirstSnapshot_IsABaseline_OldResultsAreNotReplayed()
    {
        var monitor = new LightGuideMonitor(Options());

        var events = monitor.Process(Snapshot(lj: "NG", ljCount: "7", complete: true));

        Assert.Empty(events);
    }

    [Fact]
    public void ProgramStart_AndStepChanges_AreReported()
    {
        var monitor = new LightGuideMonitor(Options());
        monitor.Process(Snapshot());

        var started = monitor.Process(Snapshot("CVG300", 1, running: true));
        var sameStep = monitor.Process(Snapshot("CVG300", 1, running: true));
        var nextStep = monitor.Process(Snapshot("CVG300", 2, running: true));

        Assert.Collection(started,
            e => Assert.Equal(new ProgramStarted("CVG300"), e),
            e => Assert.Equal(new StepChanged("CVG300", 1, "Step 1"), e));
        Assert.Empty(sameStep);
        Assert.Equal(new StepChanged("CVG300", 2, "Step 2"), Assert.Single(nextStep));
    }

    [Fact]
    public void WithCounter_RepeatedNgResults_AreEachReported()
    {
        var monitor = new LightGuideMonitor(Options());
        monitor.Process(Snapshot("P", 1, running: true, lj: "OK", ljCount: "10"));

        var first = monitor.Process(Snapshot("P", 1, running: true, lj: "NG", ljCount: "11", height: "1.2"));
        var second = monitor.Process(Snapshot("P", 1, running: true, lj: "NG", ljCount: "12", height: "1.3"));
        var unchanged = monitor.Process(Snapshot("P", 1, running: true, lj: "NG", ljCount: "12", height: "1.3"));
        var pass = monitor.Process(Snapshot("P", 1, running: true, lj: "OK", ljCount: "13"));

        var e1 = Assert.IsType<CheckEvaluated>(Assert.Single(first));
        Assert.False(e1.Passed);
        Assert.Equal("1.2", e1.Measurements["LJ_Height"]);
        Assert.False(Assert.IsType<CheckEvaluated>(Assert.Single(second)).Passed);
        Assert.Empty(unchanged);
        Assert.True(Assert.IsType<CheckEvaluated>(Assert.Single(pass)).Passed);
    }

    [Fact]
    public void WithoutCounter_ResultIsReportedWhenValueChanges_AndAfterBeingCleared()
    {
        var monitor = new LightGuideMonitor(Options(withCounter: false));
        monitor.Process(Snapshot("P", 1, running: true, lj: ""));

        var ng = monitor.Process(Snapshot("P", 1, running: true, lj: "NG"));
        var same = monitor.Process(Snapshot("P", 1, running: true, lj: "NG"));
        var cleared = monitor.Process(Snapshot("P", 1, running: true, lj: ""));
        var ngAgain = monitor.Process(Snapshot("P", 1, running: true, lj: "NG"));

        Assert.Single(ng);
        Assert.Empty(same);
        Assert.Empty(cleared);
        Assert.Single(ngAgain);
    }

    [Fact]
    public void CheckResult_IsReportedBeforeCompletion_InTheSamePoll()
    {
        var monitor = new LightGuideMonitor(Options());
        monitor.Process(Snapshot("P", 5, running: true, ljCount: "1", lj: "OK"));

        var events = monitor.Process(Snapshot("P", 5, running: false, complete: true, lj: "OK", ljCount: "2"));

        Assert.Collection(events,
            e => Assert.IsType<CheckEvaluated>(e),
            e => Assert.Equal(new ProgramCompleted("P"), e));
    }

    [Fact]
    public void Completion_ViaLgsState_IsReportedOnce()
    {
        var monitor = new LightGuideMonitor(Options());
        monitor.Process(Snapshot("P", 5, running: true, state: "Program Running"));

        var ended = monitor.Process(Snapshot("", null, running: false, state: "Program Ended"));
        var stillEnded = monitor.Process(Snapshot("", null, running: false, state: "Program Ended", complete: true));

        Assert.Equal(new ProgramCompleted("P"), Assert.Single(ended));
        Assert.Empty(stillEnded);
    }

    [Fact]
    public void CompletionArrivingAPollLate_IsStillACompletion_NotAnAbort()
    {
        var monitor = new LightGuideMonitor(Options());
        monitor.Process(Snapshot("P", 5, running: true));

        var stopped = monitor.Process(Snapshot("P", 5, running: false));
        var completed = monitor.Process(Snapshot("P", 5, running: false, complete: true));

        Assert.Empty(stopped);
        Assert.Equal(new ProgramCompleted("P"), Assert.Single(completed));
    }

    [Fact]
    public void StoppingWithoutCompletion_IsAnAbort_AfterTheGracePolls()
    {
        var monitor = new LightGuideMonitor(Options());
        monitor.Process(Snapshot("P", 3, running: true));

        var e1 = monitor.Process(Snapshot("", null));
        var e2 = monitor.Process(Snapshot("", null));
        var e3 = monitor.Process(Snapshot("", null));
        var e4 = monitor.Process(Snapshot("", null));

        Assert.Empty(e1);
        Assert.Empty(e2);
        Assert.Equal(new ProgramAborted("P"), Assert.Single(e3));
        Assert.Empty(e4);
    }

    [Fact]
    public void NewRun_AfterCompletion_CanCompleteAgain()
    {
        var monitor = new LightGuideMonitor(Options());
        monitor.Process(Snapshot("P", 1, running: true));
        monitor.Process(Snapshot("P", 9, running: false, complete: true));

        var restarted = monitor.Process(Snapshot("P", 1, running: true, complete: false));
        var completedAgain = monitor.Process(Snapshot("P", 9, running: false, complete: true));

        Assert.Contains(new ProgramStarted("P"), restarted);
        Assert.Equal(new ProgramCompleted("P"), Assert.Single(completedAgain));
    }

    [Theory]
    [InlineData("OK", true)]
    [InlineData("ok", true)]
    [InlineData("1", true)]
    [InlineData("NG", false)]
    [InlineData("NOK", false)]
    [InlineData("0", false)]
    public void DefaultPassValues(string value, bool pass)
    {
        Assert.Equal(pass, new LightGuideCheckOptions().IsPass(value));
    }

    [Fact]
    public void ConfiguredPassValues_ReplaceTheDefaults()
    {
        var check = new LightGuideCheckOptions { PassValues = { "GOOD" } };

        Assert.True(check.IsPass("good"));
        Assert.False(check.IsPass("OK"));
    }
}
