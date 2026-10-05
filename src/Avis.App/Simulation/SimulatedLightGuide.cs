using System.Drawing.Imaging;
using Avis.Configuration;
using Avis.Imaging;
using Avis.LightGuide;

namespace Avis.App.Simulation;

/// <summary>
/// Stand-in for the LightGuide Web API: holds the same variables a real
/// LightGuide station would (WI_Name, StepNumberMain, WI_Running, WI_Complete,
/// LGSState and each check's result/counter variables). The SIMULATOR tab
/// changes them; LightGuideWorker polls them through the same monitor logic as
/// the real station, so the simulation exercises the real detection code.
/// </summary>
public class SimulatedLightGuide : ILightGuideVariableSource
{
    private readonly LightGuideOptions _options;
    private readonly ImageStagingOptions _images;
    private readonly Dictionary<string, string?> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public SimulatedLightGuide(LightGuideOptions options, ImageStagingOptions images)
    {
        _options = options;
        _images = images;
        Set(options.RunningVariable, "False");
        Set(options.CompleteVariable, "False");
        Set(options.StateVariable, "File Browser");
    }

    public event Action<string>? Logged;

    public IReadOnlyList<LightGuideCheckOptions> Checks => _options.Checks;

    public Task<IReadOnlyDictionary<string, string?>> GetVariablesAsync(IReadOnlyList<string> names, CancellationToken ct = default)
    {
        lock (_lock)
        {
            IReadOnlyDictionary<string, string?> snapshot = names
                .Where(_variables.ContainsKey)
                .ToDictionary(n => n, n => _variables[n], StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(snapshot);
        }
    }

    public void StartProgram(string program)
    {
        Set(_options.ProgramVariable, program);
        Set(_options.CompleteVariable, "False");
        Set(_options.StepVariable, "1");
        Set(_options.StepCommentVariable, "Step 1");
        Set(_options.StateVariable, "Program Running");
        Set(_options.RunningVariable, "True");
        Log($"program '{program}' started at step 1");
    }

    public void SetStep(int step, string? comment)
    {
        Set(_options.StepVariable, step.ToString());
        Set(_options.StepCommentVariable, string.IsNullOrWhiteSpace(comment) ? $"Step {step}" : comment);
        Log($"step {step}");
    }

    /// <summary>Posts a check result the way a LightGuide program would: value first, then bump the counter.</summary>
    public void PostResult(LightGuideCheckOptions check, bool pass)
    {
        if (check.Kind == CheckKind.IV4)
        {
            DropSimulatedIv4Image(pass);
        }
        var value = pass ? (check.PassValues.FirstOrDefault() ?? "OK") : "NG";
        Set(check.ResultVariable, value);
        foreach (var measurement in check.MeasurementVariables)
        {
            Set(measurement, (pass ? 1.25 : 1.62).ToString("0.00"));
        }
        if (!string.IsNullOrWhiteSpace(check.CounterVariable))
        {
            lock (_lock)
            {
                _variables.TryGetValue(check.CounterVariable, out var count);
                _variables[check.CounterVariable] = ((int.TryParse(count, out var n) ? n : 0) + 1).ToString();
            }
        }
        else
        {
            // Without a counter, LightGuide must clear the result between runs so a
            // repeated NG is seen as new - mimic that so the simulation stays honest.
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1.5, _options.PollIntervalSeconds * 3)));
                Set(check.ResultVariable, "");
            });
        }
        Log($"{check.Name} = {value}");
    }

    public void CompleteProgram()
    {
        Set(_options.RunningVariable, "False");
        Set(_options.CompleteVariable, "True");
        Set(_options.StateVariable, "Program Ended");
        Log("program completed");
    }

    public void AbortProgram()
    {
        Set(_options.RunningVariable, "False");
        Set(_options.CompleteVariable, "False");
        Set(_options.StateVariable, "File Browser");
        Set(_options.ProgramVariable, "");
        Log("program aborted");
    }

    private void DropSimulatedIv4Image(bool pass)
    {
        try
        {
            var folder = AvisOptions.ResolvePath(_images.DropFolder);
            Directory.CreateDirectory(folder);
            using var bitmap = new Bitmap(640, 480);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(pass ? Color.FromArgb(0x1E, 0x8E, 0x3E) : Color.FromArgb(0xBD, 0x28, 0x32));
                using var font = new Font("Segoe UI", 36, FontStyle.Bold);
                g.DrawString($"SIMULATED IV4\n{(pass ? "OK" : "NG")}\n{DateTime.Now:HH:mm:ss.fff}", font, Brushes.White, 40, 120);
            }
            bitmap.Save(Path.Combine(folder, $"IV4_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png"), ImageFormat.Png);
        }
        catch (Exception ex)
        {
            Log($"could not write simulated IV4 image: {ex.Message}");
        }
    }

    private void Set(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        lock (_lock)
        {
            _variables[name] = value;
        }
    }

    private void Log(string message) => Logged?.Invoke($"{DateTime.Now:HH:mm:ss}  LightGuide  {message}");
}
