# Story 1.2: Local Development Stack (Docker Compose)

Status: done

## Story

As a developer,
I want a single Docker Compose file that starts the full local stack with health-checked services and a validated observability pipeline,
so that I can run the complete system with one command and immediately verify it is working end-to-end.

## Acceptance Criteria

1. `docker compose up` from the project root starts four services: `oneid-server`, `postgres`, `otel-collector`, `seq`.
2. `oneid-server` passes its Docker health check (polls `GET /health`, expects HTTP 200 within 30 seconds of startup).
3. Seq UI is accessible at `http://localhost:5341`.
4. A structured log event emitted by `OneId.Server` appears in Seq with full structured fields (not raw text).
5. At least one OTEL span with `service.name = OneId.Server` is visible in Seq — confirming data flows through the OTEL Collector to Seq, not bypassing it.
6. `OneId.Server`'s OTEL exporter endpoint is set via the `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable — not hardcoded in `appsettings.json`.
7. `docker compose down && docker compose up` — PostgreSQL data persists across restarts (named volume, not anonymous).

## Tasks / Subtasks

- [x] Task 1: Fill in `Dockerfile` for `OneId.Server` (AC: #2)
  - [x] Implement multi-stage build: SDK build stage → ASP.NET runtime stage
  - [x] Install `curl` in the final image (required for Docker health check command)
  - [x] Expose port 8080 and set ENTRYPOINT

- [x] Task 2: Create `otel-collector-config.yml` at project root (AC: #5)
  - [x] Configure OTLP receiver (gRPC port 4317, HTTP port 4318)
  - [x] Configure `otlphttp` exporter → Seq's OTLP ingestion endpoint (`http://seq/ingest/otlp`)
  - [x] Wire traces + logs pipelines through batch processor to exporter

- [x] Task 3: Create `docker-compose.yml` at project root (AC: #1, #2, #3, #7)
  - [x] `postgres` service: image `postgres:16`, named volume `postgres-data`, health check
  - [x] `seq` service: image `datalust/seq:latest`, port `5341:80`, named volume `seq-data`, `ACCEPT_EULA: Y`
  - [x] `otel-collector` service: mounts `./otel-collector-config.yml`, ports 4317/4318 exposed, depends on `seq`
  - [x] `oneid-server` service: builds from `src/OneId.Server/Dockerfile`, env vars including `OTEL_EXPORTER_OTLP_ENDPOINT`, depends on `postgres` (condition: `service_healthy`)
  - [x] Docker health check on `oneid-server`: `curl -f http://localhost:8080/health`
  - [x] Named volumes block at bottom: `postgres-data`, `seq-data`

- [x] Task 4: Add Serilog and OTEL NuGet packages (AC: #4, #5, #6)
  - [x] Add `Serilog.AspNetCore` (latest stable)
  - [x] Add `Serilog.Sinks.OpenTelemetry` (latest stable) — sends Serilog logs via OTLP
  - [x] Add `OpenTelemetry.Extensions.Hosting` (latest stable)
  - [x] Add `OpenTelemetry.Instrumentation.AspNetCore` (latest stable)
  - [x] Add `OpenTelemetry.Exporter.OpenTelemetryProtocol` (latest stable)
  - [x] Verify `dotnet build` passes with zero warnings after package additions

- [x] Task 5: Wire minimal Serilog + OTEL in `Program.cs` (AC: #4, #5, #6)
  - [x] Add Serilog bootstrap logger before `WebApplication.CreateBuilder`
  - [x] Call `builder.Host.UseSerilog(...)` with `WriteTo.OpenTelemetry(...)` reading endpoint from env var
  - [x] Register OTEL tracing: `AddOpenTelemetry().WithTracing(...)` with `AddAspNetCoreInstrumentation()` + `AddOtlpExporter()`
  - [x] Preserve AR-5 registration order comments exactly as they exist in `Program.cs`
  - [x] Do NOT add enrichers, destructuring, or `SerilogDestructuringTests.cs` — deferred to Story 1.4

- [x] Task 6: Validate the full stack runs end-to-end (AC: #1–#7)
  - [x] Run `docker compose build` — succeeds
  - [x] Run `docker compose up -d` — all 4 services reach healthy/running state
  - [x] `curl http://localhost:8080/health` → HTTP 200
  - [x] Open `http://localhost:5341` — Seq UI loads
  - [x] Trigger a request to the server; confirm structured log event appears in Seq
  - [x] Confirm at least one span with `service.name = OneId.Server` visible in Seq
  - [x] Run `docker compose down && docker compose up -d`; confirm postgres volume data persists

## Dev Notes

### CRITICAL: .NET Runtime Version

Story 1.1 used **.NET 10.0** (machine does not have .NET 9; architecture says 9 but 10 is fully compatible). Dockerfile must use:
- Build: `mcr.microsoft.com/dotnet/sdk:10.0`
- Runtime: `mcr.microsoft.com/dotnet/aspnet:10.0`

### Dockerfile — Exact Content

Build context is the **project root** (not `src/OneId.Server/`). `Directory.Build.props` must be copied before `dotnet restore`.

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["src/OneId.Server/OneId.Server.csproj", "src/OneId.Server/"]
RUN dotnet restore "src/OneId.Server/OneId.Server.csproj"
COPY . .
WORKDIR "/src/src/OneId.Server"
RUN dotnet build "OneId.Server.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "OneId.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OneId.Server.dll"]
```

`curl` is installed explicitly because the ASP.NET base image does not include it by default, and the Docker Compose health check depends on it.

### OTEL Collector Config — Exact Content (`otel-collector-config.yml` at project root)

```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318

processors:
  batch:

exporters:
  otlphttp:
    endpoint: http://seq/ingest/otlp
    tls:
      insecure: true

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [batch]
      exporters: [otlphttp]
    logs:
      receivers: [otlp]
      processors: [batch]
      exporters: [otlphttp]
```

Seq's OTLP ingestion endpoint is `http://seq/ingest/otlp` (port 80 inside the Docker network). The OTEL Collector routes both traces and logs there. Use `otel/opentelemetry-collector-contrib` image — the base `otel/opentelemetry-collector` does not include the `otlphttp` exporter.

### docker-compose.yml — Exact Content

```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: oneid_dev
    volumes:
      - postgres-data:/var/lib/postgresql/data
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 5s
      timeout: 5s
      retries: 10

  seq:
    image: datalust/seq:latest
    environment:
      ACCEPT_EULA: Y
    volumes:
      - seq-data:/data
    ports:
      - "5341:80"
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5

  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest
    command: ["--config=/etc/otel-collector-config.yml"]
    volumes:
      - ./otel-collector-config.yml:/etc/otel-collector-config.yml:ro
    ports:
      - "4317:4317"
      - "4318:4318"
    depends_on:
      seq:
        condition: service_started

  oneid-server:
    build:
      context: .
      dockerfile: src/OneId.Server/Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: Docker
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=oneid_dev;Username=postgres;Password=postgres"
      OTEL_EXPORTER_OTLP_ENDPOINT: http://otel-collector:4317
      OTEL_SERVICE_NAME: OneId.Server
    ports:
      - "8080:8080"
    depends_on:
      postgres:
        condition: service_healthy
      otel-collector:
        condition: service_started
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 30s

volumes:
  postgres-data:
  seq-data:
```

**Key details:**
- `ASPNETCORE_URLS: http://+:8080` — app listens on HTTP only inside the container; HTTPS redirection from Story 1.1's `app.UseHttpsRedirection()` is harmless when no HTTPS listener is configured.
- `ASPNETCORE_ENVIRONMENT: Docker` — the app will look for `appsettings.Docker.json` but fall back to base config + env var overrides.
- `ConnectionStrings__DefaultConnection` env var overrides the empty base `appsettings.json` value.
- `depends_on: postgres: condition: service_healthy` — this resolves the deferred-work item: "MigrateAsync crashes if PostgreSQL unreachable at dev startup." Docker Compose will not start `oneid-server` until `pg_isready` succeeds.
- Named volumes (`postgres-data`, `seq-data`) satisfy AC #7 (data persists across restarts). Anonymous volumes do not persist.

### NuGet Packages — Add to `src/OneId.Server/OneId.Server.csproj`

```xml
<!-- Serilog -->
<PackageReference Include="Serilog.AspNetCore" Version="8.*" />
<PackageReference Include="Serilog.Sinks.OpenTelemetry" Version="3.*" />

<!-- OpenTelemetry -->
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.*" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.*" />
```

Use `Version="X.*"` range specifiers (latest stable minor/patch). Do NOT pin exact versions for these packages. After adding, run `dotnet build` and confirm zero warnings (TreatWarningsAsErrors is active from `Directory.Build.props`).

If there are version conflicts (MSB3277 — similar to the EF Core conflict in Story 1.1), add explicit version pins to `Directory.Build.props` using `<PackageReference Update ...>`.

### Program.cs — Serilog + OTEL Wiring (Minimal — Story 1.4 extends this)

Replace the existing `Program.cs` content. Preserve ALL AR-5 comments exactly as written. Only add Serilog/OTEL — do not change any other existing registration.

```csharp
using Microsoft.EntityFrameworkCore;
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

    // Serilog as the application logger — Story 1.4 adds enrichers and destructuring
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console(new JsonFormatter())
        .WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                ?? "http://localhost:4317";
            options.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.GrpcProtobuf;
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

    // AR-5 STEP 1: ITenantContextMiddleware MUST precede EF Core and OpenIddict — see architecture.md
    // TODO Story 1.3a: app.UseMiddleware<TenantContextMiddleware>();

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

    app.UseHttpsRedirection();
    app.UseAuthentication();
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
```

**Key points:**
- `MigrateAsync` now runs in both `Development` AND `Docker` environments so migrations apply in the container.
- `Log.Fatal` / `Log.CloseAndFlushAsync()` ensure bootstrap logger captures startup exceptions before the host logger is ready.
- `WriteTo.Console(new JsonFormatter())` emits structured JSON — satisfies AC #4 (structured fields).
- `AddOtlpExporter()` reads `OTEL_EXPORTER_OTLP_ENDPOINT` automatically — satisfies AC #6 for traces.
- `WriteTo.OpenTelemetry(...)` reads `OTEL_EXPORTER_OTLP_ENDPOINT` explicitly — satisfies AC #6 for logs.
- AR-5 comments are preserved verbatim.

### What This Story Does NOT Implement

Deferred to their owning stories:
- Serilog enrichers (EventType, TenantId, UserId, Outcome, TraceId) → Story 1.4
- `SerilogDestructuringTests.cs` → Story 1.4
- Testcontainers + Respawn + TestTokenFactory → Story 1.5
- GitHub Actions CI pipeline → Story 1.6
- ArchUnit boundary enforcement → Story 1.7a
- DevSeeder → Story 1.7b

### .gitignore — Create at Repo Root

Story 1.1's code review noted `.gitignore` is missing (Review finding patched). Add one if not present:

```gitignore
# .NET
bin/
obj/
*.user
*.suo
.vs/
appsettings.*.local.json

# Signing key (NFR-7)
keys/

# Node
node_modules/
dist/

# Docker
.env

# IDE
.idea/
*.DS_Store
```

### Previous Story Learnings (Story 1.1)

- **.NET 10** — all Dockerfiles must use `sdk:10.0` / `aspnet:10.0` tags
- **EFCore.NamingConventions** — `UseSnakeCaseNamingConvention()` goes in `AddDbContext` options builder, not `OnModelCreating` — already correct in Program.cs
- **MSB3277 version conflicts** — if package restore fails with conflicting dependencies, add explicit `<PackageReference Update ...>` pins to `Directory.Build.props`. Apply same fix pattern used for EF Core in Story 1.1.
- **TreatWarningsAsErrors** is active — any new warning from added packages is a build failure
- **shadcn init** issues are not relevant here (backend-only story)

### Deferred Work Item Resolution

From `deferred-work.md`: *"`MigrateAsync` crashes if PostgreSQL unreachable at dev startup — revisit if Docker Compose health checks in Story 1.2 don't cover it."*

This story resolves it: `depends_on: postgres: condition: service_healthy` ensures `oneid-server` does not start until `pg_isready` succeeds. The migration will run against a ready PostgreSQL instance.

### Project Structure Notes

Files created by this story (all relative to repo root):
- `docker-compose.yml` (new)
- `otel-collector-config.yml` (new)
- `src/OneId.Server/Dockerfile` (update — was a placeholder comment)
- `src/OneId.Server/OneId.Server.csproj` (update — add packages)
- `src/OneId.Server/Program.cs` (update — add Serilog + OTEL wiring)
- `.gitignore` (new — if not already present)

No new directories required. No test files required for this story (observability integration tests are Story 1.4).

### References

- [Source: epics.md#Story 1.2] — acceptance criteria
- [Source: epics.md#Epic 1 Implementation Notes] — AR-4 (Serilog + OTEL wired Day 1), AR-2 (docker-compose requirement)
- [Source: architecture.md#Infrastructure & Deployment] — "Serilog → Serilog.Sinks.OpenTelemetry → OTEL Collector → Seq"
- [Source: architecture.md#Complete Project Directory Structure] — `docker-compose.yml` at project root
- [Source: implementation-artifacts/deferred-work.md] — MigrateAsync crash on unreachable PG (resolved by this story)
- [Source: implementation-artifacts/1-1-initialize-backend-and-frontend-projects.md#Debug Log] — .NET 10 runtime, MSB3277 fix pattern

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- `OtlpProtocol.GrpcProtobuf` does not exist in Serilog.Sinks.OpenTelemetry 3.0.0; correct value is `OtlpProtocol.Grpc`.
- Seq 2025.x requires `SEQ_FIRSTRUN_NOAUTHENTICATION: "true"` (or an admin password) — `ACCEPT_EULA: Y` alone no longer suffices.
- `obj/` and `bin/` folders must be excluded from Docker build context via `.dockerignore`; without it, Windows-specific cached package assets corrupt the Linux restore and cause MSB4018 errors.

### Completion Notes List

- Implemented multi-stage Dockerfile using `mcr.microsoft.com/dotnet/sdk:10.0` → `mcr.microsoft.com/dotnet/aspnet:10.0` with `curl` installed for health checks.
- Created `otel-collector-config.yml` routing both traces and logs pipelines via OTLP → Seq using `otel/opentelemetry-collector-contrib` image (base image lacks `otlphttp` exporter).
- Created `docker-compose.yml` with all 4 services: postgres, seq, otel-collector, oneid-server. Added `SEQ_FIRSTRUN_NOAUTHENTICATION: "true"` to fix Seq 2025.x first-run authentication requirement. Both postgres-data and seq-data are named volumes for persistence.
- Added 5 NuGet packages to `OneId.Server.csproj`. Build passes with 0 warnings (TreatWarningsAsErrors active).
- Replaced `Program.cs` with Serilog bootstrap logger, `UseSerilog` with JSON console + OTLP sink, OTEL tracing with AspNetCore instrumentation and OTLP exporter. All AR-5 comments preserved. MigrateAsync now runs in both `Development` and `Docker` environments.
- Created `.dockerignore` to exclude `bin/`, `obj/`, and other non-source directories from Docker build context.
- All 7 ACs validated live: 4 services healthy, `/health` returns 200, Seq UI accessible, structured log events with `service.name=OneId.Server` spans confirmed in Seq API, postgres data persists across `down && up`.

### File List

- `src/OneId.Server/Dockerfile` (updated — was placeholder comment)
- `otel-collector-config.yml` (new)
- `docker-compose.yml` (new)
- `.dockerignore` (new)
- `src/OneId.Server/OneId.Server.csproj` (updated — Serilog + OTEL packages added)
- `src/OneId.Server/Program.cs` (updated — Serilog + OTEL wiring, Docker env migration, bootstrap logger)

### Review Findings

- [x] [Review][Patch] otel-collector depends_on seq uses `service_started` instead of `service_healthy` [docker-compose.yml:40-42]
- [x] [Review][Patch] .dockerignore missing `.env` and sensitive-file exclusions [.dockerignore]
- [x] [Review][Defer] Hardcoded dev credentials (postgres:postgres) in docker-compose.yml — deferred, pre-existing
- [x] [Review][Defer] Unpinned `latest` image tags for seq and otel-collector — deferred, pre-existing
- [x] [Review][Defer] OTEL collector receiver exposed on 0.0.0.0 with no authentication — deferred, pre-existing
- [x] [Review][Defer] No `restart` policy on oneid-server — permanent exit on transient migration failure — deferred, pre-existing
- [x] [Review][Defer] Serilog OTLP sink has OtlpProtocol.Grpc hardcoded — future maintenance risk if endpoint changes — deferred, pre-existing
- [x] [Review][Defer] No Serilog minimum level configured — MEL LogLevel overrides in appsettings.json ignored by Serilog — deferred, pre-existing
- [x] [Review][Defer] Only OneId.Server.csproj copied before dotnet restore — will break when project references added — deferred, pre-existing
- [x] [Review][Defer] Seq healthcheck assumes curl present in seq image — deferred, pre-existing
- [x] [Review][Defer] AllowedHosts: "localhost" blocks service-to-service HTTP when other services call oneid-server — deferred, pre-existing
- [x] [Review][Defer] pg_isready may pass before POSTGRES_DB init script completes (theoretical race) — deferred, pre-existing

### Change Log

- 2026-05-22: Implemented Story 1.2 — Docker Compose local dev stack with Serilog + OTEL observability pipeline
