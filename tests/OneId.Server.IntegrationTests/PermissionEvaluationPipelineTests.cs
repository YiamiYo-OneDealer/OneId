using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Domain.Enums;
using OneId.Server.Infrastructure.Persistence;
using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.IntegrationTests.Helpers;
using OtpNet;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace OneId.Server.IntegrationTests;

[Collection("IntegrationTests")]
[Trait("Category", "PermissionEvaluation")]
public class PermissionEvaluationPipelineTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    // ── Auth helpers ──────────────────────────────────────────────────────────

    private async Task<JsonElement> GetJwtPayloadForTotpUserAsync()
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
        Assert.Equal(HttpStatusCode.OK, step1.StatusCode);
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
        Assert.Equal(HttpStatusCode.OK, step2.StatusCode);

        var accessToken = (await step2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString()!;

        var payloadJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(accessToken.Split('.')[1]));
        return JsonSerializer.Deserialize<JsonElement>(payloadJson);
    }

    private static HashSet<string> ExtractPermissions(JsonElement payload)
    {
        if (!payload.TryGetProperty("permissions", out var permsProp))
            return [];

        return permsProp.ValueKind switch
        {
            JsonValueKind.Array => permsProp.EnumerateArray()
                .Select(e => e.GetString()!)
                .ToHashSet(),
            JsonValueKind.String => [permsProp.GetString()!],
            _ => []
        };
    }

    // ── Seeding helpers ───────────────────────────────────────────────────────

    private async Task<Guid> GetPermissionGuidAsync(string permissionId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Permissions.IgnoreQueryFilters()
            .Where(p => p.PermissionId == permissionId)
            .Select(p => p.Id)
            .FirstAsync();
    }

    private async Task<Guid> SeedRoleWithPermissionsAsync(string roleName, params string[] permissionIds)
    {
        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(DevSeeder.DevTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = DevSeeder.DevTenantId,
            Name = roleName,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        foreach (var permId in permissionIds)
        {
            var permGuid = await GetPermissionGuidAsync(permId);
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permGuid });
        }
        await db.SaveChangesAsync();

        return role.Id;
    }

    private async Task<Guid> SeedGroupWithDirectRoleAsync(string groupName, Guid roleId)
    {
        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(DevSeeder.DevTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var group = new Group
        {
            Id = Guid.NewGuid(),
            TenantId = DevSeeder.DevTenantId,
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
        tenantCtx.Initialize(DevSeeder.DevTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var roleSet = new RoleSet
        {
            Id = Guid.NewGuid(),
            TenantId = DevSeeder.DevTenantId,
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
            TenantId = DevSeeder.DevTenantId,
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
        tenantCtx.Initialize(DevSeeder.DevTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.UserGroups.Add(new UserGroup { UserId = userId, GroupId = groupId });
        await db.SaveChangesAsync();
    }

    private async Task SeedOverrideAsync(Guid userId, string permissionId, PermissionOverrideType overrideType,
        DateTimeOffset? expiresAt = null)
    {
        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(DevSeeder.DevTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            Id = Guid.NewGuid(),
            TenantId = DevSeeder.DevTenantId,
            UserId = userId,
            PermissionId = permissionId,
            OverrideType = overrideType,
            Reason = "test",
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = Guid.Empty,
        });
        await db.SaveChangesAsync();
    }

    // ── AC 2: Permission union across Groups and RoleSets ─────────────────────

    [Fact]
    public async Task PermissionUnionIntegrationTest_DeduplicatesAcrossGroupsAndRoleSets()
    {
        // R1: od.crm.read + od.crm.write (direct role on G1)
        var r1Id = await SeedRoleWithPermissionsAsync("R1", Permissions.CrmRead, Permissions.CrmWrite);

        // R2: od.crm.write + od.finance.read (via RoleSet RS1 on G2)
        var r2Id = await SeedRoleWithPermissionsAsync("R2", Permissions.CrmWrite, Permissions.FinanceRead);

        var g1Id = await SeedGroupWithDirectRoleAsync("G1", r1Id);
        var g2Id = await SeedGroupWithRoleSetAsync("G2", "RS1", r2Id);

        await AssignUserToGroupAsync(DevSeeder.TotpUserId, g1Id);
        await AssignUserToGroupAsync(DevSeeder.TotpUserId, g2Id);

        var payload = await GetJwtPayloadForTotpUserAsync();
        var permissions = ExtractPermissions(payload);

        // Deduplicated union: CrmRead + CrmWrite + FinanceRead (CrmWrite appears in both roles but only once)
        Assert.Contains(Permissions.CrmRead, permissions);
        Assert.Contains(Permissions.CrmWrite, permissions);
        Assert.Contains(Permissions.FinanceRead, permissions);
        Assert.Equal(3, permissions.Count);
    }

    // ── AC 3: DENY is terminal ────────────────────────────────────────────────

    [Fact]
    public async Task DenyTerminalIntegrationTest_DenyOverrideExcludesPermissionEvenWhenGroupGrantsIt()
    {
        var roleId = await SeedRoleWithPermissionsAsync("RWithCrmWrite", Permissions.CrmWrite);
        var groupId = await SeedGroupWithDirectRoleAsync("GroupCrmWrite", roleId);
        await AssignUserToGroupAsync(DevSeeder.TotpUserId, groupId);

        // DENY override — no expiry, blocks CrmWrite even though the group grants it
        await SeedOverrideAsync(DevSeeder.TotpUserId, Permissions.CrmWrite, PermissionOverrideType.Deny);

        var payload = await GetJwtPayloadForTotpUserAsync();
        var permissions = ExtractPermissions(payload);

        Assert.DoesNotContain(Permissions.CrmWrite, permissions);
    }

    // ── AC 4: ALLOW override is additive ─────────────────────────────────────

    [Fact]
    public async Task AllowOverrideIntegrationTest_AllowAddsPermissionNotPresentInAnyGroup()
    {
        // User has no group assignments — FinanceWrite would not appear from groups
        await SeedOverrideAsync(DevSeeder.TotpUserId, Permissions.FinanceWrite, PermissionOverrideType.Allow);

        var payload = await GetJwtPayloadForTotpUserAsync();
        var permissions = ExtractPermissions(payload);

        Assert.Contains(Permissions.FinanceWrite, permissions);
    }

    // ── AC 5: Expired DENY does not apply ────────────────────────────────────

    [Fact]
    public async Task ExpiredDenyOverrideIntegrationTest_ExpiredDenyDoesNotBlockPermission()
    {
        var roleId = await SeedRoleWithPermissionsAsync("RWithCrmWrite2", Permissions.CrmWrite);
        var groupId = await SeedGroupWithDirectRoleAsync("GroupCrmWrite2", roleId);
        await AssignUserToGroupAsync(DevSeeder.TotpUserId, groupId);

        // DENY override that expired 1 minute ago — must have no effect
        await SeedOverrideAsync(DevSeeder.TotpUserId, Permissions.CrmWrite, PermissionOverrideType.Deny,
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        var payload = await GetJwtPayloadForTotpUserAsync();
        var permissions = ExtractPermissions(payload);

        Assert.Contains(Permissions.CrmWrite, permissions);
    }

    // ── Edge case: no group assignments ──────────────────────────────────────

    [Fact]
    public async Task NoGroupAssignments_NoOverrides_EmptyPermissions()
    {
        // TotpUser has no group assignments and no overrides — permissions should be empty/absent
        var payload = await GetJwtPayloadForTotpUserAsync();
        var permissions = ExtractPermissions(payload);

        Assert.Empty(permissions);
    }
}
