namespace Avis.Configuration;

public class StationOptions
{
    public const string SectionName = "Station";

    public string Name { get; set; } = "Station-01";

    // Used to build the x-operator-override header value: "<Domain>\<employeeId>"
    public string OperatorDomain { get; set; } = "Jabil";

    /// <summary>
    /// How long a badge scan stays valid for attribution to the next JabilEye
    /// capture. Each scan is meant to cover exactly one part: if the camera
    /// captures nothing within this window the scan expires, and a capture that
    /// arrives with no scan this recent has Start WIP skipped rather than being
    /// attributed to a stale or missing operator.
    /// </summary>
    public double OperatorScanWindowSeconds { get; set; } = 15;
}
