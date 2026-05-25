using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Middleware;
using OneId.Server.Infrastructure.Persistence;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace OneId.Server.IntegrationTests;

// Same collection as DevSeederIntegrationTests — sequential execution prevents Serilog static logger race.
[Collection("Integration")]
public class RegistrationOrderIntegrationTests : IClassFixture<OneIdTestFactory>
{
    private readonly OneIdTestFactory _factory;

    public RegistrationOrderIntegrationTests(OneIdTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TenantId_IsNonNull_WhenMiddlewareRunsInCorrectOrder()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/test/tenant-id");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(Guid.TryParse(body, out var parsedId), $"Expected a Guid in body, got: {body}");
        Assert.Equal(OneIdTestFactory.TestTenantId, parsedId);
    }

    [Fact]
    public void TenantId_ThrowsGuard_WhenAccessedOnFreshDiScope()
    {
        using var scope = _factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        // Fresh scope — TenantContextMiddleware has not run for this scope
        var ex = Assert.Throws<InvalidOperationException>(() => tenantCtx.TenantId);
        Assert.Equal(
            "Tenant context not initialized — check middleware registration order in Program.cs",
            ex.Message);
    }
}

public class OneIdTestFactory : WebApplicationFactory<Program>
{
    public static readonly Guid TestTenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing"); // skips MigrateAsync in Program.cs

        builder.ConfigureServices(services =>
        {
            // Replace PostgreSQL DbContext with InMemory — no real DB needed for ordering tests
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opt =>
                opt.UseInMemoryDatabase("TestDb_RegistrationOrder")
                   .UseOpenIddict());

            // Test auth scheme — injects tid Guid claim without a real JWT
            services.AddAuthentication(defaultScheme: "Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        });

        builder.Configure(app =>
        {
            // Minimal pipeline for ordering tests — mirrors Program.cs middleware order
            app.UseRouting();
            app.UseAuthentication();
            app.UseMiddleware<TenantContextMiddleware>();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/test/tenant-id", (ITenantContext ctx) =>
                    ctx.TenantId.ToString());
            });
        });
    }
}

internal class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[] { new Claim("tid", OneIdTestFactory.TestTenantId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
