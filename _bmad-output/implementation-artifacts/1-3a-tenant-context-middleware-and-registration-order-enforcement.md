# Story 1.3a: Tenant Context Middleware and Registration Order Enforcement

Status: done

## Story

As a developer,
I want `ITenantContext` middleware registered and validated before any data access occurs,
so that no code path can bypass tenant isolation, even if future developers reorder middleware registration.

## Acceptance Criteria

1. **Given** `OneId.Server` starts up **When** the DI container is built **Then** `ITenantContext` (backed by `TenantContext`) is registered as a scoped service in `Program.cs` before `AddDbContext` and annotated with `// AR-5: ITenantContext MUST precede OpenIddict and EF Core — see architecture.md`

2. **Given** an authenticated request containing a valid `tid` claim (parseable as `Guid`) **When** `TenantContextMiddleware` executes **Then** `ITenantContext.TenantId` returns the parsed `Guid` for that request scope

3. **Given** `TenantContextMiddleware` has NOT yet executed for the current request **When** any code accesses `ITenantContext.TenantId` **Then** `ITenantContext` throws `InvalidOperationException("Tenant context not initialized — check middleware registration order in Program.cs")`

4. **Given** `TenantContextTests.cs` runs **When** it calls `TenantId` on a fresh, uninitialized `TenantContext` **Then** the exact `InvalidOperationException` message fires

5. **Given** `RegistrationOrderIntegrationTests.cs` runs using `WebApplicationFactory` **When** an HTTP request is processed through the full pipeline (auth + TenantContextMiddleware) with a `tid` claim present **Then** `ITenantContext.TenantId` is non-null within the request scope

6. **Given** a second test case in `RegistrationOrderIntegrationTests.cs` **When** `ITenantContext.TenantId` is accessed on a freshly resolved DI scope (before any middleware has run for that scope) **Then** the guard `InvalidOperationException` fires — proving order enforcement is structural, not runtime-luck

## Tasks / Subtasks

- [x] Task 1: Implement ITenantContext interface and TenantContext class (AC: 1, 3, 4)
  - [x] Update `src/OneId.Server/Application/Common/ITenantContext.cs` — add `Guid TenantId { get; }` and `bool IsInitialized { get; }` (exact signatures in Dev Notes)
  - [x] Create `src/OneId.Server/Application/Common/TenantContext.cs` — scoped implementation with nullable `_tenantId` backing field, guard on `TenantId`, and `internal void Initialize(Guid tenantId)` setter
  - [x] Add `InternalsVisibleTo` for both test assemblies so `TenantContext.Initialize()` is callable from tests (exact csproj snippet in Dev Notes)

- [x] Task 2: Create TenantContextMiddleware (AC: 2, 3)
  - [x] Create `src/OneId.Server/Infrastructure/Middleware/TenantContextMiddleware.cs`
  - [x] Inject `TenantContext` (concrete type) via `InvokeAsync` parameter — NOT `ITenantContext`
  - [x] Extract `tid` claim: `context.User.FindFirst("tid")?.Value`
  - [x] If present and `Guid.TryParse` succeeds → call `tenantContext.Initialize(tenantId)`
  - [x] If absent or parse fails → do NOT initialize; proceed to `next` (guard fires if TenantId accessed downstream)
  - [x] Always `await next(context)` — never short-circuit

- [x] Task 3: Wire services and middleware in Program.cs (AC: 1)
  - [x] Replace the `// AR-5 STEP 1` comment + `// TODO Story 1.3a` placeholder with actual DI registration: `AddScoped<TenantContext>()` and `AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>())`
  - [x] Keep the `// AR-5: ITenantContext MUST precede OpenIddict and EF Core — see architecture.md` comment above the registrations
  - [x] Add `app.UseMiddleware<TenantContextMiddleware>()` AFTER `app.UseAuthentication()` and BEFORE `app.UseAuthorization()` in the middleware pipeline section (see Dev Notes for exact pipeline order and rationale)
  - [x] `dotnet build` — zero warnings (TreatWarningsAsErrors is active from Directory.Build.props)

