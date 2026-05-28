using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.IntegrationTests.Helpers;
using OtpNet;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace OneId.Server.IntegrationTests;

[Collection("IntegrationTests")]
public class TestTokenFactoryContractTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task TestTokenFactory_ClaimShape_MatchesProductionITokenClaimsEnricher()
    {
        // Issue a real token via the full ITokenClaimsEnricher pipeline (password + MFA).
        var step1 = await Client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = DevSeeder.TotpUserEmail,
            ["password"] = "Admin123!",
            ["client_id"] = "oneid-dev-client",
            ["scope"] = "openid email profile offline_access",
        }));
        step1.EnsureSuccessStatusCode();
        var mfaToken = (await step1.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("mfa_session_token").GetString()!;

        var step2 = await Client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:oneid:mfa",
            ["mfa_session_token"] = mfaToken,
            ["totp_code"] = new Totp(Base32Encoding.ToBytes(DevSeeder.TotpUserTotpSecret))
                                .ComputeTotp(DateTime.UtcNow),
            ["client_id"] = "oneid-dev-client",
            ["scope"] = "openid email profile offline_access",
        }));
        Assert.Equal(HttpStatusCode.OK, step2.StatusCode);
        var accessToken = (await step2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString()!;

        // Introspect the token — this exercises the full ITokenClaimsEnricher pipeline
        // plus IntrospectionEnricher (dimensional_attributes, license).
        var introspectResponse = await Client.PostAsync("/connect/introspect", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = accessToken,
            ["client_id"] = "oneid-sample-app",
            ["client_secret"] = "sample-app-secret",
        }));
        Assert.Equal(HttpStatusCode.OK, introspectResponse.StatusCode);
        var body = await introspectResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("active").GetBoolean());

        // Assert enriched response shape — catches drift between TestTokenFactory claims and production pipeline.
        Assert.True(body.TryGetProperty("permissions", out var perms),
            "permissions must be present — PermissionEvaluationEnricher must be active");
        Assert.Equal(JsonValueKind.Array, perms.ValueKind);

        Assert.True(body.TryGetProperty("dimensional_attributes", out var dims),
            "dimensional_attributes must be present — IntrospectionEnricher must be active");
        Assert.Equal(JsonValueKind.Object, dims.ValueKind);
        Assert.Equal(5, dims.EnumerateObject().Count());

        Assert.True(body.TryGetProperty("license", out var lic),
            "license must be present — IntrospectionEnricher must be active");
        Assert.Equal(JsonValueKind.Object, lic.ValueKind);
        Assert.True(lic.TryGetProperty("status", out _));
        Assert.True(lic.TryGetProperty("seats_used", out _));
        Assert.True(lic.TryGetProperty("max_seats", out _));
    }
}
