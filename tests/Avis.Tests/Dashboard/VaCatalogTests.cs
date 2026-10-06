using System.Net;
using Avis.Dashboard;
using Avis.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Avis.Tests.Dashboard;

public sealed class VaCatalogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "avis-va-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    private const string MapJson = """
        {
          "version": "v1",
          "generatedAt": "2026-10-06T00:00:00Z",
          "products": [
            {
              "productId": "p1", "product": "CVG300", "partNumbers": ["PN-1001"], "docId": "doc1", "docVersion": 2,
              "title": "CVG300 VA", "pdfOnly": false,
              "pages": [
                { "n": 1, "file": "page-1.png", "url": "/api/station/va-file/doc1/page-1.png" },
                { "n": 2, "file": "page-2.png", "url": "/api/station/va-file/doc1/page-2.png" },
                { "n": 3, "file": "page-3.png", "url": "/api/station/va-file/doc1/page-3.png" }
              ],
              "steps": [ { "step": 2, "pages": [2] }, { "step": 5, "pages": [2, 3] } ],
              "defaultPages": [1]
            }
          ]
        }
        """;

    private VaCatalog NewCatalog() => new(_dir, NullLogger<VaCatalog>.Instance);

    private (VaCacheSync Sync, FakeHttpMessageHandler Http) NewSync(VaCatalog catalog, Func<HttpRequestMessage, HttpResponseMessage>? handler = null)
    {
        var http = new FakeHttpMessageHandler(handler ?? (req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == "/api/station/va-map")
            {
                if (req.Headers.TryGetValues("If-None-Match", out var tags) && tags.Contains("\"v1\""))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotModified);
                }
                var ok = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(MapJson) };
                ok.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v1\"");
                return ok;
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) };
        }));
        var options = new DashboardOptions { BaseUrl = "http://pi:3230", ApiKey = "k", StationName = "S1" };
        var client = new DashboardClient(new HttpClient(http), options, NullLogger<DashboardClient>.Instance);
        return (new VaCacheSync(client, catalog, NullLogger<VaCacheSync>.Instance), http);
    }

    [Fact]
    public async Task Sync_DownloadsPages_WritesStepPages_AndResolves()
    {
        var catalog = NewCatalog();
        var (sync, http) = NewSync(catalog);

        Assert.True(await sync.SyncAsync(CancellationToken.None));

        Assert.Equal("k", http.Requests[0].Headers.GetValues(DashboardClient.KeyHeader).Single());
        Assert.Equal("S1", http.Requests[0].Headers.GetValues(DashboardClient.StationHeader).Single());
        Assert.True(File.Exists(Path.Combine(_dir, "doc1", "files", "page-3.png")));

        var step2 = catalog.Resolve("PN-1001", null, null, 2);
        Assert.Equal(Path.Combine(_dir, "doc1", "html", "step-2.html"), step2);
        var html = File.ReadAllText(Path.Combine(_dir, "doc1", "html", "step-5.html"));
        Assert.Contains("../files/page-2.png", html);
        Assert.Contains("../files/page-3.png", html);
        Assert.DoesNotContain("page-1.png", html);
    }

    [Fact]
    public async Task UnmappedStep_FallsBackToDefaultPages()
    {
        var catalog = NewCatalog();
        await NewSync(catalog).Sync.SyncAsync(CancellationToken.None);

        Assert.EndsWith("default.html", catalog.Resolve("PN-1001", null, null, 9));
        Assert.EndsWith("default.html", catalog.Resolve("PN-1001", null, null, null));
    }

    [Fact]
    public async Task Product_IsMatchedByPartNumber_ThenModel_ThenProgram()
    {
        var catalog = NewCatalog();
        await NewSync(catalog).Sync.SyncAsync(CancellationToken.None);

        Assert.Equal("CVG300", catalog.ProductFor("pn-1001", null, null));
        Assert.Equal("CVG300", catalog.ProductFor("OTHER", "cvg300", null));
        Assert.Equal("CVG300", catalog.ProductFor(null, null, "CVG300"));
        Assert.Null(catalog.ProductFor("OTHER", "OTHER", "OTHER"));
        Assert.Null(catalog.Resolve("OTHER", null, null, 2)); // -> station falls back to the INI
    }

    [Fact]
    public async Task SecondSync_IsNotModified_AndCacheSurvivesRestart()
    {
        var catalog = NewCatalog();
        var (sync, http) = NewSync(catalog);
        await sync.SyncAsync(CancellationToken.None);
        var calls = http.Requests.Count;

        Assert.False(await sync.SyncAsync(CancellationToken.None));
        Assert.Equal(calls + 1, http.Requests.Count); // only the map request, no downloads

        var restarted = NewCatalog();
        Assert.Equal("v1", restarted.Version);
        Assert.Equal("\"v1\"", restarted.ETag);
        Assert.NotNull(restarted.Resolve("PN-1001", null, null, 2));
    }

    [Fact]
    public async Task DashboardDown_KeepsTheCachedMap()
    {
        var catalog = NewCatalog();
        await NewSync(catalog).Sync.SyncAsync(CancellationToken.None);

        var (down, _) = NewSync(catalog, _ => throw new HttpRequestException("unreachable"));
        await Assert.ThrowsAsync<DashboardApiException>(() => down.SyncAsync(CancellationToken.None));

        Assert.NotNull(catalog.Resolve("PN-1001", null, null, 2));
    }

    [Fact]
    public void PdfOnly_OpensThePdfAtTheMappedPage()
    {
        var product = new VaProduct("P", new() { "PN" }, "d", 1, "t", PdfOnly: true,
            Pages: new() { new(1, "original.pdf", "u"), new(2, "original.pdf", "u") },
            Steps: new() { new(3, new() { 2 }) },
            DefaultPages: new());
        var docDir = Path.Combine(_dir, "d");

        VaCatalog.WriteHtmlPages(docDir, product);

        var html = File.ReadAllText(Path.Combine(docDir, "html", "step-3.html"));
        Assert.Contains("../files/original.pdf#page=2", html);
        Assert.False(File.Exists(Path.Combine(docDir, "html", "default.html")));
    }
}
