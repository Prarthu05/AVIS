using Avis.Station;
using Xunit;

namespace Avis.Tests.Station;

public class ScreenSelectorTests
{
    private static readonly StationState Building = new()
    {
        Phase = StationPhase.InProcess,
        Program = "CVG300",
        Step = 2,
        VisualAidUrl = "https://va/step2",
    };

    [Fact]
    public void NewStepWithVisualAid_ShowsVisualAid()
    {
        var next = Building with { Step = 3, VisualAidUrl = "https://va/step3" };

        Assert.Equal(StationScreen.VisualAid, ScreenSelector.Decide(Building, next, visualAidAvailable: true));
    }

    [Fact]
    public void NewStepWithTheSameModelWideVisualAid_StillShowsVisualAid()
    {
        Assert.Equal(StationScreen.VisualAid, ScreenSelector.Decide(Building, Building with { Step = 3 }, true));
    }

    [Theory]
    [InlineData(StationPhase.WaitingForBadge)]
    [InlineData(StationPhase.StartingWip)]
    [InlineData(StationPhase.ReworkRequired)]
    [InlineData(StationPhase.EscalationRequired)]
    [InlineData(StationPhase.Completing)]
    [InlineData(StationPhase.Completed)]
    [InlineData(StationPhase.NotConfirmed)]
    [InlineData(StationPhase.Idle)]
    public void OperatorNeeded_ShowsStationScreen(StationPhase phase)
    {
        Assert.Equal(StationScreen.Station, ScreenSelector.Decide(Building, Building with { Phase = phase }, true));
    }

    [Fact]
    public void ReworkCleared_GoesBackToTheVisualAid_OnTheSameStep()
    {
        var rework = Building with { Phase = StationPhase.ReworkRequired };

        Assert.Equal(StationScreen.Station, ScreenSelector.Decide(Building, rework, true));
        Assert.Equal(StationScreen.VisualAid, ScreenSelector.Decide(rework, Building, true));
    }

    [Fact]
    public void NothingChanged_LeavesTheScreenAlone_SoAManualSwitchSticks()
    {
        Assert.Null(ScreenSelector.Decide(Building, Building with { Message = "LJ OK" }, true));

        var rework = Building with { Phase = StationPhase.ReworkRequired };
        Assert.Null(ScreenSelector.Decide(rework, rework with { Message = "still NG" }, true));
    }

    [Fact]
    public void WipStartedButNoLightGuideStepYet_StaysOnTheStationScreen()
    {
        var starting = new StationState { Phase = StationPhase.StartingWip };
        var started = new StationState { Phase = StationPhase.InProcess, VisualAidUrl = "https://va/model" };

        Assert.Null(ScreenSelector.Decide(starting, started, true));
    }

    [Fact]
    public void NoVisualAid_OrBrowserNotReady_NeverShowsAnEmptyVisualAidScreen()
    {
        var noVa = Building with { Step = 3, VisualAidUrl = null };

        Assert.Null(ScreenSelector.Decide(Building, noVa, true));
        Assert.Null(ScreenSelector.Decide(Building, Building with { Step = 3 }, visualAidAvailable: false));
    }

    [Fact]
    public void FirstDecision_WithNoPreviousState()
    {
        Assert.Equal(StationScreen.Station, ScreenSelector.Decide(null, new StationState(), true));
        Assert.Equal(StationScreen.VisualAid, ScreenSelector.Decide(null, Building, true));
    }
}
