using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OneId.Server.Domain.Entities;
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
public class PasswordResetTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task ForgotPassword_RegisteredEmail_Returns200AndStoresToken()
    {
        var response = await Client.PostAsJsonAsync("/account/forgot-password",
            new { email = DevSeeder.TotpUserEmail });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.IgnoreQueryFilters()
            .FirstAsync(u => u.Email == DevSeeder.TotpUserEmail);

        Assert.NotNull(user.PasswordResetToken);
        Assert.NotNull(user.PasswordResetTokenExpiry);
        Assert.True(user.PasswordResetTokenExpiry > DateTimeOffset.UtcNow);
        Assert.True(user.PasswordResetTokenExpiry <= DateTimeOffset.UtcNow.AddHours(1).AddMinutes(1));
    }

    [Fact]
    public async Task ForgotPassword_UnregisteredEmail_Returns200WithNoTokenStored()
    {
        var response = await Client.PostAsJsonAsync("/account/forgot-password",
            new { email = "nobody@unknown.dev" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.IgnoreQueryFilters()
            .FirstAsync(u => u.Email == DevSeeder.TotpUserEmail);

        Assert.Null(user.PasswordResetToken);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_UpdatesPasswordAndRevokesTokens()
    {
        // Issue a token so we can verify jti revocation
        var accessToken = await IssueMfaTokenAsync();
        Assert.True(await IsTokenActiveAsync(accessToken));

        // Trigger forgot-password to get a reset token
        await Client.PostAsJsonAsync("/account/forgot-password",
            new { email = DevSeeder.TotpUserEmail });

        var token = await ReadResetTokenAsync(DevSeeder.TotpUserEmail);

        var resetResponse = await Client.PostAsJsonAsync("/account/reset-password",
            new { token, newPassword = "NewPassword456!" });

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        // Token must be consumed (null)
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.IgnoreQueryFilters()
            .FirstAsync(u => u.Email == DevSeeder.TotpUserEmail);
        Assert.Null(user.PasswordResetToken);

        // JTIs for that user must be revoked
        Assert.False(await IsTokenActiveAsync(accessToken));

        // New password must work for login (password grant step returns mfa_session_token)
        var loginResponse = await Client.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = DevSeeder.TotpUserEmail,
                ["password"] = "NewPassword456!",
                ["client_id"] = "oneid-dev-client",
                ["scope"] = "openid email profile offline_access",
            }));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(loginBody.TryGetProperty("mfa_session_token", out _), "New password should authenticate and return mfa_session_token");
    }

    [Fact]
    public async Task ResetPassword_ExpiredToken_Returns400()
    {
        await Client.PostAsJsonAsync("/account/forgot-password",
            new { email = DevSeeder.TotpUserEmail });

        var token = await ReadResetTokenAsync(DevSeeder.TotpUserEmail);

        // Manually expire the token
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.IgnoreQueryFilters()
                .FirstAsync(u => u.Email == DevSeeder.TotpUserEmail);
            user.PasswordResetTokenExpiry = DateTimeOffset.UtcNow.AddHours(-2);
            await db.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync("/account/reset-password",
            new { token, newPassword = "NewPassword456!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_or_expired_token", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ResetPassword_AlreadyUsedToken_Returns400()
    {
        await Client.PostAsJsonAsync("/account/forgot-password",
            new { email = DevSeeder.TotpUserEmail });

        var token = await ReadResetTokenAsync(DevSeeder.TotpUserEmail);

        // First use — succeeds
        var first = await Client.PostAsJsonAsync("/account/reset-password",
            new { token, newPassword = "NewPassword456!" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Second use — token is consumed
        var second = await Client.PostAsJsonAsync("/account/reset-password",
            new { token, newPassword = "AnotherPassword789!" });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_or_expired_token", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ResetPassword_PasswordReuse_Returns400()
    {
        await Client.PostAsJsonAsync("/account/forgot-password",
            new { email = DevSeeder.TotpUserEmail });

        var token = await ReadResetTokenAsync(DevSeeder.TotpUserEmail);

        var response = await Client.PostAsJsonAsync("/account/reset-password",
            new { token, newPassword = "Admin123!" }); // TotpUser's current password

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("password_reuse", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/account/reset-password",
            new { token = "aaaabbbbccccddddaaaabbbbccccdddd", newPassword = "NewPassword456!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_or_expired_token", body.GetProperty("error").GetString());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<string> ReadResetTokenAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.IgnoreQueryFilters()
            .FirstAsync(u => u.Email == email);
        return user.PasswordResetToken!;
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
