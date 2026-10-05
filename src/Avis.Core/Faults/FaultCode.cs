namespace Avis.Faults;

/// <summary>
/// Every fault AVIS can raise, grouped by the integration point it came from.
/// Each one gets a short display code (e.g. "IF-03") for radio/verbal reference
/// on the floor and in the dashboard fault feed - adjust the codes together with
/// the floor team, never reuse a retired code for a different fault.
/// </summary>
public enum FaultCode
{
    // CFG - station configuration
    ConfigIniUnavailable,

    // SCN - badge scanner
    NoRecentBadgeScan,

    // JE - JabilEye camera
    CameraUnreachable,
    CaptureNotPass,
    AssetIdUnreadable,

    // IF - iFactory MES
    IFactoryUnreachable,
    WipNotFound,
    StartWipRejected,
    CompleteWipRejected,
    WipAttributeRejected,

    // LG - LightGuide
    LightGuideUnreachable,
    LightGuideResultWithoutPart,
    LightGuideProgramAborted,

    // LJ / IV / CHK - quality checks reported through LightGuide variables
    LjCheckFailed,
    Iv4CheckFailed,
    CheckFailed,

    // IMG - IV4 image staging on the station PC
    ImageNotFound,
    ImageStagingFailed,

    // ST - station sequence
    ReworkLimitReached,
    PartAbandoned,
    PartNotConfirmed,
}

public enum FaultSeverity
{
    Info,
    Warning,
    Error,
    Critical,
}

public static class FaultCodeExtensions
{
    public static string ToDisplayCode(this FaultCode code) => code switch
    {
        FaultCode.ConfigIniUnavailable => "CFG-01",
        FaultCode.NoRecentBadgeScan => "SCN-01",
        FaultCode.CameraUnreachable => "JE-01",
        FaultCode.CaptureNotPass => "JE-02",
        FaultCode.AssetIdUnreadable => "JE-03",
        FaultCode.IFactoryUnreachable => "IF-01",
        FaultCode.WipNotFound => "IF-02",
        FaultCode.StartWipRejected => "IF-03",
        FaultCode.CompleteWipRejected => "IF-04",
        FaultCode.WipAttributeRejected => "IF-05",
        FaultCode.LightGuideUnreachable => "LG-01",
        FaultCode.LightGuideResultWithoutPart => "LG-02",
        FaultCode.LightGuideProgramAborted => "LG-03",
        FaultCode.LjCheckFailed => "LJ-01",
        FaultCode.Iv4CheckFailed => "IV-01",
        FaultCode.CheckFailed => "CHK-01",
        FaultCode.ImageNotFound => "IMG-01",
        FaultCode.ImageStagingFailed => "IMG-02",
        FaultCode.ReworkLimitReached => "ST-01",
        FaultCode.PartAbandoned => "ST-02",
        FaultCode.PartNotConfirmed => "ST-03",
        _ => "UNK-00",
    };

    public static string ToTitle(this FaultCode code) => code switch
    {
        FaultCode.ConfigIniUnavailable => "Station Config Unavailable",
        FaultCode.NoRecentBadgeScan => "No Badge Scan",
        FaultCode.CameraUnreachable => "Camera Unreachable",
        FaultCode.CaptureNotPass => "Capture Not Pass",
        FaultCode.AssetIdUnreadable => "Asset ID Unreadable",
        FaultCode.IFactoryUnreachable => "iFactory Unreachable",
        FaultCode.WipNotFound => "WIP Not Found",
        FaultCode.StartWipRejected => "Start WIP Rejected",
        FaultCode.CompleteWipRejected => "Complete WIP Rejected",
        FaultCode.WipAttributeRejected => "WIP Attribute Rejected",
        FaultCode.LightGuideUnreachable => "LightGuide Unreachable",
        FaultCode.LightGuideResultWithoutPart => "Result Without Active Part",
        FaultCode.LightGuideProgramAborted => "LightGuide Program Aborted",
        FaultCode.LjCheckFailed => "LJ Check Failed",
        FaultCode.Iv4CheckFailed => "IV4 Check Failed",
        FaultCode.CheckFailed => "Check Failed",
        FaultCode.ImageNotFound => "IV4 Image Not Found",
        FaultCode.ImageStagingFailed => "Image Staging Failed",
        FaultCode.ReworkLimitReached => "Rework Limit Reached - Escalate",
        FaultCode.PartAbandoned => "Part Abandoned",
        FaultCode.PartNotConfirmed => "Part Not Confirmed",
        _ => "Unknown Fault",
    };

    /// <summary>The integration point a fault belongs to - the dashboard groups/filter by this.</summary>
    public static string ToIntegrationPoint(this FaultCode code) => code.ToDisplayCode() switch
    {
        var c when c.StartsWith("CFG") => "Config",
        var c when c.StartsWith("SCN") => "Badge Scanner",
        var c when c.StartsWith("JE") => "JabilEye",
        var c when c.StartsWith("IF") => "iFactory",
        var c when c.StartsWith("LG") => "LightGuide",
        var c when c.StartsWith("LJ") => "LJ",
        var c when c.StartsWith("IV") => "IV4",
        var c when c.StartsWith("CHK") => "Check",
        var c when c.StartsWith("IMG") => "Image Staging",
        var c when c.StartsWith("ST") => "Station",
        _ => "Unknown",
    };
}
