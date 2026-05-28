using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace OneId.Server.IntegrationTests.Helpers;

[Collection("IntegrationTests")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly OneIdWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(OneIdWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    // Runs before every [Fact] — restores DB and clears the in-memory cache.
    // Cache must be cleared alongside the DB reset so caching tests don't see stale data
    // from a previous test that had different DB state for the same userId/tenantId.
    public async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
        ((MemoryCache)Factory.Services.GetRequiredService<IMemoryCache>()).Clear();
    }

    // Container lifecycle is managed by the collection fixture (OneIdWebApplicationFactory).
    public Task DisposeAsync() => Task.CompletedTask;
}
