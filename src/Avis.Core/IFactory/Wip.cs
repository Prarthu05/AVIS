using System.Text.Json;

namespace Avis.IFactory;

public record Wip(long Id, string SerialNumber, string MaterialName, string WipStatus, JsonElement Raw)
{
    public static Wip FromJson(JsonElement data)
    {
        return new Wip(
            Id: data.GetProperty("id").GetInt64(),
            SerialNumber: data.TryGetProperty("serialNumber", out var sn) ? sn.GetString() ?? "" : "",
            MaterialName: data.TryGetProperty("materialName", out var mn) ? mn.GetString() ?? "" : "",
            WipStatus: data.TryGetProperty("wipStatus", out var ws) ? ws.GetString() ?? "" : "",
            Raw: data);
    }
}
