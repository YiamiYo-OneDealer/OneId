using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.IntegrationTests.Helpers;
using OtpNet;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using Xunit;

namespace OneId.Server.IntegrationTests.OpenIddict;

[Collection("IntegrationTests")]
public class RoleChangeInvalidationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task RevokeAllUserTokens_IntrospectionReturnsActiveFalse()
    {
        var accessToken = await IssueMfaTokenAsync();

        Assert.True(await IsTokenActiveAsync(accessToken), "Token should be active before revocation");

        using var scope = Factory.Services.CreateScope();
        var revoker = scope.ServiceProvider.GetRequiredService<IUserTokenRevoker>();
        await revoker.RevokeAllUserTokensAsync(DevSeeder.TotpUserId);

        Assert.False(await IsTokenActiveAsync(accessToken), "Token should be inactive after revocation");
    }

    [Fact]
    public async Task RevokeAllUserTokens_DoesNotAffectOtherUserTokens()
    {
        // Issue token for TotpUser (pre-enrolled)
        var totpUserToken = await IssueMfaTokenAsync();

        // Issue token for AdminUser by completing TOTP enrollment dynamically
        var adminUserToken = await IssueAdminUserTokenAsync();

        Assert.True(await IsTokenActiveAsync(totpUserToken), "TotpUser token should be active before revocation");
        Assert.True(await IsTokenActiveAsync(adminUserToken), "AdminUser token should be active before revocation");

        // Revoke only TotpUser's tokens
        using var scope = Factory.Services.CreateScope();
        var revoker = scope.ServiceProvider.GetRequiredService<IUserTokenRevoker>();
        await revoker.RevokeAllUserTokensAsync(DevSeeder.TotpUserId);

        Assert.False(await IsTokenActiveAsync(totpUserToken), "TotpUser token should be inactive after revocation");
        Assert.True(await IsTokenActiveAsync(adminUserToken), "AdminUser token should still be active — revocation must not affect other users");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<string> IssueMfaTokenAsync()
    {
        var step1 = await Client.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = DevSeeder.TotpUserEmail,
                ["password"] = "Admin123!",
                ["client_id"] = "oneid-dev-client",
                ["scope"] = "openid email profile offline_access",
            }));
        var mfaToken = (await step1.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("mfa_session_token").GetString()!;

        var totpCode = new Totp(Base32Encoding.ToBytes(DevSeeder.TotpUserTotpSecret))
            .ComputeTotp(DateTime.UtcNow);

        var step2 = await Client.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "urn:oneid:mfa",
                ["mfa_session_token"] = mfaToken,
                ["totp_code"] = totpCode,
                ["client_id"] = "oneid-dev-client",
                ["scope"] = "openid email profile offline_access",
            }));
        Assert.Equal(HttpStatusCode.OK, step2.StatusCode);
        return (await step2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString()!;
    }

    /// <summary>
    /// Enrolls AdminUser (not yet enrolled) and issues a token.
    /// Password grant returns mfa_session_token + totp_enrollment_uri; we extract
    /// the base32 secret, generate the TOTP code, and complete the MFA grant.
    /// </summary>
    private async Task<string> IssueAdminUserTokenAsync()
    {
        var step1 = await Client.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = "admin@oneid.dev",
                ["password"] = "Admin123!",
                ["client_id"] = "oneid-dev-client",
                ["scope"] = "openid email profile offline_access",
            }));
        Assert.Equal(HttpStatusCode.OK, step1.StatusCode);

        var step1Body = await step1.Content.ReadFromJsonAsync<JsonElement>();
        var mfaToken = step1Body.GetProperty("mfa_session_token").GetString()!;
        var enrollmentUri = step1Body.GetProperty("totp_enrollment_uri").GetString()!;

        // Extract base32 secret from otpauth URI: ?secret=BASE32&
        var match = Regex.Match(enrollmentUri, @"[?&]secret=([^&]+)");
        Assert.True(match.Success, "Enrollment URI must contain a secret parameter");
        var base32Secret = match.Groups[1].Value;

        var totpCode = new Totp(Base32Encoding.ToBytes(base32Secret))
            .ComputeTotp(DateTime.UtcNow);

        var step2 = await Client.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "urn:oneid:mfa",
                ["mfa_session_token"] = mfaToken,
                ["totp_code"] = totpCode,
                ["client_id"] = "oneid-dev-client",
                ["scope"] = "openid email profile offline_access",
            }));
        Assert.Equal(HttpStatusCode.OK, step2.StatusCode);
        return (await step2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString()!;
    }

    private static FormUrlEncodedContent IntrospectRequest(string accessToken) =>
        new(new Dictionary<string, string>
        {
            ["token"] = accessToken,
            ["client_id"] = "oneid-sample-app",
            ["client_secret"] = "sample-app-secret",
        });

    private async Task<bool> IsTokenActiveAsync(string accessToken)
    {
        var response = await Client.PostAsync("/connect/introspect", IntrospectRequest(accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("active").GetBoolean();
    }
}