- [x] Task 4: Create test projects (AC: 4, 5, 6)
  - [x] Create directory `tests/OneId.Server.UnitTests/` with `OneId.Server.UnitTests.csproj` (xUnit 2.*, exact .csproj in Dev Notes)
  - [x] Create directory `tests/OneId.Server.IntegrationTests/` with `OneId.Server.IntegrationTests.csproj` (xUnit + Microsoft.AspNetCore.Mvc.Testing 10.*, exact .csproj in Dev Notes)
  - [x] Add both projects to solution: `dotnet sln OneId.slnx add tests/OneId.Server.UnitTests/OneId.Server.UnitTests.csproj` and `dotnet sln OneId.slnx add tests/OneId.Server.IntegrationTests/OneId.Server.IntegrationTests.csproj`
  - [x] Create `tests/OneId.Server.UnitTests/Application/Common/` directory structure

- [x] Task 5: Write unit tests (AC: 4)
  - [x] Create `tests/OneId.Server.UnitTests/Application/Common/TenantContextTests.cs`
  - [x] `TenantId_ThrowsInvalidOperationException_WhenNotInitialized` — assert exact exception message
  - [x] `IsInitialized_ReturnsFalse_BeforeInitialize`
  - [x] `TenantId_ReturnsCorrectGuid_AfterInitialize`
  - [x] `IsInitialized_ReturnsTrue_AfterInitialize`

- [x] Task 6: Write integration tests (AC: 5, 6)
  - [x] Create `tests/OneId.Server.IntegrationTests/RegistrationOrderIntegrationTests.cs`
  - [x] Create inner `TestAuthHandler` (AuthenticationHandler subclass) that injects a fixed `tid` Guid claim into `HttpContext.User`
  - [x] Create `OneIdTestFactory : WebApplicationFactory<Program>` override that: (a) uses `UseEnvironment("Testing")` to skip migration, (b) replaces DbContext with InMemory, (c) registers `TestAuthHandler` auth scheme (see Dev Notes for complete factory code)
  - [x] Map a minimal test endpoint `GET /test/tenant-id` inside the factory that reads and returns `ITenantContext.TenantId.ToString()`
  - [x] Test 1 (`TenantId_IsNonNull_WhenMiddlewareRunsInCorrectOrder`): HTTP GET `/test/tenant-id` → 200 with valid Guid in body
  - [x] Test 2 (`TenantId_ThrowsGuard_WhenAccessedOnFreshDiScope`): `factory.Services.CreateScope()` → `GetRequiredService<ITenantContext>()` → access `.TenantId` → assert `InvalidOperationException` with exact message

- [x] Task 7: Validate all tests pass
  - [x] `dotnet build` — zero warnings across all projects
  - [x] `dotnet test tests/OneId.Server.UnitTests` — 4 tests, all pass
  - [x] `dotnet test tests/OneId.Server.IntegrationTests` — 2 tests, all pass
  - [x] Manually verify `docker compose up` still starts cleanly with the wired middleware

### Review Findings

