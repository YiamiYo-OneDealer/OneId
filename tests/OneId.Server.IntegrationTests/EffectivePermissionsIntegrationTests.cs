using Microsoft.EntityFrameworkCore;
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
[Trait("Category", "EffectivePermissions")]
public class EffectivePermissionsIntegrationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    // TotpUser is in SystemTenantId — seed all test data in that tenant.
    private static readonly Guid TestTenantId = SystemSeeder.SystemTenantId;

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
        tenantCtx.Initialize(TestTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            Email = $"eff-perm-{Guid.NewGuid():N}@test.com",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<Guid> SeedRoleWithPermissionsAsync(string roleName, params string[] permissionIds)
    {
        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(TestTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            Name = roleName,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        foreach (var permId in permissionIds)
        {
            var perm = await db.Permissions.IgnoreQueryFilters()
                .FirstAsync(p => p.PermissionId == permId);
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
        }
        await db.SaveChangesAsync();
        return role.Id;
    }

    private async Task<Guid> SeedGroupWithDirectRoleAsync(string groupName, Guid roleId)
    {
        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(TestTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var group = new Group
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            Name = groupName,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        db.GroupRoles.Add(new GroupRole { GroupId = group.Id, RoleId = roleId });
        await db.SaveChangesAsync();
        return group.Id;
    }

    private async Task<Guid> SeedGroupWithRoleSetAsync(string groupName, string roleSetName, Guid roleId)
    {
        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(TestTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var roleSet = new RoleSet
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            Name = roleSetName,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.RoleSets.Add(roleSet);
        await db.SaveChangesAsync();
        db.RoleSetRoles.Add(new RoleSetRole { RoleSetId = roleSet.Id, RoleId = roleId });
        await db.SaveChangesAsync();

        var group = new Group
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            Name = groupName,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        db.GroupRoleSets.Add(new GroupRoleSet { GroupId = group.Id, RoleSetId = roleSet.Id });
        await db.SaveChangesAsync();
        return group.Id;
    }

    private async Task AssignUserToGroupAsync(Guid userId, Guid groupId)
    {
        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(TestTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.UserGroups.Add(new UserGroup { UserId = userId, GroupId = groupId });
        await db.SaveChangesAsync();
    }

    private async Task SeedDenyOverrideAsync(Guid userId, string permissionId)
    {
        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(TestTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            UserId = userId,
            PermissionId = permissionId,
            OverrideType = PermissionOverrideType.Deny,
            Reason = "test deny",
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = Guid.Empty,
        });
        await db.SaveChangesAsync();
    }

    // ── AC2: Full provenance test (story spec test) ───────────────────────────

    [Fact]
    public async Task EffectivePermissionsIntegrationTest_ProvenanceChainAndDenyOverride()
    {
        var userId = await SeedUserAsync();

        // G1: Role R1 → od.crm.read
        var r1Id = await SeedRoleWithPermissionsAsync("R1", Permissions.CrmRead);
        var g1Id = await SeedGroupWithDirectRoleAsync("G1", r1Id);

        // G2: RoleSet RS1 → Role R2 → od.finance.read
        var r2Id = await SeedRoleWithPermissionsAsync("R2", Permissions.FinanceRead);
        var g2Id = await SeedGroupWithRoleSetAsync("G2", "RS1", r2Id);

        await AssignUserToGroupAsync(userId, g1Id);
        await AssignUserToGroupAsync(userId, g2Id);

        // DENY override on od.crm.write (not group-granted)
        await SeedDenyOverrideAsync(userId, Permissions.CrmWrite);

        var client = await AuthClientAsync();
        var response = await client.GetAsync($"/api/tenant/users/{userId}/effective-permissions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(userId.ToString(), body.GetProperty("userId").GetString());
        Assert.True(body.GetProperty("hasGroupAssignments").GetBoolean());

        var perms = body.GetProperty("permissions").EnumerateArray().ToList();

        // od.crm.read: present with provenance [user, group:G1, role:R1, permission]
        var crmRead = perms.FirstOrDefault(p => p.GetProperty("id").GetString() == Permissions.CrmRead);
        Assert.False(crmRead.ValueKind == JsonValueKind.Undefined, "od.crm.read must be present");
        Assert.False(crmRead.GetProperty("isDenied").GetBoolean());
        var crmReadChain = crmRead.GetProperty("provenanceChain").EnumerateArray().ToList();
        Assert.Equal("user",       crmReadChain[0].GetProperty("nodeType").GetString());
        Assert.Equal("group",      crmReadChain[1].GetProperty("nodeType").GetString());
        Assert.Equal(g1Id.ToString(), crmReadChain[1].GetProperty("id").GetString());
        Assert.Equal("role",       crmReadChain[2].GetProperty("nodeType").GetString());
        Assert.Equal(r1Id.ToString(), crmReadChain[2].GetProperty("id").GetString());
        Assert.Equal("permission", crmReadChain[3].GetProperty("nodeType").GetString());

        // od.finance.read: present with provenance [user, group:G2, roleSet:RS1, role:R2, permission]
        var financeRead = perms.FirstOrDefault(p => p.GetProperty("id").GetString() == Permissions.FinanceRead);
        Assert.False(financeRead.ValueKind == JsonValueKind.Undefined, "od.finance.read must be present");
        Assert.False(financeRead.GetProperty("isDenied").GetBoolean());
        var finChain = financeRead.GetProperty("provenanceChain").EnumerateArray().ToList();
        Assert.Equal("user",    finChain[0].GetProperty("nodeType").GetString());
        Assert.Equal("group",   finChain[1].GetProperty("nodeType").GetString());
        Assert.Equal(g2Id.ToString(), finChain[1].GetProperty("id").GetString());
        Assert.Equal("roleSet", finChain[2].GetProperty("nodeType").GetString());
        Assert.Equal("role",    finChain[3].GetProperty("nodeType").GetString());
        Assert.Equal("permission", finChain[4].GetProperty("nodeType").GetString());

        // od.crm.write: present with isDenied = true
        var crmWrite = perms.FirstOrDefault(p => p.GetProperty("id").GetString() == Permissions.CrmWrite);
        Assert.False(crmWrite.ValueKind == JsonValueKind.Undefined, "od.crm.write must be present as denied");
        Assert.True(crmWrite.GetProperty("isDenied").GetBoolean());
    }

    // ── AC2: 404 for user in different tenant ─────────────────────────────────

    [Fact]
    public async Task GetEffectivePermissions_UserInDifferentTenant_Returns404()
    {
        // Seed user in a different tenant (DevTenantId)
        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(DevSeeder.DevTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = DevSeeder.DevTenantId,
            Email = $"other-tenant-{Guid.NewGuid():N}@test.com",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var client = await AuthClientAsync();
        var response = await client.GetAsync($"/api/tenant/users/{user.Id}/effective-permissions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── AC2: User with no groups returns empty permissions ────────────────────

    [Fact]
    public async Task GetEffectivePermissions_NoGroups_ReturnsEmptyPermissions()
    {
        var userId = await SeedUserAsync();

        var client = await AuthClientAsync();
        var response = await client.GetAsync($"/api/tenant/users/{userId}/effective-permissions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(body.GetProperty("hasGroupAssignments").GetBoolean());
        Assert.Empty(body.GetProperty("permissions").EnumerateArray());
    }

    // ── AC2: Unauthenticated request returns 401 ─────────────────────────────

    [Fact]
    public async Task GetEffectivePermissions_Unauthenticated_Returns401()
    {
        var response = await Client.GetAsync($"/api/tenant/users/{Guid.NewGuid()}/effective-permissions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
