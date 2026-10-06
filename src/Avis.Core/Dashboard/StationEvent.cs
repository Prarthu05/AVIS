using System.Text.Json.Serialization;

namespace Avis.Dashboard;

/// <summary>
/// One thing that happened at the station, in the dashboard's event format
/// (dashboard/src/lib/domain.ts StationEvent). Every event for a unit carries
/// the same UnitId - the correlation id created when NTID + Asset ID are joined.
/// </summary>
public record StationEvent
{
    [JsonPropertyName("id")] public string Id { get; init; } = Guid.NewGuid().ToString();
    [JsonPropertyName("type")] public string Type { get; init; } = "";
    [JsonPropertyName("at")] public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
    [JsonPropertyName("unitId")] public string? UnitId { get; init; }
    [JsonPropertyName("assetId")] public string? AssetId { get; init; }
    [JsonPropertyName("ntid")] public string? Ntid { get; init; }
    [JsonPropertyName("wipId")] public long? WipId { get; init; }
    [JsonPropertyName("material")] public string? Material { get; init; }
    [JsonPropertyName("product")] public string? Product { get; init; }
    [JsonPropertyName("program")] public string? Program { get; init; }
    [JsonPropertyName("step")] public int? Step { get; init; }
    [JsonPropertyName("stepComment")] public string? StepComment { get; init; }
    [JsonPropertyName("check")] public string? Check { get; init; }
    [JsonPropertyName("result")] public string? Result { get; init; }
    [JsonPropertyName("value")] public string? Value { get; init; }
    [JsonPropertyName("attempt")] public int? Attempt { get; init; }
    [JsonPropertyName("failures")] public int? Failures { get; init; }
    [JsonPropertyName("measurements")] public IReadOnlyDictionary<string, string?>? Measurements { get; init; }
    [JsonPropertyName("imagePath")] public string? ImagePath { get; init; }
    [JsonPropertyName("faultCode")] public string? FaultCode { get; init; }
    [JsonPropertyName("faultTitle")] public string? FaultTitle { get; init; }
    [JsonPropertyName("integrationPoint")] public string? IntegrationPoint { get; init; }
    [JsonPropertyName("severity")] public string? Severity { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }
    [JsonPropertyName("phase")] public string? Phase { get; init; }
    [JsonPropertyName("links")] public IReadOnlyDictionary<string, string>? Links { get; init; }
    [JsonPropertyName("appVersion")] public string? AppVersion { get; init; }

    public static class Types
    {
        public const string UnitStarted = "unit_started";
        public const string StepChanged = "step_changed";
        public const string CheckResult = "check_result";
        public const string UnitConfirmed = "unit_confirmed";
        public const string UnitNotConfirmed = "unit_not_confirmed";
        public const string UnitAbandoned = "unit_abandoned";
        public const string Escalated = "escalated";
        public const string Fault = "fault";
        public const string Heartbeat = "heartbeat";
    }
}

/// <summary>Where the station sequence sends its events (the dashboard outbox, or nowhere).</summary>
public interface IStationEventSink
{
    void Emit(StationEvent e);
}

public sealed class NullStationEventSink : IStationEventSink
{
    public static readonly NullStationEventSink Instance = new();
    public void Emit(StationEvent e) { }
}