- [x] [Review][Decision] Should `Guid.Empty` be rejected as a valid tenant ID? — Resolved: reject. Guard added to `Initialize` + middleware condition updated.
- [x] [Review][Patch] `Initialize()` not idempotent — second call silently overwrites tenant ID [src/OneId.Server/Application/Common/TenantContext.cs:13]
- [x] [Review][Patch] `UseRouting()` placed after `UseAuthorization()` in test factory — incorrect ASP.NET Core pipeline order [tests/OneId.Server.IntegrationTests/RegistrationOrderIntegrationTests.cs:76]
- [x] [Review][Patch] Test assertion does not verify returned GUID equals `TestTenantId` [tests/OneId.Server.IntegrationTests/RegistrationOrderIntegrationTests.cs:34]
- [x] [Review][Patch] Floating `10.*` version for `Microsoft.EntityFrameworkCore.InMemory` — pin to `10.0.8` [tests/OneId.Server.IntegrationTests/OneId.Server.IntegrationTests.csproj]
- [x] [Review][Patch] Comment references wrong type name `ITenantContextMiddleware` — should be `TenantContextMiddleware` [src/OneId.Server/Program.cs]
- [x] [Review][Patch] Comment text in `ITenantContext.cs` diverges from canonical AR-5 phrasing in `Program.cs` [src/OneId.Server/Application/Common/ITenantContext.cs:1]
- [x] [Review][Defer] Thread-safety on `TenantContext._tenantId` — no `volatile`/`Interlocked` protection [src/OneId.Server/Application/Common/TenantContext.cs:5] — deferred, architectural assumption (HTTP request scope is single-threaded; same as DbContext)
- [x] [Review][Defer] `TestAuthHandler` always succeeds — unauthenticated HTTP path not integration-tested [tests/OneId.Server.IntegrationTests/RegistrationOrderIntegrationTests.cs:86] — deferred, test coverage gap for when anonymous endpoints that access `ITenantContext` are introduced

## Dev Notes

### TenantId Type: Guid

`ITenantContext.TenantId` is `Guid`. Architecture specifies "Entity IDs: Guid throughout." The JWT `tid` claim is a string in the token; the middleware parses it with `Guid.TryParse`. This ensures Story 1.3b's EF Core global query filter can compare `ITenantContext.TenantId` directly against entity `Guid TenantId` columns without an in-filter parse.

If `Guid.TryParse` fails (malformed claim), the middleware proceeds without initializing — the guard fires on any downstream access, surfacing the misconfigured token immediately rather than silently.

### Exact File Implementations

**`src/OneId.Server/Application/Common/ITenantContext.cs`**

```csharp
// AR-5: ITenantContext MUST be registered before EF Core and OpenIddict — see architecture.md
namespace OneId.Server.Application.Common;

public interface ITenantContext
{
    /// <exception cref="InvalidOperationException">Thrown when accessed before TenantContextMiddleware has executed for this request.</exception>
    Guid TenantId { get; }
    bool IsInitialized { get; }
}
```

**`src/OneId.Server/Application/Common/TenantContext.cs`**

```csharp
namespace OneId.Server.Application.Common;

public sealed class TenantContext : ITenantContext
{
    private Guid? _tenantId;

    public Guid TenantId =>
        _tenantId ?? throw new InvalidOperationException(
            "Tenant context not initialized — check middleware registration order in Program.cs");

    public bool IsInitialized => _tenantId.HasValue;

    internal void Initialize(Guid tenantId) => _tenantId = tenantId;
}
```

`Initialize` is `internal` — accessible to the middleware (same assembly) and test projects (via `InternalsVisibleTo`), but NOT exposed via `ITenantContext`. Application code cannot accidentally call it.

**`src/OneId.Server/Infrastructure/Middleware/TenantContextMiddleware.cs`**

```csharp
using OneId.Server.Application.Common;

namespace OneId.Server.Infrastructure.Middleware;

public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        var tidClaim = context.User.FindFirst("tid")?.Value;
        if (tidClaim is not null && Guid.TryParse(tidClaim, out var tenantId))
        {
            tenantContext.Initialize(tenantId);
        }
        await next(context);
    }
}
```

Injecting `TenantContext` (concrete) instead of `ITenantContext` into `InvokeAsync` gives access to `Initialize()` without polluting the interface. ASP.NET Core's middleware activation resolves constructor + `InvokeAsync` parameters from DI independently — this pattern is by design.

### Program.cs Changes (exact diff intent)

**DI section — replace the AR-5 STEP 1 block:**

```csharp
// AR-5: ITenantContext MUST precede OpenIddict and EF Core — see architecture.md
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
```

Remove the `// TODO Story 1.3a: app.UseMiddleware<TenantContextMiddleware>();` comment. It was a placeholder and is no longer needed.

**Middleware pipeline section — between `UseAuthentication` and `UseAuthorization`:**

