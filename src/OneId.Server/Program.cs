using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Caching;
using OneId.Server.Infrastructure.Logging;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Internal;
using OneId.Server.Application.TenantAdmin;
using OneId.Server.Infrastructure.Middleware;
using OneId.Server.Infrastructure.Persistence;
using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.Infrastructure.Email;
using OneId.Server.Infrastructure.OpenIddict;
using OpenIddict.Abstractions;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;
using System.Security.Cryptography;

// Bootstrap logger: captures startup logs before host is built.
// Wrapped in try-catch: in-process test scenarios may start multiple Program instances,
// and Serilog's static logger can only be initialized once per process.
try
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .CreateBootstrapLogger();
}
catch (InvalidOperationException)
{
    // Logger already frozen — proceeding with existing logger
}

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSerilogEnrichers();

    // Serilog as the application logger
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console(new JsonFormatter())
        .WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                ?? "http://localhost:4317";
            options.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
            options.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "OneId.Server"
            };
        }));

    // OTEL tracing — AddOtlpExporter() reads OTEL_EXPORTER_OTLP_ENDPOINT automatically
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddOtlpExporter());

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    // AR-10: All cache access must go through ICacheService — direct IMemoryCache injection is forbidden
    // outside Infrastructure/Caching/ and is enforced by InternalBoundaryTests.cs.
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
    builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
    builder.Services.AddTokenPipeline();
    builder.Services.AddRevocationHandler();
    builder.Services.AddEmailSender();

    // AR-5: ITenantContext MUST precede OpenIddict and EF Core — see architecture.md
    builder.Services.AddScoped<TenantContext>();
    builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

    // Story 3.2: Internal Admin tenant CRUD handlers (AR-8: boundary enforced in AddInternalAdminHandlers)
    builder.Services.AddInternalAdminHandlers();

    // Story 4a.2: Tenant Admin role management handlers
    builder.Services.AddTenantAdminHandlers();

    // Story 3.8: Audit log service
    builder.Services.AddScoped<IAuditService, AuditService>();

    // AR-5 STEP 2: EF Core with global query filters referencing ITenantContext
    // Global query filters are added in Story 1.3b once ITenantContext is wired
    builder.Services.AddDbContext<AppDbContext>(options =>
        options
            .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured."))
            .UseSnakeCaseNamingConvention()
            .UseOpenIddict()); // AR-5 STEP 2.5: registers OpenIddict entity configurations in AppDbContext

    // AR-5 STEP 3: OpenIddict registered AFTER EF Core and ITenantContext — see architecture.md
    builder.Services.AddOpenIddict()
        .AddCore(options =>
        {
            options.UseEntityFrameworkCore()
                   .UseDbContext<AppDbContext>();
        })
        .AddServer(options =>
        {
            // Endpoints
            options.SetAuthorizationEndpointUris("/connect/authorize")
                   .SetTokenEndpointUris("/connect/token")
                   .SetIntrospectionEndpointUris("/connect/introspect")
                   .SetUserInfoEndpointUris("/connect/userinfo")
                   .SetRevocationEndpointUris("/connect/revoke");

            // Flows
            options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
            options.AllowClientCredentialsFlow();
            options.AllowRefreshTokenFlow();
            options.AllowPasswordFlow();
            options.AllowCustomFlow("urn:oneid:mfa");

            // Scopes
            options.RegisterScopes(
                OpenIddictConstants.Scopes.OpenId,
                OpenIddictConstants.Scopes.Email,
                OpenIddictConstants.Scopes.Profile,
                OpenIddictConstants.Scopes.Roles,
                OpenIddictConstants.Scopes.OfflineAccess);

            // Token lifetimes — configurable via appsettings.json "OpenIddict" section (NFR-2 budget)
            var oidcConfig = builder.Configuration.GetSection("OpenIddict");
            options.SetAccessTokenLifetime(
                TimeSpan.FromMinutes(oidcConfig.GetValue<int>("AccessTokenLifetimeMinutes", 15)));
            options.SetRefreshTokenLifetime(
                TimeSpan.FromHours(oidcConfig.GetValue<int>("RefreshTokenSlidingExpiryHours", 8)));

            // Enforce strict refresh token rotation: redeemed tokens cannot be reused at all.
            // Prevents replay attacks; no grace window needed in the admin console use case.
            options.SetRefreshTokenReuseLeeway(TimeSpan.Zero);

            // File-based stable RS256 signing key — survives app restarts (enforced by DevSigningKeyStabilityTest)
            var keysDir = Path.Combine(builder.Environment.ContentRootPath, "keys");
            Directory.CreateDirectory(keysDir);
            var keyPath = Path.Combine(keysDir, "dev-signing.key");

            if (!File.Exists(keyPath))
            {
                using var newRsa = RSA.Create(2048);
                File.WriteAllText(keyPath, Convert.ToBase64String(newRsa.ExportRSAPrivateKey()));
            }

            // Load into a long-lived RSA instance — must NOT be in a 'using' block:
            // OpenIddict holds a reference for the application lifetime.
            var persistedRsa = RSA.Create();
            persistedRsa.ImportRSAPrivateKey(Convert.FromBase64String(File.ReadAllText(keyPath)), out _);
            options.AddSigningKey(new RsaSecurityKey(persistedRsa) { KeyId = "dev-rs256-key" });

            // Dev encryption: ephemeral (refresh tokens are short-lived; no cross-restart persistence needed in dev)
            options.AddEphemeralEncryptionKey();

            // Standard JWT format (not JWE) — required for OneDealer v2 introspection compatibility
            options.DisableAccessTokenEncryption();

            // Passthrough: Stories 2.2+ add controllers for auth/token.
            // Discovery (/.well-known/openid-configuration) and JWKS are fully served by OpenIddict.
            // DisableTransportSecurityRequirement: allows HTTP in dev/test — production must use HTTPS.
            options.UseAspNetCore()
                   .EnableAuthorizationEndpointPassthrough()
                   .EnableTokenEndpointPassthrough()
                   .DisableTransportSecurityRequirement();

            // Enrich introspection responses with permissions, dimensional_attributes, and license.
            // Stage 1: evaluate and store data on transaction (needs GenericTokenPrincipal).
            options.AddEventHandler(IntrospectionDataEnricher.Descriptor);

            // Stage 2: write enrichment data to response (needs AddParameter to preserve empty arrays).
            options.AddEventHandler(IntrospectionResponseEnricher.Descriptor);
        })
        .AddValidation(options =>
        {
            options.UseLocalServer();
            options.UseAspNetCore();
        });

    builder.Services.AddHealthChecks();
    builder.Services.AddProblemDetails();

    var app = builder.Build();

    // Runs in all environments — migrations and system bootstrap are always required.
    if (!app.Environment.IsEnvironment("Testing"))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await SystemSeeder.SeedAsync(db);
    }

    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
    {
        app.MapOpenApi();

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var dp = app.Services.GetRequiredService<IDataProtectionProvider>();
        await DevSeeder.SeedAsync(db, manager, dp);
    }

    // Must be first — wraps entire pipeline to catch exceptions from any layer
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // Request logging: adds Outcome field to HTTP request completion log events
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set(
                "Outcome",
                httpContext.Response.StatusCode < 400 ? "Success" : "Failure");
        };
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms — {Outcome}";
    });

    app.UseHttpsRedirection();
    app.UseAuthentication();
    // AR-5: TenantContextMiddleware MUST precede OpenIddict and EF Core — see architecture.md
    app.UseMiddleware<TenantContextMiddleware>();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
