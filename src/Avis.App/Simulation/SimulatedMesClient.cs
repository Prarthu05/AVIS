using System.Collections.Concurrent;
using System.Text.Json;
using Avis.IFactory;

namespace Avis.App.Simulation;

/// <summary>
/// In-memory iFactory: every Asset ID has a WIP (except ones containing
/// UnknownAssetMarker), Start/Complete always succeed unless the simulator's
/// "fail next iFactory call" switch is on. Every call is logged for the SIMULATOR tab.
/// </summary>
public class SimulatedMesClient : IMesClient
{
    private readonly SimulationOptions _options;
    private readonly ConcurrentDictionary<string, string> _status = new(StringComparer.OrdinalIgnoreCase);
    private long _nextHistoryId = 5000;
    private volatile bool _failNext;

    public SimulatedMesClient(SimulationOptions options)
    {
        _options = options;
    }

    public event Action<string>? CallLogged;

    /// <summary>Makes the next iFactory call fail once - to try out the Reset popup.</summary>
    public void FailNextCall() => _failNext = true;

    public Task VerifyCredentialsAsync(CancellationToken ct = default)
    {
        Log("VerifyCredentials -> OK");
        return Task.CompletedTask;
    }

    public async Task<Wip?> GetWipBySerialAsync(string serialNumber, CancellationToken ct = default)
    {
        await Task.Delay(200, ct);
        ThrowIfFailing($"GET /api/wips?serialNumber={serialNumber}");
        if (serialNumber.Contains(_options.UnknownAssetMarker, StringComparison.OrdinalIgnoreCase))
        {
            Log($"GET /api/wips?serialNumber={serialNumber} -> not found");
            return null;
        }
        var status = _status.GetOrAdd(serialNumber, "InQueue");
        var wip = new Wip(WipId(serialNumber), serialNumber, _options.Material, status, JsonDocument.Parse("{}").RootElement);
        Log($"GET /api/wips?serialNumber={serialNumber} -> WIP {wip.Id} ({wip.MaterialName}, {status})");
        return wip;
    }

    public async Task<long> StartWipAsync(long wipId, string resourceName, string? operatorId = null, CancellationToken ct = default)
    {
        await Task.Delay(200, ct);
        ThrowIfFailing($"POST /api/wips/{wipId}/processSteps/start");
        var historyId = Interlocked.Increment(ref _nextHistoryId);
        Log($"POST /api/wips/{wipId}/processSteps/start  resource='{resourceName}'  x-operator-override='{operatorId}' -> historyId {historyId}");
        return historyId;
    }

    public async Task<JsonElement> CompleteWipProcessStepAsync(long wipId, long wipProcessStepHistoryId, string? operatorId = null, CancellationToken ct = default)
    {
        await Task.Delay(200, ct);
        ThrowIfFailing($"POST /api/wips/{wipId}/processSteps/{wipProcessStepHistoryId}/complete");
        foreach (var key in _status.Keys.Where(k => WipId(k) == wipId))
        {
            _status[key] = "Completed";
        }
        Log($"POST /api/wips/{wipId}/processSteps/{wipProcessStepHistoryId}/complete  x-operator-override='{operatorId}' -> OK");
        return JsonDocument.Parse("{}").RootElement;
    }

    public Task AddWipAttributeAsync(long wipId, string name, string attributeType, string value, string? operatorId = null, CancellationToken ct = default)
    {
        Log($"POST /api/wips/{wipId}/attributes  {name}={value} ({attributeType})");
        return Task.CompletedTask;
    }

    private void ThrowIfFailing(string call)
    {
        if (_failNext)
        {
            _failNext = false;
            Log($"{call} -> SIMULATED FAILURE");
            throw new IFactoryApiException($"Simulated iFactory failure on {call}", 503);
        }
    }

    private static long WipId(string serial) => 100000 + (Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(serial)) % 900000);

    private void Log(string message) => CallLogged?.Invoke($"{DateTime.Now:HH:mm:ss}  iFactory  {message}");
}
