using System.Text.Json;

namespace Avis.IFactory;

/// <summary>The iFactory calls the station sequence needs - an interface so the sequence can be unit tested.</summary>
public interface IMesClient
{
    Task VerifyCredentialsAsync(CancellationToken ct = default);
    Task<Wip?> GetWipBySerialAsync(string serialNumber, CancellationToken ct = default);
    Task<long> StartWipAsync(long wipId, string resourceName, string? operatorId = null, CancellationToken ct = default);
    Task<JsonElement> CompleteWipProcessStepAsync(long wipId, long wipProcessStepHistoryId, string? operatorId = null, CancellationToken ct = default);
    Task AddWipAttributeAsync(long wipId, string name, string attributeType, string value, string? operatorId = null, CancellationToken ct = default);
}
