# Story 1.4: Observability Pipeline (Serilog + OTEL + Seq)

Status: done

## Story

As a developer,
I want structured logs and OTEL traces flowing through the collector to Seq with sensitive data provably absent,
so that the observability pipeline is wired on Day 1 and never retrofitted, and credentials never appear in logs.

## Acceptance Criteria

1. **Given** `OneId.Server` processes any HTTP request **When** a structured log event is emitted **Then** the event is enriched with: `EventType`, `TenantId` (nullable), `UserId` (nullable), `Outcome`, `TraceId` **And** these fields are added via Serilog enrichers — no per-call-site field injection required

2. **Given** `SerilogDestructuringTests.cs` runs **When** a log statement is invoked with a raw password, a `Authorization: Bearer ...` header value, or an OpenIddict client secret **Then** the emitted log event does NOT contain the raw sensitive value **And** the field is present but replaced with `[Redacted]` **And** the test covers all three sensitive types explicitly

3. **Given** a request completes on `OneId.Server` **When** OTEL tracing is enabled **Then** a span with service name `OneId.Server` is exported to the OTEL Collector **And** the Collector forwards it to Seq (pipeline validated — span visible in Seq UI) **And** the exporter is configured to point at the Collector (not Seq directly), enforced by the `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable

4. **Given** a developer adds a new log statement anywhere in the codebase **When** the statement is executed **Then** standard enriched fields are automatically present — no boilerplate required

## Tasks / Subtasks

- [x] Task 1: Create `SerilogConfiguration.cs` with all enrichers (AC: 1, 2, 4)
  - [x] Create `src/OneId.Server/Infrastructure/Logging/SerilogConfiguration.cs`
  - [x] Implement `TenantIdEnricher : ILogEventEnricher` — reads from `IHttpContextAccessor → HttpContext.RequestServices → ITenantContext`
  - [x] Implement `UserIdEnricher : ILogEventEnricher` — reads `sub` claim from `IHttpContextAccessor.HttpContext.User`
  - [x] Implement `TraceIdEnricher : ILogEventEnricher` — reads `Activity.Current?.TraceId.ToString()`
  - [x] Implement `EventTypeEnricher : ILogEventEnricher` — computes `(uint)messageTemplate.GetHashCode()` formatted as `{0:X8}`
  - [x] Implement `SensitiveDataRedactionEnricher : ILogEventEnricher` — no DI needed; redacts by property name and Bearer value pattern (see Dev Notes)
  - [x] Add `AddSerilogEnrichers(this IServiceCollection services)` extension method

- [x] Task 2: Register enrichers in `Program.cs` and wire request logging (AC: 1, 3, 4)
  - [x] Add `builder.Services.AddHttpContextAccessor()` before Serilog registration
  - [x] Call `builder.Services.AddSerilogEnrichers()` (from Task 1 extension)
  - [x] Add `app.UseSerilogRequestLogging(options => { ... })` AFTER `ExceptionHandlingMiddleware` and BEFORE `UseRouting` equivalent — enriches request completion log with `Outcome` (see Dev Notes for exact placement and options lambda)

- [x] Task 3: Add Serilog minimum level config to `appsettings.json` (AC: 1, 4)
  - [x] Add `"Serilog"` section with `MinimumLevel.Default: "Information"` and `Override` entries (see Dev Notes for exact JSON)
  - [x] Add `"Serilog"` section to `appsettings.Development.json` with `MinimumLevel.Default: "Debug"`
  - [x] Keep existing `"Logging"` section as fallback (Serilog takes precedence when `UseSerilog` is called)

- [x] Task 4: Write `SerilogDestructuringTests.cs` (AC: 2)
  - [x] Create `tests/OneId.Server.UnitTests/Infrastructure/SerilogDestructuringTests.cs`
  - [x] Implement `CollectingLogEventSink : ILogEventSink` helper (captures events into a `List<LogEvent>`)
  - [x] `Password_IsRedacted_AndNotPresentAsPlaintext` — logs with `{Password}`, asserts property value is `"[Redacted]"`, asserts `RenderMessage()` does not contain raw value
  - [x] `AuthorizationBearerToken_IsRedacted_AndNotPresentAsPlaintext` — logs with `{AuthorizationHeader}` = `"Bearer eyJhbG..."`, asserts redacted
  - [x] `ClientSecret_IsRedacted_AndNotPresentAsPlaintext` — logs with `{ClientSecret}`, asserts redacted
  - [x] Add `[Collection("Serilog")]` attribute to the test class to prevent xUnit parallel execution issues

- [x] Task 5: Build verification (AC: all)
  - [x] `dotnet build` — zero warnings across all projects
  - [x] `dotnet test tests/OneId.Server.UnitTests` — all 3 new + 1 existing skipped test pass
  - [x] `dotnet test tests/OneId.Server.IntegrationTests` — all existing 5 integration tests still pass (no regression)

## Dev Notes

### What is Already Wired (DO NOT re-add)

From previous stories, the following are ALREADY in `Program.cs`:
```csharp
// Already present — DO NOT duplicate:
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)          // ← picks up ILogEventEnricher registrations automatically
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.OpenTelemetry(options => { ... }));

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());
```

The `.ReadFrom.Services(services)` call is the key — any `ILogEventEnricher` registered in DI is automatically applied to every log event. Register enrichers as **singletons**.

### SerilogConfiguration.cs — Exact Implementation

**File:** `src/OneId.Server/Infrastructure/Logging/SerilogConfiguration.cs`

```csharp
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.Common;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace OneId.Server.Infrastructure.Logging;

