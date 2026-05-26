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

    // ── AC2: POST creates assignment ──────────────────────────────────────────

    [Fact]
    public async Task Post_ValidAssignment_Returns201WithDto()
    {
        var userId = await SeedUserAsync($"assign-test-{Guid.NewGuid():N}@test.com");
        var valueId = await SeedDimensionValueAsync(DimensionAxis.Company, $"Acme-{Guid.NewGuid():N}");
        var client = await AuthClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/tenant/users/{userId}/dimensions",
            new { valueId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(userId.ToString(), body.GetProperty("userId").GetString());
        Assert.Equal(valueId.ToString(), body.GetProperty("dimensionValueId").GetString());
        Assert.Equal("Company", body.GetProperty("axis").GetString());
        Assert.NotEqual(Guid.Empty, Guid.Parse(body.GetProperty("id").GetString()!));
    }

    [Fact]
    public async Task Post_MultipleValuesToSameAxis_Allowed()
    {
        var userId = await SeedUserAsync($"multi-axis-{Guid.NewGuid():N}@test.com");
        var valueId1 = await SeedDimensionValueAsync(DimensionAxis.Make, $"Toyota-{Guid.NewGuid():N}");
        var valueId2 = await SeedDimensionValueAsync(DimensionAxis.Make, $"Honda-{Guid.NewGuid():N}");
        var client = await AuthClientAsync();

        var r1 = await client.PostAsJsonAsync($"/api/tenant/users/{userId}/dimensions", new { valueId = valueId1 });
        var r2 = await client.PostAsJsonAsync($"/api/tenant/users/{userId}/dimensions", new { valueId = valueId2 });

        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
    }

    // ── AC3: POST inactive/cross-tenant value returns 422 ────────────────────

    [Fact]
    public async Task Post_InactiveValue_Returns422()
    {
        var userId = await SeedUserAsync($"inactive-val-{Guid.NewGuid():N}@test.com");
        var inactiveValueId = await SeedDimensionValueAsync(DimensionAxis.Branch, $"ClosedBranch-{Guid.NewGuid():N}", isActive: false);
        var client = await AuthClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/tenant/users/{userId}/dimensions",
            new { valueId = inactiveValueId });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_dimension_value", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Post_NonExistentValue_Returns422()
    {
        var userId = await SeedUserAsync($"nonexistent-val-{Guid.NewGuid():N}@test.com");
        var client = await AuthClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/tenant/users/{userId}/dimensions",
            new { valueId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ── AC4: POST with non-existent user returns 404 ─────────────────────────

    [Fact]
    public async Task Post_NonExistentUser_Returns404()
    {
        var valueId = await SeedDimensionValueAsync(DimensionAxis.Location, $"Paris-{Guid.NewGuid():N}");
        var client = await AuthClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/tenant/users/{Guid.NewGuid()}/dimensions",
            new { valueId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("user_not_found", body.GetProperty("error").GetString());
    }

    // ── AC5: POST duplicate returns 409 ──────────────────────────────────────

    [Fact]
    public async Task Post_DuplicateAssignment_Returns409()
    {
        var userId = await SeedUserAsync($"dupe-assign-{Guid.NewGuid():N}@test.com");
        var valueId = await SeedDimensionValueAsync(DimensionAxis.MarketSegment, $"Retail-{Guid.NewGuid():N}");
        var client = await AuthClientAsync();

        await client.PostAsJsonAsync($"/api/tenant/users/{userId}/dimensions", new { valueId });
        var response = await client.PostAsJsonAsync($"/api/tenant/users/{userId}/dimensions", new { valueId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("already_assigned", body.GetProperty("error").GetString());
    }

    // ── AC6: DELETE physically removes assignment ─────────────────────────────

    [Fact]
    public async Task Delete_ExistingAssignment_Returns204()
    {
        var userId = await SeedUserAsync($"delete-assign-{Guid.NewGuid():N}@test.com");
        var valueId = await SeedDimensionValueAsync(DimensionAxis.Company, $"DeleteMe-{Guid.NewGuid():N}");
        var client = await AuthClientAsync();

        var createResponse = await client.PostAsJsonAsync(
            $"/api/tenant/users/{userId}/dimensions",
            new { valueId });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var assignmentId = created.GetProperty("id").GetString()!;

        var deleteResponse = await client.DeleteAsync(
            $"/api/tenant/users/{userId}/dimensions/{assignmentId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_OtherAssignmentsUnaffected()
    {
        var userId = await SeedUserAsync($"multi-delete-{Guid.NewGuid():N}@test.com");
        var valueId1 = await SeedDimensionValueAsync(DimensionAxis.Branch, $"Branch1-{Guid.NewGuid():N}");
        var valueId2 = await SeedDimensionValueAsync(DimensionAxis.Branch, $"Branch2-{Guid.NewGuid():N}");
        var client = await AuthClientAsync();

        var r1 = await client.PostAsJsonAsync($"/api/tenant/users/{userId}/dimensions", new { valueId = valueId1 });
        var r2 = await client.PostAsJsonAsync($"/api/tenant/users/{userId}/dimensions", new { valueId = valueId2 });
        var assignmentId1 = (await r1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        await client.DeleteAsync($"/api/tenant/users/{userId}/dimensions/{assignmentId1}");

        var getResponse = await client.GetAsync($"/api/tenant/users/{userId}/dimensions");
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var branches = body.GetProperty("branch").EnumerateArray().ToList();
        Assert.Single(branches);
    }

    // ── AC7: DELETE non-existent returns 404 ─────────────────────────────────

    [Fact]
    public async Task Delete_NonExistentAssignment_Returns404()
    {
        var userId = await SeedUserAsync($"del-404-{Guid.NewGuid():N}@test.com");
        var client = await AuthClientAsync();

        var response = await client.DeleteAsync(
            $"/api/tenant/users/{userId}/dimensions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── AC8: GET returns grouped response with all 5 axes ────────────────────

    [Fact]
    public async Task Get_UserWithNoAssignments_ReturnsAllFiveAxesEmpty()
    {
        var userId = await SeedUserAsync($"no-assign-{Guid.NewGuid():N}@test.com");
        var client = await AuthClientAsync();

        var response = await client.GetAsync($"/api/tenant/users/{userId}/dimensions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // All 5 axes must be present
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
        var companyValueId = await SeedDimensionValueAsync(DimensionAxis.Company, $"Acme-{Guid.NewGuid():N}");
        var makeValueId1 = await SeedDimensionValueAsync(DimensionAxis.Make, $"Toyota-{Guid.NewGuid():N}");
        var makeValueId2 = await SeedDimensionValueAsync(DimensionAxis.Make, $"BMW-{Guid.NewGuid():N}");
        var client = await AuthClientAsync();

        await client.PostAsJsonAsync($"/api/tenant/users/{userId}/dimensions", new { valueId = companyValueId });
        await client.PostAsJsonAsync($"/api/tenant/users/{userId}/dimensions", new { valueId = makeValueId1 });
        await client.PostAsJsonAsync($"/api/tenant/users/{userId}/dimensions", new { valueId = makeValueId2 });

        var response = await client.GetAsync($"/api/tenant/users/{userId}/dimensions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("company").GetArrayLength());
        Assert.Equal(2, body.GetProperty("make").GetArrayLength());
        Assert.Equal(0, body.GetProperty("location").GetArrayLength());
    }

    // ── AC9: GET with non-existent user returns 404 ──────────────────────────

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
