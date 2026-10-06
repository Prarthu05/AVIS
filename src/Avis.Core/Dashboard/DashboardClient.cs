using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Avis.Dashboard;

public class DashboardApiException : Exception
{
    public DashboardApiException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>Station-side client for the dashboard's /api/station/* endpoints.</summary>
public class DashboardClient
{
    public const string KeyHeader = "x-avis-station-key";
    public const string StationHeader = "x-avis-station";

    private readonly HttpClient _http;
    private readonly DashboardOptions _options;
    private readonly ILogger<DashboardClient> _logger;

    public DashboardClient(HttpClient http, DashboardOptions options, ILogger<DashboardClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task PostEventsAsync(IReadOnlyList<StationEvent> events, CancellationToken ct)
    {
        using var request = NewRequest(HttpMethod.Post, "/api/station/events");
        request.Content = JsonContent.Create(new { events }, options: EventOutbox.Json);
        using var response = await SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new DashboardApiException($"POST events -> HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
        }
    }

    /// <summary>The approved VA map; NotModified when <paramref name="etag"/> is still current.</summary>
    public async Task<(bool NotModified, string? Json, string? ETag)> GetVaMapAsync(string? etag, CancellationToken ct)
    {
        using var request = NewRequest(HttpMethod.Get, "/api/station/va-map");
        if (!string.IsNullOrEmpty(etag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        }
        using var response = await SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return (true, null, etag);
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new DashboardApiException($"GET va-map -> HTTP {(int)response.StatusCode}");
        }
        return (false, await response.Content.ReadAsStringAsync(ct), response.Headers.ETag?.ToString());
    }

    public async Task DownloadAsync(string relativeUrl, string targetPath, CancellationToken ct)
    {
        using var request = NewRequest(HttpMethod.Get, relativeUrl);
        using var response = await SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new DashboardApiException($"GET {relativeUrl} -> HTTP {(int)response.StatusCode}");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var tmp = targetPath + ".part";
        await using (var file = File.Create(tmp))
        {
            await response.Content.CopyToAsync(file, ct);
        }
        File.Move(tmp, targetPath, overwrite: true);
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, _options.BaseUrl.TrimEnd('/') + path);
        request.Headers.Add(KeyHeader, _options.ApiKey);
        request.Headers.Add(StationHeader, _options.StationName);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new DashboardApiException($"{request.Method} {request.RequestUri?.AbsolutePath} failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // HttpClient timeout - a dashboard problem, not a shutdown.
            throw new DashboardApiException($"{request.Method} {request.RequestUri?.AbsolutePath} timed out", ex);
        }
    }
}
