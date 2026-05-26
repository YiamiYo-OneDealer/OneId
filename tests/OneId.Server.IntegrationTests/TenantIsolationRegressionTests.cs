using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;
using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.IntegrationTests.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OtpNet;

namespace OneId.Server.IntegrationTests;

/// <summary>
/// Regression test suite proving EF Core global query filters enforce tenant data isolation.
/// Uses real PostgreSQL (TestContainers) — InMemory would not reliably exercise HasQueryFilter.
/// Extended in Epic 4a: add Role, Group, Permission isolation assertions by inheriting TenantIsolationTestBase.
/// </summary>
[Trait("Category", "TenantIsolation")]
public class TenantIsolationRegressionTests : TenantIsolationTestBase
{
    public TenantIsolationRegressionTests(OneIdWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task User_IsNotVisible_FromOtherTenant()
    {
        var tenantBId = await SeedSecondTenantAsync();

        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(tenantBId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var users = await db.Users.ToListAsync();

        // DevSeeder users (AdminUser, TotpUser) belong to DevTenantId, not TenantB
        Assert.Empty(users);
    }

    [Fact]
    public async Task User_IsVisible_FromOwningTenant()
    {
        using var scope = Factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(DevSeeder.DevTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var users = await db.Users.ToListAsync();

        // DevSeeder seeds AdminUser + TotpUser under DevTenantId
        Assert.NotEmpty(users);
        Assert.All(users, u => Assert.Equal(DevSeeder.DevTenantId, u.TenantId));
    }

    [Fact]
    public async Task DbQuery_WithoutTenantContext_ThrowsInvalidOperationException()
    {
        using var scope = Factory.Services.CreateScope();
        // Deliberately do NOT initialize TenantContext — guard must fire
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.Users.ToListAsync());
    }

    // Story 4a.2: Role tenant isolation assertions
    [Fact]
    public async Task Role_IsNotVisible_FromOtherTenant()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
            tenantCtx.Initialize(DevSeeder.DevTenantId);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Roles.Add(new Role
            {
                Id = Guid.NewGuid(),
                TenantId = DevSeeder.DevTenantId,
                Name = "IsolationTestRole",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var tenantBId = await SeedSecondTenantAsync();

        using var scope2 = Factory.Services.CreateScope();
        var tenantCtx2 = scope2.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx2.Initialize(tenantBId);
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();

        var roles = await db2.Roles.ToListAsync();
        Assert.Empty(roles);
    }

    [Fact]
    public async Task Role_IsVisible_FromOwningTenant()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
            tenantCtx.Initialize(DevSeeder.DevTenantId);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Roles.Add(new Role
            {
                Id = Guid.NewGuid(),
                TenantId = DevSeeder.DevTenantId,
                Name = "OwningTenantRole",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        using var scope2 = Factory.Services.CreateScope();
        var tenantCtx2 = scope2.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx2.Initialize(DevSeeder.DevTenantId);
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();

        var roles = await db2.Roles.ToListAsync();
        Assert.NotEmpty(roles);
        Assert.All(roles, r => Assert.Equal(DevSeeder.DevTenantId, r.TenantId));
    }

    // Story 4a.3: RoleSet tenant isolation assertions
    [Fact]
    public async Task RoleSet_IsNotVisible_FromOtherTenant()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
            tenantCtx.Initialize(DevSeeder.DevTenantId);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RoleSets.Add(new RoleSet
            {
                Id = Guid.NewGuid(),
                TenantId = DevSeeder.DevTenantId,
                Name = "IsolationTestRoleSet",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var tenantBId = await SeedSecondTenantAsync();

        using var scope2 = Factory.Services.CreateScope();
        var tenantCtx2 = scope2.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx2.Initialize(tenantBId);
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();

        var roleSets = await db2.RoleSets.ToListAsync();
        Assert.Empty(roleSets);
    }

    [Fact]
    public async Task RoleSet_IsVisible_FromOwningTenant()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
            tenantCtx.Initialize(DevSeeder.DevTenantId);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RoleSets.Add(new RoleSet
            {
                Id = Guid.NewGuid(),
                TenantId = DevSeeder.DevTenantId,
                Name = "OwningTenantRoleSet",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        using var scope2 = Factory.Services.CreateScope();
        var tenantCtx2 = scope2.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx2.Initialize(DevSeeder.DevTenantId);
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();

        var roleSets = await db2.RoleSets.ToListAsync();
        Assert.NotEmpty(roleSets);
        Assert.All(roleSets, rs => Assert.Equal(DevSeeder.DevTenantId, rs.TenantId));
    }

    // AC9: Permission records are global — readable by Internal Admin regardless of tenant context
    [Fact]
    public async Task Permissions_AreGlobal_InternalAdminCanReadAllRegardlessOfTenantContext()
    {
        var client = await AuthInternalAdminClientAsync();
        var response = await client.GetAsync("/api/internal/permissions?status=All&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var totalCount = body.GetProperty("totalCount").GetInt32();
        // DevSeeder seeds all PermissionCatalog entries — must be present after reset
        Assert.True(totalCount > 0, "Expected seeded permissions to be visible to Internal Admin");
    }

    private async Task<HttpClient> AuthInternalAdminClientAsync()
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
}

/// <summary>
/// Story 4a.4: Group isolation regression tests.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "TenantIsolation")]
public class GroupIsolationRegressionTests(OneIdWebApplicationFactory factory) : TenantIsolationTestBase(factory)
{
    [Fact]
    public async Task Group_IsNotVisible_FromOtherTenant()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
            tenantCtx.Initialize(DevSeeder.DevTenantId);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Groups.Add(new Group
            {
                Id = Guid.NewGuid(),
                TenantId = DevSeeder.DevTenantId,
                Name = $"IsolationTestGroup-{Guid.NewGuid():N}",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var tenantBId = await SeedSecondTenantAsync();

        using var scope2 = Factory.Services.CreateScope();
        var tenantCtx2 = scope2.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx2.Initialize(tenantBId);
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();

        var groups = await db2.Groups.ToListAsync();
        Assert.Empty(groups);
    }

    [Fact]
    public async Task Group_IsVisible_FromOwningTenant()
    {
        var groupId = Guid.NewGuid();
        using (var scope = Factory.Services.CreateScope())
        {
            var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
            tenantCtx.Initialize(DevSeeder.DevTenantId);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Groups.Add(new Group
            {
                Id = groupId,
                TenantId = DevSeeder.DevTenantId,
                Name = $"VisibleGroup-{Guid.NewGuid():N}",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        using var scope2 = Factory.Services.CreateScope();
        var tenantCtx2 = scope2.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx2.Initialize(DevSeeder.DevTenantId);
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();

        var groups = await db2.Groups.Where(g => g.Id == groupId).ToListAsync();
        Assert.NotEmpty(groups);
    }
}

/// <summary>
/// Base class for tenant isolation regression tests.
/// Provides seed helpers and scoped context factory for Epic 4a extension.
/// </summary>
public abstract class TenantIsolationTestBase : IntegrationTestBase
{
    protected TenantIsolationTestBase(OneIdWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// Seeds a second tenant into the database and returns its ID.
    /// DevTenantId is always TenantA (seeded by DevSeeder on reset).
    /// </summary>
    protected async Task<Guid> SeedSecondTenantAsync()
    {
        var tenantBId = Guid.NewGuid();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = tenantBId,
            Name = $"IsolationTestTenantB-{tenantBId:N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        return tenantBId;
    }
}
