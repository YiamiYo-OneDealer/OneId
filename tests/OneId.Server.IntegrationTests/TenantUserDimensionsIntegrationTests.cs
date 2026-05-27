using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Domain.Enums;
using OneId.Server.Infrastructure.Persistence;
using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.IntegrationTests.Helpers;
using OtpNet;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OneId.Server.IntegrationTests;

[Collection("IntegrationTests")]
[Trait("Category", "TenantAdmin")]
public class TenantUserDimensionsIntegrationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private async Task<HttpClient> AuthClientAsync()
    {
        var step1 = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = DevSeeder.TotpUserEmail,
                ["password"] = "Admin123!",
                ["client_id"] = "oneid-dev-client",
                ["scope"] = "openid",
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
                ["scope"] = "openid",
            }));
        step2.EnsureSuccessStatusCode();
        var token = (await step2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString()!;

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> SeedDimensionValueAsync(DimensionAxis axis, string value, bool isActive = true)
    {
        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(DevSeeder.DevTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = new DimensionValue
        {
            Id = Guid.NewGuid(),
            TenantId = DevSeeder.DevTenantId,
            Axis = axis,
            Value = value,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.DimensionValues.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }

    private async Task<Guid> SeedUserAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(DevSeeder.DevTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = DevSeeder.DevTenantId,
            Email = email,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    // ── PUT: set assignments ──────────────────────────────────────────────────

    [Fact]
    public async Task Put_ValidValueIds_Returns204AndGetConfirmsState()
    {
        var userId = await SeedUserAsync($"put-test-{Guid.NewGuid():N}@test.com");
        var companyId = await SeedDimensionValueAsync(DimensionAxis.Company, $"Acme-{Guid.NewGuid():N}");
        var makeId = await SeedDimensionValueAsync(DimensionAxis.Make, $"Toyota-{Guid.NewGuid():N}");
        var client = await AuthClientAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/tenant/users/{userId}/dimensions",
            new { valueIds = new[] { companyId, makeId } });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await client.GetAsync($"/api/tenant/users/{userId}/dimensions");
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("company").GetArrayLength());
        Assert.Equal(1, body.GetProperty("make").GetArrayLength());
        Assert.Equal(0, body.GetProperty("location").GetArrayLength());
    }

    [Fact]
    public async Task Put_EmptyList_ClearsAllAssignments()
    {
        var userId = await SeedUserAsync($"put-clear-{Guid.NewGuid():N}@test.com");
        var makeId = await SeedDimensionValueAsync(DimensionAxis.Make, $"BMW-{Guid.NewGuid():N}");
        var client = await AuthClientAsync();

        await client.PutAsJsonAsync($"/api/tenant/users/{userId}/dimensions",
            new { valueIds = new[] { makeId } });

        var clearResponse = await client.PutAsJsonAsync(
            $"/api/tenant/users/{userId}/dimensions",
            new { valueIds = Array.Empty<Guid>() });

        Assert.Equal(HttpStatusCode.NoContent, clearResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/tenant/users/{userId}/dimensions");
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("make").GetArrayLength());
    }

    [Fact]
    public async Task Put_ReplacesExistingAssignments()
    {
        var userId = await SeedUserAsync($"put-replace-{Guid.NewGuid():N}@test.com");
        var makeId1 = await SeedDimensionValueAsync(DimensionAxis.Make, $"Toyota-{Guid.NewGuid():N}");
        var makeId2 = await SeedDimensionValueAsync(DimensionAxis.Make, $"Honda-{Guid.NewGuid():N}");
        var client = await AuthClientAsync();

        await client.PutAsJsonAsync($"/api/tenant/users/{userId}/dimensions",
            new { valueIds = new[] { makeId1 } });

        var replaceResponse = await client.PutAsJsonAsync(
            $"/api/tenant/users/{userId}/dimensions",
            new { valueIds = new[] { makeId2 } });

        Assert.Equal(HttpStatusCode.NoContent, replaceResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/tenant/users/{userId}/dimensions");
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var makes = body.GetProperty("make").EnumerateArray().ToList();
        Assert.Single(makes);
    }

    [Fact]
    public async Task Put_MultipleValuesPerAxis_Allowed()
    {
        var userId = await SeedUserAsync($"put-multi-{Guid.NewGuid():N}@test.com");
        var makeId1 = await SeedDimensionValueAsync(DimensionAxis.Make, $"Toyota-{Guid.NewGuid():N}");
        var makeId2 = await SeedDimensionValueAsync(DimensionAxis.Make, $"BMW-{Guid.NewGuid():N}");
        var client = await AuthClientAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/tenant/users/{userId}/dimensions",
            new { valueIds = new[] { makeId1, makeId2 } });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await client.GetAsync($"/api/tenant/users/{userId}/dimensions");
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("make").GetArrayLength());
    }

    [Fact]
    public async Task Put_InactiveValue_Returns422()
    {
        var userId = await SeedUserAsync($"put-inactive-{Guid.NewGuid():N}@test.com");
        var inactiveId = await SeedDimensionValueAsync(DimensionAxis.Branch, $"ClosedBranch-{Guid.NewGuid():N}", isActive: false);
        var client = await AuthClientAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/tenant/users/{userId}/dimensions",
            new { valueIds = new[] { inactiveId } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_dimension_value", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Put_NonExistentValue_Returns422()
    {
        var userId = await SeedUserAsync($"put-novalue-{Guid.NewGuid():N}@test.com");
        var client = await AuthClientAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/tenant/users/{userId}/dimensions",
            new { valueIds = new[] { Guid.NewGuid() } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Put_NonExistentUser_Returns404()
    {
        var valueId = await SeedDimensionValueAsync(DimensionAxis.Location, $"Paris-{Guid.NewGuid():N}");
        var client = await AuthClientAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/tenant/users/{Guid.NewGuid()}/dimensions",
            new { valueIds = new[] { valueId } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("user_not_found", body.GetProperty("error").GetString());
    }

    // ── GET: grouped response ─────────────────────────────────────────────────

    [Fact]
    public async Task Get_UserWithNoAssignments_ReturnsAllFiveAxesEmpty()
    {
        var userId = await SeedUserAsync($"no-assign-{Guid.NewGuid():N}@test.com");
        var client = await AuthClientAsync();

        var response = await client.GetAsync($"/api/tenant/users/{userId}/dimensions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.TryGetProperty("company", out var company));
        Assert.True(body.TryGetProperty("location", out var location));
        Assert.True(body.TryGetProperty("branch", out var branch));
        Assert.True(body.TryGetProperty("make", out var make));
        Assert.True(body.TryGetProperty("marketSegment", out var marketSegment));

        Assert.Equal(JsonValueKind.Array, company.ValueKind);
        Assert.Equal(0, company.GetArrayLength());
        Assert.Equal(0, location.GetArrayLength());
        Assert.Equal(0, branch.GetArrayLength());
        Assert.Equal(0, make.GetArrayLength());
        Assert.Equal(0, marketSegment.GetArrayLength());
    }

    [Fact]
    public async Task Get_UserWithAssignments_ReturnsGroupedByAxis()
    {
        var userId = await SeedUserAsync($"grouped-{Guid.NewGuid():N}@test.com");
        var companyId = await SeedDimensionValueAsync(DimensionAxis.Company, $"Acme-{Guid.NewGuid():N}");
        var makeId1 = await SeedDimensionValueAsync(DimensionAxis.Make, $"Toyota-{Guid.NewGuid():N}");
        var makeId2 = await SeedDimensionValueAsync(DimensionAxis.Make, $"BMW-{Guid.NewGuid():N}");
        var client = await AuthClientAsync();

        await client.PutAsJsonAsync($"/api/tenant/users/{userId}/dimensions",
            new { valueIds = new[] { companyId, makeId1, makeId2 } });

        var response = await client.GetAsync($"/api/tenant/users/{userId}/dimensions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("company").GetArrayLength());
        Assert.Equal(2, body.GetProperty("make").GetArrayLength());
        Assert.Equal(0, body.GetProperty("location").GetArrayLength());
    }

    [Fact]
    public async Task Get_NonExistentUser_Returns404()
    {
        var client = await AuthClientAsync();

        var response = await client.GetAsync($"/api/tenant/users/{Guid.NewGuid()}/dimensions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("user_not_found", body.GetProperty("error").GetString());
    }
}
