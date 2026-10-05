using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Avis.Configuration;
using Microsoft.Extensions.Logging;

namespace Avis.IFactory;

public class IFactoryClient : IMesClient
{
    private readonly HttpClient _httpClient;
    private readonly IFactoryOptions _options;
    private readonly ILogger<IFactoryClient> _logger;
    private readonly TokenProvider _tokenProvider;

    public IFactoryClient(HttpClient httpClient, IFactoryOptions options, ILogger<IFactoryClient> logger, TokenProvider? tokenProvider = null)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
        _tokenProvider = tokenProvider ?? new TokenProvider(
            httpClient, options.BaseUrl, options.Username, options.Password, options.TokenRefreshMarginSeconds, logger);
    }

    public async Task VerifyCredentialsAsync(CancellationToken ct = default)
    {
        await _tokenProvider.GetTokenAsync(ct: ct);
    }

    public async Task<Wip?> GetWipBySerialAsync(string serialNumber, CancellationToken ct = default)
    {
        using var response = await SendAsync(
            HttpMethod.Get, "/api/wips", queryParams: new() { ["serialNumber"] = serialNumber }, ct: ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("wips", out var wipsElement) || wipsElement.GetArrayLength() == 0)
        {
            return null;
        }
        if (wipsElement.GetArrayLength() > 1)
        {
            _logger.LogWarning("Multiple WIPs found for serial {Serial}; using the first one", serialNumber);
        }
        return Wip.FromJson(wipsElement[0].Clone());
    }

    public async Task<long> StartWipAsync(long wipId, string resourceName, string? operatorId = null, CancellationToken ct = default)
    {
        using var response = await SendAsync(
            HttpMethod.Post, $"/api/wips/{wipId}/processSteps/start",
            operatorId: operatorId, jsonBody: new { resourceName }, ct: ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("wipProcessStepHistoryId").GetInt64();
    }

    public async Task<JsonElement> CompleteWipProcessStepAsync(long wipId, long wipProcessStepHistoryId, string? operatorId = null, CancellationToken ct = default)
    {
        using var response = await SendAsync(
            HttpMethod.Post, $"/api/wips/{wipId}/processSteps/{wipProcessStepHistoryId}/complete", operatorId: operatorId, ct: ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    public async Task<JsonElement> AbortWipProcessStepAsync(long wipId, long wipProcessStepHistoryId, string? resourceName = null, string? operatorId = null, CancellationToken ct = default)
    {
        object body = resourceName is not null ? new { resourceName } : new { };
        using var response = await SendAsync(
            HttpMethod.Post, $"/api/wips/{wipId}/processSteps/{wipProcessStepHistoryId}/abort", operatorId: operatorId, jsonBody: body, ct: ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseBody);
        return doc.RootElement.Clone();
    }

    public async Task InsertSymptomAsync(long wipId, long wipProcessStepHistoryId, string symptomLabel, string? failureMessage = null, string? operatorId = null, CancellationToken ct = default)
    {
        var body = failureMessage is not null
            ? new Dictionary<string, string> { ["symptomLabel"] = symptomLabel, ["failureMessage"] = failureMessage }
            : new Dictionary<string, string> { ["symptomLabel"] = symptomLabel };

        using var response = await SendAsync(
            HttpMethod.Post, $"/api/wips/{wipId}/processSteps/{wipProcessStepHistoryId}/symptoms", operatorId: operatorId, jsonBody: body, ct: ct);
    }

    public async Task AddWipAttributeAsync(long wipId, string name, string attributeType, string value, string? operatorId = null, CancellationToken ct = default)
    {
        var body = new
        {
            wipAttributeName = name,
            wipAttributeType = attributeType,
            wipAttributeValue = value,
        };
        using var response = await SendAsync(HttpMethod.Post, $"/api/wips/{wipId}/attributes", operatorId: operatorId, jsonBody: body, ct: ct);
    }

    public async Task<JsonElement> HoldWipAsync(string serialNumber, string reasonCode, string comment, string? material = null, string? operatorId = null, CancellationToken ct = default)
    {
        var body = new Dictionary<string, string?>
        {
            ["SerialNumber"] = serialNumber,
            ["ReasonCode"] = reasonCode,
            ["Comment"] = comment,
        };
        if (material is not null)
        {
            body["Material"] = material;
        }
        using var response = await SendAsync(HttpMethod.Post, "/api/WipHold", operatorId: operatorId, jsonBody: body, ct: ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseBody);
        return doc.RootElement.Clone();
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string? operatorId = null,
        object? jsonBody = null,
        Dictionary<string, string>? queryParams = null,
        CancellationToken ct = default)
    {
        var attempts = Math.Max(1, _options.Retry.MaxAttempts);
        string? lastError = null;
        var needTokenRefresh = false;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var token = await _tokenProvider.GetTokenAsync(needTokenRefresh, ct);
            needTokenRefresh = false;

            using var request = new HttpRequestMessage(method, BuildUrl(path, queryParams));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (!string.IsNullOrEmpty(operatorId))
            {
                request.Headers.Add(_options.OperatorOverrideHeader, operatorId);
            }
            if (jsonBody is not null)
            {
                request.Content = JsonContent.Create(jsonBody);
            }

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, ct);
            }
            catch (HttpRequestException ex)
            {
                lastError = ex.Message;
                _logger.LogWarning("iFactory {Method} {Path} failed (attempt {Attempt}/{Attempts}): {Error}", method, path, attempt, attempts, ex.Message);
                await SleepBackoffAsync(attempt, ct);
                continue;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // HttpClient.Timeout surfaces as TaskCanceledException, not
                // HttpRequestException. Left uncaught it looks exactly like a
                // shutdown request to every caller's OperationCanceledException
                // handler, silently stopping the worker loop - treat it as the
                // retryable network failure it is instead.
                lastError = $"timed out after {_httpClient.Timeout.TotalSeconds:0.#}s";
                _logger.LogWarning("iFactory {Method} {Path} timed out (attempt {Attempt}/{Attempts})", method, path, attempt, attempts);
                await SleepBackoffAsync(attempt, ct);
                continue;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                lastError = "401 Unauthorized";
                needTokenRefresh = true;
                response.Dispose();
                _logger.LogWarning("iFactory {Method} {Path} returned 401; refreshing token (attempt {Attempt}/{Attempts})", method, path, attempt, attempts);
                await SleepBackoffAsync(attempt, ct);
                continue;
            }

            if ((int)response.StatusCode >= 500)
            {
                lastError = $"HTTP {(int)response.StatusCode}";
                response.Dispose();
                _logger.LogWarning("iFactory {Method} {Path} returned {Status} (attempt {Attempt}/{Attempts})", method, path, (int)response.StatusCode, attempt, attempts);
                await SleepBackoffAsync(attempt, ct);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                var status = (int)response.StatusCode;
                response.Dispose();
                throw new IFactoryApiException($"{method} {path} -> HTTP {status}: {errorBody}", status, errorBody);
            }

            return response;
        }

        throw new IFactoryApiException($"{method} {path} failed after {attempts} attempts: {lastError}");
    }

    private Task SleepBackoffAsync(int attempt, CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(_options.Retry.BackoffBaseSeconds * attempt);
        return Task.Delay(delay, ct);
    }

    private string BuildUrl(string path, Dictionary<string, string>? queryParams)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}{path}";
        if (queryParams is { Count: > 0 })
        {
            var query = string.Join("&", queryParams.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
            url += $"?{query}";
        }
        return url;
    }
}