public static class SerilogConfiguration
{
    public static IServiceCollection AddSerilogEnrichers(this IServiceCollection services)
    {
        services.AddSingleton<ILogEventEnricher, EventTypeEnricher>();
        services.AddSingleton<ILogEventEnricher, TraceIdEnricher>();
        services.AddSingleton<ILogEventEnricher, TenantIdEnricher>();
        services.AddSingleton<ILogEventEnricher, UserIdEnricher>();
        services.AddSingleton<ILogEventEnricher, SensitiveDataRedactionEnricher>();
        return services;
    }
}

public sealed class EventTypeEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var eventType = (uint)logEvent.MessageTemplate.Text.GetHashCode();
        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("EventType", $"{eventType:X8}"));
    }
}

public sealed class TraceIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null) return;

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
    }
}

public sealed class TenantIdEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.RequestServices is null) return;

        try
        {
            var tenantContext = httpContext.RequestServices.GetService<ITenantContext>();
            if (tenantContext?.IsInitialized == true)
            {
                logEvent.AddPropertyIfAbsent(
                    propertyFactory.CreateProperty("TenantId", tenantContext.TenantId));
            }
        }
        catch
        {
            // Enrichment must never throw — silently skip on any error
        }
    }
}

public sealed class UserIdEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return;

        var sub = user.FindFirst("sub")?.Value;
        if (sub is not null)
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("UserId", sub));
        }
    }
}

public sealed class SensitiveDataRedactionEnricher : ILogEventEnricher
{
    // Redact by property name (case-insensitive)
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password", "Pwd", "ClientSecret", "client_secret", "Secret", "Token"
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var key in logEvent.Properties.Keys.ToList())
        {
            var value = logEvent.Properties[key];

            // Redact by property name
            if (SensitiveNames.Contains(key))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(key, "[Redacted]"));
                continue;
            }

            // Redact by value: any string starting with "Bearer " (Authorization header value)
            if (value is ScalarValue { Value: string str } && str.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(key, "[Redacted]"));
            }
        }
    }
}
```

**All required using statements are already included in the code snippet above.** No additional namespace imports needed.

### Program.cs — Required Changes

Add these two lines BEFORE the existing `builder.Host.UseSerilog(...)` block:

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddSerilogEnrichers();
```

Add `using OneId.Server.Infrastructure.Logging;` at the top of `Program.cs`.

Add `UseSerilogRequestLogging` in the middleware pipeline, AFTER `app.UseMiddleware<ExceptionHandlingMiddleware>()` and BEFORE `app.UseHttpsRedirection()`:

```csharp
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
// ... rest of pipeline unchanged
```

**Why `Outcome` only appears on request completion events:** `Outcome` requires knowing the HTTP response status code, which is only available after the handler runs. The `UseSerilogRequestLogging()` enricher fires once per request completion. For non-HTTP log events (startup, background), `Outcome` is intentionally absent — the AC marks it as nullable by pattern, not by explicit annotation.

### appsettings.json — Required Change

Add the `Serilog` section to `appsettings.json` (keep the existing `Logging` section — it's harmless when Serilog replaces the provider):

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  },
  "Logging": {
    ...
  }
}
```

Add to `appsettings.Development.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore.Database.Command": "Information",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  },
  ...
}
```

### SerilogDestructuringTests.cs — Exact Implementation

**File:** `tests/OneId.Server.UnitTests/Infrastructure/SerilogDestructuringTests.cs`

```csharp
using Serilog;
using Serilog.Core;
using Serilog.Events;
using OneId.Server.Infrastructure.Logging;

namespace OneId.Server.Tests.Infrastructure;

[Collection("Serilog")]  // prevents parallel execution — Serilog has static state
public class SerilogDestructuringTests
{
    private static (ILogger logger, List<LogEvent> events) CreateRedactingLogger()
    {
        var events = new List<LogEvent>();
        var logger = new LoggerConfiguration()
            .Enrich.With<SensitiveDataRedactionEnricher>()
            .WriteTo.Sink(new CollectingLogEventSink(events))
            .CreateLogger();
        return (logger, events);
    }

