using Avis.Faults;
using Avis.LightGuide;
using Avis.Station;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Avis.App.Workers;

/// <summary>
/// "LightGuide runs program, reports program &amp; step": polls the LightGuide Web
/// API for its status variables and each check's result variables, and feeds the
/// resulting events (step changes, LJ/IV4 results, completion) to the sequencer.
/// </summary>
public class LightGuideWorker : BackgroundService
{
    private readonly ILogger<LightGuideWorker> _logger;
    private readonly ILightGuideVariableSource _client;
    private readonly LightGuideOptions _options;
    private readonly StationSequencer _sequencer;
    private readonly StationStatus _status;
    private readonly FaultLog _faults;

    public LightGuideWorker(
        ILogger<LightGuideWorker> logger,
        ILightGuideVariableSource client,
        LightGuideOptions options,
        StationSequencer sequencer,
        StationStatus status,
        FaultLog faults)
    {
        _logger = logger;
        _client = client;
        _options = options;
        _sequencer = sequencer;
        _status = status;
        _faults = faults;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("LightGuide integration is disabled (LightGuide:Enabled=false) - parts will start in iFactory but never be completed by AVIS");
            return;
        }

        var monitor = new LightGuideMonitor(_options);
        var interval = TimeSpan.FromSeconds(Math.Max(0.1, _options.PollIntervalSeconds));
        var consecutiveFailures = 0;
        var outageReported = false;

        _logger.LogInformation("Polling LightGuide at {BaseUrl} for {Variables}", _options.BaseUrl, string.Join(",", monitor.VariableNames));

        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<LightGuideEvent> events = Array.Empty<LightGuideEvent>();
            try
            {
                var values = await _client.GetVariablesAsync(monitor.VariableNames, stoppingToken);
                if (consecutiveFailures > 0 || _status.Current.LightGuide != ConnectionState.Ok)
                {
                    if (outageReported)
                    {
                        _logger.LogInformation("LightGuide reachable again");
                    }
                    _status.Update(s => s with { LightGuide = ConnectionState.Ok });
                }
                consecutiveFailures = 0;
                outageReported = false;
                events = monitor.Process(values);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                _logger.LogDebug(ex, "LightGuide poll failed ({Count} in a row)", consecutiveFailures);
                if (consecutiveFailures >= Math.Max(1, _options.UnreachableAfterFailedPolls) && !outageReported)
                {
                    outageReported = true;
                    _status.Update(s => s with { LightGuide = ConnectionState.Error });
                    _faults.Record(FaultCode.LightGuideUnreachable, FaultSeverity.Error,
                        $"LightGuide Web API at {_options.BaseUrl} not reachable: {ex.Message}");
                }
            }

            foreach (var e in events)
            {
                try
                {
                    await _sequencer.OnLightGuideEventAsync(e, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Station sequence failed while handling LightGuide event {Event}", e);
                }
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
