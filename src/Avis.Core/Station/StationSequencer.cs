using System.Collections.Immutable;
using Avis.Configuration;
using Avis.Dashboard;
using Avis.Faults;
using Avis.IFactory;
using Avis.Imaging;
using Avis.LightGuide;
using Microsoft.Extensions.Logging;

namespace Avis.Station;

/// <summary>One part (HN) from Asset ID capture to "move to next station".</summary>
public class PartSession
{
    private readonly Dictionary<string, CheckState> _checks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _lastEvaluation = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyDictionary<string, string?>> _measurements = new(StringComparer.OrdinalIgnoreCase);

    public PartSession(string assetId, string operatorId, string operatorLogin, Wip wip, long historyId, string? model, DateTimeOffset startedAt)
    {
        AssetId = assetId;
        OperatorId = operatorId;
        OperatorLogin = operatorLogin;
        Wip = wip;
        HistoryId = historyId;
        Model = model;
        StartedAt = startedAt;
    }

    /// <summary>Correlation id: tags every dashboard event and log line for this unit.</summary>
    public string UnitId { get; } = Guid.NewGuid().ToString();
    public string AssetId { get; }
    public string OperatorId { get; }
    public string OperatorLogin { get; }
    public Wip Wip { get; }
    public long HistoryId { get; }
    public string? Model { get; }
    /// <summary>Product name as the dashboard knows it (falls back to the model / material).</summary>
    public string? Product { get; set; }
    public DateTimeOffset StartedAt { get; }
    public string? Program { get; set; }
    public int? Step { get; set; }
    public bool Escalated { get; set; }
    public bool Finished { get; set; }

    public IReadOnlyCollection<CheckState> Checks => _checks.Values;

    public IReadOnlyDictionary<string, string?> MeasurementsFor(string check) =>
        _measurements.TryGetValue(check, out var m) ? m : new Dictionary<string, string?>();

    /// <summary>When the previous result for this check arrived (or the part started) - IV4 images older than this aren't this attempt's.</summary>
    public DateTimeOffset ImageWindowStart(string check) =>
        _lastEvaluation.TryGetValue(check, out var at) ? at : StartedAt;

    public CheckState Record(CheckEvaluated result, DateTimeOffset at)
    {
        _checks.TryGetValue(result.Check.Name, out var previous);
        var state = new CheckState(
            result.Check.Name,
            result.Passed,
            result.Value,
            (previous?.Attempts ?? 0) + 1,
            (previous?.Failures ?? 0) + (result.Passed ? 0 : 1),
            at,
            null);
        _checks[result.Check.Name] = state;
        _lastEvaluation[result.Check.Name] = at;
        _measurements[result.Check.Name] = result.Measurements;
        return state;
    }

    public void SetImage(string check, string imagePath)
    {
        if (_checks.TryGetValue(check, out var state))
        {
            _checks[check] = state with { ImagePath = imagePath };
        }
    }
}

/// <summary>
/// The station sequence from the AVIS flow chart:
/// <code>
/// badge scan (NTID) ─┐
///                    ├─► iFactory Start WIP (NTID + Asset ID) ─► LightGuide runs program
/// JabilEye Asset ID ─┘                                              │
///        each step ─► visual aid    each LJ/IV4 result ─► OK: carry on
///                                                         NG: highlight, rework, re-check (up to 5)
///                                                         5th NG: escalate
///        program complete ─► all OK: results + Complete WIP to iFactory ─► next station
/// </code>
/// All entry points are serialised, so a capture and a LightGuide result can
/// never interleave half-way through each other.
/// </summary>
public class StationSequencer
{
    private readonly IMesClient _mes;
    private readonly CurrentOperatorState _operator;
    private readonly IOperatorAlerts _alerts;
    private readonly FaultLog _faults;
    private readonly StationStatus _status;
    private readonly ImageStager? _images;
    private readonly CounterStore _counters;
    private readonly StationOptions _station;
    private readonly IFactoryOptions _ifactory;
    private readonly JabilEyeCameraOptions _camera;
    private readonly SequenceOptions _sequence;
    private readonly AviProjectConfig _ini;
    private readonly ILogger<StationSequencer> _logger;
    private readonly TimeProvider _time;
    private readonly IVisualAidResolver _visualAids;
    private readonly IStationEventSink _events;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private PartSession? _session;

