using System.Reflection;
using Avis.Dashboard;
using Avis.Faults;
using Avis.Station;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Avis.App.Workers;

/// <summary>
/// Keeps the station and the AVIS dashboard on the Pi in step:
/// uploads the event outbox, sends a heartbeat (live station status) and
/// pulls the approved visual aids into the local VA cache. Every fault the
/// station records is also forwarded as an event. The dashboard being down
/// never affects production - events wait in the outbox and the last
/// downloaded VAs (or the INI) keep being used.
/// </summary>
public class DashboardWorker : BackgroundService
{
    private const int BatchSize = 200;

    private readonly DashboardClient _client;
    private readonly EventOutbox _outbox;
    private readonly VaCacheSync _vaSync;
    private readonly StationStatus _status;
    private readonly StationSequencer _sequencer;
    private readonly FaultLog _faults;
    private readonly DashboardOptions _options;
    private readonly ILogger<DashboardWorker> _logger;
    private readonly TimeProvider _time;
    private readonly string _appVersion =
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

    private bool _lastUploadFailed;

    public DashboardWorker(
        DashboardClient client,
        EventOutbox outbox,
        VaCacheSync vaSync,
        StationStatus status,
        StationSequencer sequencer,
        FaultLog faults,
        DashboardOptions options,
        ILogger<DashboardWorker> logger,
        TimeProvider time)
    {
        _client = client;
        _outbox = outbox;
        _vaSync = vaSync;
        _status = status;
        _sequencer = sequencer;
        _faults = faults;
        _options = options;
        _logger = logger;
        _time = time;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _faults.FaultRecorded += OnFault;
        return base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _faults.FaultRecorded -= OnFault;
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dashboard sync on: {BaseUrl} as {Station} ({Queued} events queued)",
            _options.BaseUrl, _options.StationName, _outbox.Count);

        var upload = TimeSpan.FromSeconds(Math.Max(1, _options.UploadIntervalSeconds));
        var heartbeat = TimeSpan.FromSeconds(Math.Max(5, _options.HeartbeatIntervalSeconds));
        var vaSync = TimeSpan.FromSeconds(Math.Max(10, _options.VaSyncIntervalSeconds));
        var nextHeartbeat = _time.GetUtcNow();
        var nextVaSync = _time.GetUtcNow();

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _time.GetUtcNow();
            if (now >= nextVaSync)
            {
                await SyncVisualAidsAsync(stoppingToken);
                nextVaSync = now + vaSync;
            }
            if (now >= nextHeartbeat)
            {
                await SendHeartbeatAsync(stoppingToken);
                nextHeartbeat = now + heartbeat;
            }
            await FlushOutboxAsync(stoppingToken);

            try
            {
                await Task.Delay(upload, _time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // One last try so a clean shutdown doesn't leave the final part's events behind.
        using var last = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await FlushOutboxAsync(last.Token);
    }

    internal async Task FlushOutboxAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var batch = _outbox.Peek(BatchSize);
                if (batch.Count == 0)
                {
                    break;
                }
                await _client.PostEventsAsync(batch, ct);
                _outbox.Acknowledge(batch);
                if (_lastUploadFailed)
                {
                    _logger.LogInformation("Dashboard reachable again - uploading queued events");
                    _lastUploadFailed = false;
                }
                if (batch.Count < BatchSize)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (DashboardApiException ex)
        {
            // Logged once per outage, not every 5 s.
            if (!_lastUploadFailed)
            {
                _logger.LogWarning("Dashboard upload failed ({Message}) - {Count} events kept in the outbox", ex.Message, _outbox.Count);
                _lastUploadFailed = true;
            }
        }
    }

    internal async Task SendHeartbeatAsync(CancellationToken ct)
    {
        try
        {
            await _client.PostEventsAsync(new[] { BuildHeartbeat(_status.Current) }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (DashboardApiException ex)
        {
            _logger.LogDebug("Dashboard heartbeat failed: {Message}", ex.Message);
        }
    }

    internal async Task SyncVisualAidsAsync(CancellationToken ct)
    {
        try
        {
            await _vaSync.SyncAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is DashboardApiException or IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug("VA sync skipped: {Message}", ex.Message);
        }
    }

    internal StationEvent BuildHeartbeat(StationState s) => new()
    {
        Type = StationEvent.Types.Heartbeat,
        At = _time.GetUtcNow(),
        Phase = s.Phase.ToString(),
        Message = s.Message,
        AssetId = s.AssetId,
        Ntid = s.OperatorId,
        WipId = s.WipId,
        Material = s.Material,
        Program = s.Program,
        Step = s.Step,
        StepComment = s.StepComment,
        Links = new Dictionary<string, string>
        {
            ["scanner"] = s.Scanner.ToString(),
            ["camera"] = s.Camera.ToString(),
            ["ifactory"] = s.IFactory.ToString(),
            ["lightguide"] = s.LightGuide.ToString(),
        },
        AppVersion = _appVersion,
    };

    private void OnFault(FaultRecord f)
    {
        try
        {
            _outbox.Emit(ToEvent(f, _sequencer.UnitIdFor(f.AssetId)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not queue fault {Code} for the dashboard", f.Code);
        }
    }

    internal static StationEvent ToEvent(FaultRecord f, string? unitId) => new()
    {
        Type = StationEvent.Types.Fault,
        At = f.Timestamp,
        UnitId = unitId,
        AssetId = f.AssetId,
        Ntid = f.OperatorId,
        WipId = f.WipId,
        FaultCode = f.Code,
        FaultTitle = f.Title,
        IntegrationPoint = f.IntegrationPoint,
        Severity = f.Severity.ToString(),
        Message = f.Message,
    };
}
