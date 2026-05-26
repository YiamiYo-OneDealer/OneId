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
public class TenantGroupsIntegrationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
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

    private async Task<Guid> SeedRoleSetAsync(string name, Guid roleId)
    {
        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(DevSeeder.DevTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleSet = new RoleSet
        {
            Id = Guid.NewGuid(),
            TenantId = DevSeeder.DevTenantId,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            RoleSetRoles = [new RoleSetRole { RoleId = roleId }],
        };
        db.RoleSets.Add(roleSet);
        await db.SaveChangesAsync();
        return roleSet.Id;
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

    private async Task<Guid> SeedRoleSetInOtherTenantAsync()
    {
        var tenantBId = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = tenantBId,
            Name = $"CrossTenantTestRS-{tenantBId:N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        using var scope2 = Factory.Services.CreateScope();
        var tenantCtx = scope2.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(tenantBId);
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleSet = new RoleSet
        {
            Id = Guid.NewGuid(),
            TenantId = tenantBId,
            Name = "OtherTenantRoleSet",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db2.RoleSets.Add(roleSet);
        await db2.SaveChangesAsync();
        return roleSet.Id;
    }

    // ── AC1: POST creates group ────────────────────────────────────────────────

    [Fact]
    public async Task Post_ValidGroup_Returns201WithInlineRolesAndRoleSets()
    {
        var roleId = await SeedRoleAsync("Post_Valid_Role");
        var roleSetId = await SeedRoleSetAsync("Post_Valid_RoleSet", roleId);
        var client = await AuthClientAsync();

        var response = await client.PostAsJsonAsync("/api/tenant/groups",
            new { name = "Test Group", roleIds = new[] { roleId }, roleSetIds = new[] { roleSetId } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Test Group", body.GetProperty("name").GetString());
        Assert.NotEqual(Guid.Empty, Guid.Parse(body.GetProperty("id").GetString()!));
        Assert.True(body.GetProperty("version").GetUInt32() > 0);
        Assert.NotNull(response.Headers.Location);

        var roles = body.GetProperty("roles").EnumerateArray().ToList();
        Assert.Single(roles);
        Assert.Equal(roleId.ToString(), roles[0].GetProperty("id").GetString());

        var roleSets = body.GetProperty("roleSets").EnumerateArray().ToList();
        Assert.Single(roleSets);
        Assert.Equal(roleSetId.ToString(), roleSets[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task Post_WithCrossTenantRoleId_Returns422()
    {
        var otherRoleId = await SeedRoleInOtherTenantAsync();
        var client = await AuthClientAsync();

        var response = await client.PostAsJsonAsync("/api/tenant/groups",
            new { name = "Bad Group", roleIds = new[] { otherRoleId }, roleSetIds = Array.Empty<Guid>() });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_role_ids", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Post_WithCrossTenantRoleSetId_Returns422()
    {
        var otherRoleSetId = await SeedRoleSetInOtherTenantAsync();
        var client = await AuthClientAsync();

        var response = await client.PostAsJsonAsync("/api/tenant/groups",
            new { name = "Bad Group", roleIds = Array.Empty<Guid>(), roleSetIds = new[] { otherRoleSetId } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_role_set_ids", body.GetProperty("error").GetString());
    }

    // ── AC2: GET list ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_List_ReturnsPaginatedGroupsWithTotalCount()
    {
        var client = await AuthClientAsync();
        await client.PostAsJsonAsync("/api/tenant/groups",
            new { name = "ListGroup1", roleIds = Array.Empty<Guid>(), roleSetIds = Array.Empty<Guid>() });
        await client.PostAsJsonAsync("/api/tenant/groups",
            new { name = "ListGroup2", roleIds = Array.Empty<Guid>(), roleSetIds = Array.Empty<Guid>() });

        var response = await client.GetAsync("/api/tenant/groups?page=1&pageSize=25");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 2);
    }

    // ── AC3: GET single ────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_ExistingGroup_Returns200WithSummaries()
    {
        var roleId = await SeedRoleAsync("GetSingle_Role");
        var client = await AuthClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/tenant/groups",
            new { name = "GetSingleGroup", roleIds = new[] { roleId }, roleSetIds = Array.Empty<Guid>() });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString();

        var response = await client.GetAsync($"/api/tenant/groups/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("GetSingleGroup", body.GetProperty("name").GetString());
        Assert.Single(body.GetProperty("roles").EnumerateArray().ToList());
    }

    [Fact]
    public async Task Get_NonExistentGroup_Returns404()
    {
        var client = await AuthClientAsync();
        var response = await client.GetAsync($"/api/tenant/groups/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── AC4: PUT update ────────────────────────────────────────────────────────

    [Fact]
    public async Task Put_ValidVersion_Returns200Updated()
    {
        var roleId = await SeedRoleAsync("Update_Role");
        var client = await AuthClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/tenant/groups",
            new { name = "UpdateGroup", roleIds = Array.Empty<Guid>(), roleSetIds = Array.Empty<Guid>() });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString();
        var version = created.GetProperty("version").GetUInt32();

        var updateResp = await client.PutAsJsonAsync($"/api/tenant/groups/{id}",
            new { name = "UpdatedGroup", roleIds = new[] { roleId }, roleSetIds = Array.Empty<Guid>(), version });

        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var body = await updateResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("UpdatedGroup", body.GetProperty("name").GetString());
        Assert.Single(body.GetProperty("roles").EnumerateArray().ToList());
    }

    [Fact]
    public async Task Put_StaleVersion_Returns409()
    {
        var client = await AuthClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/tenant/groups",
            new { name = "StaleGroup", roleIds = Array.Empty<Guid>(), roleSetIds = Array.Empty<Guid>() });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString();

        var updateResp = await client.PutAsJsonAsync($"/api/tenant/groups/{id}",
            new { name = "StaleGroup", roleIds = Array.Empty<Guid>(), roleSetIds = Array.Empty<Guid>(), version = 0u });

        Assert.Equal(HttpStatusCode.Conflict, updateResp.StatusCode);
    }

    // ── AC5: DELETE group ──────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingGroup_Returns204()
    {
        var client = await AuthClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/tenant/groups",
            new { name = "DeleteGroup", roleIds = Array.Empty<Guid>(), roleSetIds = Array.Empty<Guid>() });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString();

        var deleteResp = await client.DeleteAsync($"/api/tenant/groups/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var getResp = await client.GetAsync($"/api/tenant/groups/{id}");
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistentGroup_Returns404()
    {
        var client = await AuthClientAsync();
        var response = await client.DeleteAsync($"/api/tenant/groups/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_GroupWithUser_Returns204_UserUnaffected()
    {
        var userId = await SeedUserAsync($"groupdelete-{Guid.NewGuid()}@test.com");
        var client = await AuthClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/tenant/groups",
            new { name = "GroupWithUser", roleIds = Array.Empty<Guid>(), roleSetIds = Array.Empty<Guid>() });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var groupId = created.GetProperty("id").GetString();

        await client.PutAsJsonAsync($"/api/tenant/groups/{groupId}/members",
            new { userId });

        var deleteResp = await client.DeleteAsync($"/api/tenant/groups/{groupId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        // Verify user still exists in DB
        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(DevSeeder.DevTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FindAsync(userId);
        Assert.NotNull(user);
    }

    // ── AC6: PUT add member ────────────────────────────────────────────────────

    [Fact]
    public async Task AddMember_ValidUser_Returns200()
    {
        var userId = await SeedUserAsync($"addmember-{Guid.NewGuid()}@test.com");
        var client = await AuthClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/tenant/groups",
            new { name = "MemberGroup", roleIds = Array.Empty<Guid>(), roleSetIds = Array.Empty<Guid>() });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var groupId = created.GetProperty("id").GetString();

        var addResp = await client.PutAsJsonAsync($"/api/tenant/groups/{groupId}/members",
            new { userId });

        Assert.Equal(HttpStatusCode.OK, addResp.StatusCode);
    }

    [Fact]
    public async Task AddMember_SameUserTwice_Returns200Idempotent()
    {
        var userId = await SeedUserAsync($"idempotent-{Guid.NewGuid()}@test.com");
        var client = await AuthClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/tenant/groups",
            new { name = "IdempotentGroup", roleIds = Array.Empty<Guid>(), roleSetIds = Array.Empty<Guid>() });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var groupId = created.GetProperty("id").GetString();

        await client.PutAsJsonAsync($"/api/tenant/groups/{groupId}/members", new { userId });
        var secondResp = await client.PutAsJsonAsync($"/api/tenant/groups/{groupId}/members", new { userId });

        Assert.Equal(HttpStatusCode.OK, secondResp.StatusCode);

        // Verify no duplicate records
        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(DevSeeder.DevTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = db.UserGroups.Count(ug => ug.UserId == userId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AddMember_CrossTenantUser_Returns404()
    {
        var otherTenantUserId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Tenants.Add(new Tenant
            {
                Id = tenantBId,
                Name = $"MemberCrossTenant-{tenantBId:N}",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        using (var scope = Factory.Services.CreateScope())
        {
            var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
            tenantCtx.Initialize(tenantBId);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User
            {
                Id = otherTenantUserId,
                TenantId = tenantBId,
                Email = $"crosstenant-{otherTenantUserId}@test.com",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var client = await AuthClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/tenant/groups",
            new { name = "CrossTenantMemberGroup", roleIds = Array.Empty<Guid>(), roleSetIds = Array.Empty<Guid>() });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var groupId = created.GetProperty("id").GetString();

        var addResp = await client.PutAsJsonAsync($"/api/tenant/groups/{groupId}/members",
            new { userId = otherTenantUserId });

        Assert.Equal(HttpStatusCode.NotFound, addResp.StatusCode);
    }

    // ── AC7: DELETE remove member ──────────────────────────────────────────────

    [Fact]
    public async Task RemoveMember_ExistingMembership_Returns204()
    {
        var userId = await SeedUserAsync($"removemember-{Guid.NewGuid()}@test.com");
        var client = await AuthClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/tenant/groups",
            new { name = "RemoveMemberGroup", roleIds = Array.Empty<Guid>(), roleSetIds = Array.Empty<Guid>() });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var groupId = created.GetProperty("id").GetString();

        await client.PutAsJsonAsync($"/api/tenant/groups/{groupId}/members", new { userId });

        var removeResp = await client.DeleteAsync($"/api/tenant/groups/{groupId}/members/{userId}");
        Assert.Equal(HttpStatusCode.NoContent, removeResp.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_NotAMember_Returns404()
    {
        var client = await AuthClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/tenant/groups",
            new { name = "NotMemberGroup", roleIds = Array.Empty<Guid>(), roleSetIds = Array.Empty<Guid>() });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var groupId = created.GetProperty("id").GetString();

        var removeResp = await client.DeleteAsync($"/api/tenant/groups/{groupId}/members/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, removeResp.StatusCode);
    }

    // ── AC9: Auth ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var response = await Client.GetAsync("/api/tenant/groups");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
