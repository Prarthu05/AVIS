using System.Text;
using System.Text.Json;
using Avis.IFactory;
using Xunit;

namespace Avis.Tests.IFactory;

public class JwtHelperTests
{
    private static string Base64UrlEncode(string json)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string MakeJwt(object payload)
    {
        var header = Base64UrlEncode(JsonSerializer.Serialize(new { alg = "none" }));
        var payloadEncoded = Base64UrlEncode(JsonSerializer.Serialize(payload));
        return $"{header}.{payloadEncoded}.signature";
    }

    [Fact]
    public void DecodeExpiry_ReadsExpiryClaim()
    {
        var exp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600;
        var token = MakeJwt(new { exp });

        Assert.Equal(exp, JwtHelper.DecodeExpiry(token));
    }

    [Fact]
    public void DecodeExpiry_ReturnsNullForGarbage()
    {
        Assert.Null(JwtHelper.DecodeExpiry("not-a-jwt"));
    }

    [Fact]
    public void DecodeExpiry_ReturnsNullWhenExpClaimMissing()
    {
        var token = MakeJwt(new { sub = "svc" });
        Assert.Null(JwtHelper.DecodeExpiry(token));
    }
}
