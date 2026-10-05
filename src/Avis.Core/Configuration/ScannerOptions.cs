namespace Avis.Configuration;

public class ScannerOptions
{
    public const string SectionName = "Scanner";

    /// <summary>"Serial" (RS232/USB-COM) or "Hid" (USB/Bluetooth keyboard-wedge).</summary>
    public string Mode { get; set; } = "Serial";

    // Serial mode settings
    public string Port { get; set; } = "COM3";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";

    /// <summary>"CR", "CRLF", or "LF" - must match the scanner's configured suffix.</summary>
    public string LineTerminator { get; set; } = "CR";

    // HID mode settings
    public string HidDeviceNameMatch { get; set; } = "SCAN";

    // Shared
    public double ReconnectDelaySeconds { get; set; } = 3.0;
    public double ReadTimeoutSeconds { get; set; } = 1.0;

    /// <summary>Optional regex; group 1 (or the whole match) becomes the employee ID.</summary>
    public string? EmployeeIdRegex { get; set; }
}
