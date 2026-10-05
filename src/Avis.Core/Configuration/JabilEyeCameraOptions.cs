namespace Avis.Configuration;

/// <summary>One physical JabilEye camera/station this middleware instance talks to.</summary>
public class JabilEyeCameraOptions
{
    public const string SectionName = "JabilEyeCamera";

    /// <summary>JabilEye hostname or IP address (e.g. "192.168.1.50").</summary>
    public string HostName { get; set; } = "";

    /// <summary>The program name configured in this JabilEye's UI (case sensitive).</summary>
    public string ProgramName { get; set; } = "";

    /// <summary>The iFactory resource name a capture from this camera should Start WIP against.</summary>
    public string ResourceName { get; set; } = "";

    public double RequestTimeoutSeconds { get; set; } = 10;

    /// <summary>How often to poll GetResults for a new capture, since the camera is hardware-triggered.</summary>
    public double PollIntervalSeconds { get; set; } = 1.0;

    /// <summary>Vision tool code whose task_output holds the Asset ID - 1005 = CODE_READING_V1 (barcode).</summary>
    public int AssetIdToolCode { get; set; } = 1005;
}
