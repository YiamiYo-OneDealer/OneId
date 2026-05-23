# Story 1.5: Integration Test Infrastructure (Testcontainers + Respawn + TestTokenFactory)

Status: done

## Story

As a developer,
I want a reusable test base with a real PostgreSQL instance, clean per-test state, and a pinned test token factory,
so that every integration test runs against real infrastructure without external dependencies or test pollution between runs.

## Acceptance Criteria

1. **Given** an integration test class extends `IntegrationTestBase`
   **When** the test collection initializes (`IAsyncLifetime.InitializeAsync`)
   **Then** Testcontainers starts a PostgreSQL container and applies all EF Core migrations
   **And** a Respawn `Respawner` is created once after migrations complete (not per-test)

2. **Given** each `[Fact]` in an integration test class is about to run
   **When** per-test setup executes (`IntegrationTestBase.InitializeAsync`)
   **Then** `await checkpoint.ResetAsync(connection)` is called — restoring the database to a clean post-migration state before each test
   **And** this pattern ensures no test-order-dependent failures regardless of test execution order

3. **Given** an integration test needs a JWT
   **When** `TestTokenFactory.CreateToken(tenantId, userId, roles, seatCount)` is called
   **Then** a signed JWT is returned with exactly these claims: `tid` (string), `sub` (string), `scope` ("openid"), `seat_count` (integer, default 50), `roles` (string array)
   **And** claim names are snake_case: `seat_count` not `seatCount` — this is the pinned contract that Epic 3's license middleware depends on

4. **Given** `TestTokenFactoryContractTests.cs` exists
   **When** it runs at this stage (before Epic 3 wires the production token pipeline)
   **Then** it contains: `[Fact(Skip = "Wired in Epic 3 — remove Skip and make this pass in the licensing middleware story")]` with body `Assert.Fail("TestTokenFactory claim shape not yet validated against production ITokenClaimsEnricher — wire in Epic 3")`
   **And** this skip is visible as a known gap in CI test reports (not silent green)

5. **Given** CI runs the integration test suite
   **When** `dotnet test` executes
   **Then** all integration tests pass using Testcontainers — no external PostgreSQL required
   **And** the existing in-memory tests (`[Collection("Integration")]`) continue to pass unmodified

## Tasks / Subtasks

- [x] Task 1: Add NuGet packages (AC: 1, 2)
  - [x] Add `<PackageReference Include="Testcontainers.PostgreSql" Version="4.12.0" />` to `OneId.Server.IntegrationTests.csproj`
  - [x] Add `<PackageReference Include="Respawn" Version="7.0.0" />` to `OneId.Server.IntegrationTests.csproj`
  - [x] Do NOT remove `Microsoft.EntityFrameworkCore.InMemory` — existing in-memory tests still use it

- [x] Task 2: Create `Helpers/WebApplicationFactory.cs` (AC: 1, 2, 5)
  - [x] Create `tests/OneId.Server.IntegrationTests/Helpers/WebApplicationFactory.cs`
  - [x] Implement `IntegrationTestsCollection` with `[CollectionDefinition("IntegrationTests")]` and `ICollectionFixture<OneIdWebApplicationFactory>`
  - [x] Implement `OneIdWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime`
  - [x] In `IAsyncLifetime.InitializeAsync`: start container → access `Services` (triggers host creation) → run `MigrateAsync` → create Respawner (see Dev Notes for exact implementation)
  - [x] In `ConfigureWebHost`: `UseEnvironment("Testing")` + replace `DbContextOptions<AppDbContext>` with Testcontainers connection string (see Dev Notes for exact pattern)
  - [x] Expose `ResetDatabaseAsync()` method that opens an NpgsqlConnection and calls `_respawner.ResetAsync(conn)`
  - [x] In `IAsyncLifetime.DisposeAsync`: dispose the container

- [x] Task 3: Create `Helpers/TestTokenFactory.cs` (AC: 3)
  - [x] Create `tests/OneId.Server.IntegrationTests/Helpers/TestTokenFactory.cs`
  - [x] Implement `TestTokenFactory.CreateToken(Guid tenantId, Guid userId, string[]? roles = null, int seatCount = 50)` returning a signed JWT string
  - [x] Claims must be: `tid` (string), `sub` (string), `scope` ("openid"), `seat_count` (int — not string), `roles` (string array — always present, even if empty)
  - [x] Sign with HMAC-SHA256 using the pinned test key constant (see Dev Notes)
  - [x] Expose `internal static readonly SymmetricSecurityKey TestSigningKey` for Epic 2 JWT Bearer configuration in WebApplicationFactory

