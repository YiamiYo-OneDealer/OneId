using Microsoft.EntityFrameworkCore;
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
[Trait("Category", "EffectivePermissions")]
public class EffectivePermissionsPreviewIntegrationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
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

    private async Task<(Guid GroupId, Guid RoleId)> SeedGroupWithRoleAndPermissionAsync(string groupName, string roleName, string permissionId)
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

        var perm = await db.Permissions.IgnoreQueryFilters().FirstAsync(p => p.PermissionId == permissionId);
        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
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
        db.GroupRoles.Add(new GroupRole { GroupId = group.Id, RoleId = role.Id });
        await db.SaveChangesAsync();

        return (group.Id, role.Id);
    }

    // ── AC3: Preview with groupIds returns group's permissions ────────────────

    [Fact]
    public async Task EffectivePermissionsPreviewIntegrationTest_GroupPermissionsReturned()
    {
        var (groupId, _) = await SeedGroupWithRoleAndPermissionAsync("PreviewG1", "PreviewR1", Permissions.OdBpView);

        var client = await AuthClientAsync();
        var response = await client.PostAsJsonAsync(
            "/api/tenant/effective-permissions/preview",
            new { groupIds = new[] { groupId } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("", body.GetProperty("userId").GetString());
        Assert.True(body.GetProperty("hasGroupAssignments").GetBoolean());

        var permIds = body.GetProperty("permissions").EnumerateArray()
            .Select(p => p.GetProperty("id").GetString())
            .ToHashSet();

        Assert.Contains(Permissions.OdBpView, permIds);
    }

    // ── AC3: Preview with empty groupIds returns empty permissions ────────────

    [Fact]
    public async Task EffectivePermissionsPreview_EmptyGroupIds_ReturnsEmpty()
    {
        var client = await AuthClientAsync();
        var response = await client.PostAsJsonAsync(
            "/api/tenant/effective-permissions/preview",
            new { groupIds = Array.Empty<Guid>() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(body.GetProperty("hasGroupAssignments").GetBoolean());
        Assert.Empty(body.GetProperty("permissions").EnumerateArray());
    }

    // ── AC3: Cross-tenant groupId is silently ignored ─────────────────────────

    [Fact]
    public async Task EffectivePermissionsPreview_CrossTenantGroupId_IgnoredSilently()
    {
        // Seed a group in DevTenant (cross-tenant from TotpUser's perspective)
        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(DevSeeder.DevTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var crossTenantGroup = new Group
        {
            Id = Guid.NewGuid(),
            TenantId = DevSeeder.DevTenantId,
            Name = "CrossTenantGroup",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Groups.Add(crossTenantGroup);
        await db.SaveChangesAsync();

        var client = await AuthClientAsync();
        var response = await client.PostAsJsonAsync(
            "/api/tenant/effective-permissions/preview",
            new { groupIds = new[] { crossTenantGroup.Id } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Cross-tenant group ID silently ignored → no permissions
        Assert.Empty(body.GetProperty("permissions").EnumerateArray());
    }

    // ── AC3: Unauthenticated returns 401 ──────────────────────────────────────

    [Fact]
    public async Task EffectivePermissionsPreview_Unauthenticated_Returns401()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/tenant/effective-permissions/preview",
            new { groupIds = Array.Empty<Guid>() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
