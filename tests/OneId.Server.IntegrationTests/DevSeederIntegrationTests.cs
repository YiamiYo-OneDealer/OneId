using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.IntegrationTests;

// Same collection as RegistrationOrderIntegrationTests — xUnit 2.x runs classes in parallel by
// default; the Serilog static bootstrap logger is not safe for concurrent factory startups.
[Collection("Integration")]
public class DevSeederIntegrationTests : IClassFixture<TenantIsolationServiceFactory>
{
    private readonly TenantIsolationServiceFactory _factory;

    public DevSeederIntegrationTests(TenantIsolationServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task User_IsInvisible_ToOtherTenant()
    {
        var (_, tenant2Id) = await SeedIsolationData();

        using var scope = _factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(tenant2Id);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var users = await db.Users.ToListAsync();

        Assert.Empty(users);
    }

    [Fact]
    public async Task User_IsVisible_ToOwningTenant()
    {
        var (tenant1Id, _) = await SeedIsolationData();

        using var scope = _factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(tenant1Id);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var users = await db.Users.ToListAsync();

        var user = Assert.Single(users);
        Assert.Equal("user@tenant1.com", user.Email);
    }

    private async Task<(Guid tenant1Id, Guid tenant2Id)> SeedIsolationData()
    {
        // Tenant is NOT tenant-scoped — no context init needed for inserting tenants
        using var tenantScope = _factory.Services.CreateScope();
        var tenantDb = tenantScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant1 = new Tenant
        {
            Id = Guid.NewGuid(),
            // Unique name per call prevents unique-index conflicts across test runs
            Name = $"Isolation-Tenant1-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var tenant2 = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = $"Isolation-Tenant2-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        tenantDb.Tenants.Add(tenant1);
        tenantDb.Tenants.Add(tenant2);
        await tenantDb.SaveChangesAsync();

        // User is tenant-scoped — initialize TenantContext before inserting
        using var userScope = _factory.Services.CreateScope();
        var userTenantCtx = userScope.ServiceProvider.GetRequiredService<TenantContext>();
        userTenantCtx.Initialize(tenant1.Id);
        var userDb = userScope.ServiceProvider.GetRequiredService<AppDbContext>();
        userDb.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant1.Id,
            Email = "user@tenant1.com",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await userDb.SaveChangesAsync();

        return (tenant1.Id, tenant2.Id);
    }
}

/// <summary>
/// Provides a clean service scope with AppDbContext (InMemory) and TenantContext.
/// Uses ServiceCollection directly — no WebApplicationFactory needed because these
/// tests only need DI scopes, not an HTTP server.
/// Avoids the "two EF Core providers" error caused by RemoveAll{DbContextOptions} not
/// cleaning up the Npgsql internal service registrations from Program.cs.
/// </summary>
public class TenantIsolationServiceFactory : IDisposable
{
    private readonly ServiceProvider _provider;

    public TenantIsolationServiceFactory()
    {
        var services = new ServiceCollection();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseInMemoryDatabase("TestDb_TenantIsolation"));

        _provider = services.BuildServiceProvider();
    }

    public IServiceProvider Services => _provider;

    public void Dispose() => _provider.Dispose();
}
