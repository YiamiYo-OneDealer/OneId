using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OneId.Server.Infrastructure.Persistence;
using System.Text.Json;
using System.Net.Http.Json;
using Testcontainers.PostgreSql;

namespace OneId.Server.IntegrationTests.OpenIddict;

// Not in [Collection("IntegrationTests")] — uses its own factories with Development environment.
// Run sequentially: two sequential factory lifecycles simulate app restarts.
public class DevSigningKeyStabilityTest
{
    [Fact]
    public async Task SigningKey_IsFileBased_AndSurvivesAppRestart()
    {
        string? kid1;

        var factory1 = new DevEnvironmentFactory();
        await factory1.StartAsync();
        try
        {
            var client1 = factory1.CreateClient();
            var discoveryDoc = await client1.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration");
            var jwksUri = discoveryDoc.GetProperty("jwks_uri").GetString()!;
            var jwks1Response = await client1.GetAsync(jwksUri);
            jwks1Response.EnsureSuccessStatusCode();
            var jwks1 = await jwks1Response.Content.ReadFromJsonAsync<JsonElement>();
            kid1 = jwks1.GetProperty("keys")[0].GetProperty("kid").GetString();
        }
        finally
        {
            await factory1.DisposeAsync();
        }

        // Simulate app restart: factory1 disposed, factory2 starts fresh
        string? kid2;
        var factory2 = new DevEnvironmentFactory();
        await factory2.StartAsync();
        try
        {
            var client2 = factory2.CreateClient();
            var discoveryDoc = await client2.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration");
            var jwksUri = discoveryDoc.GetProperty("jwks_uri").GetString()!;
            var jwks2Response = await client2.GetAsync(jwksUri);
            jwks2Response.EnsureSuccessStatusCode();
            var jwks2 = await jwks2Response.Content.ReadFromJsonAsync<JsonElement>();
            kid2 = jwks2.GetProperty("keys")[0].GetProperty("kid").GetString();

            // Verify key file exists — content root is accessible via IWebHostEnvironment
            using var scope = factory2.Services.CreateScope();
            var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
            var keyPath = System.IO.Path.Combine(env.ContentRootPath, "keys", "dev-signing.key");
            Assert.True(System.IO.File.Exists(keyPath), $"Key file not found at: {keyPath}");
        }
        finally
        {
            await factory2.DisposeAsync();
        }

        // Same kid across restarts proves file-based (not ephemeral)
        Assert.NotNull(kid1);
        Assert.Equal(kid1, kid2);
    }
}

// Dedicated factory using Development environment with a real PostgreSQL container.
// Must NOT use OneIdWebApplicationFactory — that uses Testing environment (no key generation).
internal sealed class DevEnvironmentFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _dbContainer =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task StartAsync() => await _dbContainer.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opt =>
                opt.UseNpgsql(_dbContainer.GetConnectionString())
                   .UseSnakeCaseNamingConvention()
                   .UseOpenIddict());
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
