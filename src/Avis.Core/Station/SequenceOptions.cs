namespace Avis.Station;

public class SequenceOptions
{
    public const string SectionName = "Sequence";

    /// <summary>Failed attempts of one check on one part before it is escalated ("5th failure").</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>Send each check's last result (and measurements) to iFactory as WIP attributes before completing.</summary>
    public bool SendCheckResultsToIFactory { get; set; } = true;

    /// <summary>iFactory wipAttributeType used for the check-result attributes.</summary>
    public string WipAttributeType { get; set; } = "String";

    /// <summary>Prefix for the attribute names, e.g. "AVIS_" -> AVIS_LJ, AVIS_LJ_Height.</summary>
    public string WipAttributePrefix { get; set; } = "AVIS_";

    /// <summary>
    /// Checks (by name) that must have passed before a part is confirmed. Blank =
    /// only the checks LightGuide actually ran on this part need to be passing.
    /// </summary>
    public List<string> RequiredChecks { get; set; } = new();
}