- [x] Task 4: Create `Helpers/IntegrationTestBase.cs` (AC: 2)
  - [x] Create `tests/OneId.Server.IntegrationTests/Helpers/IntegrationTestBase.cs`
  - [x] Abstract class with `[Collection("IntegrationTests")]` attribute
  - [x] Implements `IAsyncLifetime`
  - [x] Constructor injects `OneIdWebApplicationFactory factory`; exposes `Factory` and `Client = factory.CreateClient()`
  - [x] `InitializeAsync()` calls `Factory.ResetDatabaseAsync()` — clean DB before every test
  - [x] `DisposeAsync()` returns `Task.CompletedTask` — container lifecycle managed by collection fixture

- [x] Task 5: Create `TestTokenFactoryContractTests.cs` (AC: 4)
  - [x] Create `tests/OneId.Server.IntegrationTests/TestTokenFactoryContractTests.cs`
  - [x] Single `[Fact(Skip = "Wired in Epic 3 — remove Skip and make this pass in the licensing middleware story")]`
  - [x] Body: `Assert.Fail("TestTokenFactory claim shape not yet validated against production ITokenClaimsEnricher — wire in Epic 3")`

- [x] Task 6: Build and test verification (AC: 5)
  - [x] `dotnet build` — zero warnings across all projects
  - [x] `dotnet test tests/OneId.Server.IntegrationTests` — all existing tests pass; new `TestTokenFactoryContractTests` appears as 1 skipped
  - [x] `dotnet test tests/OneId.Server.UnitTests` — all existing tests still pass (no regression)

## Dev Notes

### What Already Exists — DO NOT Modify

**Existing integration tests** (`[Collection("Integration")]`) use in-memory DB with inline factories. They are DONE and must not be touched:
- `ConcurrencyConflictTests.cs` — uses `ConcurrencyTestFactory` (InMemory)
- `RegistrationOrderIntegrationTests.cs` — uses `OneIdTestFactory` (InMemory)
- `DevSeederIntegrationTests.cs` — uses `TenantIsolationServiceFactory` (InMemory, direct ServiceCollection)

These 5 tests pass today. This story adds NEW infrastructure alongside them.

### Connection String Source

`Program.cs` line 55 reads: `builder.Configuration.GetConnectionString("DefaultConnection")`. The `ConfigureWebHost` approach uses `RemoveAll<DbContextOptions<AppDbContext>>` + re-add with container URL (not `ConfigureAppConfiguration`) because `AddDbContext` runs during `ConfigureServices` and reads config at that time — an app-config override arrives too late.

### Migration Guard in Program.cs

`Program.cs` only runs `MigrateAsync()` when environment is `Development` or `Docker`:
```csharp
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
```
`UseEnvironment("Testing")` in the factory skips this, which is correct — we run migrations manually in `InitializeAsync` after the container is ready.

### WebApplicationFactory.cs — Exact Implementation

**Key timing constraint:** `_dbContainer.StartAsync()` MUST complete before `Services` is accessed. `WebApplicationFactory` creates the host lazily on first `Services`/`Server`/`CreateClient()` access. Starting the container first guarantees `GetConnectionString()` is valid when `ConfigureWebHost` runs.

**File:** `tests/OneId.Server.IntegrationTests/Helpers/WebApplicationFactory.cs`

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using OneId.Server.Infrastructure.Persistence;
using Respawn;
using Testcontainers.PostgreSql;
using Xunit;

namespace OneId.Server.IntegrationTests.Helpers;

[CollectionDefinition("IntegrationTests")]
public class IntegrationTestsCollection : ICollectionFixture<OneIdWebApplicationFactory>
{ }

