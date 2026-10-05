using System.Text.Json;
using Avis.Camera;
using Xunit;

namespace Avis.Tests.Camera;

public class AssetIdExtractorTests
{
    private const int CodeReadingTool = 1005;

    private static JabilEyeTaskResult MakeTask(int tool, string? output) => new(
        TaskId: "task-1",
        TaskName: "Task1",
        TaskTool: tool,
        ExecutionResult: JabilEyeResultCode.Pass,
        TaskOutput: output is null ? null : JsonSerializer.SerializeToElement(output),
        Raw: default);

    private static JabilEyeTaskResult MakeTaskWithResultsOutput(int tool, JsonElement output) => new(
        TaskId: "task-1",
        TaskName: "Task1",
        TaskTool: tool,
        ExecutionResult: JabilEyeResultCode.Pass,
        TaskOutput: output,
        Raw: default);

    private static JabilEyeJobResult MakeJob(params JabilEyeTaskResult[] tasks) => new(
        JobId: "job-1", JobName: "Test1", ExecutionResult: JabilEyeResultCode.Pass, Tasks: tasks, Raw: default);

    private static JabilEyeExecutionResult MakeResult(params JabilEyeJobResult[] jobs) => new(
        ProgramId: "program-1", ProgramFilename: "prog", ExecutionId: "exec-1",
        ExecutionResult: JabilEyeResultCode.Pass, Jobs: jobs, Raw: default);

    [Fact]
    public void ReturnsOutputFromMatchingTool()
    {
        var result = MakeResult(MakeJob(MakeTask(CodeReadingTool, "DEVSRV000010")));

        Assert.Equal("DEVSRV000010", AssetIdExtractor.Extract(result, CodeReadingTool));
    }

    [Fact]
    public void IgnoresTasksWithDifferentToolCode()
    {
        var result = MakeResult(MakeJob(MakeTask(1003, "not-the-barcode")));

        Assert.Null(AssetIdExtractor.Extract(result, CodeReadingTool));
    }

    [Fact]
    public void SearchesAcrossMultipleJobs()
    {
        var result = MakeResult(
            MakeJob(MakeTask(1001, "unrelated")),
            MakeJob(MakeTask(CodeReadingTool, "DEVSRV000010")));

        Assert.Equal("DEVSRV000010", AssetIdExtractor.Extract(result, CodeReadingTool));
    }

    [Fact]
    public void ReturnsNullWhenTaskOutputMissing()
    {
        var result = MakeResult(MakeJob(MakeTask(CodeReadingTool, output: null)));

        Assert.Null(AssetIdExtractor.Extract(result, CodeReadingTool));
    }

    [Fact]
    public void ReturnsNullWhenNoJobsRanYet()
    {
        var result = MakeResult();

        Assert.Null(AssetIdExtractor.Extract(result, CodeReadingTool));
    }

    // Confirmed against a real Acura/JabilEye production integration: task_output
    // is not always the bare string the API doc's example shows - it can be
    // {"results":[{"format","region","text","quality"}]}.
    [Fact]
    public void ReturnsTextFromResultsObjectShape()
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            results = new[]
            {
                new { format = "CODE128", region = new { }, text = "DEVSRV000010", quality = 95 },
            },
        });
        var result = MakeResult(MakeJob(MakeTaskWithResultsOutput(CodeReadingTool, output)));

        Assert.Equal("DEVSRV000010", AssetIdExtractor.Extract(result, CodeReadingTool));
    }

    // Older JabilEye API versions return {"results":[{"format","text"}]} with no
    // region/quality - same extraction should still work.
    [Fact]
    public void ReturnsTextFromOlderResultsObjectShapeWithoutRegionOrQuality()
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            results = new[] { new { format = "CODE128", text = "DEVSRV000010" } },
        });
        var result = MakeResult(MakeJob(MakeTaskWithResultsOutput(CodeReadingTool, output)));

        Assert.Equal("DEVSRV000010", AssetIdExtractor.Extract(result, CodeReadingTool));
    }

    [Fact]
    public void ReturnsNullWhenResultsArrayIsEmpty()
    {
        var output = JsonSerializer.SerializeToElement(new { results = Array.Empty<object>() });
        var result = MakeResult(MakeJob(MakeTaskWithResultsOutput(CodeReadingTool, output)));

        Assert.Null(AssetIdExtractor.Extract(result, CodeReadingTool));
    }
}