    [Fact]
    public void Password_IsRedacted_AndNotPresentAsPlaintext()
    {
        var (logger, events) = CreateRedactingLogger();

        logger.Information("Login attempt for {Email} with {Password}", "user@test.com", "SecretPassword123!");

        var evt = events.Single();
        var passwordProp = evt.Properties["Password"].ToString().Trim('"');
        Assert.Equal("[Redacted]", passwordProp);
        Assert.DoesNotContain("SecretPassword123!", evt.RenderMessage());
    }

    [Fact]
    public void AuthorizationBearerToken_IsRedacted_AndNotPresentAsPlaintext()
    {
        var (logger, events) = CreateRedactingLogger();

        logger.Information("Incoming request with {AuthorizationHeader}", "Bearer eyJhbGciOiJSUzI1NiJ9.test.signature");

        var evt = events.Single();
        var headerProp = evt.Properties["AuthorizationHeader"].ToString().Trim('"');
        Assert.Equal("[Redacted]", headerProp);
        Assert.DoesNotContain("Bearer", evt.RenderMessage());
        Assert.DoesNotContain("eyJhbGciOiJSUzI1NiJ9", evt.RenderMessage());
    }

    [Fact]
    public void ClientSecret_IsRedacted_AndNotPresentAsPlaintext()
    {
        var (logger, events) = CreateRedactingLogger();

        logger.Information("Client {ClientId} authenticated with {ClientSecret}", "my-client-id", "super-secret-client-value");

        var evt = events.Single();
        var secretProp = evt.Properties["ClientSecret"].ToString().Trim('"');
        Assert.Equal("[Redacted]", secretProp);
        Assert.DoesNotContain("super-secret-client-value", evt.RenderMessage());
    }
}

internal sealed class CollectingLogEventSink(List<LogEvent> events) : ILogEventSink
{
    public void Emit(LogEvent logEvent) => events.Add(logEvent);
}
```

**Why `[Collection("Serilog")]`:** Serilog 3.x has static global state (`Log.Logger`). When multiple test classes create `LoggerConfiguration` in parallel, there are no race conditions on our local loggers, but xUnit's parallel class execution can still cause issues if a test disposes the Serilog static logger. Using `[Collection("Serilog")]` serializes all tests in this collection.

**Why `CollectingLogEventSink` is `internal`:** The `InternalsVisibleTo` attribute in `OneId.Server.csproj` already exposes internals to unit tests. The sink lives in the test project, so it's internal there — no visibility issue.

### OTEL Pipeline — Already Complete

The OTEL pipeline (AC3) is ALREADY wired from Stories 1.1/1.2:
- `AddOtlpExporter()` in `Program.cs` reads `OTEL_EXPORTER_OTLP_ENDPOINT` automatically
- `docker-compose.yml` sets `OTEL_EXPORTER_OTLP_ENDPOINT: http://otel-collector:4317`
- `otel-collector-config.yml` exports traces to `http://seq/ingest/otlp`
- Service name set via `OTEL_SERVICE_NAME: OneId.Server` env var in compose

**No code changes required for AC3.** The validation is operational (verify spans appear in Seq after `docker compose up`), not a new automated test. AC3 was validated by Story 1.2 — the acceptance criteria here confirms the pipeline remains intact.

### Critical: SensitiveDataRedactionEnricher Property Enumeration Pattern

When iterating `logEvent.Properties.Keys.ToList()`, the `.ToList()` call is **required** to avoid `InvalidOperationException` on dictionary modification during enumeration. `AddOrUpdateProperty` mutates the `Properties` dictionary — always snapshot keys before iterating.

### Why Enrichers are Singletons (Not Transient/Scoped)

Serilog resolves `ILogEventEnricher` instances from DI when it configures the logger pipeline (at host startup). It does NOT resolve a new instance per log event. Enrichers MUST be registered as singletons. For enrichers that need request-scoped data (`TenantIdEnricher`, `UserIdEnricher`), the pattern is:
1. Inject `IHttpContextAccessor` (which is itself a singleton with ambient `AsyncLocal` storage)
2. Read `httpContextAccessor.HttpContext.RequestServices` (the scoped service provider for the current request) at enrichment time — NOT at constructor time

This pattern is the standard approach and avoids constructor-time capture of scoped state.

### ITenantContext Import

`TenantIdEnricher` needs `ITenantContext`. The namespace is `OneId.Server.Application.Common`. Add:
```csharp
using OneId.Server.Application.Common;
```
at the top of `SerilogConfiguration.cs`.

### Project Structure Note