public sealed class OneIdWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private Respawner _respawner = default!;

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _dbContainer.StartAsync();

        // Accessing Services triggers lazy host creation → ConfigureWebHost runs here
        // Container is already started so GetConnectionString() is valid
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // Checkpoint created ONCE after migrations — captures post-migration baseline
        await using var conn = new NpgsqlConnection(_dbContainer.GetConnectionString());
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            TablesToIgnore = [new Table("__EFMigrationsHistory")]
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace production Npgsql options with Testcontainers connection string.
            // Npgsql → Npgsql replacement avoids the "two EF Core providers" error
            // that occurs when replacing Npgsql with InMemory (different providers conflict).
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opt =>
                opt.UseNpgsql(_dbContainer.GetConnectionString())
                   .UseSnakeCaseNamingConvention());
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var conn = new NpgsqlConnection(_dbContainer.GetConnectionString());
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
    }
}
```

**Why `RemoveAll<DbContextOptions<AppDbContext>>`:** This removes only the options registered by Program.cs's `AddDbContext`. Internal Npgsql services remain but that's fine since we're still using Npgsql — no provider conflict.

**Why NOT `ConfigureAppConfiguration`:** Program.cs calls `AddDbContext` during `ConfigureServices`. By the time `ConfigureAppConfiguration` overrides are applied in test factories, `AddDbContext` has already read the connection string from the original configuration. Connection string override via config arrives too late to affect the options object.

### TestTokenFactory.cs — Exact Implementation

**File:** `tests/OneId.Server.IntegrationTests/Helpers/TestTokenFactory.cs`

```csharp
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace OneId.Server.IntegrationTests.Helpers;

public static class TestTokenFactory
{
    // Exposed internal so WebApplicationFactory can configure JWT Bearer validation in Epic 2
    internal static readonly SymmetricSecurityKey TestSigningKey =
        new(Encoding.UTF8.GetBytes("oneid-integration-test-signing-key-must-be-at-least-32!!"));

    public static string CreateToken(
        Guid tenantId,
        Guid userId,
        string[]? roles = null,
        int seatCount = 50)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                ["tid"] = tenantId.ToString(),
                ["sub"] = userId.ToString(),
                ["scope"] = "openid",
                ["seat_count"] = seatCount,        // integer in JWT payload — NOT string
                ["roles"] = roles ?? Array.Empty<string>(),  // always present, even if empty
            },
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(TestSigningKey, SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
```

**Claim contract (pinned for Epic 3 validation):**
- `tid` → string (tenant Guid as string)
- `sub` → string (user Guid as string)
- `scope` → string `"openid"`
- `seat_count` → integer (JSON number, default 50) — `SecurityTokenDescriptor.Claims` with `object` value serializes `int` as JSON number
- `roles` → string array (JSON array, always present)

**Why `JsonWebTokenHandler` + `SecurityTokenDescriptor.Claims` dict:** Correctly serializes `int` and `string[]` as JSON number and JSON array in the JWT payload. The older `JwtSecurityTokenHandler` + `ClaimsIdentity` would serialize `seat_count` as the string `"50"` — wrong type.

**Why `internal` signing key:** Epic 2's story for JWT Bearer validation will need to configure `WebApplicationFactory.ConfigureWebHost` to validate tokens signed with this key. The `internal` access is within the same assembly.

### IntegrationTestBase.cs — Exact Implementation

**File:** `tests/OneId.Server.IntegrationTests/Helpers/IntegrationTestBase.cs`

```csharp
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

    // Runs before every [Fact] — restores DB to clean post-migration state
    public async Task InitializeAsync() => await Factory.ResetDatabaseAsync();

    // Container lifecycle is managed by the collection fixture (OneIdWebApplicationFactory)
    public Task DisposeAsync() => Task.CompletedTask;
}
```

**xUnit collection fixture lifecycle:**
1. `OneIdWebApplicationFactory.InitializeAsync()` — once per collection (starts container, runs migrations, creates Respawner)
2. For each test method: `IntegrationTestBase.InitializeAsync()` → `ResetDatabaseAsync()` → clean DB
3. `OneIdWebApplicationFactory.DisposeAsync()` — once per collection (stops container)

**`[Collection]` inheritance:** xUnit propagates `[Collection("IntegrationTests")]` to all concrete subclasses. Concrete test classes DO NOT need to add `[Collection(...)]` again.

### TestTokenFactoryContractTests.cs — Exact Implementation

**File:** `tests/OneId.Server.IntegrationTests/TestTokenFactoryContractTests.cs`

```csharp
namespace OneId.Server.IntegrationTests;

