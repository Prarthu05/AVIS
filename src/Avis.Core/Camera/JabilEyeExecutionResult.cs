using System.Text.Json;

namespace Avis.Camera;

/// <summary>Result Code enumeration, JabilEye API/MQTT Return Definition &amp; Enumeration.</summary>
public enum JabilEyeResultCode
{
    // 0 also appears in practice (e.g. GetResults before any execution has run) but isn't
    // in the documented enumeration - treat any undefined value as "not yet executed".
    Untrained = 1,
    Pass = 2,
    Fail = 3,
    NotFound = 4,
    NotCompleted = 5,
}

public record JabilEyeTaskResult(
    string TaskId,
    string TaskName,
    int TaskTool,
    JabilEyeResultCode? ExecutionResult,
    JsonElement? TaskOutput,
    JsonElement Raw)
{
    public static JabilEyeTaskResult FromJson(JsonElement data) => new(
        TaskId: data.TryGetProperty("task_id", out var id) ? id.GetString() ?? "" : "",
        TaskName: data.TryGetProperty("task_name", out var name) ? name.GetString() ?? "" : "",
        TaskTool: data.TryGetProperty("task_tool", out var tool) && TryGetFlexibleInt32(tool, out var toolCode) ? toolCode : 0,
        ExecutionResult: ReadResultCode(data, "task_execution"),
        TaskOutput: data.TryGetProperty("task_output", out var output) ? output.Clone() : null,
        Raw: data);

    internal static JabilEyeResultCode? ReadResultCode(JsonElement data, string executionPropertyName)
    {
        if (!data.TryGetProperty(executionPropertyName, out var execution) || execution.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        if (!execution.TryGetProperty("execution_result", out var result) || !TryGetFlexibleInt32(result, out var code))
        {
            return null;
        }
        return Enum.IsDefined(typeof(JabilEyeResultCode), code) ? (JabilEyeResultCode)code : null;
    }

    // Confirmed via live testing: this camera sends at least some fields the API docs
    // show as JSON numbers (task_tool, execution_result) as JSON strings instead -
    // JsonElement.TryGetInt32() throws InvalidOperationException on a straight type
    // mismatch rather than just failing, so every numeric read here has to tolerate
    // either encoding. task_tool specifically comes back as "<code>-<NAME>" (e.g.
    // "1005-CODE_READING_V1"), not just the code as a plain numeric string - take
    // the leading digits rather than requiring the whole string to parse as a number.
    private static bool TryGetFlexibleInt32(JsonElement element, out int value)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value))
        {
            return true;
        }
        if (element.ValueKind == JsonValueKind.String)
        {
            var digits = new string((element.GetString() ?? "").TakeWhile(char.IsDigit).ToArray());
            if (digits.Length > 0 && int.TryParse(digits, out value))
            {
                return true;
            }
        }
        value = 0;
        return false;
    }
}

public record JabilEyeJobResult(
    string JobId,
    string JobName,
    JabilEyeResultCode? ExecutionResult,
    IReadOnlyList<JabilEyeTaskResult> Tasks,
    JsonElement Raw)
{
    public static JabilEyeJobResult FromJson(JsonElement data) => new(
        JobId: data.TryGetProperty("job_id", out var id) ? id.GetString() ?? "" : "",
        JobName: data.TryGetProperty("job_name", out var name) ? name.GetString() ?? "" : "",
        ExecutionResult: JabilEyeTaskResult.ReadResultCode(data, "job_execution"),
        Tasks: data.TryGetProperty("job_tasks", out var tasks) && tasks.ValueKind == JsonValueKind.Array
            ? tasks.EnumerateArray().Select(JabilEyeTaskResult.FromJson).ToList()
            : new List<JabilEyeTaskResult>(),
        Raw: data);
}

public record JabilEyeExecutionResult(
    string ProgramId,
    string ProgramFilename,
    string ExecutionId,
    JabilEyeResultCode? ExecutionResult,
    IReadOnlyList<JabilEyeJobResult> Jobs,
    JsonElement Raw)
{
    public static JabilEyeExecutionResult FromJson(JsonElement data) => new(
        ProgramId: data.TryGetProperty("program_id", out var id) ? id.GetString() ?? "" : "",
        ProgramFilename: data.TryGetProperty("program_filename", out var fn) ? fn.GetString() ?? "" : "",
        ExecutionId: data.TryGetProperty("program_execution", out var exec) && exec.TryGetProperty("execution_id", out var execId)
            ? execId.GetString() ?? ""
            : "",
        ExecutionResult: JabilEyeTaskResult.ReadResultCode(data, "program_execution"),
        Jobs: data.TryGetProperty("program_jobs", out var jobs) && jobs.ValueKind == JsonValueKind.Array
            ? jobs.EnumerateArray().Select(JabilEyeJobResult.FromJson).ToList()
            : new List<JabilEyeJobResult>(),
        Raw: data);
}