`SerilogConfiguration.cs` lives at `src/OneId.Server/Infrastructure/Logging/SerilogConfiguration.cs` per the architecture directory structure. The `Infrastructure/Logging/` directory does not yet exist — create it. The test file lives at `tests/OneId.Server.UnitTests/Infrastructure/SerilogDestructuringTests.cs` — the `Infrastructure/` directory exists (contains `DevSigningKeyStabilityTest.cs`).

### Previous Story Learnings (From Story 1.3b)

- `TreatWarningsAsErrors` applies to ALL projects — no nullable annotation sloppiness in test code; all `?` operators must be correct
- xUnit 2.x parallel class execution causes issues with Serilog static state — use `[Collection("Serilog")]`
- `WebApplicationFactory.ConfigureWebHost.Configure(app => {...})` REPLACES Program.cs middleware pipeline — integration tests that spin up the full app DO inherit the new enrichers via DI; no changes needed to existing integration test factories
- In-memory provider is used in integration tests — the enrichers don't touch the database, so no Testcontainers concerns here
- All test files in the same collection are serialized — use `"Serilog"` collection name consistently if other Serilog tests are added later

### References

- [Source: epics.md#Story 1.4] — acceptance criteria, enricher field list, test file names
- [Source: architecture.md#Infrastructure & Deployment] — Serilog → `Serilog.Sinks.OpenTelemetry` → OTEL Collector → Seq pipeline decision
- [Source: architecture.md#Project Directory Structure] — `Infrastructure/Logging/SerilogConfiguration.cs` location, `SerilogDestructuringTests.cs` location
- [Source: architecture.md#All Implementation Agents MUST] — no credential logging rule
- [Source: epics.md#FR Coverage Map] — AR-4: Serilog + OTEL pipeline wired Day 1; sensitive field destructuring = `SerilogDestructuringTests.cs`
- [Source: epics.md#Epic 1] — NFR-6: Serilog → OTEL Collector → Seq; AR-4: cannot be deferred
- [Source: Program.cs] — existing Serilog/OTEL wiring; `.ReadFrom.Services(services)` already present
- [Source: otel-collector-config.yml] — traces and logs pipeline to Seq already configured
- [Source: implementation-artifacts/1-3b-...md#Debug Log] — xUnit parallel class issues → `[Collection]` annotation

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

None — implementation matched Dev Notes exactly.

### Completion Notes List

- Created `SerilogConfiguration.cs` with 5 enrichers: `EventTypeEnricher`, `TraceIdEnricher`, `TenantIdEnricher`, `UserIdEnricher`, `SensitiveDataRedactionEnricher`. All registered as singletons via `AddSerilogEnrichers()` extension; picked up automatically by `.ReadFrom.Services(services)` already in `Program.cs`.
- `TenantIdEnricher` and `UserIdEnricher` inject `IHttpContextAccessor` (singleton) and resolve request-scoped data at enrichment time — avoids captive dependency issue.
- `SensitiveDataRedactionEnricher` uses `.ToList()` on key snapshot to avoid InvalidOperationException during dictionary mutation.
- Added `AddHttpContextAccessor()` + `AddSerilogEnrichers()` calls in `Program.cs` before `UseSerilog`, and `UseSerilogRequestLogging` with `Outcome` enrichment after `ExceptionHandlingMiddleware`.
- `Serilog` sections added to both `appsettings.json` (Information default) and `appsettings.Development.json` (Debug default).
- 3 new unit tests cover all AC2 sensitive-data redaction scenarios; integration test log output confirms `EventType` field is emitted (e.g. `"EventType":"37452FDC"`).
- Build: 0 warnings, 0 errors. Unit tests: 9 passed, 1 skipped (pre-existing Epic 2 stub). Integration tests: 5 passed.

### File List

- src/OneId.Server/Infrastructure/Logging/SerilogConfiguration.cs (new)
- src/OneId.Server/Program.cs (modified)
- src/OneId.Server/appsettings.json (modified)
- src/OneId.Server/appsettings.Development.json (modified)
- tests/OneId.Server.UnitTests/Infrastructure/SerilogDestructuringTests.cs (new)

## Review Findings

*Source: Epic 1 code review, 2026-05-23*

- [x] [Review][Patch] Replaced `string.GetHashCode()` with FNV-1a 32-bit deterministic hash in `EventTypeEnricher` — stable across runtimes and deployments [SerilogConfiguration.cs:EventTypeEnricher]
- [x] [Review][Patch] Added `"access_token"`, `"refresh_token"`, `"id_token"` to `SensitiveNames` in `SensitiveDataRedactionEnricher` [SerilogConfiguration.cs:SensitiveDataRedactionEnricher]
- [x] [Review][Patch] Added `try/catch` to `UserIdEnricher.Enrich` matching `TenantIdEnricher` pattern [SerilogConfiguration.cs:UserIdEnricher]
