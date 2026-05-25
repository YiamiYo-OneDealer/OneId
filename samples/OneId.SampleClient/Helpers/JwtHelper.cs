using System.Text;
using System.Text.Json;

namespace OneId.SampleClient.Helpers;

internal static class JwtHelper
{
    /// <summary>
    /// Decodes the payload of a JWT without signature validation.
    /// Returns a flat list of (claim-type, value) pairs; array-valued claims produce one pair per element.
    /// </summary>
    public static List<(string Name, string Value)> DecodePayloadClaims(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2)
            return [];

        var payload = parts[1];
        // Base64url → standard Base64
        var padded = payload + new string('=', (4 - payload.Length % 4) % 4);
        var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        var json = Encoding.UTF8.GetString(bytes);

        var claims = new List<(string Name, string Value)>();
        using var doc = JsonDocument.Parse(json);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.Value.EnumerateArray())
                    claims.Add((prop.Name, item.ToString()));
            }
            else
            {
                claims.Add((prop.Name, prop.Value.ToString()));
            }
        }

        return claims;
    }

    /// <summary>
    /// Returns a human-readable UTC datetime string for known Unix-timestamp claim names,
    /// or null for non-timestamp claims.
    /// </summary>
    public static string? TryFormatTimestamp(string claimName, string value)
    {
        if (claimName is not ("exp" or "iat" or "nbf"))
            return null;

        if (!long.TryParse(value, out var unix))
            return null;

        return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss UTC");
    }
}