    public StationSequencer(
        IMesClient mes,
        CurrentOperatorState currentOperator,
        IOperatorAlerts alerts,
        FaultLog faults,
        StationStatus status,
        ImageStager? images,
        CounterStore counters,
        StationOptions station,
        IFactoryOptions ifactory,
        JabilEyeCameraOptions camera,
        SequenceOptions sequence,
        AviProjectConfig ini,
        ILogger<StationSequencer> logger,
        TimeProvider time,
        IVisualAidResolver? visualAids = null,
        IStationEventSink? events = null)
    {
        _mes = mes;
        _operator = currentOperator;
        _alerts = alerts;
        _faults = faults;
        _status = status;
        _images = images;
        _counters = counters;
        _station = station;
        _ifactory = ifactory;
        _camera = camera;
        _sequence = sequence;
        _ini = ini;
        _logger = logger;
        _time = time;
        _visualAids = visualAids ?? NoVisualAids.Instance;
        _events = events ?? NullStationEventSink.Instance;

        var c = counters.Current;
        _status.Update(s => s with { PassCount = c.Pass, FailCount = c.Fail, ReworkCount = c.Rework });
    }

    /// <summary>The active part, if any (for diagnostics/tests).</summary>
    public PartSession? Session => _session;

    /// <summary>Correlation id of the current/last part if it is <paramref name="assetId"/> - lets faults join their unit's timeline.</summary>
    public string? UnitIdFor(string? assetId) =>
        _session is { } s && assetId is not null && string.Equals(s.AssetId, assetId, StringComparison.OrdinalIgnoreCase) ? s.UnitId : null;

    /// <summary>Badge scan - deliberately NOT behind the gate: a part waiting for its badge holds the gate.</summary>
    public void OnBadgeScanned(string employeeId)
    {
        _logger.LogInformation("Badge scanned: {EmployeeId}", employeeId);
        _operator.SetOperator(employeeId);
        _status.Update(s => s with { OperatorId = employeeId, Scanner = ConnectionState.Ok });
    }

