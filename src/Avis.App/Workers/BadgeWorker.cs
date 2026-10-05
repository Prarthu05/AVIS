using Avis.Faults;
using Avis.IFactory;
using Avis.Scanner;
using Avis.Station;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Avis.App.Workers;

/// <summary>
/// "Operator scans ID": reads badge scans (NTID) from the scanner and hands them
/// to the sequencer. Also checks the iFactory credentials at startup, without
/// blocking the station from starting if iFactory is briefly unavailable.
/// </summary>
public class BadgeWorker : BackgroundService
{
    private static readonly TimeSpan CredentialRetryInterval = TimeSpan.FromSeconds(60);

    private readonly ILogger<BadgeWorker> _logger;
    private readonly IScannerReader _scanner;
    private readonly IMesClient _mes;
    private readonly StationSequencer _sequencer;
    private readonly StationStatus _status;
    private readonly FaultLog _faults;

    public BadgeWorker(
        ILogger<BadgeWorker> logger,
        IScannerReader scanner,
        IMesClient mes,
        StationSequencer sequencer,
        StationStatus status,
        FaultLog faults)
    {
        _logger = logger;
        _scanner = scanner;
        _mes = mes;
        _sequencer = sequencer;
        _status = status;
        _faults = faults;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Scanner.Start() can block for a few seconds (HID message-loop start-up) -
        // don't hold up the other workers' start while it does.
        await Task.Yield();
        _ = Task.Run(() => VerifyIFactoryAsync(stoppingToken), stoppingToken);

        try
        {
            _scanner.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Badge scanner failed to start - badge scans will not be captured");
            _status.Update(s => s with { Scanner = ConnectionState.Error });
            return;
        }

        _logger.LogInformation("Waiting for badge scans...");
        try
        {
            await foreach (var employeeId in _scanner.Reader.ReadAllAsync(stoppingToken))
            {
                var id = employeeId.Trim();
                if (id.Length > 0)
                {
                    _sequencer.OnBadgeScanned(id);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutdown
        }
    }

    private async Task VerifyIFactoryAsync(CancellationToken ct)
    {
        var reported = false;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _mes.VerifyCredentialsAsync(ct);
                _logger.LogInformation("iFactory authentication OK");
                _status.Update(s => s with { IFactory = ConnectionState.Ok });
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _status.Update(s => s with { IFactory = ConnectionState.Error });
                if (!reported)
                {
                    reported = true;
                    _faults.Record(FaultCode.IFactoryUnreachable, FaultSeverity.Error,
                        $"Could not authenticate to iFactory - check IFactory BaseUrl and IFACTORY_USERNAME/IFACTORY_PASSWORD: {ex.Message}");
                }
            }

            try
            {
                await Task.Delay(CredentialRetryInterval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await _scanner.StopAsync();
    }
}
