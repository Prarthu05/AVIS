using System.Net;
using Avis.LightGuide;
using Avis.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Avis.Tests.LightGuide;

public class LightGuideClientTests
{
    private static LightGuideClient MakeClient(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler), new LightGuideOptions { BaseUrl = "http://lightguide.test:54274/" }, NullLogger<LightGuideClient>.Instance);

    [Fact]
    public async Task GetVariables_RequestsAllNamesInOneCall()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""[{"Name":"WI_Name","Value":"P1"},{"Name":"My Var","Value":"x"}]"""),
        });

        var values = await MakeClient(handler).GetVariablesAsync(new[] { "WI_Name", "My Var" });

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://lightguide.test:54274/Variable/WI_Name,My%20Var", request.RequestUri!.AbsoluteUri);
        Assert.Equal("P1", values["WI_Name"]);
        Assert.Equal("x", values["My Var"]);
    }

    [Fact]
    public async Task HttpError_ThrowsLightGuideApiException()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("nope") });

        await Assert.ThrowsAsync<LightGuideApiException>(() => MakeClient(handler).GetVariablesAsync(new[] { "X" }));
    }

    [Fact]
    public async Task Timeout_ThrowsLightGuideApiException_NotCancellation()
    {
        var handler = new FakeHttpMessageHandler((Func<HttpRequestMessage, HttpResponseMessage>)(_ => throw new TaskCanceledException("timeout")));

        await Assert.ThrowsAsync<LightGuideApiException>(() => MakeClient(handler).GetVariablesAsync(new[] { "X" }));
    }

    [Fact]
    public async Task Log_PostsJsonStringToMessageOrErrorEndpoint()
    {
        var bodies = new List<string>();
        var handler = new FakeHttpMessageHandler(async req =>
        {
            bodies.Add(await req.Content!.ReadAsStringAsync());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };
        });
        var client = MakeClient(handler);

        await client.LogAsync("hello", isError: false);
        await client.LogAsync("bad", isError: true);

        Assert.Equal("/Application/Message", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("/Application/Error", handler.Requests[1].RequestUri!.AbsolutePath);
        Assert.Equal("\"hello\"", bodies[0]);
    }
}