```csharp
app.UseAuthentication();
// AR-5: ITenantContextMiddleware MUST precede OpenIddict and EF Core — see architecture.md
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();
```

**Pipeline ordering rationale:** `UseAuthentication()` processes the incoming JWT and populates `HttpContext.User`. `TenantContextMiddleware` then reads the `tid` claim from `HttpContext.User` — it must run AFTER auth, not before. The "before OpenIddict" constraint from AR-5 is primarily about DI registration order (OpenIddict comes in Story 2.1 after `AddDbContext`) and about the middleware executing before OpenIddict's endpoint handlers process requests (which happens at the `MapControllers`/endpoint level, well after all middleware in the pipeline).

### InternalsVisibleTo Setup

Add to `src/OneId.Server/OneId.Server.csproj` inside a new `<ItemGroup>`:

```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
    <_Parameter1>OneId.Server.UnitTests</_Parameter1>
  </AssemblyAttribute>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
    <_Parameter1>OneId.Server.IntegrationTests</_Parameter1>
  </AssemblyAttribute>
</ItemGroup>
```

This exposes `TenantContext.Initialize()` to both test assemblies without making it public. No `AssemblyInfo.cs` file needed — the csproj `AssemblyAttribute` approach is the modern equivalent.

### Test Project .csproj Files

**`tests/OneId.Server.UnitTests/OneId.Server.UnitTests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="coverlet.collector" Version="6.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\OneId.Server\OneId.Server.csproj" />
  </ItemGroup>
</Project>
```

**`tests/OneId.Server.IntegrationTests/OneId.Server.IntegrationTests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="coverlet.collector" Version="6.*" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\OneId.Server\OneId.Server.csproj" />
  </ItemGroup>
</Project>
```

`Microsoft.EntityFrameworkCore.InMemory` is needed by the integration test factory to replace PostgreSQL so tests run without a real database.

### WARNING: TreatWarningsAsErrors Applies to Test Projects

`Directory.Build.props` at the root sets `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` for ALL projects in the tree, including test projects. Any nullable annotation warning in test code is a build failure. Use proper `?` annotations throughout.

