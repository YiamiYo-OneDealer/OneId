using Microsoft.EntityFrameworkCore;
using OneId.Server.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// AR-5 STEP 1: ITenantContextMiddleware MUST precede EF Core and OpenIddict — see architecture.md
// TODO Story 1.3a: app.UseMiddleware<TenantContextMiddleware>();

// AR-5 STEP 2: EF Core with global query filters referencing ITenantContext
// Global query filters are added in Story 1.3b once ITenantContext is wired
builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured."))
        .UseSnakeCaseNamingConvention()); // EFCore.NamingConventions: applies snake_case to all entities

// AR-5 STEP 3: OpenIddict registered AFTER EF Core — Story 2.1 wires this
// TODO Story 2.1: builder.Services.AddOpenIddict()...

builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Apply pending EF Core migrations automatically in development
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
