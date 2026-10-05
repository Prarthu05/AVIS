namespace Avis.Station;

/// <summary>The two screens the station window swaps between (in the same window).</summary>
public enum StationScreen
{
    /// <summary>iFactory + part status + banner - whenever the operator has to act or read something.</summary>
    Station,

    /// <summary>The visual aid for the current LightGuide step - while the operator is just building.</summary>
    VisualAid,
}

/// <summary>
/// Decides when the station window should swap screens:
/// <list type="bullet">
/// <item>Visual aid comes forward when LightGuide is running a step that has a
/// visual aid and nothing needs the operator - on every new step, and again
/// as soon as a rework is cleared.</item>
/// <item>The station screen comes forward whenever the operator is needed:
/// scan badge, rework, escalation, not confirmed, part confirmed / move on, idle.</item>
/// </list>
/// It only reacts to a change (new step, new phase, new visual aid), so an
/// operator who switches screens by hand isn't pulled back on every status update.
/// Returns null when the screen should stay as it is.
/// </summary>
public static class ScreenSelector
{
    public static StationScreen? Decide(StationState? previous, StationState current, bool visualAidAvailable)
    {
        var showVisualAid = visualAidAvailable
            && current.Phase == StationPhase.InProcess
            && current.Step is not null
            && !string.IsNullOrWhiteSpace(current.VisualAidUrl);

        if (showVisualAid)
        {
            var changed = previous is null
                || previous.Phase != current.Phase
                || previous.Step != current.Step
                || previous.Program != current.Program
                || previous.VisualAidUrl != current.VisualAidUrl;
            return changed ? StationScreen.VisualAid : null;
        }

        if (current.Phase != StationPhase.InProcess && (previous is null || previous.Phase != current.Phase))
        {
            return StationScreen.Station;
        }

        return null;
    }
}
