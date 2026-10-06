using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Avis.Dashboard;

/// <summary>Resolves which visual aid to show for the part and LightGuide step at hand.</summary>
public interface IVisualAidResolver
{
    /// <summary>
    /// Local file (or URL) to show, or null when this resolver has nothing for
    /// the product - the caller then falls back to the INI [VISUAL_ADD] entries.
    /// </summary>
    string? Resolve(string? material, string? model, string? program, int? step);

    /// <summary>The dashboard product name the part matches (for analytics), or null.</summary>
    string? ProductFor(string? material, string? model, string? program);
}

public sealed class NoVisualAids : IVisualAidResolver
{
    public static readonly NoVisualAids Instance = new();
    public string? Resolve(string? material, string? model, string? program, int? step) => null;
    public string? ProductFor(string? material, string? model, string? program) => null;
}

// ---------- The map the dashboard serves (dashboard/src/lib/vaStore.ts StationVaMap) ----------
public record VaMap(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("products")] List<VaProduct> Products);

public record VaProduct(
    [property: JsonPropertyName("product")] string Product,
    [property: JsonPropertyName("partNumbers")] List<string> PartNumbers,
    [property: JsonPropertyName("docId")] string DocId,
    [property: JsonPropertyName("docVersion")] int DocVersion,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("pdfOnly")] bool PdfOnly,
    [property: JsonPropertyName("pages")] List<VaPageFile> Pages,
    [property: JsonPropertyName("steps")] List<VaStep> Steps,
    [property: JsonPropertyName("defaultPages")] List<int> DefaultPages);

public record VaPageFile(
    [property: JsonPropertyName("n")] int N,
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("url")] string Url);

public record VaStep(
    [property: JsonPropertyName("step")] int Step,
    [property: JsonPropertyName("pages")] List<int> Pages);

