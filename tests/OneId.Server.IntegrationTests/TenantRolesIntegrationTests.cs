using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
public class TenantRolesIntegrationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    // TotpUser has IsTenantAdmin = true — same two-step auth as InternalTenantsIntegrationTests
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

    // A valid permissionId that exists in DevSeeder (seeded from PermissionCatalog)
    private const string ValidPermissionId = "oneid.admin.roles.view";

    // ── AC1: POST creates role ────────────────────────────────────────────────

    [Fact]
    public async Task Post_ValidRole_Returns201WithBody()
    {
        var client = await AuthClientAsync();
        var response = await client.PostAsJsonAsync("/api/tenant/roles",
            new { name = "Test Role", permissionIds = new[] { ValidPermissionId } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Test Role", body.GetProperty("name").GetString());
        Assert.NotEqual(Guid.Empty, Guid.Parse(body.GetProperty("id").GetString()!));
        Assert.True(body.GetProperty("version").GetUInt32() > 0);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task Post_WithInvalidPermissionId_Returns422()
    {
        var client = await AuthClientAsync();
        var response = await client.PostAsJsonAsync("/api/tenant/roles",
            new { name = "Bad Role", permissionIds = new[] { "od.does.not.exist" } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_permission_ids", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Post_WithInactivePermissionId_Returns422()
    {
        // Deactivate a permission directly in DB, then try to reference it
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var perm = await db.Permissions.FirstAsync(p => p.PermissionId == ValidPermissionId);
            perm.Status = OneId.Server.Domain.Enums.PermissionStatus.Inactive;
            await db.SaveChangesAsync();
        }

        var client = await AuthClientAsync();
        var response = await client.PostAsJsonAsync("/api/tenant/roles",
            new { name = "Inactive Perm Role", permissionIds = new[] { ValidPermissionId } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ── AC2: GET list ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetList_Returns200WithPaginatedResult()
    {
        var client = await AuthClientAsync();

        // Create a role first
        await client.PostAsJsonAsync("/api/tenant/roles",
            new { name = "List Role", permissionIds = new[] { ValidPermissionId } });

        var response = await client.GetAsync("/api/tenant/roles?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 1);
        Assert.True(body.GetProperty("items").GetArrayLength() >= 1);
    }

    // ── AC3: GET single ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_Existing_Returns200WithPermissionIds()
    {
        var client = await AuthClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/tenant/roles",
            new { name = "Get Role", permissionIds = new[] { ValidPermissionId } });
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = createBody.GetProperty("id").GetString()!;

        var response = await client.GetAsync($"/api/tenant/roles/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, body.GetProperty("id").GetString());
        var permIds = body.GetProperty("permissionIds").EnumerateArray().Select(p => p.GetString()).ToList();
        Assert.Contains(ValidPermissionId, permIds);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var client = await AuthClientAsync();
        var response = await client.GetAsync($"/api/tenant/roles/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── AC4: PUT update ───────────────────────────────────────────────────────

    [Fact]
    public async Task Put_ValidVersion_Returns200WithUpdatedRole()
    {
        var client = await AuthClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/tenant/roles",
            new { name = "Update Role", permissionIds = new[] { ValidPermissionId } });
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = createBody.GetProperty("id").GetString()!;
        var version = createBody.GetProperty("version").GetUInt32();

        var putResp = await client.PutAsJsonAsync($"/api/tenant/roles/{id}",
            new { name = "Updated Role", permissionIds = new[] { ValidPermissionId }, version });

        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);
        var body = await putResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Role", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Put_StaleVersion_Returns409()
    {
        var client = await AuthClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/tenant/roles",
            new { name = "Stale Role", permissionIds = new[] { ValidPermissionId } });
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = createBody.GetProperty("id").GetString()!;

        var putResp = await client.PutAsJsonAsync($"/api/tenant/roles/{id}",
            new { name = "New Name", permissionIds = new[] { ValidPermissionId }, version = 1u });

        Assert.Equal(HttpStatusCode.Conflict, putResp.StatusCode);
    }

    [Fact]
    public async Task Put_InvalidPermissionId_Returns422()
    {
        var client = await AuthClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/tenant/roles",
            new { name = "422 Role", permissionIds = new[] { ValidPermissionId } });
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = createBody.GetProperty("id").GetString()!;
        var version = createBody.GetProperty("version").GetUInt32();

        var putResp = await client.PutAsJsonAsync($"/api/tenant/roles/{id}",
            new { name = "New Name", permissionIds = new[] { "od.bad.perm" }, version });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, putResp.StatusCode);
    }

    // ── AC5: DELETE ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_UnassignedRole_Returns204()
    {
        var client = await AuthClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/tenant/roles",
            new { name = "Delete Role", permissionIds = new[] { ValidPermissionId } });
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = createBody.GetProperty("id").GetString()!;

        var deleteResp = await client.DeleteAsync($"/api/tenant/roles/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var getResp = await client.GetAsync($"/api/tenant/roles/{id}");
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404()
    {
        var client = await AuthClientAsync();
        var response = await client.DeleteAsync($"/api/tenant/roles/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── AC7: Auth enforcement ─────────────────────────────────────────────────

    [Fact]
    public async Task GetList_Unauthenticated_Returns401()
    {
        var response = await Factory.CreateClient().GetAsync("/api/tenant/roles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetList_WithoutTenantAdminRole_Returns403()
    {
        // admin@oneid.dev has no IsTenantAdmin — JWT contains no TenantAdmin role.
        // No TOTP enrolled, so password grant returns access_token directly.
        var tokenResp = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = "admin@oneid.dev",
                ["password"] = "Admin123!",
                ["client_id"] = "oneid-dev-client",
                ["scope"] = "openid",
            }));
        tokenResp.EnsureSuccessStatusCode();
        var token = (await tokenResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString()!;

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/tenant/roles");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
