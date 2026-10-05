using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Avis.LightGuide;

public class LightGuideApiException : Exception
{
    public LightGuideApiException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>Where LightGuide variable snapshots come from - the real Web API, or the simulator.</summary>
public interface ILightGuideVariableSource
{
    Task<IReadOnlyDictionary<string, string?>> GetVariablesAsync(IReadOnlyList<string> names, CancellationToken ct = default);
}

/// <summary>Thin client for the LightGuide Web API endpoints AVIS uses.</summary>
public class LightGuideClient : ILightGuideVariableSource
{
    private readonly HttpClient _httpClient;
    private readonly LightGuideOptions _options;
    private readonly ILogger<LightGuideClient> _logger;

    public LightGuideClient(HttpClient httpClient, LightGuideOptions options, ILogger<LightGuideClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    /// <summary>GET /Variable/{name1},{name2},... - returns name -> value (case-insensitive; missing variables are absent).</summary>
    public async Task<IReadOnlyDictionary<string, string?>> GetVariablesAsync(IReadOnlyList<string> names, CancellationToken ct = default)
    {
        if (names.Count == 0)
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        var path = "/Variable/" + string.Join(",", names.Select(Uri.EscapeDataString));
        var body = await SendAsync(HttpMethod.Get, path, null, ct);
        return LightGuideVariableParser.Parse(body, names);
    }

    /// <summary>Writes a line to the LightGuide log (POST /Application/Message or /Application/Error).</summary>
    public async Task LogAsync(string message, bool isError, CancellationToken ct = default)
    {
        var content = new StringContent(JsonSerializer.Serialize(message), Encoding.UTF8, "application/json");
        await SendAsync(HttpMethod.Post, isError ? "/Application/Error" : "/Application/Message", content, ct);
    }

    private async Task<string> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        var url = _options.BaseUrl.TrimEnd('/') + path;
        try
        {
            using var request = new HttpRequestMessage(method, url) { Content = content };
            using var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new LightGuideApiException($"{method} {path} -> HTTP {(int)response.StatusCode}: {body}");
            }
            return body;
        }
        catch (HttpRequestException ex)
        {
            throw new LightGuideApiException($"{method} {path} failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new LightGuideApiException($"{method} {path} timed out after {_httpClient.Timeout.TotalSeconds:0.#}s", ex);
        }
    }
}

/// <summary>
/// The LightGuide wiki documents the GET /Variable request but not its response
/// body, so this accepts every reasonable shape rather than guessing one:
/// <list type="bullet">
/// <item>[{"Name":"X","Value":"1"}, ...] (same shape as the Set Variables body)</item>
/// <item>{"X":"1","Y":"2"}</item>
/// <item>{"Name":"X","Value":"1"} (single variable)</item>
/// <item>["1","2"] (values in request order)</item>
/// <item>"1" / 1 / plain text (single variable)</item>
/// </list>
/// </summary>
public static class LightGuideVariableParser
{
    public static IReadOnlyDictionary<string, string?> Parse(string body, IReadOnlyList<string> requestedNames)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var trimmed = body.Trim();

        JsonDocument? doc = null;
        try
        {
            if (trimmed.Length > 0)
            {
                doc = JsonDocument.Parse(trimmed);
            }
        }
        catch (JsonException)
        {
            doc = null;
        }

        if (doc is null)
        {
            // Plain text: only unambiguous for a single variable.
            if (requestedNames.Count == 1)
            {
                result[requestedNames[0]] = trimmed;
            }
            else
            {
                var parts = trimmed.Split(',');
                if (parts.Length == requestedNames.Count)
                {
                    for (var i = 0; i < parts.Length; i++)
                    {
                        result[requestedNames[i]] = parts[i].Trim();
                    }
                }
            }
            return result;
        }

        using (doc)
        {
            var root = doc.RootElement;
            switch (root.ValueKind)
            {
                case JsonValueKind.Array:
                    var index = 0;
                    foreach (var item in root.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object && TryReadNameValue(item, out var name, out var value))
                        {
                            result[name] = value;
                        }
                        else if (item.ValueKind != JsonValueKind.Object && index < requestedNames.Count)
                        {
                            result[requestedNames[index]] = AsString(item);
                        }
                        index++;
                    }
                    break;

                case JsonValueKind.Object:
                    if (TryReadNameValue(root, out var singleName, out var singleValue))
                    {
                        result[singleName] = singleValue;
                    }
                    else
                    {
                        foreach (var property in root.EnumerateObject())
                        {
                            result[property.Name] = property.Value.ValueKind == JsonValueKind.Object
                                && TryGetPropertyIgnoreCase(property.Value, "Value", out var nested)
                                    ? AsString(nested)
                                    : AsString(property.Value);
                        }
                    }
                    break;

                default:
                    if (requestedNames.Count == 1)
                    {
                        result[requestedNames[0]] = AsString(root);
                    }
                    break;
            }
        }
        return result;
    }

    private static bool TryReadNameValue(JsonElement obj, out string name, out string? value)
    {
        name = "";
        value = null;
        if (!TryGetPropertyIgnoreCase(obj, "Name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        name = nameElement.GetString() ?? "";
        value = TryGetPropertyIgnoreCase(obj, "Value", out var valueElement) ? AsString(valueElement) : null;
        return name.Length > 0;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement obj, string propertyName, out JsonElement value)
    {
        foreach (var property in obj.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string? AsString(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => element.GetRawText(),
    };
}
