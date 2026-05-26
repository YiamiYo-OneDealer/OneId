using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.IntegrationTests.Helpers;
using OtpNet;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json;
using Xunit;

namespace OneId.Server.IntegrationTests;

[Collection("IntegrationTests")]
[Trait("Category", "InternalAdmin")]
public class TenantSuspensionIntegrationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    // ── AC1: Suspend sets Tenant status to Suspended ──────────────────────────

    [Fact]
    public async Task Suspend_ReturnsOkWithSuspendedStatus()
    {
        // AdminUser issues its own token (fresh TOTP enrollment each test)
        var adminToken = await IssueAdminUserTokenAsync();
        var adminClient = MakeClient(adminToken);

        var response = await adminClient.PostAsync(
            $"/api/internal/tenants/{DevSeeder.DevTenantId}/suspend", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("status").GetInt32()); // TenantStatus.Suspended = 1
        Assert.Equal(DevSeeder.DevTenantId.ToString(), body.GetProperty("id").GetString());
    }

    // ── AC2 + AC3: Suspend revokes all active sessions ────────────────────────

    [Fact]
    public async Task Suspend_RevokesAllUserTokens_IntrospectionReturnsFalse()
    {
        // Issue tokens for both seeded users before suspending
        var totpToken = await IssueMfaTokenAsync();
        var adminToken = await IssueAdminUserTokenAsync();

        Assert.True(await IsTokenActiveAsync(totpToken), "TotpUser token should be active before suspension");
        Assert.True(await IsTokenActiveAsync(adminToken), "AdminUser token should be active before suspension");

        // Use AdminUser token to call the suspend endpoint
        var adminClient = MakeClient(adminToken);
        var suspendResponse = await adminClient.PostAsync(
            $"/api/internal/tenants/{DevSeeder.DevTenantId}/suspend", null);
        Assert.Equal(HttpStatusCode.OK, suspendResponse.StatusCode);

        // Both tokens must now be inactive (jtis revoked)
        Assert.False(await IsTokenActiveAsync(totpToken), "TotpUser token should be inactive after suspension");
        Assert.False(await IsTokenActiveAsync(adminToken), "AdminUser token should be inactive after suspension");
    }

    // ── AC2: Suspend blocks new token issuance ────────────────────────────────

    [Fact]
    public async Task Suspend_BlocksNewTokenIssuance()
    {
        var adminToken = await IssueAdminUserTokenAsync();
        var adminClient = MakeClient(adminToken);

        var suspendResponse = await adminClient.PostAsync(
            $"/api/internal/tenants/{DevSeeder.DevTenantId}/suspend", null);
        Assert.Equal(HttpStatusCode.OK, suspendResponse.StatusCode);

        // Attempt to issue a new token for TotpUser (password grant step)
        var tokenResponse = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = DevSeeder.TotpUserEmail,
                ["password"] = "Admin123!",
                ["client_id"] = "oneid-dev-client",
                ["scope"] = "openid",
            }));

        // OpenIddict returns OAuth2 error response body for token endpoint failures
        var body = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("tenant_suspended", body.GetProperty("error").GetString());
    }

    // ── AC4: Reinstate restores active status and allows token issuance ───────

    [Fact]
    public async Task Reinstate_ReturnsOkWithActiveStatus()
    {
        var adminToken = await IssueAdminUserTokenAsync();
        var adminClient = MakeClient(adminToken);

        await adminClient.PostAsync($"/api/internal/tenants/{DevSeeder.DevTenantId}/suspend", null);

        var reinstateResponse = await adminClient.PostAsync(
            $"/api/internal/tenants/{DevSeeder.DevTenantId}/reinstate", null);

        Assert.Equal(HttpStatusCode.OK, reinstateResponse.StatusCode);
        var body = await reinstateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("status").GetInt32()); // TenantStatus.Active = 0
    }

    [Fact]
    public async Task Reinstate_AllowsNewTokenIssuance()
    {
        var adminToken = await IssueAdminUserTokenAsync();
        var adminClient = MakeClient(adminToken);

        await adminClient.PostAsync($"/api/internal/tenants/{DevSeeder.DevTenantId}/suspend", null);
        await adminClient.PostAsync($"/api/internal/tenants/{DevSeeder.DevTenantId}/reinstate", null);

        // After reinstate, password grant step should succeed (not return tenant_suspended)
        // We check the first step — a successful response has mfa_session_token (not an error)
        var tokenResponse = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = DevSeeder.TotpUserEmail,
                ["password"] = "Admin123!",
                ["client_id"] = "oneid-dev-client",
                ["scope"] = "openid",
            }));

        var body = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        // A successful tenant check results in mfa_required = true, not tenant_suspended error
        Assert.True(body.TryGetProperty("mfa_session_token", out _),
            "Expected mfa_session_token in response after reinstate — tenant_suspended was not returned");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient MakeClient(string bearerToken)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", bearerToken);
        return client;
    }

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
    /// Enrolls AdminUser (not yet enrolled in clean DB) and issues a token.
    /// Each call generates a fresh TOTP secret, so no replay conflicts between tests.
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
