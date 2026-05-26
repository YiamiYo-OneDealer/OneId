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
public class TenantDimensionsIntegrationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
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

    // ── AC2: POST creates dimension value ─────────────────────────────────────

    [Fact]
    public async Task Post_ValidValue_Returns201WithDto()
    {
        var client = await AuthClientAsync();
        var uniqueValue = $"Toyota-{Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync(
            "/api/tenant/dimensions/make/values",
            new { value = uniqueValue });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Make", body.GetProperty("axis").GetString());
        Assert.Equal(uniqueValue, body.GetProperty("value").GetString());
        Assert.NotEqual(Guid.Empty, Guid.Parse(body.GetProperty("id").GetString()!));
        Assert.True(body.GetProperty("version").GetUInt32() > 0);
    }

    [Fact]
    public async Task Post_ValidValue_CaseInsensitiveAxis_Returns201()
    {
        var client = await AuthClientAsync();
        var uniqueValue = $"Berlin-{Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync(
            "/api/tenant/dimensions/Location/values",
            new { value = uniqueValue });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── AC3: POST duplicate returns 409 ───────────────────────────────────────

    [Fact]
    public async Task Post_DuplicateValue_Returns409()
    {
        var uniqueValue = $"Duplicate-{Guid.NewGuid():N}";
        await SeedDimensionValueAsync(DimensionAxis.Branch, uniqueValue);
        var client = await AuthClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/tenant/dimensions/Branch/values",
            new { value = uniqueValue });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("duplicate_value", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Post_DuplicateInactiveValue_Returns409()
    {
        var uniqueValue = $"InactiveDupe-{Guid.NewGuid():N}";
        await SeedDimensionValueAsync(DimensionAxis.Company, uniqueValue, isActive: false);
        var client = await AuthClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/tenant/dimensions/Company/values",
            new { value = uniqueValue });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── AC4: GET returns active values sorted ─────────────────────────────────

    [Fact]
    public async Task Get_ActiveValues_ReturnsListSortedByValue()
    {
        await SeedDimensionValueAsync(DimensionAxis.Make, $"Zebra-{Guid.NewGuid():N}");
        await SeedDimensionValueAsync(DimensionAxis.Make, $"Apple-{Guid.NewGuid():N}");
        var client = await AuthClientAsync();

        var response = await client.GetAsync("/api/tenant/dimensions/Make/values");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        var items = body.EnumerateArray().ToList();
        Assert.True(items.Count >= 2);
        // Verify sorted order
        var values = items.Select(i => i.GetProperty("value").GetString()!).ToList();
        Assert.Equal(values.OrderBy(v => v).ToList(), values);
    }

    // ── AC5+7: DELETE soft-deletes and hides from GET ─────────────────────────

    [Fact]
    public async Task Delete_ExistingValue_Returns204AndHidesFromList()
    {
        var uniqueValue = $"ToDelete-{Guid.NewGuid():N}";
        var id = await SeedDimensionValueAsync(DimensionAxis.Location, uniqueValue);
        var client = await AuthClientAsync();

        var deleteResponse = await client.DeleteAsync($"/api/tenant/dimensions/Location/values/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/tenant/dimensions/Location/values");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.EnumerateArray().ToList();
        Assert.DoesNotContain(items, i => i.GetProperty("value").GetString() == uniqueValue);
    }

    [Fact]
    public async Task Delete_AlreadyInactive_Returns404()
    {
        var uniqueValue = $"AlreadyInactive-{Guid.NewGuid():N}";
        var id = await SeedDimensionValueAsync(DimensionAxis.MarketSegment, uniqueValue, isActive: false);
        var client = await AuthClientAsync();

        // Soft-delete query filter means inactive entity is NOT found
        var response = await client.DeleteAsync($"/api/tenant/dimensions/MarketSegment/values/{id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── AC6: DELETE non-existent returns 404 ─────────────────────────────────

    [Fact]
    public async Task Delete_NonExistentId_Returns404()
    {
        var client = await AuthClientAsync();

        var response = await client.DeleteAsync($"/api/tenant/dimensions/Company/values/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── AC7: GET excludes inactive ────────────────────────────────────────────

    [Fact]
    public async Task Get_InactiveValue_NotReturnedInList()
    {
        var uniqueValue = $"HiddenInactive-{Guid.NewGuid():N}";
        await SeedDimensionValueAsync(DimensionAxis.Company, uniqueValue, isActive: false);
        var client = await AuthClientAsync();

        var response = await client.GetAsync("/api/tenant/dimensions/Company/values");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.EnumerateArray().ToList();
        Assert.DoesNotContain(items, i => i.GetProperty("value").GetString() == uniqueValue);
    }

    // ── AC8: Invalid axis returns 400 ─────────────────────────────────────────

    [Fact]
    public async Task Get_InvalidAxis_Returns400()
    {
        var client = await AuthClientAsync();

        var response = await client.GetAsync("/api/tenant/dimensions/InvalidAxis/values");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_axis", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Post_InvalidAxis_Returns400()
    {
        var client = await AuthClientAsync();

        var response = await client.PostAsJsonAsync("/api/tenant/dimensions/NotAnAxis/values", new { value = "test" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_axis", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Delete_InvalidAxis_Returns400()
    {
        var client = await AuthClientAsync();

        var response = await client.DeleteAsync($"/api/tenant/dimensions/NotAnAxis/values/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_axis", body.GetProperty("error").GetString());
    }
}