public class TestTokenFactoryContractTests
{
    [Fact(Skip = "Wired in Epic 3 — remove Skip and make this pass in the licensing middleware story")]
    public void TestTokenFactory_ClaimShape_MatchesProductionITokenClaimsEnricher()
    {
        Assert.Fail("TestTokenFactory claim shape not yet validated against production ITokenClaimsEnricher — wire in Epic 3");
    }
}
```

**Why this test exists:** Creates a visible known gap in the CI test report. Epic 3's licensing middleware story must activate this test and verify that the production `ITokenClaimsEnricher` outputs `tid`, `sub`, `scope`, `seat_count` (int), `roles` (array) matching `TestTokenFactory.CreateToken`.

### Namespace Separation: Two Parallel Collections

| Collection | Attribute | Factory | DB Provider | Purpose |
|---|---|---|---|---|
| `"Integration"` | `[Collection("Integration")]` | Inline per-file | InMemory | Registration order, concurrency, tenant isolation |
| `"IntegrationTests"` | `[Collection("IntegrationTests")]` via `IntegrationTestBase` | `OneIdWebApplicationFactory` | Real PostgreSQL (Testcontainers) | Full-stack integration tests (Epic 2+) |

The two collections do NOT share fixtures and do NOT interfere with each other. Do not add `[Collection("IntegrationTests")]` to the existing files.

### Package Transitive Dependencies

`EFCore.NamingConventions` and `Npgsql` are transitively available via `<ProjectReference Include="..\..\src\OneId.Server\OneId.Server.csproj" />`. No explicit package reference needed for `UseSnakeCaseNamingConvention()` or `NpgsqlConnection`. If build fails with missing symbol, add `<PackageReference Include="EFCore.NamingConventions" Version="10.0.1" />` explicitly.

`Microsoft.IdentityModel.JsonWebTokens` and `Microsoft.IdentityModel.Tokens` are available via `<FrameworkReference Include="Microsoft.AspNetCore.App" />` which includes `Microsoft.AspNetCore.Authentication.JwtBearer`.

### What Epic 2 Must Add to WebApplicationFactory

When OpenIddict JWT Bearer is wired in Epic 2, the `WebApplicationFactory.ConfigureWebHost` will need to add:
```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = TestTokenFactory.TestSigningKey,  // ← the internal key
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero,
        };
    });
