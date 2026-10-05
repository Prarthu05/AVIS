using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Avis.IFactory;

public class TokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _username;
    private readonly string _password;
    private readonly int _refreshMarginSeconds;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _cachedToken;
    private double _cachedExpiresAt;

    public TokenProvider(
        HttpClient httpClient,
        string baseUrl,
        string username,
        string password,
        int refreshMarginSeconds,
        ILogger logger)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _username = username;
        _password = password;
        _refreshMarginSeconds = refreshMarginSeconds;
        _logger = logger;
    }

    public async Task<string> GetTokenAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (!forceRefresh && _cachedToken is not null && _cachedExpiresAt - _refreshMarginSeconds > now)
            {
                return _cachedToken;
            }
            return await FetchTokenAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task InvalidateAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _cachedToken = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string> FetchTokenAsync(CancellationToken ct)
    {
        var encryptedPassword = PasswordEncryption.Encrypt(_password);
        var url = $"{_baseUrl}/api/tokens?userLogin={Uri.EscapeDataString(_username)}&password={Uri.EscapeDataString(encryptedPassword)}";

        // Every failure here is surfaced as IFactoryApiException so callers that
        // handle "iFactory unreachable" actually see it - a raw HttpRequestException
        // would be misreported by whatever generic handler catches it, and a raw
        // timeout (TaskCanceledException) looks like a shutdown request.
        string body;
        try
        {
            using var response = await _httpClient.GetAsync(url, ct);
            body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new IFactoryApiException(
                    $"Token request -> HTTP {(int)response.StatusCode}", (int)response.StatusCode, body);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new IFactoryApiException($"Token request failed: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new IFactoryApiException($"Token request timed out after {_httpClient.Timeout.TotalSeconds:0.#}s");
        }

        string? token;
        try
        {
            using var doc = JsonDocument.Parse(body);
            token = doc.RootElement.TryGetProperty("token", out var tokenElement) ? tokenElement.GetString() : null;
        }
        catch (JsonException)
        {
            throw new IFactoryApiException("Token response was not valid JSON", payload: body);
        }
        if (string.IsNullOrEmpty(token))
        {
            throw new IFactoryApiException("Token response did not include a 'token' field", payload: body);
        }

        var expiresAt = JwtHelper.DecodeExpiry(token) ?? (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600);
        _cachedToken = token;
        _cachedExpiresAt = expiresAt;
        _logger.LogInformation("Fetched new iFactory token for user {User} (expires at {ExpiresAt})", _username, expiresAt);
        return token;
    }
}