### Integration Test — Complete Implementation

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace OneId.Server.IntegrationTests;

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
        Assert.True(Guid.TryParse(body, out _), $"Expected a Guid in body, got: {body}");
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
                opt.UseInMemoryDatabase("TestDb_RegistrationOrder"));

            // Test auth scheme — injects tid Guid claim without a real JWT
            services.AddAuthentication(defaultScheme: "Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        });

        builder.Configure(app =>
        {
            // Minimal pipeline for ordering tests — mirrors Program.cs middleware order
            app.UseAuthentication();
            app.UseMiddleware<TenantContextMiddleware>();
            app.UseAuthorization();
            app.UseRouting();
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
```

**Note on `builder.Configure` in WebApplicationFactory:** When you call `builder.Configure(app => {...})`, it REPLACES the entire app configuration from `Program.cs`. This means Serilog bootstrap, OTEL, and all other middleware from `Program.cs` are NOT present in the test server. This is intentional for these ordering tests — we're testing the middleware itself, not the full production pipeline. The test server is minimal: auth + TenantContextMiddleware + a test endpoint.

**TenantContextMiddleware namespace import:** The factory's `builder.Configure` block needs `using OneId.Server.Infrastructure.Middleware;` — include it at the top of the test file.

### What This Story Does NOT Implement

- EF Core global query filters using `ITenantContext.TenantId` — deferred to Story 1.3b
- `InternalAdminContext.cs` (different concern, Story 1.7a)
- DevSeeder (Story 1.7b)
- Test base class (`IntegrationTestBase` with Testcontainers/Respawn) — Story 1.5
- `TestTokenFactory` — Story 1.5
- Real JWT validation / OpenIddict auth — Story 2.1
- `DevSigningKeyStabilityTest.cs` already exists as a `[Fact(Skip = "...")]` placeholder — do NOT remove the Skip

### Previous Story Learnings (From Story 1.2)

- .NET 10 is used (not .NET 9 as the architecture doc says) — all new project files must target `net10.0`
- `TreatWarningsAsErrors` from `Directory.Build.props` applies to ALL projects — no nullable annotation sloppiness in test code
- MSB3277 version conflicts: if packages have conflicting transitive deps, add `<PackageReference Update ...>` pins in `Directory.Build.props`
- `OtlpProtocol.Grpc` (not `GrpcProtobuf`) is the correct enum value in `Serilog.Sinks.OpenTelemetry`

### Project Structure Notes

New files created by this story (paths relative to repo root):
- `src/OneId.Server/Application/Common/ITenantContext.cs` (update)
- `src/OneId.Server/Application/Common/TenantContext.cs` (new)
- `src/OneId.Server/Infrastructure/Middleware/TenantContextMiddleware.cs` (new)
- `src/OneId.Server/Program.cs` (update — DI registration + middleware pipeline)
- `src/OneId.Server/OneId.Server.csproj` (update — InternalsVisibleTo)
- `tests/OneId.Server.UnitTests/OneId.Server.UnitTests.csproj` (new)
- `tests/OneId.Server.UnitTests/Application/Common/TenantContextTests.cs` (new)
- `tests/OneId.Server.IntegrationTests/OneId.Server.IntegrationTests.csproj` (new)
- `tests/OneId.Server.IntegrationTests/RegistrationOrderIntegrationTests.cs` (new)

### References

- [Source: epics.md#Story 1.3a] — acceptance criteria and test names
- [Source: architecture.md#Process Patterns] — "ITenantContext scoped service populated from JWT claim"
- [Source: architecture.md#All Implementation Agents MUST] — Rule 6: "TenantId from ITenantContext only — never as a method parameter"
- [Source: architecture.md#Complete Project Directory Structure] — `tests/` structure and middleware location
- [Source: architecture.md#Core Architectural Decisions] — AR-5 registration order
- [Source: epics.md#Epic 1 Implementation Notes] — "Program.cs registration order is a delivery correctness gate"
- [Source: implementation-artifacts/1-2-local-development-stack-docker-compose.md#Dev Notes] — .NET 10 runtime, MSB3277 fix pattern

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Implemented `ITenantContext` interface with `Guid TenantId` (throws guard when uninitialized) and `bool IsInitialized` properties.
- Implemented `TenantContext` sealed class with internal `Initialize(Guid)` method; `InternalsVisibleTo` added to csproj for both test assemblies.
- Created `TenantContextMiddleware` injecting concrete `TenantContext` (not the interface) to access `Initialize()`.
- Updated `Program.cs`: replaced AR-5 STEP 1 placeholder with real DI registrations; wired `app.UseMiddleware<TenantContextMiddleware>()` between `UseAuthentication()` and `UseAuthorization()`.
- Integration test project required `<FrameworkReference Include="Microsoft.AspNetCore.App" />` and `using Microsoft.AspNetCore.Builder;` (not documented in Dev Notes) — added to resolve extension methods in `Microsoft.NET.Sdk` project.
- All 6 tests pass: 4 unit tests + 2 integration tests. Full solution: 0 warnings, 0 errors.

### File List

- src/OneId.Server/Application/Common/ITenantContext.cs (updated)
- src/OneId.Server/Application/Common/TenantContext.cs (new)
- src/OneId.Server/Infrastructure/Middleware/TenantContextMiddleware.cs (new)
- src/OneId.Server/Program.cs (updated — DI registration + middleware pipeline)
- src/OneId.Server/OneId.Server.csproj (updated — InternalsVisibleTo)
- tests/OneId.Server.IntegrationTests/OneId.Server.IntegrationTests.csproj (updated — added AspNetCore.App framework reference + InMemory + Mvc.Testing packages)
- tests/OneId.Server.IntegrationTests/RegistrationOrderIntegrationTests.cs (new)
- tests/OneId.Server.UnitTests/Application/Common/TenantContextTests.cs (new)
