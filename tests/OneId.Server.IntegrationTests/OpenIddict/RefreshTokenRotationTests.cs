using Microsoft.IdentityModel.Tokens;
using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.IntegrationTests.Helpers;
using OtpNet;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace OneId.Server.IntegrationTests.OpenIddict;

[Collection("IntegrationTests")]
public class RefreshTokenRotationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private async Task<(string AccessToken, string RefreshToken)> IssueMfaTokenWithRefreshAsync()
    {
        var step1 = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
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

        var step2 = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:oneid:mfa",
                ["mfa_session_token"] = mfaToken,
                ["totp_code"] = new Totp(Base32Encoding.ToBytes(DevSeeder.TotpUserTotpSecret))
                                    .ComputeTotp(DateTime.UtcNow),
                ["client_id"] = "oneid-dev-client",
                ["scope"] = "openid email profile offline_access",
            }));
        Assert.Equal(HttpStatusCode.OK, step2.StatusCode);
        var body = await step2.Content.ReadFromJsonAsync<JsonElement>();

        return (body.GetProperty("access_token").GetString()!,
                body.GetProperty("refresh_token").GetString()!);
    }

    [Fact]
    public async Task RefreshToken_ValidToken_IssuesNewAccessAndRefreshTokens()
    {
        var (_, refreshToken) = await IssueMfaTokenWithRefreshAsync();

        var response = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = "oneid-dev-client",
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("access_token", out var newAt) && !string.IsNullOrEmpty(newAt.GetString()),
            "New access_token must be present");
        Assert.True(body.TryGetProperty("refresh_token", out var newRt) && !string.IsNullOrEmpty(newRt.GetString()),
            "New refresh_token must be present");
        Assert.True(refreshToken != newRt.GetString(), "New refresh token must differ from consumed token");
    }

    [Fact]
    public async Task RefreshToken_ConsumedToken_Returns400InvalidGrant()
    {
        var (_, refreshToken) = await IssueMfaTokenWithRefreshAsync();

        // First use — succeeds
        var first = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = "oneid-dev-client",
            }));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Replay the same consumed refresh token — must fail
        var second = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = "oneid-dev-client",
            }));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var error = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", error.GetProperty("error").GetString());
    }

    [Fact]
    public async Task RefreshToken_NewAccessToken_PassesSignatureValidation()
    {
        var (_, refreshToken) = await IssueMfaTokenWithRefreshAsync();

        var response = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = "oneid-dev-client",
            }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var newAccessToken = body.GetProperty("access_token").GetString()!;

        // Verify RS256 structure: JWT has 3 base64url-encoded segments
        var parts = newAccessToken.Split('.');
        Assert.Equal(3, parts.Length);
        var headerJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[0]));
        var header = JsonSerializer.Deserialize<JsonElement>(headerJson);
        Assert.Equal("RS256", header.GetProperty("alg").GetString());

        // Verify the new token is accepted by the introspection endpoint (signature is valid)
        var introspectResponse = await Client.PostAsync("/connect/introspect",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = newAccessToken,
                ["client_id"] = "oneid-sample-app",
                ["client_secret"] = "sample-app-secret",
            }));
        Assert.Equal(HttpStatusCode.OK, introspectResponse.StatusCode);
        var introspectBody = await introspectResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(introspectBody.GetProperty("active").GetBoolean(),
            "New access token from refresh must be active (valid RS256 signature)");
    }
}
