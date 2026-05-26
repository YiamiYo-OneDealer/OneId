using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Middleware;
using OneId.Server.Infrastructure.Persistence;
using OneId.Server.IntegrationTests.Helpers;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace OneId.Server.IntegrationTests;

// Same collection as RegistrationOrderIntegrationTests — sequential to avoid Serilog static logger race.
[Collection("Integration")]
public class TenantResolutionIntegrationTests : IClassFixture<TenantResolutionTestFactory>
{
    private readonly TenantResolutionTestFactory _factory;

    public TenantResolutionIntegrationTests(TenantResolutionTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TenantId_IsPopulatedFromJwtTidClaim()
    {
        var expectedTenantId = Guid.NewGuid();
        // TestTokenFactory creates a real JWT containing tid, sub, scope, seat_count, roles
        var token = TestTokenFactory.CreateToken(tenantId: expectedTenantId, userId: Guid.NewGuid());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/test/tenant-context");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(Guid.TryParse(body, out var parsedId), $"Expected a Guid in body, got: {body}");
        Assert.Equal(expectedTenantId, parsedId);
    }

    [Fact]
    public async Task UnauthenticatedRequest_ToProtectedEndpoint_Returns401()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/test/tenant-context");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

/// <summary>
/// Test factory for TenantResolutionIntegrationTests.
/// Validates real TestTokenFactory JWTs via a custom handler — exercises the real JWT claim extraction path.
/// Uses InMemory DB because tenant resolution is middleware-only; no DB queries needed.
/// </summary>
public class TenantResolutionTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // InMemory DB — no real PostgreSQL needed; test targets middleware claim extraction only
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opt =>
                opt.UseInMemoryDatabase("TestDb_TenantResolution")
                   .UseOpenIddict());

            // Override default auth scheme with a handler that validates TestTokenFactory JWTs.
            // This exercises TenantContextMiddleware's real context.User.FindFirst("tid") path.
            services.AddAuthentication(defaultScheme: "TestJwt")
                .AddScheme<AuthenticationSchemeOptions, TestJwtAuthHandler>("TestJwt", _ => { });
        });

        builder.Configure(app =>
        {
            app.UseRouting();
            app.UseAuthentication();
            app.UseMiddleware<TenantContextMiddleware>();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/test/tenant-context", (ITenantContext ctx) =>
                    ctx.TenantId.ToString())
                    .RequireAuthorization();
            });
        });
    }
}

/// <summary>
/// Auth handler that validates real JWTs signed with TestTokenFactory.TestSigningKey.
/// Unlike TestAuthHandler (which injects claims directly), this handler exercises the
/// full JWT parsing and claim extraction pipeline from the token payload.
/// </summary>
internal class TestJwtAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private static readonly JsonWebTokenHandler _handler = new();

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = authorization["Bearer ".Length..].Trim();

        var result = await _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = TestTokenFactory.TestSigningKey,
            ValidateIssuer = false,
            ValidateAudience = false,
        });

        if (!result.IsValid)
            return AuthenticateResult.Fail(result.Exception ?? new InvalidOperationException("Invalid test JWT"));

        var principal = new ClaimsPrincipal(result.ClaimsIdentity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
