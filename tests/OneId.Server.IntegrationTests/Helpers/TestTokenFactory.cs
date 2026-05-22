using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace OneId.Server.IntegrationTests.Helpers;

public static class TestTokenFactory
{
    // Exposed internal so WebApplicationFactory can configure JWT Bearer validation in Epic 2.
    internal static readonly SymmetricSecurityKey TestSigningKey =
        new(Encoding.UTF8.GetBytes("oneid-integration-test-signing-key-must-be-at-least-32!!"));

    public static string CreateToken(
        Guid tenantId,
        Guid userId,
        string[]? roles = null,
        int seatCount = 50)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                ["tid"] = tenantId.ToString(),
                ["sub"] = userId.ToString(),
                ["scope"] = "openid",
                ["seat_count"] = seatCount,                      // integer in JWT payload — NOT string
                ["roles"] = roles ?? Array.Empty<string>(),      // always present, even if empty
            },
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(TestSigningKey, SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
