using Avis.Camera;
using Avis.Configuration;
using Avis.Faults;
using Avis.Station;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Avis.App.Workers;

/// <summary>
/// "Sensor detects HN -> triggers JabilEye capture -> AssetID ready". The
/// conveyor's part-detect sensor is wired straight into the camera's hardware
/// trigger, so AVIS never triggers it - it polls GetResults for new captures and
/// hands each passing capture's Asset ID to the sequencer.
/// </summary>
public class CameraWorker : BackgroundService
{
    private readonly ILogger<CameraWorker> _logger;
    private readonly JabilEyeCameraClient _camera;
    private readonly JabilEyeCameraOptions _options;
    private readonly StationSequencer _sequencer;
    private readonly StationStatus _status;
    private readonly FaultLog _faults;
    private readonly IOperatorAlerts _alerts;

    private bool _hasBaseline;
    private string? _lastExecutionId;

    public CameraWorker(
        ILogger<CameraWorker> logger,
        JabilEyeCameraClient camera,
        JabilEyeCameraOptions options,
        StationSequencer sequencer,
        StationStatus status,
        FaultLog faults,
        IOperatorAlerts alerts)
    {
        _logger = logger;
        _camera = camera;
        _options = options;
        _sequencer = sequencer;
        _status = status;
        _faults = faults;
        _alerts = alerts;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _camera.OpenProgramAsync(_options.ProgramName, stoppingToken);
            _logger.LogInformation("JabilEye camera {HostName} ready, program {ProgramName} loaded", _options.HostName, _options.ProgramName);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            // Non-fatal: the program may already be loaded. Keep polling.
            _logger.LogError(ex, "Could not open program {ProgramName} on JabilEye camera {HostName} - will still poll for results",
                _options.ProgramName, _options.HostName);
        }

        var pollInterval = TimeSpan.FromSeconds(Math.Max(0.1, _options.PollIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Includes HTTP timeouts (TaskCanceledException without shutdown) -
                // those must NOT end the loop.
                _status.Update(s => s with { Camera = ConnectionState.Error });
                var message = $"Error polling JabilEye camera {_options.HostName}: {ex.Message}";
                _faults.Record(FaultCode.CameraUnreachable, FaultSeverity.Error, message);
                try
                {
                    await _alerts.ShowAndWaitForResetAsync(FaultCode.CameraUnreachable, message, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                continue; // Reset pressed - poll again straight away
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        var result = await _camera.GetResultsAsync(ct);
        _status.Update(s => s.Camera == ConnectionState.Ok ? s : s with { Camera = ConnectionState.Ok });

        if (!_hasBaseline)
        {
            // Whatever capture is already on the camera at startup belongs to a part
            // from before AVIS started - don't start a WIP for it again.
            _hasBaseline = true;
            _lastExecutionId = result.ExecutionId;
            _logger.LogInformation("JabilEye baseline execution {ExecutionId} ignored", result.ExecutionId);
            return;
        }

        if (string.IsNullOrEmpty(result.ExecutionId) || result.ExecutionId == _lastExecutionId)
        {
            return;
        }
        _lastExecutionId = result.ExecutionId;

        _logger.LogInformation("New JabilEye capture {ExecutionId}: result={Result}", result.ExecutionId, result.ExecutionResult);

        if (result.ExecutionResult != JabilEyeResultCode.Pass)
        {
            _faults.Record(FaultCode.CaptureNotPass, FaultSeverity.Warning,
                $"JabilEye capture {result.ExecutionId} was {result.ExecutionResult?.ToString() ?? "not executed"} - no Asset ID, part not started.");
            _status.Update(s => s with
            {
                Message = "Camera could not read the part - check the HN position/label",
                MessageIsError = true,
            });
            return;
        }

        var assetId = AssetIdExtractor.Extract(result, _options.AssetIdToolCode);
        if (assetId is null)
        {
            _faults.Record(FaultCode.AssetIdUnreadable, FaultSeverity.Warning,
                $"JabilEye capture {result.ExecutionId} passed but no readable Asset ID (tool code {_options.AssetIdToolCode}).");
            _status.Update(s => s with { Message = "Asset ID not readable - check the label", MessageIsError = true });
            return;
        }

        try
        {
            await _sequencer.OnAssetCapturedAsync(assetId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A sequence failure is not a camera failure - don't report it as one.
            _logger.LogError(ex, "Station sequence failed for Asset ID {AssetId}", assetId);
        }
    }
}
