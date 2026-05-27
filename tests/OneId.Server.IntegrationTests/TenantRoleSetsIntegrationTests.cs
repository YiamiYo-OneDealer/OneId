using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
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
public class TenantRoleSetsIntegrationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
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

    // Seeds a Role in DevTenant and returns its Id — used as a valid roleId in role-set tests
    private async Task<Guid> SeedRoleAsync(string name)
    {
        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(DevSeeder.DevTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = DevSeeder.DevTenantId,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    // Seeds a Role in a different tenant and returns its Id — used for cross-tenant rejection tests
    private async Task<Guid> SeedRoleInOtherTenantAsync()
    {
        var tenantBId = Guid.NewGuid();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = tenantBId,
            Name = $"CrossTenantTest-{tenantBId:N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        using var scope2 = Factory.Services.CreateScope();
        var tenantCtx = scope2.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(tenantBId);
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantBId,
            Name = "OtherTenantRole",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db2.Roles.Add(role);
        await db2.SaveChangesAsync();
        return role.Id;
    }

    // ── AC1: POST creates role set ─────────────────────────────────────────────

    [Fact]
    public async Task Post_ValidRoleSet_Returns201WithInlineRoles()
    {
        var roleId = await SeedRoleAsync("Post_Valid_Role");
        var client = await AuthClientAsync();

        var response = await client.PostAsJsonAsync("/api/tenant/role-sets",
            new { name = "Test RoleSet", roleIds = new[] { roleId } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Test RoleSet", body.GetProperty("name").GetString());
        Assert.NotEqual(Guid.Empty, Guid.Parse(body.GetProperty("id").GetString()!));
        Assert.True(body.GetProperty("version").GetUInt32() > 0);
        Assert.NotNull(response.Headers.Location);
        var roles = body.GetProperty("roles").EnumerateArray().ToList();
        Assert.Single(roles);
        Assert.Equal(roleId.ToString(), roles[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task Post_WithCrossTenantRoleId_Returns422()
    {
        var otherTenantRoleId = await SeedRoleInOtherTenantAsync();
        var client = await AuthClientAsync();

        var response = await client.PostAsJsonAsync("/api/tenant/role-sets",
            new { name = "CrossTenant RoleSet", roleIds = new[] { otherTenantRoleId } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_role_ids", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Post_WithNonExistentRoleId_Returns422()
    {
        var client = await AuthClientAsync();

        var response = await client.PostAsJsonAsync("/api/tenant/role-sets",
            new { name = "Bad RoleSet", roleIds = new[] { Guid.NewGuid() } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_role_ids", body.GetProperty("error").GetString());
    }

    // ── AC2: GET list ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetList_Returns200WithPaginatedResult()
    {
        var roleId = await SeedRoleAsync("List_Role");
        var client = await AuthClientAsync();

        await client.PostAsJsonAsync("/api/tenant/role-sets",
            new { name = "List RoleSet", roleIds = new[] { roleId } });

        var response = await client.GetAsync("/api/tenant/role-sets?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 1);
        Assert.True(body.GetProperty("items").GetArrayLength() >= 1);
        // Each item must include inline roles
        var firstItem = body.GetProperty("items").EnumerateArray().First();
        Assert.True(firstItem.TryGetProperty("roles", out _));
    }

    // ── AC3: GET single ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_Existing_Returns200WithRoleSummaries()
    {
        var roleId = await SeedRoleAsync("GetById_Role");
        var client = await AuthClientAsync();

        var createResp = await client.PostAsJsonAsync("/api/tenant/role-sets",
            new { name = "GetById RoleSet", roleIds = new[] { roleId } });
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = createBody.GetProperty("id").GetString()!;

        var response = await client.GetAsync($"/api/tenant/role-sets/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, body.GetProperty("id").GetString());
        var roles = body.GetProperty("roles").EnumerateArray().ToList();
        Assert.Single(roles);
        Assert.Equal(roleId.ToString(), roles[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var client = await AuthClientAsync();
        var response = await client.GetAsync($"/api/tenant/role-sets/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── AC4: PUT update ────────────────────────────────────────────────────────

    [Fact]
    public async Task Put_ValidVersion_Returns200WithUpdatedRoleSet()
    {
        var roleId = await SeedRoleAsync("Put_Valid_Role");
        var client = await AuthClientAsync();

        var createResp = await client.PostAsJsonAsync("/api/tenant/role-sets",
            new { name = "Update RoleSet", roleIds = new[] { roleId } });
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = createBody.GetProperty("id").GetString()!;
        var version = createBody.GetProperty("version").GetUInt32();

        var putResp = await client.PutAsJsonAsync($"/api/tenant/role-sets/{id}",
            new { name = "Updated RoleSet", roleIds = new[] { roleId }, version });

        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);
        var body = await putResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated RoleSet", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Put_StaleVersion_Returns409()
    {
        var roleId = await SeedRoleAsync("Put_Stale_Role");
        var client = await AuthClientAsync();

        var createResp = await client.PostAsJsonAsync("/api/tenant/role-sets",
            new { name = "Stale RoleSet", roleIds = new[] { roleId } });
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = createBody.GetProperty("id").GetString()!;

        var putResp = await client.PutAsJsonAsync($"/api/tenant/role-sets/{id}",
            new { name = "New Name", roleIds = new[] { roleId }, version = 1u });

        Assert.Equal(HttpStatusCode.Conflict, putResp.StatusCode);
    }

    [Fact]
    public async Task Put_CrossTenantRoleId_Returns422()
    {
        var roleId = await SeedRoleAsync("Put_422_Role");
        var otherTenantRoleId = await SeedRoleInOtherTenantAsync();
        var client = await AuthClientAsync();

        var createResp = await client.PostAsJsonAsync("/api/tenant/role-sets",
            new { name = "422 RoleSet", roleIds = new[] { roleId } });
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = createBody.GetProperty("id").GetString()!;
        var version = createBody.GetProperty("version").GetUInt32();

        var putResp = await client.PutAsJsonAsync($"/api/tenant/role-sets/{id}",
            new { name = "New Name", roleIds = new[] { otherTenantRoleId }, version });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, putResp.StatusCode);
        var body = await putResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_role_ids", body.GetProperty("error").GetString());
    }

    // ── AC5: DELETE ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_UnassignedRoleSet_Returns204()
    {
        var roleId = await SeedRoleAsync("Delete_Role");
        var client = await AuthClientAsync();

        var createResp = await client.PostAsJsonAsync("/api/tenant/role-sets",
            new { name = "Delete RoleSet", roleIds = new[] { roleId } });
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = createBody.GetProperty("id").GetString()!;
        var version = createBody.GetProperty("version").GetUInt32();

        var deleteReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/tenant/role-sets/{id}")
        {
            Content = JsonContent.Create(new { version }),
        };
        var deleteResp = await client.SendAsync(deleteReq);
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var getResp = await client.GetAsync($"/api/tenant/role-sets/{id}");
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404()
    {
        var client = await AuthClientAsync();
        var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/tenant/role-sets/{Guid.NewGuid()}")
        {
            Content = JsonContent.Create(new { version = 1u }),
        };
        var response = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── AC7: Auth enforcement ──────────────────────────────────────────────────

    [Fact]
    public async Task GetList_Unauthenticated_Returns401()
    {
        var response = await Factory.CreateClient().GetAsync("/api/tenant/role-sets");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetList_WithoutTenantAdminRole_Returns403()
    {
        // admin@oneid.dev has no IsTenantAdmin — JWT contains no TenantAdmin role.
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

        var response = await client.GetAsync("/api/tenant/role-sets");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
