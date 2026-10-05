using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Avis.Configuration;
using Avis.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Avis.Tests.IFactory;

using IFactoryClient = Avis.IFactory.IFactoryClient;
using IFactoryApiException = Avis.IFactory.IFactoryApiException;

public class IFactoryClientTests
{
    private static IFactoryOptions MakeOptions() => new()
    {
        BaseUrl = "https://ifactory.example.test:60200",
        Username = "svc_account",
        Password = "pw",
        Retry = new RetryOptions { MaxAttempts = 3, BackoffBaseSeconds = 0.01 },
    };

    private static string Base64UrlEncode(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string MakeToken(double expDelta = 3600)
    {
        var exp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expDelta;
        var header = Base64UrlEncode("{\"alg\":\"none\"}");
        var payload = Base64UrlEncode($"{{\"exp\":{exp}}}");
        return $"{header}.{payload}.sig";
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body) =>
        new(status) { Content = JsonContent.Create(body) };

    private static IFactoryClient MakeClient(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler), MakeOptions(), NullLogger<IFactoryClient>.Instance);

    [Fact]
    public async Task GetWipBySerial_ReturnsWip_AndSendsBearerToken()
    {
        var token = MakeToken();
        var handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath == "/api/tokens")
            {
                Assert.Contains("userLogin=svc_account", req.RequestUri.Query);
                return JsonResponse(HttpStatusCode.OK, new { token });
            }

            Assert.Equal("/api/wips", req.RequestUri.AbsolutePath);
            Assert.Equal($"Bearer {token}", req.Headers.Authorization?.ToString());
            Assert.Contains("serialNumber=DEVSRV000010", req.RequestUri.Query);
            return JsonResponse(HttpStatusCode.OK, new
            {
                wips = new[] { new { id = 3934367, serialNumber = "DEVSRV000010", materialName = "AW1", wipStatus = "InProcess" } },
            });
        });

        var wip = await MakeClient(handler).GetWipBySerialAsync("DEVSRV000010");

        Assert.NotNull(wip);
        Assert.Equal(3934367, wip!.Id);
        Assert.Equal("InProcess", wip.WipStatus);
    }

    [Fact]
    public async Task GetWipBySerial_ReturnsNull_WhenNoWipsFound()
    {
        var token = MakeToken();
        var handler = new FakeHttpMessageHandler(req =>
            req.RequestUri!.AbsolutePath == "/api/tokens"
                ? JsonResponse(HttpStatusCode.OK, new { token })
                : JsonResponse(HttpStatusCode.OK, new { wips = Array.Empty<object>() }));

        var wip = await MakeClient(handler).GetWipBySerialAsync("UNKNOWN");

        Assert.Null(wip);
    }

    [Fact]
    public async Task StartWip_SendsOperatorOverrideHeader_AndResourceName()
    {
        var token = MakeToken();
        var handler = new FakeHttpMessageHandler(async req =>
        {
            if (req.RequestUri!.AbsolutePath == "/api/tokens")
            {
                return JsonResponse(HttpStatusCode.OK, new { token });
            }

            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("/api/wips/42/processSteps/start", req.RequestUri.AbsolutePath);
            Assert.Equal("Jabil\\4045423", req.Headers.GetValues("x-operator-override").Single());

            var bodyJson = await req.Content!.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(bodyJson);
            Assert.Equal("CVG200 AOI", doc.RootElement.GetProperty("resourceName").GetString());

            return JsonResponse(HttpStatusCode.OK, new { wipProcessStepHistoryId = 987 });
        });

        var historyId = await MakeClient(handler).StartWipAsync(42, "CVG200 AOI", operatorId: "Jabil\\4045423");

        Assert.Equal(987, historyId);
    }

    [Fact]
    public async Task Returns401_TriggersTokenRefresh_ThenSucceeds()
    {
        var tokens = new[] { MakeToken(), MakeToken() };
        var tokenCalls = 0;
        var wipCalls = 0;

        var handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath == "/api/tokens")
            {
                var t = tokens[tokenCalls];
                tokenCalls++;
                return JsonResponse(HttpStatusCode.OK, new { token = t });
            }

            wipCalls++;
            if (wipCalls == 1)
            {
                return JsonResponse(HttpStatusCode.Unauthorized, new { message = new[] { "expired" } });
            }
            return JsonResponse(HttpStatusCode.OK, new
            {
                wips = new[] { new { id = 1, serialNumber = "X", materialName = "M", wipStatus = "New" } },
            });
        });

        var wip = await MakeClient(handler).GetWipBySerialAsync("X");

        Assert.NotNull(wip);
        Assert.Equal(2, tokenCalls); // initial fetch, then forced refresh after the 401
    }

    [Fact]
    public async Task ExhaustedRetries_ThrowIFactoryApiException()
    {
        var handler = new FakeHttpMessageHandler(req =>
            req.RequestUri!.AbsolutePath == "/api/tokens"
                ? JsonResponse(HttpStatusCode.OK, new { token = MakeToken() })
                : JsonResponse(HttpStatusCode.InternalServerError, new { message = new[] { "boom" } }));

        await Assert.ThrowsAsync<IFactoryApiException>(() => MakeClient(handler).GetWipBySerialAsync("X"));
    }

    [Fact]
    public async Task ClientError_ThrowsImmediately_WithoutRetrying()
    {
        var wipCalls = 0;
        var handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath == "/api/tokens")
            {
                return JsonResponse(HttpStatusCode.OK, new { token = MakeToken() });
            }
            wipCalls++;
            return JsonResponse(HttpStatusCode.BadRequest, new { message = new[] { "bad request" } });
        });

        await Assert.ThrowsAsync<IFactoryApiException>(() => MakeClient(handler).GetWipBySerialAsync("X"));
        Assert.Equal(1, wipCalls);
    }

    [Fact]
    public async Task VerifyCredentials_FetchesAToken()
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            Assert.Equal("/api/tokens", req.RequestUri!.AbsolutePath);
            return JsonResponse(HttpStatusCode.OK, new { token = MakeToken() });
        });

        await MakeClient(handler).VerifyCredentialsAsync(); // should not throw
    }

    [Fact]
    public async Task HttpTimeout_IsRetried_AndSurfacesAsIFactoryApiException_NotCancellation()
    {
        // HttpClient.Timeout throws TaskCanceledException. Before the fix this
        // escaped as an OperationCanceledException, which every worker loop treats
        // as "shutting down" - silently stopping the station.
        var token = MakeToken();
        var wipCalls = 0;
        var handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath == "/api/tokens")
            {
                return JsonResponse(HttpStatusCode.OK, new { token });
            }
            wipCalls++;
            throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout");
        });

        var ex = await Assert.ThrowsAsync<IFactoryApiException>(() => MakeClient(handler).GetWipBySerialAsync("X"));
        Assert.Contains("timed out", ex.Message);
        Assert.Equal(3, wipCalls);
    }

    [Fact]
    public async Task TokenEndpointFailure_SurfacesAsIFactoryApiException()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("denied") });

        var ex = await Assert.ThrowsAsync<IFactoryApiException>(() => MakeClient(handler).VerifyCredentialsAsync());
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task TokenEndpointUnreachable_SurfacesAsIFactoryApiException()
    {
        var handler = new FakeHttpMessageHandler((Func<HttpRequestMessage, HttpResponseMessage>)(_ => throw new HttpRequestException("No such host is known")));

        await Assert.ThrowsAsync<IFactoryApiException>(() => MakeClient(handler).VerifyCredentialsAsync());
    }
}
