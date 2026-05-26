using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;
using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.IntegrationTests.Helpers;
using Microsoft.EntityFrameworkCore;

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
