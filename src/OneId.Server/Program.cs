using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Logging;
using OneId.Server.Infrastructure.Middleware;
using OneId.Server.Infrastructure.Persistence;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;

// Bootstrap logger: captures startup logs before host is built
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

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

    // AR-5: ITenantContext MUST precede OpenIddict and EF Core — see architecture.md
    builder.Services.AddScoped<TenantContext>();
    builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

    // AR-5 STEP 2: EF Core with global query filters referencing ITenantContext
    // Global query filters are added in Story 1.3b once ITenantContext is wired
    builder.Services.AddDbContext<AppDbContext>(options =>
        options
            .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured."))
            .UseSnakeCaseNamingConvention());

    // AR-5 STEP 3: OpenIddict registered AFTER EF Core — Story 2.1 wires this
    // TODO Story 2.1: builder.Services.AddOpenIddict()...

    builder.Services.AddHealthChecks();
    builder.Services.AddProblemDetails();

    var app = builder.Build();

    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
    {
        app.MapOpenApi();

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
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
