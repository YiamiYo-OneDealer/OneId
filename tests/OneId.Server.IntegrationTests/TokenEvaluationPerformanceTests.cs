using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.IntegrationTests.Helpers;
using OtpNet;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OneId.Server.IntegrationTests;

[Collection("IntegrationTests")]
public class TokenEvaluationPerformanceTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private async Task<string> IssueMfaTokenAsync()
    {
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
        return (await step2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString()!;
    }

    private FormUrlEncodedContent IntrospectRequest(string token) =>
        new(new Dictionary<string, string>
        {
            ["token"] = token,
            ["client_id"] = "oneid-sample-app",
            ["client_secret"] = "sample-app-secret",
        });

    // AC4: 40ms p95 ceiling — 10ms headroom against NFR-4's 50ms gate.
    // After the first call the cache is warm (permissions + dimensions cached for 5 min),
    // so subsequent calls avoid DB round-trips and should comfortably pass the threshold.
    [Fact]
    public async Task IntrospectionP95_Under40ms()
    {
        const int SampleCount = 50;
        const long BudgetMs = 40L;

        var accessToken = await IssueMfaTokenAsync();

        // Warm up: first call populates ICacheService for this userId/tenantId.
        await Client.PostAsync("/connect/introspect", IntrospectRequest(accessToken));

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
            $"p95 enriched introspection time {p95Ms}ms exceeded {BudgetMs}ms ceiling (NFR-4: ≤50ms p95, story gate: ≤40ms)");
    }
}
