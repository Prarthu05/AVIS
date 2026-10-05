using System.Threading.Channels;
using Avis.Configuration;
using Avis.Scanner.Windows;
using Microsoft.Extensions.Logging;

namespace Avis.Scanner.Hid;

/// <summary>
/// Reads badge scans from a barcode scanner acting as a USB/Bluetooth HID keyboard,
/// via the Win32 Raw Input API (through the RawInput.Sharp library) so scans are
/// isolated to this one device and never leak into whatever window has focus.
///
/// Requires an interactive desktop session - this cannot run inside a Session-0
/// Windows Service (Raw Input has no access to physical input there). It's meant
/// to run in the operator's own logon session, started via Task Scheduler "at log
/// on" - see README.
///
/// HIGH RISK / UNVERIFIED: this is the one piece of the whole rewrite that could
/// not be run or tested on real Windows during development (this dev environment
/// is Linux-only). It compiles and the API usage matches RawInput.Sharp's real
/// public surface (checked via reflection against the installed package, not
/// guessed), but the actual Raw Input registration/message-loop behavior needs
/// real-hardware verification before being trusted on the floor.
/// </summary>
public class HidScannerReader : IScannerReader
{
    private readonly ScannerOptions _options;
    private readonly ILogger<HidScannerReader> _logger;
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();
    private readonly RawInputLineAssembler _assembler = new();
    private readonly ManualResetEventSlim _formReady = new(false);

    private Thread? _messageLoopThread;
    private HiddenScannerForm? _form;

    public ChannelReader<string> Reader => _channel.Reader;

    public HidScannerReader(ScannerOptions options, ILogger<HidScannerReader> logger)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "HID scanner mode requires Windows (Raw Input API). Use Scanner:Mode 'Serial' elsewhere.");
        }
        _options = options;
        _logger = logger;
    }

    public void Start()
    {
        _messageLoopThread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "hid-scanner-raw-input",
        };
        _messageLoopThread.SetApartmentState(ApartmentState.STA);
        _messageLoopThread.Start();

        if (!_formReady.Wait(TimeSpan.FromSeconds(5)))
        {
            _logger.LogWarning("HID scanner message loop did not report ready within 5s");
        }
    }

    public Task StopAsync()
    {
        var form = _form;
        if (form is { IsHandleCreated: true })
        {
            try
            {
                form.Invoke(form.Close);
            }
            catch
            {
                // best-effort shutdown
            }
        }
        return Task.CompletedTask;
    }

    private void RunMessageLoop()
    {
        try
        {
            // Any exception on this dedicated STA thread that isn't caught here
            // would otherwise be handled by WinForms' own default (and, for a
            // background thread with no visible UI, easy-to-miss) unhandled-
            // exception behavior - route it through the logger instead so a
            // registration/message-loop failure is never silent.
            System.Windows.Forms.Application.SetUnhandledExceptionMode(
                System.Windows.Forms.UnhandledExceptionMode.CatchException);
            System.Windows.Forms.Application.ThreadException += (_, e) =>
                _logger.LogError(e.Exception, "Unhandled exception on the HID scanner message-loop thread");

            _form = new HiddenScannerForm(_options.HidDeviceNameMatch, OnKeyEvent, _logger);

            // Force the native window handle (and therefore OnHandleCreated, where
            // Raw Input registration happens) to exist right now, rather than
            // relying on Application.Run's implicit Show() call. That call routes
            // through SetVisibleCore, which this form permanently overrides to a
            // no-op so it's never actually shown - and going through Application.Run
            // alone was not reliably creating the handle at all, which meant Raw
            // Input registration silently never ran.
            _ = _form.Handle;

            _logger.LogInformation(
                "HID scanner listening for a device matching {Match}", _options.HidDeviceNameMatch);
            _formReady.Set();
            System.Windows.Forms.Application.Run(_form);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HID scanner message loop failed to start");
            _formReady.Set();
        }
    }

    private void OnKeyEvent(int virtualKey, bool isKeyUp)
    {
        var text = _assembler.Feed(virtualKey, isKeyUp);
        if (text is not null)
        {
            _logger.LogInformation("Scanner read: {Value}", text);
            _channel.Writer.TryWrite(text);
        }
    }
}
