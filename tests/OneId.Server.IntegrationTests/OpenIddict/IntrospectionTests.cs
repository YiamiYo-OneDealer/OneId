using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.IntegrationTests.Helpers;
using OpenIddict.Abstractions;
using OtpNet;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace OneId.Server.IntegrationTests.OpenIddict;

[Collection("IntegrationTests")]
public class IntrospectionTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    // ── helpers ────────────────────────────────────────────────────────────────

    private async Task<string> IssueMfaTokenAsync()
    {
        var step1 = await Client.PostAsync("/connect/token", PasswordRequest());
        step1.EnsureSuccessStatusCode();
        var mfaToken = (await step1.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("mfa_session_token").GetString()!;

        var step2 = await Client.PostAsync("/connect/token", MfaRequest(mfaToken));
        Assert.Equal(HttpStatusCode.OK, step2.StatusCode);

        return (await step2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString()!;
    }

    // Decodes the JWT payload and returns the full claims element.
    private static JsonElement DecodeJwtPayload(string accessToken)
    {
        var payloadJson = Encoding.UTF8.GetString(
            Base64UrlEncoder.DecodeBytes(accessToken.Split('.')[1]));
        return JsonSerializer.Deserialize<JsonElement>(payloadJson);
    }

    // Extracts the standard jti claim (external token identifier).
    private static string ExtractJti(string accessToken) =>
        DecodeJwtPayload(accessToken).GetProperty("jti").GetString()!;

    // Extracts OpenIddict's internal token store ID (oi_tkn_id claim).
    // This is different from jti — it is the primary key in OpenIddictTokens.
    private static string? ExtractOpenIddictTokenId(string accessToken)
    {
        var payload = DecodeJwtPayload(accessToken);
        return payload.TryGetProperty("oi_tkn_id", out var prop) ? prop.GetString() : null;
    }

    private static FormUrlEncodedContent PasswordRequest() =>
        new(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = DevSeeder.TotpUserEmail,
            ["password"] = "Admin123!",
            ["client_id"] = "oneid-dev-client",
            ["scope"] = "openid email profile offline_access",
        });

    private static FormUrlEncodedContent MfaRequest(string mfaToken) =>
        new(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:oneid:mfa",
            ["mfa_session_token"] = mfaToken,
            ["totp_code"] = new Totp(Base32Encoding.ToBytes(DevSeeder.TotpUserTotpSecret))
                                .ComputeTotp(DateTime.UtcNow),
            ["client_id"] = "oneid-dev-client",
            ["scope"] = "openid email profile offline_access",
        });

    private static FormUrlEncodedContent IntrospectRequest(string accessToken) =>
        new(new Dictionary<string, string>
        {
            ["token"] = accessToken,
            ["client_id"] = "oneid-sample-app",
            ["client_secret"] = "sample-app-secret",
        });

    // ── AC1: active token with enriched fields ──────────────────────────────

    [Fact]
    public async Task ActiveToken_IntrospectionReturnsActiveTrue()
    {
        var accessToken = await IssueMfaTokenAsync();

        var response = await Client.PostAsync("/connect/introspect", IntrospectRequest(accessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("active").GetBoolean(), "Expected active: true for a valid, non-revoked token");

        // Verify standard claims present in introspection response
        Assert.True(body.TryGetProperty("sub", out _), "sub claim must be present");
        Assert.True(body.TryGetProperty("exp", out _), "exp claim must be present");
        Assert.True(body.TryGetProperty("jti", out _), "jti claim must be present");
        Assert.True(body.TryGetProperty("iss", out _), "iss claim must be present");

        // AC1: permissions array must be present (populated by PermissionEvaluationEnricher via JWT)
        Assert.True(body.TryGetProperty("permissions", out var permsProp),
            "permissions must be present in introspection response");
        Assert.Equal(JsonValueKind.Array, permsProp.ValueKind);

        // AC1: dimensional_attributes object with all 5 axes always present
        Assert.True(body.TryGetProperty("dimensional_attributes", out var dimProp),
            "dimensional_attributes must be present in introspection response");
        Assert.Equal(JsonValueKind.Object, dimProp.ValueKind);
        foreach (var axis in new[] { "Company", "Location", "Branch", "Make", "MarketSegment" })
        {
            Assert.True(dimProp.TryGetProperty(axis, out var axisProp),
                $"dimensional_attributes must contain axis '{axis}'");
            Assert.Equal(JsonValueKind.Array, axisProp.ValueKind);
        }

        // AC1: license object with required shape
        Assert.True(body.TryGetProperty("license", out var licenseProp),
            "license must be present in introspection response");
        Assert.Equal(JsonValueKind.Object, licenseProp.ValueKind);
        Assert.True(licenseProp.TryGetProperty("status", out _), "license.status must be present");
        Assert.True(licenseProp.TryGetProperty("seats_used", out _), "license.seats_used must be present");
        Assert.True(licenseProp.TryGetProperty("max_seats", out _), "license.max_seats must be present");

        // Verify jti is present in the JWT and the token is tracked in the OpenIddict store.
        var jti = ExtractJti(accessToken);
        Assert.False(string.IsNullOrEmpty(jti), "jti must be present in the issued JWT");

        var internalTokenId = ExtractOpenIddictTokenId(accessToken);
        if (internalTokenId is not null)
        {
            using var scope = Factory.Services.CreateScope();
            var tokenManager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
            var storedToken = await tokenManager.FindByIdAsync(internalTokenId);
            Assert.NotNull(storedToken);
            Assert.True(
                await tokenManager.HasStatusAsync(storedToken, OpenIddictConstants.Statuses.Valid),
                "Token in store must have Valid status");
        }
    }

    // ── AC2: no assignments → empty enriched fields ──────────────────────────

    [Fact]
    public async Task NoAssignments_EnrichedFieldsAreEmpty()
    {
        // DevSeeder TotpUser has no Group or DimensionAssignment records — ideal for this test.
        var accessToken = await IssueMfaTokenAsync();

        var response = await Client.PostAsync("/connect/introspect", IntrospectRequest(accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("active").GetBoolean(), "Token must be active");

        // permissions must be an empty array (no group assignments)
        Assert.True(body.TryGetProperty("permissions", out var permsProp));
        Assert.Equal(JsonValueKind.Array, permsProp.ValueKind);
        Assert.Equal(0, permsProp.GetArrayLength());

        // dimensional_attributes must have all 5 axes, each an empty array
        Assert.True(body.TryGetProperty("dimensional_attributes", out var dimProp));
        foreach (var axis in new[] { "Company", "Location", "Branch", "Make", "MarketSegment" })
        {
            Assert.True(dimProp.TryGetProperty(axis, out var axisProp),
                $"axis '{axis}' must be present even with no assignments");
            Assert.Equal(JsonValueKind.Array, axisProp.ValueKind);
            Assert.Equal(0, axisProp.GetArrayLength());
        }
    }

    // ── AC2: revoked jti ────────────────────────────────────────────────────

    [Fact]
    public async Task RevokedJti_IntrospectionReturnsActiveFalse()
    {
        var accessToken = await IssueMfaTokenAsync();

        // Confirm active before revocation
        var beforeResponse = await Client.PostAsync("/connect/introspect", IntrospectRequest(accessToken));
        Assert.Equal(HttpStatusCode.OK, beforeResponse.StatusCode);
        var beforeBody = await beforeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(beforeBody.GetProperty("active").GetBoolean(), "Token should be active before revocation");

        // Revoke by the internal OpenIddict token store ID (oi_tkn_id) embedded in the JWT.
        // This is the server-side jti store that Epic 3 (Tenant suspension) and Story 2.6 (Role-change
        // invalidation) depend on — the same mechanism, exercised here as the contract.
        var internalTokenId = ExtractOpenIddictTokenId(accessToken);
        Assert.False(string.IsNullOrEmpty(internalTokenId), "oi_tkn_id must be embedded in the JWT for server-side revocation to work");

        using (var scope = Factory.Services.CreateScope())
        {
            var tokenManager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
            var storedToken = await tokenManager.FindByIdAsync(internalTokenId);
            Assert.NotNull(storedToken);
            await tokenManager.TryRevokeAsync(storedToken);
        }

        // Introspect after revocation — must return active: false
        var afterResponse = await Client.PostAsync("/connect/introspect", IntrospectRequest(accessToken));
        Assert.Equal(HttpStatusCode.OK, afterResponse.StatusCode);
        var afterBody = await afterResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(afterBody.GetProperty("active").GetBoolean(), "Expected active: false after jti revocation");

        // When active is false, no other claims should be present (RFC 7662 §2.2)
        Assert.Single(afterBody.EnumerateObject());
    }

    // ── AC3: expired token ──────────────────────────────────────────────────

    [Fact]
    public async Task ExpiredToken_IntrospectionReturnsActiveFalse()
    {
        var accessToken = await IssueMfaTokenAsync();

        var internalTokenId = ExtractOpenIddictTokenId(accessToken);
        Assert.False(string.IsNullOrEmpty(internalTokenId), "oi_tkn_id must be present in the JWT");

        // Set ExpirationDate to the past via IOpenIddictTokenManager.UpdateAsync.
        // OpenIddict checks ExpirationDate in the store during introspection validation.
        using (var scope = Factory.Services.CreateScope())
        {
            var tokenManager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
            var storedToken = await tokenManager.FindByIdAsync(internalTokenId);
            Assert.NotNull(storedToken);

            var descriptor = new OpenIddictTokenDescriptor();
            await tokenManager.PopulateAsync(descriptor, storedToken);
            descriptor.ExpirationDate = DateTimeOffset.UtcNow.AddHours(-1);
            await tokenManager.UpdateAsync(storedToken, descriptor);
        }

        var response = await Client.PostAsync("/connect/introspect", IntrospectRequest(accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("active").GetBoolean(), "Expected active: false for a token with past ExpirationDate");
    }

    // ── AC4: performance gate (NFR-4: ≤50ms p95) ────────────────────────────

    [Fact]
    public async Task IntrospectionPerformanceTest_P95_Under50ms()
    {
        const int SampleCount = 50;
        const long BudgetMs = 50L;

        // Issue one token; introspection does not consume the token, so we reuse it for all samples.
        var accessToken = await IssueMfaTokenAsync();

        var times = new List<long>(SampleCount);

        for (var i = 0; i < SampleCount; i++)
        {
            var sw = Stopwatch.StartNew();
            var response = await Client.PostAsync("/connect/introspect", IntrospectRequest(accessToken));
            sw.Stop();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            times.Add(sw.ElapsedMilliseconds);
        }

        times.Sort();
        var p95Index = (int)Math.Ceiling(SampleCount * 0.95) - 1;
        var p95Ms = times[p95Index];
        Assert.True(p95Ms <= BudgetMs,
            $"p95 introspection time {p95Ms}ms exceeded {BudgetMs}ms budget (NFR-4: ≤50ms p95)");
    }
}
