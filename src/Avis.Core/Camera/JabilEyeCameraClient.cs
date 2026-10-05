using System.Net.Http.Json;
using System.Text.Json;
using Avis.Configuration;
using Microsoft.Extensions.Logging;

namespace Avis.Camera;

/// <summary>
/// Talks to one JabilEye vision camera's local REST API (JabilEye Software
/// Reference Guide, Chapter 8 "Software Integration"). Unlike iFactory this
/// API is unauthenticated - it's a local-network industrial device, not a
/// corporate gateway - so there's no token handling here.
/// </summary>
public class JabilEyeCameraClient
{
    private readonly HttpClient _httpClient;
    private readonly JabilEyeCameraOptions _options;
    private readonly ILogger<JabilEyeCameraClient> _logger;

    public JabilEyeCameraClient(HttpClient httpClient, JabilEyeCameraOptions options, ILogger<JabilEyeCameraClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public Task OpenProgramAsync(string programName, CancellationToken ct = default) =>
        PostAsync($"/api/public/v1.0/programs/{Uri.EscapeDataString(programName)}/open", new { }, ct);

    /// <summary>Starts continuous Live Trigger mode (program's Run Mode must be set to "Live").</summary>
    public Task StartLiveTriggerAsync(CancellationToken ct = default) =>
        PostAsync(ExecutePath, new { state = 2 }, ct);

    public Task StopLiveTriggerAsync(CancellationToken ct = default) =>
        PostAsync(ExecutePath, new { state = 1 }, ct);

    /// <summary>Fires one Program Trigger execution (program's Run Mode must be set to "Program Trigger").</summary>
    public Task TriggerProgramAsync(string? assemblySerialNumber = null, string? operatorId = null, CancellationToken ct = default) =>
        PostAsync(ExecutePath, BuildTriggerBody(assemblySerialNumber, operatorId), ct);

    /// <summary>"Job Start" - begins Job Trigger mode and clears prior results on the operator UI.</summary>
    public Task StartJobAsync(string? assemblySerialNumber = null, string? operatorId = null, CancellationToken ct = default) =>
        PostAsync(ExecutePath, BuildTriggerBody(assemblySerialNumber, operatorId, includeStartState: true), ct);

    public Task TriggerJobByIndexAsync(int jobIndex, CancellationToken ct = default) =>
        PostAsync(ExecutePath, new { job_index = jobIndex }, ct);

    public Task TriggerJobByIdAsync(string jobId, CancellationToken ct = default) =>
        PostAsync(ExecutePath, new { job_id = jobId }, ct);

    /// <summary>"Job End" - JabilEye compiles all last-run results, ready for GetResultsAsync.</summary>
    public Task EndJobAsync(CancellationToken ct = default) =>
        PostAsync(ExecutePath, new { state = 1 }, ct);

    public async Task<JabilEyeExecutionResult> GetResultsAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl("/api/public/v1.0/programs/0/results"));
        request.Headers.ConnectionClose = true;
        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return JabilEyeExecutionResult.FromJson(doc.RootElement.Clone());
    }

    private const string ExecutePath = "/api/public/v1.0/programs/0/execute";

    private static object BuildTriggerBody(string? assemblySerialNumber, string? operatorId, bool includeStartState = false)
    {
        var dataCollection = new List<object>();
        if (assemblySerialNumber is not null)
        {
            dataCollection.Add(new { key = "JE_ASSEMBLY_SERIAL_NUMBER", value = assemblySerialNumber });
        }
        if (operatorId is not null)
        {
            dataCollection.Add(new { key = "JE_OPERATOR_ID", value = operatorId });
        }

        return (dataCollection.Count, includeStartState) switch
        {
            (0, false) => new { },
            (0, true) => new { state = 2 },
            (_, false) => new { data_collection = dataCollection },
            (_, true) => new { data_collection = dataCollection, state = 2 },
        };
    }

    private async Task PostAsync(string path, object body, CancellationToken ct)
    {
        _logger.LogDebug("POST {Path} to JabilEye camera {HostName}", path, _options.HostName);
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl(path)) { Content = JsonContent.Create(body) };
        // Confirmed via live testing: this camera's embedded HTTP server doesn't
        // reliably support connection reuse across requests - a connection pooled
        // from a prior call produced "response ended prematurely" errors on the
        // next one. Force a fresh connection per request instead of trusting
        // HttpClient's default pooling, which matters a lot for GetResultsAsync
        // since CameraWorker polls this repeatedly.
        request.Headers.ConnectionClose = true;
        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    private string BuildUrl(string path) => $"http://{_options.HostName}{path}";
}
