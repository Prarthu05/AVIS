using Avis.Configuration;
using Avis.Faults;
using Avis.Station;
using Microsoft.Extensions.Logging;

namespace Avis.App.Ui;

/// <summary>
/// Shows blocking fault popups on the main window's UI thread for the background
/// workers, and completes when the operator presses Reset (or on shutdown).
/// </summary>
public class OperatorAlertService : IOperatorAlerts
{
    private readonly ILogger<OperatorAlertService> _logger;
    private readonly string _stationLabel;
    private Form? _owner;

    public OperatorAlertService(ILogger<OperatorAlertService> logger, JabilEyeCameraOptions camera, StationOptions station)
    {
        _logger = logger;
        // The station's iFactory resource name in the title bar tells a supervisor
        // which station a popup belongs to on a multi-station floor.
        _stationLabel = string.IsNullOrWhiteSpace(camera.ResourceName) ? station.Name : camera.ResourceName;
    }

    public void Attach(Form owner) => _owner = owner;

    public async Task ShowAndWaitForResetAsync(FaultCode code, string message, CancellationToken ct)
    {
        var owner = _owner;
        if (owner is null || owner.IsDisposed || !owner.IsHandleCreated)
        {
            // No window to show it on (starting up / shutting down): back off, then let the caller retry.
            _logger.LogWarning("No window available for fault popup {Code}: {Message}", code.ToDisplayCode(), message);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return;
        }

        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ErrorDialogForm? dialog = null;

        using var registration = ct.Register(() =>
        {
            closed.TrySetCanceled(ct);
            TryBeginInvoke(owner, () => dialog?.Close());
        });

        var posted = TryBeginInvoke(owner, () =>
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }
            try
            {
                using (dialog = new ErrorDialogForm(code, message, _stationLabel))
                {
                    dialog.ShowDialog(owner);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not show fault popup {Code}", code.ToDisplayCode());
            }
            finally
            {
                dialog = null;
                closed.TrySetResult();
            }
        });

        if (!posted)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return;
        }
        await closed.Task;
    }

    private static bool TryBeginInvoke(Control control, Action action)
    {
        try
        {
            if (control.IsDisposed || !control.IsHandleCreated)
            {
                return false;
            }
            control.BeginInvoke(action);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false; // handle destroyed during shutdown
        }
    }
}
