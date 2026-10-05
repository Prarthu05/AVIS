using System.Text.Json;

namespace Avis.Camera;

/// <summary>
/// Pulls the Asset ID (read by a Code/Character Reading task) out of a JabilEye
/// execution result. Pure logic, no HTTP - separated from JabilEyeCameraClient
/// so it's directly testable against example result shapes without a fake server.
/// </summary>
public static class AssetIdExtractor
{
    public static string? Extract(JabilEyeExecutionResult result, int assetIdToolCode)
    {
        var task = result.Jobs
            .SelectMany(job => job.Tasks)
            .FirstOrDefault(t => t.TaskTool == assetIdToolCode);

        if (task?.TaskOutput is not { } output)
        {
            return null;
        }

        // task_output's shape isn't consistent: the API doc's own example shows a
        // bare string ("DEVSRV000010"), but a real production integration reads it
        // as {"results":[{"format","region","text","quality"}]} (or, for an older
        // JabilEye API version, {"results":[{"format","text"}]} with no region/
        // quality) and pulls out results[0].text. Handle both rather than assume -
        // which shape our specific camera/program actually returns needs
        // confirming against a real capture.
        if (output.ValueKind == JsonValueKind.String)
        {
            return NullIfBlank(output.GetString());
        }

        if (output.ValueKind == JsonValueKind.Object &&
            output.TryGetProperty("results", out var results) &&
            results.ValueKind == JsonValueKind.Array &&
            results.GetArrayLength() > 0 &&
            results[0].TryGetProperty("text", out var text) &&
            text.ValueKind == JsonValueKind.String)
        {
            return NullIfBlank(text.GetString());
        }

        return null;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
