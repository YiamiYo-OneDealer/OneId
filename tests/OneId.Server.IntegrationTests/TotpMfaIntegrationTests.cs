using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.IntegrationTests.Helpers;
using OtpNet;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OneId.Server.IntegrationTests;

[Collection("IntegrationTests")]
public class TotpMfaIntegrationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    // Factory methods — FormUrlEncodedContent is single-use and must not be reused.
    private static FormUrlEncodedContent TotpUserPasswordRequest() =>
        new(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = DevSeeder.TotpUserEmail,
            ["password"] = "Admin123!",
            ["client_id"] = "oneid-dev-client",
            ["scope"] = "openid email profile offline_access",
        });

    private static FormUrlEncodedContent AdminUserPasswordRequest() =>
        new(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "admin@oneid.dev",
            ["password"] = "Admin123!",
            ["client_id"] = "oneid-dev-client",
            ["scope"] = "openid email profile offline_access",
        });

    private static FormUrlEncodedContent MfaGrantRequest(string mfaToken, string totpCode) =>
        new(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:oneid:mfa",
            ["mfa_session_token"] = mfaToken,
            ["totp_code"] = totpCode,
            ["client_id"] = "oneid-dev-client",
            ["scope"] = "openid email profile offline_access",
        });

    // Generates a TOTP code from the pre-enrolled test user's well-known secret.
    private static string CurrentTotpCode()
        => new Totp(Base32Encoding.ToBytes(DevSeeder.TotpUserTotpSecret)).ComputeTotp(DateTime.UtcNow);

    [Fact]
    public async Task EnrolledUser_PasswordGrant_ReturnsMfaRequired_WithoutEnrollmentUri()
    {
        var response = await Client.PostAsync("/connect/token", TotpUserPasswordRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("mfa_required").GetBoolean());
        Assert.True(
            body.TryGetProperty("mfa_session_token", out var tokenProp) && tokenProp.GetString() is { Length: > 0 },
            "mfa_session_token must be present and non-empty");
        Assert.False(
            body.TryGetProperty("totp_enrollment_uri", out _),
            "Already-enrolled user must NOT receive a totp_enrollment_uri");
    }

    [Fact]
    public async Task UnenrolledUser_PasswordGrant_ReturnsMfaRequired_WithEnrollmentUri()
    {
        var response = await Client.PostAsync("/connect/token", AdminUserPasswordRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("mfa_required").GetBoolean());
        Assert.True(
            body.TryGetProperty("mfa_session_token", out var tokenProp) && tokenProp.GetString() is { Length: > 0 },
            "mfa_session_token must be present and non-empty");
        Assert.True(
            body.TryGetProperty("totp_enrollment_uri", out var uriProp),
            "Unenrolled user must receive a totp_enrollment_uri");
        var uri = uriProp.GetString()!;
        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains("secret=", uri);
    }

    [Fact]
    public async Task EnrolledUser_FullTwoStepFlow_ReturnsAccessToken()
    {
        // Step 1: password grant
        var step1Response = await Client.PostAsync("/connect/token", TotpUserPasswordRequest());
        Assert.Equal(HttpStatusCode.OK, step1Response.StatusCode);
        var step1Body = await step1Response.Content.ReadFromJsonAsync<JsonElement>();
        var mfaToken = step1Body.GetProperty("mfa_session_token").GetString()!;

        // Step 2: MFA grant with a valid TOTP code
        var step2Response = await Client.PostAsync("/connect/token", MfaGrantRequest(mfaToken, CurrentTotpCode()));
        Assert.Equal(HttpStatusCode.OK, step2Response.StatusCode);

        var step2Body = await step2Response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(step2Body.TryGetProperty("access_token", out _), "access_token must be present");
        Assert.True(step2Body.TryGetProperty("refresh_token", out _), "refresh_token must be present");
        Assert.Equal("Bearer", step2Body.GetProperty("token_type").GetString());
    }

    [Fact]
    public async Task MfaGrant_InvalidTotpCode_ReturnsInvalidGrant()
    {
        var step1Response = await Client.PostAsync("/connect/token", TotpUserPasswordRequest());
        var step1Body = await step1Response.Content.ReadFromJsonAsync<JsonElement>();
        var mfaToken = step1Body.GetProperty("mfa_session_token").GetString()!;

        // Use an obviously wrong 6-digit code
        var response = await Client.PostAsync("/connect/token", MfaGrantRequest(mfaToken, "000000"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task MfaGrant_TamperedSessionToken_ReturnsInvalidGrant()
    {
        var response = await Client.PostAsync("/connect/token",
            MfaGrantRequest("totally-invalid-garbage-token", CurrentTotpCode()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task MfaGrant_ReplayPrevention_SecondUseOfSameCodeFails()
    {
        // Capture one code to reuse across both attempts
        var code = CurrentTotpCode();

        // First session: password grant → MFA grant (succeeds, records TotpLastUsedTimeStep)
        var step1a = await Client.PostAsync("/connect/token", TotpUserPasswordRequest());
        var step1aBody = await step1a.Content.ReadFromJsonAsync<JsonElement>();
        var mfaToken1 = step1aBody.GetProperty("mfa_session_token").GetString()!;

        var firstAttempt = await Client.PostAsync("/connect/token", MfaGrantRequest(mfaToken1, code));
        Assert.Equal(HttpStatusCode.OK, firstAttempt.StatusCode);

        // Second session: new password grant (fresh session token) then replay the same code
        var step1b = await Client.PostAsync("/connect/token", TotpUserPasswordRequest());
        var step1bBody = await step1b.Content.ReadFromJsonAsync<JsonElement>();
        var mfaToken2 = step1bBody.GetProperty("mfa_session_token").GetString()!;

        var secondAttempt = await Client.PostAsync("/connect/token", MfaGrantRequest(mfaToken2, code));
        Assert.Equal(HttpStatusCode.BadRequest, secondAttempt.StatusCode);

        var body = await secondAttempt.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }
}
