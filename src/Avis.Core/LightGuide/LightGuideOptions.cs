namespace Avis.LightGuide;

public enum CheckKind
{
    /// <summary>Keyence LJ laser profiler check.</summary>
    LJ,

    /// <summary>Keyence IV4 vision check - its image is picked up from the drop folder and staged with the Asset ID.</summary>
    IV4,

    Other,
}

/// <summary>
/// One quality check that LightGuide triggers itself (LJ, IV4, ...). AVIS never
/// talks to the sensor - it only reads the LightGuide variables the check
/// writes its result into.
/// </summary>
public class LightGuideCheckOptions
{
    /// <summary>Display name, also used for the iFactory attribute name, e.g. "LJ" or "IV4".</summary>
    public string Name { get; set; } = "";

    public CheckKind Kind { get; set; } = CheckKind.Other;

    /// <summary>LightGuide variable holding the check result (OK/NG, True/False, ...).</summary>
    public string ResultVariable { get; set; } = "";

    /// <summary>
    /// Optional (strongly recommended) LightGuide variable that LightGuide
    /// increments every time it runs this check. With it, two NG results in a row
    /// are seen as two separate attempts. Without it, a new result is only detected
    /// when ResultVariable changes value, so the LightGuide program must clear
    /// ResultVariable before each re-check or a repeated NG is missed.
    /// </summary>
    public string? CounterVariable { get; set; }

    /// <summary>
    /// Result values (case-insensitive) that count as a pass. Anything else that is
    /// non-blank is a fail. Blank = the defaults: OK, PASS, GOOD, TRUE, T, 1.
    /// </summary>
    public List<string> PassValues { get; set; } = new();

    /// <summary>Extra LightGuide variables (measurements etc.) logged with the result and sent to iFactory.</summary>
    public List<string> MeasurementVariables { get; set; } = new();

    private static readonly string[] DefaultPassValues = { "OK", "PASS", "GOOD", "TRUE", "T", "1" };

    public bool IsPass(string? value)
    {
        var passValues = PassValues.Count > 0 ? (IEnumerable<string>)PassValues : DefaultPassValues;
        var v = value?.Trim() ?? "";
        return passValues.Any(p => p.Trim().Equals(v, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// LightGuide Web API (LightGuide 4.3+ standard "Web API" device, default port
/// 54274 - see https://wiki.lightguidesys.com/Web_API). AVIS polls
/// GET /Variable/{a},{b},... for the built-in status variables and each check's
/// result variables. Variable names default to LightGuide's built-in Standard
/// Variables; add them under System Settings -> Variables -> New -> Standard...
/// </summary>
public class LightGuideOptions
{
    public const string SectionName = "LightGuide";

    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "http://localhost:54274";

    public double RequestTimeoutSeconds { get; set; } = 5;

    public double PollIntervalSeconds { get; set; } = 0.5;

    /// <summary>Consecutive failed polls before LightGuide is reported unreachable (avoids fault spam on a single blip).</summary>
    public int UnreachableAfterFailedPolls { get; set; } = 3;

    /// <summary>
    /// Polls to wait, after WI_Running drops, for WI_Complete / "Program Ended"
    /// before treating the run as aborted - the two variables don't always change
    /// in the same poll.
    /// </summary>
    public int CompletionGracePolls { get; set; } = 4;

    public string ProgramVariable { get; set; } = "WI_Name";
    public string StepVariable { get; set; } = "StepNumberMain";
    public string StepCommentVariable { get; set; } = "StepCommentMain";
    public string RunningVariable { get; set; } = "WI_Running";
    public string CompleteVariable { get; set; } = "WI_Complete";
    public string StateVariable { get; set; } = "LGSState";

    public List<LightGuideCheckOptions> Checks { get; set; } = new();
}
