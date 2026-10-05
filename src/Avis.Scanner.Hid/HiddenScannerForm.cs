using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using Microsoft.Extensions.Logging;

namespace Avis.Scanner.Hid;

/// <summary>
/// A Form that is never actually shown - it exists only to own a window handle so
/// RegisterRawInputDevices has a target and WndProc receives WM_INPUT. Raw Input
/// requires a real message pump on an interactive desktop session (see README on
/// why this can't run as a Session-0 Windows Service); it's designed to be hosted
/// via Application.Run(form) on a dedicated STA thread.
/// </summary>
internal class HiddenScannerForm : System.Windows.Forms.Form
{
    private const int WM_INPUT = 0x00FF;

    private readonly string _deviceNameMatch;
    private readonly Action<int, bool> _onKeyEvent;
    private readonly ILogger _logger;
    private readonly HashSet<string> _loggedDevices = new();

    public HiddenScannerForm(string deviceNameMatch, Action<int, bool> onKeyEvent, ILogger logger)
    {
        _deviceNameMatch = deviceNameMatch;
        _onKeyEvent = onKeyEvent;
        _logger = logger;

        ShowInTaskbar = false;
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
        WindowState = System.Windows.Forms.FormWindowState.Minimized;
        Opacity = 0;
        Size = new System.Drawing.Size(1, 1);
        StartPosition = System.Windows.Forms.FormStartPosition.Manual;
        Location = new System.Drawing.Point(-2000, -2000);
    }

    // Standard WinForms trick to guarantee this form is never actually displayed,
    // regardless of what Application.Run/Show try to do with it.
    protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            RawInputDevice.RegisterDevice(HidUsageAndPage.Keyboard, RawInputDeviceFlags.InputSink, Handle);
            _logger.LogInformation("Raw Input keyboard registration succeeded");
            LogConnectedKeyboardsAtStartup();
        }
        catch (Exception ex)
        {
            // If this throws, badge scans silently never arrive with no other
            // symptom - make that loud instead of letting it vanish into
            // whatever WinForms does with an exception raised from a handle-
            // creation callback.
            _logger.LogError(ex, "Failed to register for Raw Input keyboard events - badge scans will not be captured");
        }
    }

    // Raw Input registration only ever tells you about devices that send a key
    // event - if the scanner is off, out of Bluetooth range, or not paired to
    // this PC, WM_INPUT for it simply never arrives and nothing gets logged.
    // That's indistinguishable from a filter/config bug unless something checks
    // proactively, so do that once at startup: enumerate every keyboard Windows
    // currently sees and report whether the configured scanner is among them.
    private void LogConnectedKeyboardsAtStartup()
    {
        RawInputDevice[] keyboards;
        try
        {
            keyboards = RawInputDevice.GetDevices()
                .Where(d => d.DeviceType == RawInputDeviceType.Keyboard)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not enumerate connected keyboard devices at startup");
            return;
        }

        var matching = keyboards.Where(IsMatch).ToArray();
        if (matching.Length == 0)
        {
            var others = keyboards.Length == 0
                ? "(none)"
                : string.Join("; ", keyboards.Select(DescribeDevice));
            _logger.LogWarning(
                "No connected keyboard device currently matches HidDeviceNameMatch '{ConfiguredMatch}' - " +
                "the scanner may be powered off, asleep, out of Bluetooth range, or not paired/connected to " +
                "this PC. {Count} other keyboard device(s) currently seen by Windows: {Devices}",
                _deviceNameMatch, keyboards.Length, others);
        }
        else
        {
            foreach (var device in matching)
            {
                _logger.LogInformation("Scanner detected and connected at startup: {Identity}", DescribeDevice(device));
            }
        }
    }

    private bool IsMatch(RawInputDevice? device) =>
        (device?.ProductName is { } product && product.Contains(_deviceNameMatch, StringComparison.OrdinalIgnoreCase)) ||
        (device?.ManufacturerName is { } manufacturer && manufacturer.Contains(_deviceNameMatch, StringComparison.OrdinalIgnoreCase));

    private static string DescribeDevice(RawInputDevice? device) =>
        device is null
            ? "(no device info)"
            : $"Product='{device.ProductName}' Manufacturer='{device.ManufacturerName}' VID={device.VendorId:X4} PID={device.ProductId:X4} Path={device.DevicePath}";

    protected override void WndProc(ref System.Windows.Forms.Message m)
    {
        if (m.Msg == WM_INPUT)
        {
            try
            {
                HandleRawInput(m.LParam);
            }
            catch (Exception ex)
            {
                // A malformed/unrecognized raw input packet shouldn't take the
                // listener down - skip it and keep listening. Logged at Warning
                // (not Debug) so a real, recurring failure here is actually
                // visible at the default log level instead of silently dropping
                // scans with zero trace - this previously hid whatever caused a
                // confirmed-working scanner to miss a scan with no log output at
                // all.
                _logger.LogWarning(ex, "Failed to process a raw input packet - skipping it");
            }
        }
        base.WndProc(ref m);
    }

    private void HandleRawInput(IntPtr lParam)
    {
        var data = RawInputData.FromHandle(lParam);
        if (data is not RawInputKeyboardData keyboardData)
        {
            return;
        }

        var device = data.Device;
        var matched = IsMatch(device);

        // RIDEV_INPUTSINK registration is system-wide, so every keyboard on the
        // machine (built-in, external, the scanner) fires WM_INPUT here - only a
        // device matching HidDeviceNameMatch should ever reach _onKeyEvent. Log
        // each distinct device's identity once on first sight, so a wrong or
        // empty product name (common for Bluetooth HID devices, whose raw
        // descriptor string often differs from the friendly name Windows shows
        // in Bluetooth settings) is immediately visible instead of every scan
        // silently vanishing with no trace.
        var identity = DescribeDevice(device);
        if (_loggedDevices.Add(identity))
        {
            _logger.Log(matched ? LogLevel.Information : LogLevel.Warning,
                "Keyboard input seen from device: {Identity} - {MatchState} configured HidDeviceNameMatch '{ConfiguredMatch}'",
                identity, matched ? "MATCHES" : "does NOT match", _deviceNameMatch);
        }

        if (!matched)
        {
            return;
        }

        var isKeyUp = keyboardData.Keyboard.Flags.HasFlag(RawKeyboardFlags.Up);
        _onKeyEvent(keyboardData.Keyboard.VirutalKey, isKeyUp);
    }
}
