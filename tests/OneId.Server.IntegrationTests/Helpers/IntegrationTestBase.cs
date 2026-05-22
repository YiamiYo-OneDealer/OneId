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

    // Runs before every [Fact] — restores DB to clean post-migration state.
    public async Task InitializeAsync() => await Factory.ResetDatabaseAsync();

    // Container lifecycle is managed by the collection fixture (OneIdWebApplicationFactory).
    public Task DisposeAsync() => Task.CompletedTask;
}
