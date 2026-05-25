using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using OneId.Server.Infrastructure.Persistence;
using OneId.Server.Infrastructure.Persistence.Seeds;
using OpenIddict.Abstractions;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;
using Xunit;

namespace OneId.Server.IntegrationTests.Helpers;

[CollectionDefinition("IntegrationTests")]
public class IntegrationTestsCollection : ICollectionFixture<OneIdWebApplicationFactory>
{ }

public sealed class OneIdWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private Respawner _respawner = default!;

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _dbContainer.StartAsync();

        // Accessing Services triggers lazy host creation → ConfigureWebHost runs here.
        // Container is already started so GetConnectionString() is valid.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // Checkpoint created ONCE after migrations — captures post-migration baseline.
        await using var conn = new NpgsqlConnection(_dbContainer.GetConnectionString());
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            TablesToIgnore =
            [
                new Table("__EFMigrationsHistory"),
                new Table("OpenIddictApplications"), // client registrations are reference data
                new Table("OpenIddictScopes"),        // scope registrations are reference data
            ]
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace production Npgsql options with Testcontainers connection string.
            // Npgsql → Npgsql replacement avoids the "two EF Core providers" error
            // that occurs when replacing Npgsql with InMemory (different providers conflict).
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opt =>
                opt.UseNpgsql(_dbContainer.GetConnectionString())
                   .UseSnakeCaseNamingConvention()
                   .UseOpenIddict());
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var conn = new NpgsqlConnection(_dbContainer.GetConnectionString());
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);

        // Re-seed dev data (Tenant + User) after each reset so tests always have a known starting state.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        await DevSeeder.SeedAsync(db, manager);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