```
This is NOT part of Story 1.5. Note it here so Epic 2's dev agent knows where to add it.

### xmin Concurrency Token — Requires Real PostgreSQL

The entities use `UseXminAsConcurrencyToken()` which is PostgreSQL-specific. The existing in-memory tests cannot test concurrency conflicts involving xmin (they use `DbUpdateConcurrencyException` simulation instead). Future concurrency tests that need real xmin behavior must use `IntegrationTestBase` + Testcontainers, not InMemory.

### Previous Story Learnings

From Stories 1.3a/1.3b/1.4:
- `TreatWarningsAsErrors` applies everywhere — no nullable annotation sloppiness (use `!` or null guards, never ignore nullability)
- `UseEnvironment("Testing")` is the established guard to prevent `MigrateAsync` from running in Program.cs during tests
- `builder.Configure(app => {...})` in `WebApplicationFactory.ConfigureWebHost` REPLACES the middleware pipeline — do NOT use it in `OneIdWebApplicationFactory`; use `ConfigureServices` only to preserve the full production pipeline
- The existing `TenantIsolationServiceFactory` avoids `RemoveAll` to prevent "two providers" error — this does NOT apply to Npgsql-to-Npgsql replacement, only to Npgsql-to-InMemory
- xUnit `[Collection]` serializes all test classes in the collection — `"IntegrationTests"` collection will serialize all Testcontainers tests, which is correct (one container, sequential access)

### Project Structure Notes

All new files go in the integration test project:
```
tests/OneId.Server.IntegrationTests/
├── Helpers/                               ← exists, currently empty
│   ├── WebApplicationFactory.cs           ← NEW (contains both classes + collection definition)
│   ├── TestTokenFactory.cs                ← NEW
│   └── IntegrationTestBase.cs             ← NEW
├── TestTokenFactoryContractTests.cs       ← NEW (root level, not in Helpers/)
├── ConcurrencyConflictTests.cs            ← DO NOT TOUCH
├── DevSeederIntegrationTests.cs           ← DO NOT TOUCH
├── RegistrationOrderIntegrationTests.cs   ← DO NOT TOUCH
└── OneId.Server.IntegrationTests.csproj   ← add 2 PackageReferences
```

## Review Findings

Reviewed 2026-05-23 as part of Epic 1 Group ② code review.

### Patches applied during review

- [x] **P2 — `WebApplicationFactory.DisposeAsync` missing `base.DisposeAsync`** (`Helpers/WebApplicationFactory.cs:68-71`) — The override only disposed `_dbContainer` and never stopped the ASP.NET host. Added `await base.DisposeAsync()` after container dispose to properly release the host and its Npgsql connection pool.

### Deferred

- Respawner wipes `HasData()` seed rows — logged to `deferred-work.md`. Relevant when Epic 4a adds PermissionCatalog seeding.

### References

- [Source: epics.md#Story 1.5] — acceptance criteria, claim contract, contract test skip wording, Epic 3 activation requirement
- [Source: epics.md#Epic 1] — AR-7: Testcontainers + Respawn + TestTokenFactory full claim shape including seat_count placeholder
- [Source: architecture.md#Project Directory Structure] — `Helpers/WebApplicationFactory.cs`, `Helpers/TestTokenFactory.cs` locations; `TestTokenFactoryContractTests.cs` in root
- [Source: architecture.md#All Implementation Agents MUST] — UseSnakeCaseNamingConvention, UseXminAsConcurrencyToken, etc. apply to the real DB now used in tests
- [Source: Program.cs#line 55] — connection string key is `"DefaultConnection"`, read via `GetConnectionString`
- [Source: Program.cs#line 67] — migration guard: only for `Development` or `Docker` — `Testing` environment skips it
- [Source: RegistrationOrderIntegrationTests.cs] — established `UseEnvironment("Testing")` and `RemoveAll<DbContextOptions>` pattern
- [Source: DevSeederIntegrationTests.cs] — "two EF Core providers" warning (Npgsql→Npgsql replacement avoids this)
- [Source: implementation-artifacts/1-3b-...md] — xmin requires real PostgreSQL not InMemory
- [Source: implementation-artifacts/1-4-...md] — `[Collection("Serilog")]` xUnit serialization pattern

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- `PostgreSqlBuilder()` parameterless constructor is obsolete in Testcontainers 4.12.0 (CS0618 treated as error by `TreatWarningsAsErrors`). Fix: pass image to constructor directly: `new PostgreSqlBuilder("postgres:16-alpine").Build()`.
- `Table` type is in `Respawn.Graph` namespace, not `Respawn`. Required `using Respawn.Graph;` in addition to `using Respawn;`.

### Completion Notes List

- Added `Testcontainers.PostgreSql 4.12.0` and `Respawn 7.0.0` to `OneId.Server.IntegrationTests.csproj`. `InMemory` package preserved — existing in-memory tests untouched.
- `OneIdWebApplicationFactory` uses lazy host creation pattern: container starts in `InitializeAsync` before `Services` is accessed, ensuring `GetConnectionString()` is valid when `ConfigureWebHost` runs. `RemoveAll<DbContextOptions<AppDbContext>>` + `AddDbContext` with Npgsql performs clean Npgsql→Npgsql replacement without "two providers" conflict.
- Respawn 7.0.0 `Respawner.CreateAsync` and `ResetAsync` both take an open `NpgsqlConnection` (not a connection string). `Table` class lives in `Respawn.Graph` namespace. `__EFMigrationsHistory` excluded from resets so schema state is preserved.
- `TestTokenFactory.CreateToken` uses `JsonWebTokenHandler` + `SecurityTokenDescriptor.Claims` dictionary — correctly serializes `seat_count` as JSON integer and `roles` as JSON array. HMAC-SHA256 signed with pinned 32+ byte key. `TestSigningKey` exposed `internal` for Epic 2 JWT Bearer configuration.
- `IntegrationTestBase` abstract class carries `[Collection("IntegrationTests")]` (inherited by subclasses), calls `Factory.ResetDatabaseAsync()` in `InitializeAsync` before every test, leaves container lifecycle to collection fixture.
- `TestTokenFactoryContractTests` contains one skipped test visible in CI reports as a known gap. Skip wording matches Epic 3 activation requirement exactly.
- Build: 0 warnings, 0 errors. Integration tests: 5 passed (existing), 1 skipped (new contract test). Unit tests: 9 passed, 1 skipped (pre-existing Epic 2 stub). No regressions.

### File List

- tests/OneId.Server.IntegrationTests/OneId.Server.IntegrationTests.csproj (modified — added Testcontainers.PostgreSql 4.12.0, Respawn 7.0.0)
- tests/OneId.Server.IntegrationTests/Helpers/WebApplicationFactory.cs (new)
- tests/OneId.Server.IntegrationTests/Helpers/TestTokenFactory.cs (new)
- tests/OneId.Server.IntegrationTests/Helpers/IntegrationTestBase.cs (new)
- tests/OneId.Server.IntegrationTests/TestTokenFactoryContractTests.cs (new)
