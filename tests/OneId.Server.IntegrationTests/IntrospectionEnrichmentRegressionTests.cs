using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Domain.Enums;
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
public class IntrospectionEnrichmentRegressionTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
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

    // AC5: When a dimension assignment changes and the cache is cleared,
    // the next introspection reflects the updated dimension values.
    [Fact]
    public async Task DimensionAssignmentChange_ReflectedAfterCacheInvalidation()
    {
        // Step 1: Seed a DimensionValue and UserDimensionAssignment for the TotpUser.
        Guid dimensionValueId;
        using (var scope = Factory.Services.CreateScope())
        {
            var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
            tenantCtx.Initialize(DevSeeder.DevTenantId);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var dimValue = new DimensionValue
            {
                Id = Guid.NewGuid(),
                TenantId = DevSeeder.DevTenantId,
                Axis = DimensionAxis.Company,
                Value = "Contoso",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            db.DimensionValues.Add(dimValue);

            db.UserDimensionAssignments.Add(new UserDimensionAssignment
            {
                Id = Guid.NewGuid(),
                UserId = DevSeeder.TotpUserId,
                DimensionValueId = dimValue.Id,
                AssignedAt = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync();
            dimensionValueId = dimValue.Id;
        }

        // Step 2: Issue token and introspect — expect "Contoso" in Company axis.
        var accessToken = await IssueMfaTokenAsync();
        var firstResponse = await Client.PostAsync("/connect/introspect", IntrospectRequest(accessToken));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(firstBody.GetProperty("active").GetBoolean());
        var firstDims = firstBody.GetProperty("dimensional_attributes");
        var companyValues = firstDims.GetProperty("Company").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        Assert.Contains("Contoso", companyValues);

        // Step 3: Remove the dimension assignment.
        using (var scope = Factory.Services.CreateScope())
        {
            var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
            tenantCtx.Initialize(DevSeeder.DevTenantId);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var assignment = await db.UserDimensionAssignments
                .FirstOrDefaultAsync(a => a.UserId == DevSeeder.TotpUserId && a.DimensionValueId == dimensionValueId);
            if (assignment is not null)
            {
                db.UserDimensionAssignments.Remove(assignment);
                await db.SaveChangesAsync();
            }
        }

        // Step 4: Clear IMemoryCache to simulate cache expiry (bypasses 5-min TTL for test purposes).
        // Resolving IMemoryCache directly is acceptable in test code — the AR-10 boundary rule applies
        // to production code only.
        var memoryCache = Factory.Services.GetRequiredService<IMemoryCache>();
        ((MemoryCache)memoryCache).Clear();

        // Step 5: Introspect again — Company axis must now be empty.
        var secondResponse = await Client.PostAsync("/connect/introspect", IntrospectRequest(accessToken));
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(secondBody.GetProperty("active").GetBoolean());
        var secondDims = secondBody.GetProperty("dimensional_attributes");
        var companyValuesAfter = secondDims.GetProperty("Company").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        Assert.DoesNotContain("Contoso", companyValuesAfter);
    }
}
