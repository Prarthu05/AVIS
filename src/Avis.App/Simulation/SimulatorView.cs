using Avis.LightGuide;
using Avis.Station;
using Microsoft.Extensions.Logging;

namespace Avis.App.Simulation;

/// <summary>
/// SIMULATOR tab (simulation mode only): buttons that stand in for the badge
/// scanner, the JabilEye capture, and LightGuide (program, steps, LJ/IV4 results),
/// plus two scripted runs - a good part and a part that fails 5 times - so the
/// whole flow chart can be exercised from Visual Studio.
/// </summary>
public class SimulatorView : UserControl
{
    private readonly SimulatedScannerReader _scanner;
    private readonly SimulatedLightGuide _lightGuide;
    private readonly SimulatedMesClient _mes;
    private readonly StationSequencer _sequencer;
    private readonly StationStatus _status;
    private readonly LightGuideOptions _lightGuideOptions;
    private readonly ILogger<SimulatorView> _logger;

    private readonly TextBox _badge = new() { Text = "4375789", Width = 140 };
    private readonly TextBox _asset = new() { Text = "SIM-HN-0001", Width = 160 };
    private readonly TextBox _program = new() { Text = "SIM-CVG300", Width = 140 };
    private readonly NumericUpDown _step = new() { Minimum = 1, Maximum = 999, Value = 1, Width = 70 };
    private readonly ListBox _log = new() { Dock = DockStyle.Fill, Font = new Font("Consolas", 9.75F), HorizontalScrollbar = true };
    private readonly List<Button> _scriptButtons = new();
    private int _assetCounter = 1;