    /// <summary>JabilEye captured a passing image with a readable Asset ID: join with the badge and start the WIP.</summary>
    public async Task OnAssetCapturedAsync(string assetId, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await HandleAssetAsync(assetId, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task OnLightGuideEventAsync(LightGuideEvent e, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            switch (e)
            {
                case ProgramStarted started:
                    _logger.LogInformation("LightGuide started program {Program}", started.Program);
                    if (_session is { Finished: false } running)
                    {
                        running.Program = started.Program;
                    }
                    _status.Update(s => s with { Program = started.Program, LightGuide = ConnectionState.Ok });
                    break;

                case StepChanged step:
                    HandleStep(step);
                    break;

                case CheckEvaluated check:
                    await HandleCheckAsync(check, ct);
                    break;

                case ProgramCompleted completed:
                    await HandleProgramCompletedAsync(completed, ct);
                    break;

                case ProgramAborted aborted:
                    HandleProgramAborted(aborted);
                    break;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task HandleAssetAsync(string assetId, CancellationToken ct)
    {
        if (_session is { Finished: false } previous)
        {
            _faults.Record(FaultCode.PartAbandoned, FaultSeverity.Warning,
                $"New part {assetId} arrived before part {previous.AssetId} was confirmed - part {previous.AssetId} was not completed in iFactory.",
                previous.AssetId, previous.OperatorId, previous.Wip.Id);
            previous.Finished = true;
            Emit(StationEvent.Types.UnitAbandoned, previous, e => e with { Message = $"replaced by {assetId}" });
            CountFail();
        }
        _session = null;

        _status.Update(s => s with
        {
            AssetId = assetId,
            WipId = null,
            Material = null,
            Model = null,
            Program = null,
            Step = null,
            StepComment = null,
            Checks = ImmutableList<CheckState>.Empty,
            Camera = ConnectionState.Ok,
        });

        // Join NTID + Asset ID: a badge scanned shortly before the capture counts,
        // otherwise wait for the operator to scan now.
        var window = TimeSpan.FromSeconds(Math.Max(0, _station.OperatorScanWindowSeconds));
        var employeeId = _operator.ConsumeRecentScan(window);
        if (employeeId is null)
        {
            SetPhase(StationPhase.WaitingForBadge, $"Part {assetId} detected - scan your badge to start", isError: true);
            _faults.Record(FaultCode.NoRecentBadgeScan, FaultSeverity.Info,
                $"Part {assetId} arrived with no badge scan in the last {window.TotalSeconds:0}s - waiting for the operator to scan.", assetId);
            employeeId = await _operator.WaitForNextScanAsync(ct);
        }

        var operatorLogin = OperatorLogin.Build(_station.OperatorDomain, employeeId);
        _status.Update(s => s with { OperatorId = employeeId });
        SetPhase(StationPhase.StartingWip, $"Sending {employeeId} + {assetId} to iFactory...");

        Wip? wip = null;
        while (wip is null)
        {
            try
            {
                wip = await _mes.GetWipBySerialAsync(assetId, ct);
                MarkIFactory(ConnectionState.Ok);
                if (wip is null)
                {
                    _faults.Record(FaultCode.WipNotFound, FaultSeverity.Warning,
                        $"No WIP found in iFactory for Asset ID {assetId}.", assetId, employeeId);
                    SetPhase(StationPhase.Idle, $"No WIP in iFactory for {assetId} - part not started", isError: true);
                    return;
                }
            }
            catch (Exception ex) when (IsFailure(ex, ct))
            {
                MarkIFactory(ConnectionState.Error);
                var message = $"WIP lookup failed for Asset ID {assetId}: {ex.Message}";
                _faults.Record(FaultCode.IFactoryUnreachable, FaultSeverity.Error, message, assetId, employeeId);
                await _alerts.ShowAndWaitForResetAsync(FaultCode.IFactoryUnreachable, message, ct);
            }
        }

        var model = _ini.FindModel(wip.MaterialName);
        _status.Update(s => s with { WipId = wip.Id, Material = wip.MaterialName, Model = model });

        if (_ifactory.SkipWipStatus.Contains(wip.WipStatus, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogInformation("WIP {WipId} ({AssetId}) is already {Status} - not starting it", wip.Id, assetId, wip.WipStatus);
            SetPhase(StationPhase.Idle, $"WIP {assetId} is already {wip.WipStatus} - not started", isError: true);
            return;
        }

        long? historyId = null;
        while (historyId is null)
        {
            try
            {
                historyId = await _mes.StartWipAsync(wip.Id, _camera.ResourceName, operatorLogin, ct);
                MarkIFactory(ConnectionState.Ok);
            }
            catch (Exception ex) when (IsFailure(ex, ct))
            {
                var message = $"Start WIP failed for WIP {wip.Id} (Asset ID {assetId}): {ex.Message}";
                _faults.Record(FaultCode.StartWipRejected, FaultSeverity.Error, message, assetId, employeeId, wip.Id);
                await _alerts.ShowAndWaitForResetAsync(FaultCode.StartWipRejected, message, ct);
            }
        }

        _session = new PartSession(assetId, employeeId, operatorLogin, wip, historyId.Value, model, _time.GetUtcNow())
        {
            Product = _visualAids.ProductFor(wip.MaterialName, model, null) ?? model ?? wip.MaterialName,
        };
        Emit(StationEvent.Types.UnitStarted, _session);
        _logger.LogInformation(
            "Started WIP {WipId} (Asset ID {AssetId}) on {Resource} as {Operator}, historyId={HistoryId}",
            wip.Id, assetId, _camera.ResourceName, operatorLogin, historyId);

        _status.Update(s => s with { VisualAidUrl = _visualAids.Resolve(wip.MaterialName, model, null, null) ?? _ini.FindVisualAid(model, null) ?? s.VisualAidUrl });
        SetPhase(StationPhase.InProcess, $"WIP started for {assetId} - follow LightGuide");
    }

    private void HandleStep(StepChanged step)
    {
        if (_session is { Finished: false } session)
        {
            session.Program = step.Program;
            session.Step = step.Step;
        }

        // Visual aid per (product, step): the dashboard's approved VA first (matched by
        // WIP part number, then model, then LightGuide program), then the INI entries.
        var active = _session is { Finished: false } current ? current : null;
        var url = _visualAids.Resolve(active?.Wip.MaterialName, active?.Model, step.Program, step.Step)
            ?? _ini.FindVisualAid(active?.Model, step.Step)
            ?? _ini.FindVisualAid(step.Program, step.Step);
        if (active is not null)
        {
            Emit(StationEvent.Types.StepChanged, active, e => e with { Step = step.Step, StepComment = step.StepComment });
        }
        _status.Update(s => s with
        {
            Program = step.Program,
            Step = step.Step,
            StepComment = step.StepComment,
            VisualAidUrl = url ?? s.VisualAidUrl,
            LightGuide = ConnectionState.Ok,
        });
    }

    private async Task HandleCheckAsync(CheckEvaluated result, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var session = _session is { Finished: false } s ? s : null;

        if (session is null)
        {
            _faults.Record(FaultCode.LightGuideResultWithoutPart, FaultSeverity.Warning,
                $"LightGuide reported {result.Check.Name} = '{result.Value}' but no part is active at the station.");
        }

        // IV4 images older than this check's previous result belong to an earlier attempt.
        var imageSince = session?.ImageWindowStart(result.Check.Name) ?? now.AddSeconds(-10);
        var state = session?.Record(result, now)
            ?? new CheckState(result.Check.Name, result.Passed, result.Value, 1, result.Passed ? 0 : 1, now, null);

        if (result.Check.Kind == CheckKind.IV4)
        {
            var imagePath = await StageImageAsync(result, state, session, imageSince, ct);
            if (imagePath is not null)
            {
                session?.SetImage(result.Check.Name, imagePath);
                state = state with { ImagePath = imagePath };
            }
        }

        var measurements = string.Join(", ", result.Measurements.Select(m => $"{m.Key}={m.Value}"));
        _logger.LogInformation("{Check} result {Value} ({Outcome}) for asset {AssetId}, attempt {Attempt}{Measurements}",
            result.Check.Name, result.Value, result.Passed ? "OK" : "NG", session?.AssetId, state.Attempts,
            measurements.Length > 0 ? $" [{measurements}]" : "");

        _status.Update(st => st with
        {
            Checks = st.Checks.RemoveAll(c => c.Name.Equals(state.Name, StringComparison.OrdinalIgnoreCase)).Add(state),
        });
        Emit(StationEvent.Types.CheckResult, session, e => e with
        {
            Check = result.Check.Name,
            Result = result.Passed ? "OK" : "NG",
            Value = result.Value,
            Attempt = state.Attempts,
            Failures = state.Failures,
            Measurements = result.Measurements.Count > 0 ? result.Measurements : null,
            ImagePath = state.ImagePath,
        });

        if (session is null)
        {
            return;
        }

        if (result.Passed)
        {
            var stillFailing = session.Checks.Where(c => !c.Passed).Select(c => c.Name).ToList();
            if (session.Escalated)
            {
                return; // the escalation stays on screen until the part is dealt with
            }
            if (stillFailing.Count == 0)
            {
                SetPhase(StationPhase.InProcess, $"{result.Check.Name} OK");
            }
            else
            {
                SetPhase(StationPhase.ReworkRequired, $"{result.Check.Name} OK - still NG: {string.Join(", ", stillFailing)}", isError: true);
            }
            return;
        }

        var code = result.Check.Kind switch
        {
            CheckKind.LJ => FaultCode.LjCheckFailed,
            CheckKind.IV4 => FaultCode.Iv4CheckFailed,
            _ => FaultCode.CheckFailed,
        };
        var max = Math.Max(1, _sequence.MaxFailedAttempts);
        _faults.Record(code, FaultSeverity.Warning,
            $"{result.Check.Name} NG ('{result.Value}') on asset {session.AssetId}, failure {state.Failures} of {max}"
                + (measurements.Length > 0 ? $" [{measurements}]" : "") + ".",
            session.AssetId, session.OperatorId, session.Wip.Id);

        if (state.Failures >= max)
        {
            session.Escalated = true;
            Emit(StationEvent.Types.Escalated, session, e => e with { Check = result.Check.Name, Failures = state.Failures });
            _faults.Record(FaultCode.ReworkLimitReached, FaultSeverity.Critical,
                $"{result.Check.Name} failed {state.Failures} times on asset {session.AssetId} - escalate to supervisor/engineer.",
                session.AssetId, session.OperatorId, session.Wip.Id);
            SetPhase(StationPhase.EscalationRequired,
                $"{result.Check.Name} failed {state.Failures} times - STOP. Escalate to supervisor/engineer.", isError: true);
            return;
        }

        var counters = _counters.AddRework();
        _status.Update(st => st with { ReworkCount = counters.Rework });
        SetPhase(StationPhase.ReworkRequired,
            $"{result.Check.Name} NG - rework the part and re-check (failure {state.Failures} of {max})", isError: true);
    }

    private async Task<string?> StageImageAsync(CheckEvaluated result, CheckState state, PartSession? session, DateTimeOffset since, CancellationToken ct)
    {
        if (_images is null)
        {
            return null;
        }

        var tag = new ImageTag(
            session?.AssetId ?? "NO-ASSET",
            session?.OperatorId,
            session?.Wip.Id,
            session?.Program,
            session?.Step,
            result.Check.Name,
            state.Attempts,
            result.Passed,
            result.Value,
            _time.GetLocalNow());

        try
        {
            var path = await _images.StageLatestAsync(tag, since, ct);
            if (path is null)
            {
                _faults.Record(FaultCode.ImageNotFound, FaultSeverity.Warning,
                    $"No new IV4 image arrived in the drop folder for asset {tag.AssetId} ({result.Check.Name} attempt {state.Attempts}).",
                    session?.AssetId, session?.OperatorId, session?.Wip.Id);
            }
            return path;
        }
        catch (Exception ex) when (IsFailure(ex, ct))
        {
            _faults.Record(FaultCode.ImageStagingFailed, FaultSeverity.Warning,
                $"Could not stage the IV4 image for asset {tag.AssetId}: {ex.Message}",
                session?.AssetId, session?.OperatorId, session?.Wip.Id);
            return null;
        }
    }

    private async Task HandleProgramCompletedAsync(ProgramCompleted completed, CancellationToken ct)
    {
        var session = _session is { Finished: false } s ? s : null;
        if (session is null)
        {
            _faults.Record(FaultCode.LightGuideResultWithoutPart, FaultSeverity.Warning,
                $"LightGuide completed program {completed.Program} but no part is active at the station - nothing sent to iFactory.");
            return;
        }

        var failing = session.Checks.Where(c => !c.Passed).Select(c => c.Name).ToList();
        var missing = _sequence.RequiredChecks
            .Where(r => !session.Checks.Any(c => c.Name.Equals(r, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (session.Escalated || failing.Count > 0 || missing.Count > 0)
        {
            var reason = session.Escalated
                ? "rework limit reached"
                : failing.Count > 0
                    ? $"still NG: {string.Join(", ", failing)}"
                    : $"check(s) never reported: {string.Join(", ", missing)}";
            _faults.Record(FaultCode.PartNotConfirmed, FaultSeverity.Error,
                $"LightGuide completed {completed.Program} for asset {session.AssetId} but the part is not confirmed ({reason}) - WIP not completed in iFactory.",
                session.AssetId, session.OperatorId, session.Wip.Id);
            session.Finished = true;
            Emit(StationEvent.Types.UnitNotConfirmed, session, e => e with { Message = reason });
            CountFail();
            SetPhase(session.Escalated ? StationPhase.EscalationRequired : StationPhase.NotConfirmed,
                $"Part {session.AssetId} NOT confirmed ({reason}) - do not move to next station", isError: true);
            return;
        }

        SetPhase(StationPhase.Completing, $"Sending station data for {session.AssetId} to iFactory...");

        if (_sequence.SendCheckResultsToIFactory)
        {
            await SendCheckAttributesAsync(session, ct);
        }

        var done = false;
        while (!done)
        {
            try
            {
                await _mes.CompleteWipProcessStepAsync(session.Wip.Id, session.HistoryId, session.OperatorLogin, ct);
                MarkIFactory(ConnectionState.Ok);
                done = true;
            }
            catch (Exception ex) when (IsFailure(ex, ct))
            {
                MarkIFactory(ConnectionState.Error);
                var message = $"Complete WIP failed for WIP {session.Wip.Id} (Asset ID {session.AssetId}): {ex.Message}";
                _faults.Record(FaultCode.CompleteWipRejected, FaultSeverity.Error, message, session.AssetId, session.OperatorId, session.Wip.Id);
                await _alerts.ShowAndWaitForResetAsync(FaultCode.CompleteWipRejected, message, ct);
            }
        }

        session.Finished = true;
        Emit(StationEvent.Types.UnitConfirmed, session, e => e with { Message = "Station data confirmed OK" });
        var counters = _counters.AddPass();
        _status.Update(st => st with { PassCount = counters.Pass, FailCount = counters.Fail, ReworkCount = counters.Rework });
        _logger.LogInformation("Completed WIP {WipId} (Asset ID {AssetId}) - station data confirmed OK", session.Wip.Id, session.AssetId);
        SetPhase(StationPhase.Completed, $"{session.AssetId} confirmed OK - move part to next station");
    }

    private async Task SendCheckAttributesAsync(PartSession session, CancellationToken ct)
    {
        var attributes = new List<(string Name, string Value)>();
        foreach (var check in session.Checks)
        {
            attributes.Add(($"{_sequence.WipAttributePrefix}{check.Name}", check.Passed ? "OK" : "NG"));
            attributes.Add(($"{_sequence.WipAttributePrefix}{check.Name}_Attempts", check.Attempts.ToString()));
            foreach (var m in session.MeasurementsFor(check.Name).Where(m => m.Value is not null))
            {
                attributes.Add(($"{_sequence.WipAttributePrefix}{check.Name}_{m.Key}", m.Value!));
            }
        }

        foreach (var (name, value) in attributes)
        {
            try
            {
                await _mes.AddWipAttributeAsync(session.Wip.Id, name, _sequence.WipAttributeType, value, session.OperatorLogin, ct);
            }
            catch (Exception ex) when (IsFailure(ex, ct))
            {
                // Recorded but not blocking: the pass/fail decision is the WIP completion itself.
                _faults.Record(FaultCode.WipAttributeRejected, FaultSeverity.Warning,
                    $"Could not add WIP attribute {name}={value} to WIP {session.Wip.Id}: {ex.Message}",
                    session.AssetId, session.OperatorId, session.Wip.Id);
            }
        }
    }

    private void HandleProgramAborted(ProgramAborted aborted)
    {
        var session = _session is { Finished: false } s ? s : null;
        _faults.Record(FaultCode.LightGuideProgramAborted, FaultSeverity.Warning,
            $"LightGuide program {aborted.Program} stopped without completing"
                + (session is null ? "." : $" for asset {session.AssetId} - restart it, or the part will not be confirmed."),
            session?.AssetId, session?.OperatorId, session?.Wip.Id);
        if (session is not null && !session.Escalated)
        {
            SetPhase(StationPhase.NotConfirmed, $"LightGuide program aborted - restart it for {session.AssetId}", isError: true);
        }
    }

    private void Emit(string type, PartSession? session, Func<StationEvent, StationEvent>? extra = null)
    {
        var e = new StationEvent
        {
            Type = type,
            At = _time.GetUtcNow(),
            UnitId = session?.UnitId,
            AssetId = session?.AssetId,
            Ntid = session?.OperatorId,
            WipId = session?.Wip.Id,
            Material = session?.Wip.MaterialName,
            Product = session?.Product,
            Program = session?.Program,
            Step = session?.Step,
        };
        try
        {
            _events.Emit(extra is null ? e : extra(e));
        }
        catch (Exception ex)
        {
            // Analytics must never stop the station.
            _logger.LogWarning(ex, "Could not queue dashboard event {Type}", type);
        }
    }

    private void CountFail()
    {
        var counters = _counters.AddFail();
        _status.Update(st => st with { PassCount = counters.Pass, FailCount = counters.Fail, ReworkCount = counters.Rework });
    }

    private void SetPhase(StationPhase phase, string message, bool isError = false) =>
        _status.Update(s => s with { Phase = phase, Message = message, MessageIsError = isError });

    private void MarkIFactory(ConnectionState state) => _status.Update(s => s with { IFactory = state });

    /// <summary>Any exception except the shutdown cancellation is a failure to report and retry.</summary>
    private static bool IsFailure(Exception ex, CancellationToken ct) =>
        !(ex is OperationCanceledException && ct.IsCancellationRequested);
}
