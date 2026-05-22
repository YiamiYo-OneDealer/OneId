using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OneId.Server.Infrastructure.Middleware;
using OneId.Server.Infrastructure.Persistence;
using System.Net;
using System.Text.Json;

namespace OneId.Server.IntegrationTests;

// Same collection as DevSeederIntegrationTests — sequential execution prevents Serilog static logger race.
[Collection("Integration")]
public class ConcurrencyConflictTests : IClassFixture<ConcurrencyTestFactory>
{
    private readonly ConcurrencyTestFactory _factory;

    public ConcurrencyConflictTests(ConcurrencyTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConcurrencyConflict_Returns409ProblemDetails()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/test/concurrency-conflict");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonDocument.Parse(body).RootElement;

        Assert.Equal("https://httpstatuses.io/409", problem.GetProperty("type").GetString());
        Assert.Equal("Conflict", problem.GetProperty("title").GetString());
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
        Assert.Equal(
            "The resource was modified by another request. Reload and retry.",
            problem.GetProperty("detail").GetString());
    }
}

public class ConcurrencyTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opt =>
                opt.UseInMemoryDatabase("TestDb_Concurrency"));
        });

        builder.Configure(app =>
        {
            // ExceptionHandlingMiddleware FIRST — same order as production
            app.UseMiddleware<ExceptionHandlingMiddleware>();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/test/concurrency-conflict", () =>
                {
                    // Simulate a stale-write — verifies ExceptionHandlingMiddleware maps to 409
                    throw new DbUpdateConcurrencyException("Simulated stale-write", []);
                });
            });
        });
    }
}
