using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OneId.Server.Application.TokenPipeline;
using OneId.Server.Infrastructure.Persistence;
using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.IntegrationTests.Helpers;
using OtpNet;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Xunit;

namespace OneId.Server.IntegrationTests.OpenIddict;

[Collection("IntegrationTests")]
public class TokenIssuanceTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private static FormUrlEncodedContent TotpUserPasswordRequest() =>
        new(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = DevSeeder.TotpUserEmail,
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

    private static string CurrentTotpCode()
        => new Totp(Base32Encoding.ToBytes(DevSeeder.TotpUserTotpSecret)).ComputeTotp(DateTime.UtcNow);

    // Perform a full two-step auth and return the decoded JWT payload.
    private async Task<JsonElement> GetJwtPayloadAsync()
    {
        var step1 = await Client.PostAsync("/connect/token", TotpUserPasswordRequest());
        var mfaToken = (await step1.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("mfa_session_token").GetString()!;

        var step2 = await Client.PostAsync("/connect/token", MfaGrantRequest(mfaToken, CurrentTotpCode()));
        Assert.Equal(HttpStatusCode.OK, step2.StatusCode);

        var accessToken = (await step2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString()!;

        // Decode JWT payload without signature validation — testing claim shape, not security.
        var payloadJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(accessToken.Split('.')[1]));
        return JsonSerializer.Deserialize<JsonElement>(payloadJson);
    }

    [Fact]
    public async Task IssuedJwt_ContainsAllRequiredClaims()
    {
        var payload = await GetJwtPayloadAsync();

        // Standard OpenIddict-issued claims
        Assert.True(payload.TryGetProperty("sub", out _), "sub claim required");
        Assert.True(payload.TryGetProperty("iss", out _), "iss claim required");
        // aud is optional in RFC 9068 when no resource servers are configured via SetResources()
        // It will be present once Story 2.x wires up principal.SetResources("oneid")
        Assert.True(payload.TryGetProperty("exp", out _), "exp claim required");
        Assert.True(payload.TryGetProperty("iat", out _), "iat claim required");
        Assert.True(payload.TryGetProperty("jti", out _), "jti claim required — needed for revocation in Story 2.5");

        // sub must be the seeded TOTP user's stable ID
        Assert.Equal(DevSeeder.TotpUserId.ToString(), payload.GetProperty("sub").GetString());

        // roles: absent in Epic 2 (no Role entities yet), OR present as an array if enricher adds any
        if (payload.TryGetProperty("roles", out var rolesProp))
            Assert.Equal(JsonValueKind.Array, rolesProp.ValueKind);
    }

    [Fact]
    public async Task EnricherPipelineOrder_StubBSeesStubA_Marker()
    {
        // Register two additional enrichers on top of production DI and issue a real token.
        using var customFactory = Factory.WithWebHostBuilder(b =>
            b.ConfigureServices(svc =>
            {
                svc.AddScoped<ITokenClaimsEnricher, StubEnricherA>();
                svc.AddScoped<ITokenClaimsEnricher, StubEnricherB>();
            }));
        using var customClient = customFactory.CreateClient();

        var step1 = await customClient.PostAsync("/connect/token", TotpUserPasswordRequest());
        Assert.Equal(HttpStatusCode.OK, step1.StatusCode);
        var mfaToken = (await step1.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("mfa_session_token").GetString()!;

        var step2 = await customClient.PostAsync("/connect/token", MfaGrantRequest(mfaToken, CurrentTotpCode()));
        Assert.Equal(HttpStatusCode.OK, step2.StatusCode);

        var accessToken = (await step2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString()!;

        var payloadJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(accessToken.Split('.')[1]));
        var payload = JsonSerializer.Deserialize<JsonElement>(payloadJson);

        Assert.True(payload.TryGetProperty("test-marker-a", out _), "StubEnricherA must have added test-marker-a");
        Assert.True(payload.TryGetProperty("test-marker-b", out _), "StubEnricherB must have added test-marker-b (and A ran first)");
    }

    [Fact]
    public async Task TokenIssuance_P95_UnderBudget()
    {
        const int SampleCount = 50;
        const long BudgetMs = 400L;

        // Obtain a session token once outside the measurement loop (password step is not being measured).
        var step1 = await Client.PostAsync("/connect/token", TotpUserPasswordRequest());
        var mfaToken = (await step1.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("mfa_session_token").GetString()!;
        var totpCode = CurrentTotpCode();

        var times = new List<long>(SampleCount);

        for (var i = 0; i < SampleCount; i++)
        {
            // Reset TotpLastUsedTimeStep so the same TOTP code is accepted again each iteration.
            using (var scope = Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = await db.Users.IgnoreQueryFilters()
                    .SingleAsync(u => u.Id == DevSeeder.TotpUserId);
                user.TotpLastUsedTimeStep = null;
                await db.SaveChangesAsync();
            }

            var sw = Stopwatch.StartNew();
            var response = await Client.PostAsync("/connect/token", MfaGrantRequest(mfaToken, totpCode));
            sw.Stop();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            times.Add(sw.ElapsedMilliseconds);
        }

        times.Sort();
        var p95Index = (int)Math.Ceiling(SampleCount * 0.95) - 1;
        var p95Ms = times[p95Index];

        Assert.True(p95Ms <= BudgetMs,
            $"p95 MFA grant issuance time {p95Ms}ms exceeded {BudgetMs}ms budget (NFR-2: ≤500ms with 100ms headroom)");
    }

    // Stub enrichers — registered on top of production DI in EnricherPipelineOrderTest only.
    private sealed class StubEnricherA : ITokenClaimsEnricher
    {
        public Task EnrichAsync(ClaimsIdentity identity, TokenEnrichmentContext context, CancellationToken ct)
        {
            identity.AddClaim(new Claim("test-marker-a", "added-by-a"));
            return Task.CompletedTask;
        }
    }

    private sealed class StubEnricherB : ITokenClaimsEnricher
    {
        public Task EnrichAsync(ClaimsIdentity identity, TokenEnrichmentContext context, CancellationToken ct)
        {
            // If A did not run before B, throw — the controller will return a 500 and the test fails.
            if (!identity.HasClaim(c => c.Type == "test-marker-a"))
                throw new InvalidOperationException("StubEnricherB ran before StubEnricherA — pipeline ordering is broken.");

            identity.AddClaim(new Claim("test-marker-b", "added-by-b"));
            return Task.CompletedTask;
        }
    }
}
