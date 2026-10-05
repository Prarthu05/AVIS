using System.Text;
using System.Text.Json;

namespace Avis.IFactory;

public static class JwtHelper
{
    /// <summary>
    /// Reads the `exp` claim out of a JWT without verifying its signature. We trust
    /// the token because it came from GET /api/tokens over TLS; we only need `exp`
    /// to know when to proactively refresh.
    /// </summary>
    public static double? DecodeExpiry(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2)
            {
                return null;
            }

            var payloadBase64 = parts[1].Replace('-', '+').Replace('_', '/');
            var padded = payloadBase64.PadRight(payloadBase64.Length + (4 - payloadBase64.Length % 4) % 4, '=');
            var payloadBytes = Convert.FromBase64String(padded);
            var json = Encoding.UTF8.GetString(payloadBytes);

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("exp", out var expElement))
            {
                return expElement.GetDouble();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}