/// <summary>
/// The dashboard's approved VAs, cached on the station PC:
/// <code>
/// va-cache/map.json                      the last map downloaded (+ etag)
/// va-cache/{docId}/files/page-2.png      page images
/// va-cache/{docId}/html/step-2.html      one page per step (images stacked), what the VA screen opens
/// va-cache/{docId}/html/default.html     default pages
/// </code>
/// Lookup is keyed by (product, step) - step numbers belong to each product's
/// own LightGuide program. The product is matched by WIP material (part
/// number) first, then by model / LightGuide program name.
/// </summary>
public class VaCatalog : IVisualAidResolver
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly string _root;
    private readonly ILogger<VaCatalog> _logger;
    private volatile VaMap? _map;

    public VaCatalog(string cacheDirectory, ILogger<VaCatalog> logger)
    {
        _root = Path.GetFullPath(cacheDirectory);
        _logger = logger;
        LoadCached();
    }

    public string Root => _root;
    public string? Version => _map?.Version;
    public string? ETag { get; private set; }
    public int ProductCount => _map?.Products.Count ?? 0;

    public string? ProductFor(string? material, string? model, string? program) => Match(material, model, program)?.Product;

    public string? Resolve(string? material, string? model, string? program, int? step)
    {
        var product = Match(material, model, program);
        if (product is null)
        {
            return null;
        }
        var htmlDir = Path.Combine(_root, product.DocId, "html");
        if (step is not null && product.Steps.Any(s => s.Step == step))
        {
            return PathIfExists(Path.Combine(htmlDir, $"step-{step}.html"));
        }
        return product.DefaultPages.Count > 0 ? PathIfExists(Path.Combine(htmlDir, "default.html")) : null;
    }

    private VaProduct? Match(string? material, string? model, string? program)
    {
        var map = _map;
        if (map is null)
        {
            return null;
        }
        static bool Eq(string? a, string? b) => !string.IsNullOrWhiteSpace(a) && string.Equals(a.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
        return map.Products.FirstOrDefault(p => p.PartNumbers.Any(pn => Eq(material, pn)))
            ?? map.Products.FirstOrDefault(p => Eq(model, p.Product))
            ?? map.Products.FirstOrDefault(p => Eq(program, p.Product));
    }

    private static string? PathIfExists(string path) => File.Exists(path) ? path : null;

    /// <summary>Replaces the active map once its files are all on disk.</summary>
    public void Activate(VaMap map, string json, string? etag)
    {
        Directory.CreateDirectory(_root);
        var tmp = Path.Combine(_root, "map.json.tmp");
        File.WriteAllText(tmp, JsonSerializer.Serialize(new CachedMap(etag, json)));
        File.Move(tmp, Path.Combine(_root, "map.json"), overwrite: true);
        _map = map;
        ETag = etag;
    }

    public static VaMap? Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<VaMap>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void LoadCached()
    {
        try
        {
            var file = Path.Combine(_root, "map.json");
            if (!File.Exists(file))
            {
                return;
            }
            var cached = JsonSerializer.Deserialize<CachedMap>(File.ReadAllText(file));
            var map = cached is null ? null : Parse(cached.Json);
            if (map is not null)
            {
                _map = map;
                ETag = cached!.ETag;
                _logger.LogInformation("Loaded cached VA map {Version} ({Count} products)", map.Version, map.Products.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load the cached VA map");
        }
    }

    /// <summary>Writes the per-step pages the VA screen opens. Pure file output - unit tested.</summary>
    public static void WriteHtmlPages(string docDir, VaProduct product)
    {
        var html = Path.Combine(docDir, "html");
        Directory.CreateDirectory(html);
        foreach (var group in product.Steps.GroupBy(s => s.Step))
        {
            var pages = group.SelectMany(s => s.Pages).Distinct().ToList();
            File.WriteAllText(Path.Combine(html, $"step-{group.Key}.html"), PageHtml(product, $"Step {group.Key}", pages));
        }
        if (product.DefaultPages.Count > 0)
        {
            File.WriteAllText(Path.Combine(html, "default.html"), PageHtml(product, "Overview", product.DefaultPages));
        }
    }

    private static string PageHtml(VaProduct product, string caption, IReadOnlyList<int> pages)
    {
        var files = pages.Select(n => product.Pages.FirstOrDefault(p => p.N == n)).Where(p => p is not null).ToList();
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>")
          .Append(WebUtility.HtmlEncode($"{product.Product} - {caption}"))
          .Append("</title><style>html,body{margin:0;background:#16181c;height:100%}")
          .Append(".one{height:100vh;display:flex;align-items:center;justify-content:center}")
          .Append(".one img{max-width:100%;max-height:100vh;object-fit:contain}")
          .Append(".many img{display:block;width:100%;margin:0 auto 8px;background:#fff}")
          .Append("iframe{border:0;width:100%;height:100vh}</style></head><body>");
        if (product.PdfOnly && files.Count > 0)
        {
            // No page images - show the PDF itself at the first mapped page.
            sb.Append("<iframe src=\"../files/").Append(WebUtility.HtmlEncode(files[0]!.File)).Append("#page=").Append(files[0]!.N).Append("\"></iframe>");
        }
        else
        {
            sb.Append(files.Count == 1 ? "<div class=\"one\">" : "<div class=\"many\">");
            foreach (var f in files)
            {
                sb.Append("<img src=\"../files/").Append(WebUtility.HtmlEncode(f!.File)).Append("\" alt=\"Page ").Append(f.N).Append("\">");
            }
            sb.Append("</div>");
        }
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private record CachedMap(string? ETag, string Json);
}

/// <summary>Downloads the approved VA map and any page files not cached yet, then activates it.</summary>
public class VaCacheSync
{
    private readonly DashboardClient _client;
    private readonly VaCatalog _catalog;
    private readonly ILogger<VaCacheSync> _logger;

    public VaCacheSync(DashboardClient client, VaCatalog catalog, ILogger<VaCacheSync> logger)
    {
        _client = client;
        _catalog = catalog;
        _logger = logger;
    }

    /// <summary>Returns true when a new map was activated.</summary>
    public async Task<bool> SyncAsync(CancellationToken ct)
    {
        var (notModified, json, etag) = await _client.GetVaMapAsync(_catalog.ETag, ct);
        if (notModified || json is null)
        {
            return false;
        }
        var map = VaCatalog.Parse(json) ?? throw new DashboardApiException("The dashboard returned an unreadable VA map");
        if (map.Version == _catalog.Version)
        {
            return false;
        }

        foreach (var product in map.Products)
        {
            var docDir = Path.Combine(_catalog.Root, product.DocId);
            foreach (var page in product.Pages.DistinctBy(p => p.File))
            {
                if (page.File.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || page.File.Contains(".."))
                {
                    continue;
                }
                var target = Path.Combine(docDir, "files", page.File);
                if (!File.Exists(target))
                {
                    await _client.DownloadAsync(page.Url, target, ct);
                }
            }
            VaCatalog.WriteHtmlPages(docDir, product);
        }

        _catalog.Activate(map, json, etag);
        PruneOldDocuments(map);
        _logger.LogInformation("VA map {Version} active: {Products}", map.Version,
            string.Join(", ", map.Products.Select(p => $"{p.Product} v{p.DocVersion}")));
        return true;
    }

    private void PruneOldDocuments(VaMap map)
    {
        var keep = map.Products.Select(p => p.DocId).ToHashSet();
        foreach (var dir in Directory.EnumerateDirectories(_catalog.Root))
        {
            if (!keep.Contains(Path.GetFileName(dir)))
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not remove old VA folder {Dir}", dir);
                }
            }
        }
    }
}
