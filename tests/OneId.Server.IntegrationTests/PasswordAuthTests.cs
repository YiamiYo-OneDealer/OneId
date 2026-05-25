using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Infrastructure.Persistence;
using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.IntegrationTests.Helpers;
using OtpNet;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OneId.Server.IntegrationTests;

[Collection("IntegrationTests")]
public class PasswordAuthTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    // Creates a fresh FormUrlEncodedContent each call — HttpContent is read once and cannot be reused.
    private static FormUrlEncodedContent ValidTokenRequest() =>
        new(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "admin@oneid.dev",
            ["password"] = "Admin123!",
            ["client_id"] = "oneid-dev-client",
            ["scope"] = "openid email profile offline_access",
        });

    [Fact]
    public async Task ValidCredentials_ReturnsAccessTokenAndRefreshToken()
    {
        // Story 2.3: all password grants go through MFA gate — use the pre-enrolled TOTP user.
        // Step 1: password grant → mfa_required response with session token
        var step1 = await Client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = DevSeeder.TotpUserEmail,
            ["password"] = "Admin123!",
            ["client_id"] = "oneid-dev-client",
            ["scope"] = "openid email profile offline_access",
        }));
        Assert.Equal(HttpStatusCode.OK, step1.StatusCode);
        var step1Body = await step1.Content.ReadFromJsonAsync<JsonElement>();
        var mfaToken = step1Body.GetProperty("mfa_session_token").GetString()!;

        // Step 2: MFA grant with a valid TOTP code → access token
        var totpCode = new Totp(Base32Encoding.ToBytes(DevSeeder.TotpUserTotpSecret)).ComputeTotp(DateTime.UtcNow);
        var step2 = await Client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:oneid:mfa",
            ["mfa_session_token"] = mfaToken,
            ["totp_code"] = totpCode,
            ["client_id"] = "oneid-dev-client",
            ["scope"] = "openid email profile offline_access",
        }));
        Assert.Equal(HttpStatusCode.OK, step2.StatusCode);

        var body = await step2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("access_token", out _), "access_token missing");
        Assert.True(body.TryGetProperty("refresh_token", out _), "refresh_token missing");
        Assert.True(body.TryGetProperty("expires_in", out _), "expires_in missing");
        Assert.Equal("Bearer", body.GetProperty("token_type").GetString());
    }

    [Fact]
    public async Task InvalidPassword_ReturnsInvalidGrant()
    {
        var response = await Client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "admin@oneid.dev",
            ["password"] = "WrongPassword!",
            ["client_id"] = "oneid-dev-client",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task UnknownEmail_ReturnsIdenticalErrorToWrongPassword()
    {
        var wrongPasswordResponse = await Client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "admin@oneid.dev",
            ["password"] = "Wrong!",
            ["client_id"] = "oneid-dev-client",
        }));

        var unknownEmailResponse = await Client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "ghost@nowhere.com",
            ["password"] = "Anything!",
            ["client_id"] = "oneid-dev-client",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, wrongPasswordResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownEmailResponse.StatusCode);

        var wrongBody = await wrongPasswordResponse.Content.ReadFromJsonAsync<JsonElement>();
        var unknownBody = await unknownEmailResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("invalid_grant", wrongBody.GetProperty("error").GetString());
        Assert.Equal("invalid_grant", unknownBody.GetProperty("error").GetString());
        // Error descriptions must be identical — no user enumeration
        Assert.Equal(
            wrongBody.GetProperty("error_description").GetString(),
            unknownBody.GetProperty("error_description").GetString());
    }

    [Fact]
    public async Task LockoutTriggeredIntegrationTest()
    {
        var badRequest = new Func<FormUrlEncodedContent>(() => new(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "admin@oneid.dev",
            ["password"] = "Wrong!",
            ["client_id"] = "oneid-dev-client",
        }));

        for (var i = 0; i < 5; i++)
        {
            var r = await Client.PostAsync("/connect/token", badRequest());
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }

        // Verify DB state: AccessFailedCount == 5, LockoutEnd is set and in the future
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.IgnoreQueryFilters()
            .SingleAsync(u => u.Email == "admin@oneid.dev");

        Assert.Equal(5, user.AccessFailedCount);
        Assert.True(user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
            "LockoutEnd should be set and in the future after 5 failures");
    }

    [Fact]
    public async Task LockedAccount_RejectsCorrectPassword()
    {
        var badRequest = new Func<FormUrlEncodedContent>(() => new(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "admin@oneid.dev",
            ["password"] = "Wrong!",
            ["client_id"] = "oneid-dev-client",
        }));

        // Trigger lockout
        for (var i = 0; i < 5; i++)
            await Client.PostAsync("/connect/token", badRequest());

        // Even with the correct password, the account is locked
        var response = await Client.PostAsync("/connect/token", ValidTokenRequest());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());

        // Response must NOT disclose the lockout ETA or state
        var desc = body.GetProperty("error_description").GetString() ?? string.Empty;
        Assert.DoesNotContain("minutes", desc, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("locked", desc, StringComparison.OrdinalIgnoreCase);
    }
}
