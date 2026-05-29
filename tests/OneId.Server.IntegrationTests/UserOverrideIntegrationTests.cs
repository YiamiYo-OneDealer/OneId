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
public class UserOverrideIntegrationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> SeedUserAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(DevSeeder.DevTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = DevSeeder.DevTenantId,
            Email = $"override-test-{Guid.NewGuid():N}@test.com",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static string ActivePermissionId() => Permissions.OneIdTenantsView;

    // ── AC2: POST creates override ─────────────────────────────────────────────

    [Fact]
    public async Task Post_ValidOverride_Returns201WithDto()
    {
        var client = await AuthClientAsync();
        var userId = await SeedUserAsync();
        var permId = ActivePermissionId();

        var response = await client.PostAsJsonAsync(
            $"/api/tenant/users/{userId}/overrides",
            new { permissionId = permId, overrideType = "Deny", reason = "Test denial" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(permId, body.GetProperty("permissionId").GetString());
        Assert.Equal("Deny", body.GetProperty("overrideType").GetString());
        Assert.Equal("Test denial", body.GetProperty("reason").GetString());
        Assert.False(body.GetProperty("isExpired").GetBoolean());
    }

    [Fact]
    public async Task Post_InactivePermission_Returns422()
    {
        var client = await AuthClientAsync();
        var userId = await SeedUserAsync();

        // Seed an inactive permission
        var inactivePerm = "od.inactive.test";
        using (var scope = Factory.Services.CreateScope())
        {
            var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
            tenantCtx.Initialize(DevSeeder.DevTenantId);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Permissions.Add(new Permission
            {
                Id = Guid.NewGuid(),
                PermissionId = inactivePerm,
                Label = "Inactive Test",
                Status = PermissionStatus.Inactive,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            $"/api/tenant/users/{userId}/overrides",
            new { permissionId = inactivePerm, overrideType = "Deny", reason = "Should fail" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Post_NonExistentPermission_Returns422()
    {
        var client = await AuthClientAsync();
        var userId = await SeedUserAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/tenant/users/{userId}/overrides",
            new { permissionId = "od.does.not.exist", overrideType = "Allow", reason = "Should fail" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Post_EmptyReason_Returns422()
    {
        var client = await AuthClientAsync();
        var userId = await SeedUserAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/tenant/users/{userId}/overrides",
            new { permissionId = ActivePermissionId(), overrideType = "Allow", reason = "" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Post_DuplicateOverride_Returns409()
    {
        var client = await AuthClientAsync();
        var userId = await SeedUserAsync();
        var permId = ActivePermissionId();
        var body = new { permissionId = permId, overrideType = "Allow", reason = "First override" };

        var first = await client.PostAsJsonAsync($"/api/tenant/users/{userId}/overrides", body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync($"/api/tenant/users/{userId}/overrides",
            new { permissionId = permId, overrideType = "Deny", reason = "Second override" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // ── AC3: GET returns all including expired ─────────────────────────────────

    [Fact]
    public async Task Get_IncludesExpiredOverrides_WithIsExpiredFlag()
    {
        var userId = await SeedUserAsync();
        var permId = ActivePermissionId();

        // Seed an expired override directly in DB
        using (var scope = Factory.Services.CreateScope())
        {
            var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
            tenantCtx.Initialize(DevSeeder.DevTenantId);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserPermissionOverrides.Add(new UserPermissionOverride
            {
                Id = Guid.NewGuid(),
                TenantId = DevSeeder.DevTenantId,
                UserId = userId,
                PermissionId = permId,
                OverrideType = PermissionOverrideType.Deny,
                Reason = "Expired denial",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = Guid.Empty,
            });
            await db.SaveChangesAsync();
        }

        var client = await AuthClientAsync();
        var response = await client.GetAsync($"/api/tenant/users/{userId}/overrides");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.Single(items!);
        Assert.True(items![0].GetProperty("isExpired").GetBoolean());
    }

    // ── AC4: DELETE removes record ─────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingOverride_Returns204()
    {
        var client = await AuthClientAsync();
        var userId = await SeedUserAsync();

        var createResp = await client.PostAsJsonAsync(
            $"/api/tenant/users/{userId}/overrides",
            new { permissionId = ActivePermissionId(), overrideType = "Deny", reason = "To be deleted" });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var overrideId = created.GetProperty("id").GetString()!;

        var deleteResp = await client.DeleteAsync($"/api/tenant/users/{userId}/overrides/{overrideId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var getResp = await client.GetAsync($"/api/tenant/users/{userId}/overrides");
        var items = await getResp.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.Empty(items!);
    }

    [Fact]
    public async Task Delete_NonExistentOverride_Returns404()
    {
        var client = await AuthClientAsync();
        var userId = await SeedUserAsync();

        var response = await client.DeleteAsync($"/api/tenant/users/{userId}/overrides/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Auth: 403 without TenantAdmin role ────────────────────────────────────

    [Fact]
    public async Task AllEndpoints_WithoutAuth_Return401Or403()
    {
        var userId = await SeedUserAsync();

        var getResp = await Client.GetAsync($"/api/tenant/users/{userId}/overrides");
        Assert.Equal(HttpStatusCode.Unauthorized, getResp.StatusCode);

        var postResp = await Client.PostAsJsonAsync(
            $"/api/tenant/users/{userId}/overrides",
            new { permissionId = "od.test", overrideType = "Allow", reason = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, postResp.StatusCode);

        var deleteResp = await Client.DeleteAsync($"/api/tenant/users/{userId}/overrides/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, deleteResp.StatusCode);
    }
}