    public SimulatorView(
        SimulatedScannerReader scanner,
        SimulatedLightGuide lightGuide,
        SimulatedMesClient mes,
        StationSequencer sequencer,
        StationStatus status,
        LightGuideOptions lightGuideOptions,
        ILogger<SimulatorView> logger)
    {
        _scanner = scanner;
        _lightGuide = lightGuide;
        _mes = mes;
        _sequencer = sequencer;
        _status = status;
        _lightGuideOptions = lightGuideOptions;
        _logger = logger;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10F);

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 330,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(8),
            AutoScroll = true,
        };

        panel.Controls.Add(Group("1. Operator scans ID (badge)",
            Caption("NTID"), _badge, ActionButton("Scan badge", () => Scan())));

        panel.Controls.Add(Group("2. Sensor detects HN -> JabilEye AssetID",
            Caption("Asset ID"), _asset,
            ActionButton("JabilEye capture", () => CaptureAsset()),
            ActionButton("Unknown asset", () => CaptureAsset("UNKNOWN-" + _asset.Text))));

        var lightGuideControls = new List<Control>
        {
            Caption("Program"), _program,
            ActionButton("Start program", () => _lightGuide.StartProgram(_program.Text.Trim())),
            Caption("Step"), _step,
            ActionButton("Go to step", () => _lightGuide.SetStep((int)_step.Value, null)),
            ActionButton("Next step", () =>
            {
                _step.Value = Math.Min(_step.Maximum, _step.Value + 1);
                _lightGuide.SetStep((int)_step.Value, null);
            }),
        };
        panel.Controls.Add(Group("3. LightGuide runs program / reports step", lightGuideControls.ToArray()));

        var checkControls = new List<Control>();
        foreach (var check in _lightGuideOptions.Checks)
        {
            checkControls.Add(ResultButton($"{check.Name} OK", check, pass: true));
            checkControls.Add(ResultButton($"{check.Name} NG", check, pass: false));
        }
        if (checkControls.Count == 0)
        {
            checkControls.Add(Caption("No LightGuide:Checks configured"));
        }
        panel.Controls.Add(Group("4. LJ / IV4 checks (LightGuide variables)", checkControls.ToArray()));

        panel.Controls.Add(Group("5. Finish",
            ActionButton("Complete program", () => _lightGuide.CompleteProgram()),
            ActionButton("Abort program", () => _lightGuide.AbortProgram()),
            ActionButton("Fail next iFactory call", () => _mes.FailNextCall())));

        var goodPart = ActionButton("▶ Run: good part", () => RunScriptAsync(failuresBeforePass: 0));
        var rework = ActionButton("▶ Run: 2 NG, rework, pass", () => RunScriptAsync(failuresBeforePass: 2));
        var escalate = ActionButton("▶ Run: 5 NG -> escalation", () => RunScriptAsync(failuresBeforePass: 5));
        _scriptButtons.AddRange(new[] { goodPart, rework, escalate });
        panel.Controls.Add(Group("Scripted runs (whole flow chart)", goodPart, rework, escalate));

        var logTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "  SIMULATED iFACTORY / LIGHTGUIDE TRAFFIC",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.FromArgb(0xE8, 0xEE, 0xF4),
        };

        Controls.Add(_log);
        Controls.Add(logTitle);
        Controls.Add(panel);

        _mes.CallLogged += AppendLog;
        _lightGuide.Logged += AppendLog;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _mes.CallLogged -= AppendLog;
            _lightGuide.Logged -= AppendLog;
        }
        base.Dispose(disposing);
    }

    private void Scan()
    {
        var id = _badge.Text.Trim();
        if (id.Length > 0)
        {
            AppendLog($"{DateTime.Now:HH:mm:ss}  Scanner     badge {id}");
            _scanner.Scan(id);
        }
    }

    private void CaptureAsset(string? assetOverride = null)
    {
        var asset = (assetOverride ?? _asset.Text).Trim();
        if (asset.Length == 0)
        {
            return;
        }
        AppendLog($"{DateTime.Now:HH:mm:ss}  JabilEye    capture PASS, Asset ID {asset}");
        if (assetOverride is null)
        {
            _assetCounter++;
            _asset.Text = $"SIM-HN-{_assetCounter:0000}";
        }

        // Same hand-off CameraWorker makes, off the UI thread.
        _ = Task.Run(async () =>
        {
            try
            {
                await _sequencer.OnAssetCapturedAsync(asset, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Simulated capture failed for {AssetId}", asset);
            }
        });
    }

    private Button ResultButton(string text, LightGuideCheckOptions check, bool pass)
    {
        var button = ActionButton(text, () => _lightGuide.PostResult(check, pass));
        button.BackColor = pass ? Color.FromArgb(0xDF, 0xF5, 0xE3) : Color.FromArgb(0xFB, 0xDE, 0xDE);
        return button;
    }

    /// <summary>One whole part through the flow chart, with realistic gaps so every LightGuide poll sees each change.</summary>
    private async Task RunScriptAsync(int failuresBeforePass)
    {
        foreach (var b in _scriptButtons)
        {
            b.Enabled = false;
        }
        try
        {
            var gap = TimeSpan.FromSeconds(Math.Max(1.2, _lightGuideOptions.PollIntervalSeconds * 3));
            Scan();
            await Task.Delay(300);
            CaptureAsset();

            if (!await WaitForPhaseAsync(StationPhase.InProcess, TimeSpan.FromSeconds(15)))
            {
                AppendLog("Script stopped: the part did not reach 'InProcess' (see the station banner / fault feed).");
                return;
            }

            _lightGuide.StartProgram(_program.Text.Trim());
            await Task.Delay(gap);
            var step = 1;
            foreach (var check in _lightGuideOptions.Checks)
            {
                step++;
                _lightGuide.SetStep(step, $"{check.Name} check");
                await Task.Delay(gap);

                var target = check == _lightGuideOptions.Checks.Last() ? failuresBeforePass : 0;
                for (var i = 0; i < target; i++)
                {
                    _lightGuide.PostResult(check, pass: false);
                    await Task.Delay(gap);
                }
                if (failuresBeforePass < 5)
                {
                    _lightGuide.PostResult(check, pass: true);
                    await Task.Delay(gap);
                }
            }
            _lightGuide.SetStep(step + 1, "Final step");
            await Task.Delay(gap);
            _lightGuide.CompleteProgram();
        }
        finally
        {
            foreach (var b in _scriptButtons)
            {
                b.Enabled = true;
            }
        }
    }

    private async Task<bool> WaitForPhaseAsync(StationPhase phase, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_status.Current.Phase == phase)
            {
                return true;
            }
            await Task.Delay(100);
        }
        return false;
    }

    private void AppendLog(string line)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }
        try
        {
            BeginInvoke(() =>
            {
                _log.Items.Insert(0, line);
                while (_log.Items.Count > 500)
                {
                    _log.Items.RemoveAt(_log.Items.Count - 1);
                }
            });
        }
        catch (InvalidOperationException)
        {
            // closing
        }
    }

    private static GroupBox Group(string title, params Control[] controls)
    {
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(4) };
        foreach (var c in controls)
        {
            c.Margin = new Padding(4, 6, 4, 4);
            flow.Controls.Add(c);
        }
        var box = new GroupBox { Text = title, Width = 560, Height = 100, Padding = new Padding(6) };
        box.Controls.Add(flow);
        return box;
    }

    private static Label Caption(string text) => new() { Text = text, AutoSize = true, Padding = new Padding(0, 4, 0, 0) };

    private static Button ActionButton(string text, Action onClick)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 32 };
        button.Click += (_, _) => onClick();
        return button;
    }

    private static Button ActionButton(string text, Func<Task> onClick)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 32 };
        button.Click += async (_, _) =>
        {
            try
            {
                await onClick();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Simulator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        return button;
    }
}
