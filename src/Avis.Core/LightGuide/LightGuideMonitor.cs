using Avis.Configuration;

namespace Avis.LightGuide;

public abstract record LightGuideEvent;

public record ProgramStarted(string Program) : LightGuideEvent;

public record StepChanged(string Program, int? Step, string? StepComment) : LightGuideEvent;

public record CheckEvaluated(
    LightGuideCheckOptions Check,
    bool Passed,
    string Value,
    IReadOnlyDictionary<string, string?> Measurements) : LightGuideEvent;

public record ProgramCompleted(string Program) : LightGuideEvent;

public record ProgramAborted(string Program) : LightGuideEvent;

/// <summary>
/// Turns successive LightGuide variable snapshots into events: program start,
/// step changes (drives the visual aid), each new check result, and program
/// completion/abort. Pure logic - no I/O - so it's fully unit-testable.
///
/// The first snapshot is only a baseline: whatever results were already sitting
/// in LightGuide's variables when AVIS started are never replayed as new results.
/// </summary>
public class LightGuideMonitor
{
    private readonly LightGuideOptions _options;
    private readonly Dictionary<string, string?> _lastCheckMarker = new(StringComparer.OrdinalIgnoreCase);

    private bool _hasBaseline;
    private string _program = "";
    private int? _step;
    private string? _stepComment;
    private bool _running;
    private bool _completeFlag;
    private string? _state;
    private bool _completedThisRun;
    private int? _pollsSinceStopped;
    private string _stoppedProgram = "";

    public LightGuideMonitor(LightGuideOptions options)
    {
        _options = options;
        VariableNames = new[]
            {
                options.ProgramVariable, options.StepVariable, options.StepCommentVariable,
                options.RunningVariable, options.CompleteVariable, options.StateVariable,
            }
            .Concat(options.Checks.SelectMany(c =>
                new[] { c.ResultVariable, c.CounterVariable ?? "" }.Concat(c.MeasurementVariables)))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Every LightGuide variable this monitor needs in each snapshot.</summary>
    public IReadOnlyList<string> VariableNames { get; }

    public IReadOnlyList<LightGuideEvent> Process(IReadOnlyDictionary<string, string?> values)
    {
        var events = new List<LightGuideEvent>();

        var program = Get(values, _options.ProgramVariable)?.Trim() ?? "";
        var step = int.TryParse(Get(values, _options.StepVariable)?.Trim(), out var s) ? s : (int?)null;
        var stepComment = Get(values, _options.StepCommentVariable);
        var state = Get(values, _options.StateVariable)?.Trim();
        var running = TextBool.IsTrue(Get(values, _options.RunningVariable))
            || string.Equals(state, "Program Running", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, "Paused", StringComparison.OrdinalIgnoreCase);
        var completeFlag = TextBool.IsTrue(Get(values, _options.CompleteVariable));
        var completeSignal = (completeFlag && !_completeFlag)
            || (string.Equals(state, "Program Ended", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(_state, "Program Ended", StringComparison.OrdinalIgnoreCase));

        if (!_hasBaseline)
        {
            _hasBaseline = true;
            foreach (var check in _options.Checks)
            {
                _lastCheckMarker[check.Name] = Marker(values, check);
            }
            if (running)
            {
                events.Add(new StepChanged(program, step, stepComment));
            }
            Remember(program, step, stepComment, running, completeFlag, state);
            _completedThisRun = !running && completeFlag;
            return events;
        }

        if (running && !_running)
        {
            _completedThisRun = false;
            _pollsSinceStopped = null;
            events.Add(new ProgramStarted(program));
        }

        if (running && (program != _program || step != _step || stepComment != _stepComment || !_running))
        {
            events.Add(new StepChanged(program, step, stepComment));
        }

        // Results before completion: a check that finishes in the same poll as the
        // program must be evaluated before the program is confirmed.
        foreach (var check in _options.Checks)
        {
            var marker = Marker(values, check);
            _lastCheckMarker.TryGetValue(check.Name, out var previous);
            _lastCheckMarker[check.Name] = marker;

            var result = Get(values, check.ResultVariable)?.Trim();
            if (string.IsNullOrEmpty(marker) || marker == previous || string.IsNullOrEmpty(result))
            {
                continue;
            }

            var measurements = check.MeasurementVariables
                .ToDictionary(m => m, m => Get(values, m), StringComparer.OrdinalIgnoreCase);
            events.Add(new CheckEvaluated(check, check.IsPass(result), result, measurements));
        }

        if (completeSignal && !_completedThisRun)
        {
            // WI_Name may already be blank once the program has ended.
            var completedProgram = program.Length > 0 ? program : _program.Length > 0 ? _program : _stoppedProgram;
            _completedThisRun = true;
            _pollsSinceStopped = null;
            events.Add(new ProgramCompleted(completedProgram));
        }
        else if (_running && !running && !_completedThisRun)
        {
            // Running dropped without a completion signal yet - give WI_Complete /
            // LGSState a few polls to catch up before calling it an abort.
            _pollsSinceStopped = 0;
            _stoppedProgram = _program;
        }
        else if (_pollsSinceStopped is not null && !running && !_completedThisRun)
        {
            _pollsSinceStopped++;
            if (_pollsSinceStopped >= Math.Max(1, _options.CompletionGracePolls))
            {
                _pollsSinceStopped = null;
                _completedThisRun = true; // one outcome per run
                events.Add(new ProgramAborted(_stoppedProgram));
            }
        }

        Remember(program, step, stepComment, running, completeFlag, state);
        return events;
    }

    private void Remember(string program, int? step, string? stepComment, bool running, bool completeFlag, string? state)
    {
        _program = program;
        _step = step;
        _stepComment = stepComment;
        _running = running;
        _completeFlag = completeFlag;
        _state = state;
    }

    private static string? Marker(IReadOnlyDictionary<string, string?> values, LightGuideCheckOptions check) =>
        string.IsNullOrWhiteSpace(check.CounterVariable)
            ? Get(values, check.ResultVariable)?.Trim()
            : Get(values, check.CounterVariable)?.Trim();

    private static string? Get(IReadOnlyDictionary<string, string?> values, string name) =>
        !string.IsNullOrWhiteSpace(name) && values.TryGetValue(name, out var v) ? v : null;
}
