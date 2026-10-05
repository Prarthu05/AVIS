using System.Text.Json;
using Avis.IFactory;

namespace Avis.Tests.Fakes;

public class FakeMesClient : IMesClient
{
    public Dictionary<string, Wip> Wips { get; } = new();
    public List<string> Calls { get; } = new();
    public List<(long WipId, string Name, string Value, string? Operator)> Attributes { get; } = new();
    public Queue<Exception> StartWipFailures { get; } = new();
    public Queue<Exception> LookupFailures { get; } = new();
    public long NextHistoryId { get; set; } = 900;

    public static Wip MakeWip(long id, string serial, string material = "PN-1001", string status = "InProcess") =>
        new(id, serial, material, status, JsonDocument.Parse("{}").RootElement);

    public Task VerifyCredentialsAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<Wip?> GetWipBySerialAsync(string serialNumber, CancellationToken ct = default)
    {
        Calls.Add($"lookup {serialNumber}");
        if (LookupFailures.TryDequeue(out var ex))
        {
            throw ex;
        }
        return Task.FromResult(Wips.TryGetValue(serialNumber, out var wip) ? wip : null);
    }

    public Task<long> StartWipAsync(long wipId, string resourceName, string? operatorId = null, CancellationToken ct = default)
    {
        Calls.Add($"start {wipId} {resourceName} {operatorId}");
        if (StartWipFailures.TryDequeue(out var ex))
        {
            throw ex;
        }
        return Task.FromResult(NextHistoryId++);
    }

    public Task<JsonElement> CompleteWipProcessStepAsync(long wipId, long wipProcessStepHistoryId, string? operatorId = null, CancellationToken ct = default)
    {
        Calls.Add($"complete {wipId} {wipProcessStepHistoryId} {operatorId}");
        return Task.FromResult(JsonDocument.Parse("{}").RootElement);
    }

    public Task AddWipAttributeAsync(long wipId, string name, string attributeType, string value, string? operatorId = null, CancellationToken ct = default)
    {
        Attributes.Add((wipId, name, value, operatorId));
        return Task.CompletedTask;
    }
}

public class FakeAlerts : Avis.Station.IOperatorAlerts
{
    public List<(Avis.Faults.FaultCode Code, string Message)> Shown { get; } = new();

    public Task ShowAndWaitForResetAsync(Avis.Faults.FaultCode code, string message, CancellationToken ct)
    {
        Shown.Add((code, message));
        return Task.CompletedTask; // operator presses Reset immediately
    }
}

public sealed class FakeTimeProvider : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => UtcNow;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    public void Advance(TimeSpan by) => UtcNow += by;
}
