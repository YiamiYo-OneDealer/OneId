# Story 1.3b: EF Core Global Query Filters, Entity Stubs, and Concurrency Tokens

Status: done

## Story

As a developer,
I want EF Core configured with tenant-scoped global query filters and optimistic concurrency tokens, with integration tests proving isolation,
so that cross-tenant data leakage is structurally impossible and concurrent write conflicts produce a correct HTTP 409 response.

## Acceptance Criteria

1. **Given** EF Core `AppDbContext` is configured **When** global query filters are registered **Then** every tenant-scoped entity (`User`) has a filter on `TenantId` referencing `ITenantContext.TenantId` **And** the filter is applied automatically — no query-site `Where(x => x.TenantId == ...)` is needed

2. **Given** entity stub types are defined for this epic's migration scope **When** `UseXminAsConcurrencyToken()` is applied **Then** it is configured on: `Tenant`, `User` — these are the only mutable entities in scope for Epic 1 **And** a comment in `AppDbContext.OnModelCreating` states: `// AR-14: UseXminAsConcurrencyToken applied to all mutable entities. Each epic that introduces a new mutable entity is responsible for adding it here.` **And** `Role`, `RoleSet`, `Group`, `Permission`, `DimensionValue` are NOT yet in scope

3. **Given** `DevSeederIntegrationTests.cs` runs **When** a `User` record is seeded under Tenant 1 **Then** a query executed with Tenant 2's `ITenantContext` active does NOT return that user (global filter applied) **And** the same user IS returned when Tenant 1's context is active

4. **Given** two concurrent requests attempt to update the same entity **When** the second request's `xmin` value does not match the current row `xmin` **Then** EF Core throws `DbUpdateConcurrencyException` **And** the global exception handler maps this to HTTP `409 Conflict` with body: `{ "type": "https://httpstatuses.io/409", "title": "Conflict", "detail": "The resource was modified by another request. Reload and retry." }` **And** an integration test simulates a stale-write and asserts the 409 response

## Tasks / Subtasks

- [x] Task 1: Create entity stubs — Tenant and User (AC: 1, 2, 3)
  - [x] Create `src/OneId.Server/Domain/Entities/Tenant.cs` — stub with Id, Name, CreatedAt, UpdatedAt, DeletedAt (no TenantId — Tenant IS the tenant)
  - [x] Create `src/OneId.Server/Domain/Entities/User.cs` — stub with Id, TenantId, Email, CreatedAt, UpdatedAt, DeletedAt
  - [x] Create `src/OneId.Server/Infrastructure/Persistence/Configurations/TenantConfiguration.cs` — `IEntityTypeConfiguration<Tenant>` (columns, PK, indexes, xmin concurrency token, soft-delete filter)
  - [x] Create `src/OneId.Server/Infrastructure/Persistence/Configurations/UserConfiguration.cs` — `IEntityTypeConfiguration<User>` (columns, PK, FK, unique email+tenant, xmin concurrency token — do NOT add global query filter here)
  - [x] Register `DbSet<Tenant>` and `DbSet<User>` on `AppDbContext`

- [x] Task 2: Update AppDbContext — inject ITenantContext and add global query filters (AC: 1, 2)
  - [x] Change `AppDbContext` constructor to accept `ITenantContext tenantContext` as a second parameter (primary constructor parameter)
  - [x] Call `builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)` in `OnModelCreating` (picks up all `IEntityTypeConfiguration<T>` automatically)
  - [x] Add global query filter for `User`: `builder.Entity<User>().HasQueryFilter(u => !u.DeletedAt.HasValue && u.TenantId == (tenantContext.IsInitialized ? tenantContext.TenantId : Guid.Empty))`
  - [x] Add the AR-14 comment for xmin tracking (exact text in Dev Notes)
  - [x] Verify `Tenant` has only a soft-delete filter (no tenant-id filter — Tenant is not tenant-scoped): configure this in `TenantConfiguration.cs`

- [x] Task 3: Create ExceptionHandlingMiddleware and wire it (AC: 4)
  - [x] Create `src/OneId.Server/Infrastructure/Middleware/ExceptionHandlingMiddleware.cs` — catches `DbUpdateConcurrencyException` → writes RFC 9457 Problem Details JSON with status 409 (exact implementation in Dev Notes)
  - [x] Wire `app.UseMiddleware<ExceptionHandlingMiddleware>()` in `Program.cs` BEFORE `app.UseAuthentication()` (must wrap the full pipeline to catch exceptions from any downstream middleware or controller)

- [x] Task 4: Create and apply EF Core migration (AC: 2)
  - [x] Run `dotnet ef migrations add AddTenantAndUserEntities --project src/OneId.Server --startup-project src/OneId.Server` to generate migration
  - [x] Verify migration contains `CREATE TABLE tenants` and `CREATE TABLE users` with snake_case columns
  - [x] Verify migration does NOT add an `xmin` column (it's a PostgreSQL system column — Npgsql uses it automatically, no DDL needed)
  - [x] Run `dotnet build` — zero warnings

- [x] Task 5: Write DevSeederIntegrationTests — cross-tenant isolation (AC: 3)
  - [x] Create `tests/OneId.Server.IntegrationTests/DevSeederIntegrationTests.cs`
  - [x] Use a new factory `TenantIsolationServiceFactory` (plain `ServiceCollection`) with in-memory DB name `"TestDb_TenantIsolation"` (separate from `"TestDb_RegistrationOrder"` used in Story 1.3a)
  - [x] `User_IsInvisible_ToOtherTenant` test: seed user under Tenant1, query with Tenant2 context → zero results
  - [x] `User_IsVisible_ToOwningTenant` test: seed user under Tenant1, query with Tenant1 context → one result with correct data
  - [x] Tests use `TenantContext.Initialize()` directly (internal — accessible via `InternalsVisibleTo`) to set tenant context without HTTP middleware

- [x] Task 6: Write ConcurrencyConflict integration test — 409 response (AC: 4)
  - [x] Create `tests/OneId.Server.IntegrationTests/ConcurrencyConflictTests.cs`
  - [x] Use a new factory `ConcurrencyTestFactory` with a test endpoint `GET /test/concurrency-conflict` that manually throws `new DbUpdateConcurrencyException()` with an empty entries list
  - [x] Factory must wire `ExceptionHandlingMiddleware` in its minimal pipeline
  - [x] `ConcurrencyConflict_Returns409ProblemDetails` test: GET `/test/concurrency-conflict` → 200 OK is NOT returned; response status is 409, Content-Type is `application/problem+json`, body deserializes to `{ type, title, status, detail }` with exact values (see Dev Notes for expected body)

- [x] Task 7: Validate all tests pass
  - [x] `dotnet build` — zero warnings across all projects
  - [x] `dotnet test tests/OneId.Server.UnitTests` — all existing tests still pass
  - [x] `dotnet test tests/OneId.Server.IntegrationTests` — 2 existing + 3 new tests pass (2 tenant isolation + 1 concurrency)
  - [x] Verify `docker compose up` still starts cleanly (no migration errors on the updated schema)

## Dev Notes

### Entity Stubs — Exact Implementations

**`src/OneId.Server/Domain/Entities/Tenant.cs`**

```csharp
namespace OneId.Server.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
```

Tenant is NOT tenant-scoped — it IS the tenant root. No `TenantId` foreign key. `Suspend()`/`Reinstate()` domain methods come in Epic 3.

**`src/OneId.Server/Domain/Entities/User.cs`**

```csharp
namespace OneId.Server.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Email { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
```

`Email` is the minimum discriminating field needed for the isolation test. `PasswordHash` and auth fields come in Epic 2. `Deactivate()`/`Activate()` domain methods come in Epic 3. `IsActive bool` is intentionally NOT included — status is derived from `DeletedAt` for now (full status model in Epic 3).

### Entity Configuration — Exact Implementations

**`src/OneId.Server/Infrastructure/Persistence/Configurations/TenantConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();
        builder.Property(t => t.DeletedAt);

        builder.HasIndex(t => t.Name).IsUnique();

        // AR-14: xmin-based optimistic concurrency for Tenant
        builder.UseXminAsConcurrencyToken();

        // Soft-delete filter — Tenant is NOT tenant-scoped, so no TenantId filter
        builder.HasQueryFilter(t => !t.DeletedAt.HasValue);
    }
}
```

**`src/OneId.Server/Infrastructure/Persistence/Configurations/UserConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.TenantId).IsRequired();
        builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt).IsRequired();
        builder.Property(u => u.DeletedAt);

        // Unique email per tenant (not globally — same email can exist across tenants)
        builder.HasIndex(u => new { u.TenantId, u.Email })
            .IsUnique()
            .HasFilter("deleted_at IS NULL");

        // AR-14: xmin-based optimistic concurrency for User
        builder.UseXminAsConcurrencyToken();

        // DO NOT add HasQueryFilter here — the global query filter references ITenantContext
        // which is only available in AppDbContext, not in IEntityTypeConfiguration<T>.
        // The tenant-isolation + soft-delete filter is added in AppDbContext.OnModelCreating.
    }
}
```

### AppDbContext — Updated Implementation

```csharp
using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
    : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // snake_case naming is applied via UseSnakeCaseNamingConvention() on DbContextOptionsBuilder in Program.cs
        // (EFCore.NamingConventions v6+ API — not a ModelBuilder extension)

        // Apply all IEntityTypeConfiguration<T> classes from this assembly
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // AR-5 STEP 2: Tenant-isolation global query filter for User
        // Uses IsInitialized guard — prevents guard exception during startup/DevSeeder/IgnoreQueryFilters paths.
        // InternalAdminContext and DevSeeder must call .IgnoreQueryFilters() for cross-tenant access.
        builder.Entity<User>().HasQueryFilter(u =>
            !u.DeletedAt.HasValue &&
            u.TenantId == (tenantContext.IsInitialized ? tenantContext.TenantId : Guid.Empty));

        // AR-14: UseXminAsConcurrencyToken applied to all mutable entities.
        // Each epic that introduces a new mutable entity is responsible for adding it here.
        // Story 1.3b adds: Tenant, User
        // Epic 3 adds: License, IdpConfiguration, AuditLog
        // Epic 4a adds: Role, RoleSet, Group, Permission, DimensionValue, UserDimensionAssignment
        // Note: xmin is a PostgreSQL system column. No migration column is needed. In-memory provider ignores it.
    }
}
```

**Why `IsInitialized` guard in the query filter:**
- At startup, `Database.MigrateAsync()` runs DDL — no entity queries, so filter is never evaluated.
- DevSeeder will access `AppDbContext` with an uninitialized `TenantContext` (no HTTP request scope). Without the guard, every DevSeeder query would throw `InvalidOperationException`. With the guard, DevSeeder queries with uninitialized context return `TenantId == Guid.Empty` results (zero rows) — the DevSeeder must explicitly call `.IgnoreQueryFilters()` to seed across tenants (Story 1.7b covers DevSeeder implementation).
- `InternalAdminContext` bypass path (Story 1.7a) similarly uses `.IgnoreQueryFilters()`.

### ExceptionHandlingMiddleware — Exact Implementation

**`src/OneId.Server/Infrastructure/Middleware/ExceptionHandlingMiddleware.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace OneId.Server.Infrastructure.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/problem+json";

            var problem = new
            {
                type = "https://httpstatuses.io/409",
                title = "Conflict",
                status = 409,
                detail = "The resource was modified by another request. Reload and retry."
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
        }
    }
}
```

**Wire in `Program.cs`** — add BEFORE `app.UseHttpsRedirection()` so it wraps the entire pipeline:

```csharp
// Must be first — wraps entire pipeline to catch exceptions from any layer
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
// AR-5: TenantContextMiddleware MUST precede OpenIddict and EF Core — see architecture.md
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
```

**Why first in pipeline:** `DbUpdateConcurrencyException` can be thrown by controllers, services called by controllers, or even by OpenIddict in later epics. Placing `ExceptionHandlingMiddleware` first ensures it catches from any depth.

### Migration — Key Verification Points

After running `dotnet ef migrations add AddTenantAndUserEntities`:

1. Migration `Up()` should contain `CREATE TABLE tenants` and `CREATE TABLE users`
2. Column names must be snake_case (`tenant_id`, `created_at`, `deleted_at`, etc.) — confirmed by `UseSnakeCaseNamingConvention()` already in `Program.cs`
3. No `xmin` column in the migration — `UseXminAsConcurrencyToken()` uses PostgreSQL's built-in system column (automatic, zero migration DDL)
4. `deleted_at` should be nullable (`timestamptz NULL`)

If the migration output shows an `xmin` column being created, something is wrong — stop and investigate. The Npgsql extension handles xmin as a system column, not a user-defined column.

**Important:** Delete the existing empty `InitialCreate` migration (or keep it and add on top). Since `InitialCreate` has empty `Up()`/`Down()` methods, it's safe to run `AddTenantAndUserEntities` on top of it.

### DevSeederIntegrationTests — Exact Implementation

**`tests/OneId.Server.IntegrationTests/DevSeederIntegrationTests.cs`**

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.IntegrationTests;

public class DevSeederIntegrationTests : IClassFixture<TenantIsolationTestFactory>
{
    private readonly TenantIsolationTestFactory _factory;

    public DevSeederIntegrationTests(TenantIsolationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task User_IsInvisible_ToOtherTenant()
    {
        // Arrange — seed Tenant1 + Tenant2 + User under Tenant1
        var (tenant1Id, tenant2Id) = await SeedIsolationData();

        // Act — query as Tenant2
        using var scope = _factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(tenant2Id);   // internal method — InternalsVisibleTo
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var users = await db.Users.ToListAsync();

        // Assert — Tenant2 cannot see Tenant1's user
        Assert.Empty(users);
    }

    [Fact]
    public async Task User_IsVisible_ToOwningTenant()
    {
        // Arrange — seed Tenant1 + Tenant2 + User under Tenant1
        var (tenant1Id, _) = await SeedIsolationData();

        // Act — query as Tenant1
        using var scope = _factory.Services.CreateScope();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(tenant1Id);   // internal method — InternalsVisibleTo
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var users = await db.Users.ToListAsync();

        // Assert — Tenant1 sees exactly its own user
        var user = Assert.Single(users);
        Assert.Equal("user@tenant1.com", user.Email);
    }

    private async Task<(Guid tenant1Id, Guid tenant2Id)> SeedIsolationData()
    {
        using var scope = _factory.Services.CreateScope();
        // DO NOT initialize TenantContext here — Tenant is not tenant-scoped, no filter needed
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant1 = new Tenant { Id = Guid.NewGuid(), Name = "Isolation-Tenant1", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var tenant2 = new Tenant { Id = Guid.NewGuid(), Name = "Isolation-Tenant2", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.Tenants.Add(tenant1);
        db.Tenants.Add(tenant2);
        await db.SaveChangesAsync();

        // Seed user under Tenant1 — initialize TenantContext so the filter allows the insert
        using var userScope = _factory.Services.CreateScope();
        var userTenantCtx = userScope.ServiceProvider.GetRequiredService<TenantContext>();
        userTenantCtx.Initialize(tenant1.Id);
        var userDb = userScope.ServiceProvider.GetRequiredService<AppDbContext>();
        userDb.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant1.Id,
            Email = "user@tenant1.com",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await userDb.SaveChangesAsync();

        return (tenant1.Id, tenant2.Id);
    }
}

public class TenantIsolationTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Fresh in-memory DB — isolated from other test factories
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opt =>
                opt.UseInMemoryDatabase("TestDb_TenantIsolation"));
        });
    }
}
```

**Important notes on seeding:**
- `Tenant` entity has only a soft-delete filter, not a tenant isolation filter. Inserting tenants does NOT require `TenantContext` initialization.
- `User` entity has the tenant isolation filter. The query filter uses `IsInitialized ? TenantId : Guid.Empty`. When inserting, EF Core `Add()` doesn't check query filters — only `SELECT` queries are filtered. So technically you could insert users without initializing the context. However, initializing for clarity is correct practice.
- In-memory `EnsureCreated()` is called automatically by EF Core in-memory provider when the DbContext is first used. No explicit call needed.

### ConcurrencyConflictTests — Exact Implementation

**`tests/OneId.Server.IntegrationTests/ConcurrencyConflictTests.cs`**

```csharp
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
            // ExceptionHandlingMiddleware FIRST — same as production pipeline order
            app.UseMiddleware<ExceptionHandlingMiddleware>();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/test/concurrency-conflict", () =>
                {
                    // Simulate a stale-write — entries list is empty for test purposes
                    throw new DbUpdateConcurrencyException("Simulated stale-write", []);
                });
            });
        });
    }
}
```

**Why this approach:** `UseXminAsConcurrencyToken()` is PostgreSQL-specific. In-memory provider ignores xmin. Rather than require a real PostgreSQL container (Story 1.5 scope), this test verifies that `ExceptionHandlingMiddleware` correctly maps `DbUpdateConcurrencyException` to 409 Problem Details — the behavioral contract that matters. Full end-to-end `xmin` mismatch behavior is validated in Story 1.5's Testcontainers infrastructure.

### WARNING: AppDbContext Constructor Change Affects Existing Tests

The `AppDbContext` constructor now requires `ITenantContext`. The existing `OneIdTestFactory` in `RegistrationOrderIntegrationTests.cs` uses `services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase(...))`. This works because DI resolves `ITenantContext` from the registered services — `TenantContext` is registered as scoped in `Program.cs` and the test factory's services inherit that registration. **No changes to `RegistrationOrderIntegrationTests.cs` are needed.**

Verify by running `dotnet test tests/OneId.Server.IntegrationTests` — all 5 tests (2 original + 3 new) should pass.

### WARNING: Global Query Filter Interaction with Test Scopes

Each `CreateScope()` call creates a fresh `TenantContext` instance (scoped lifetime). The `AppDbContext` instance within that scope captures that specific `TenantContext` reference. Always:
1. Resolve `TenantContext` from the scope BEFORE resolving `AppDbContext`
2. Call `tenantCtx.Initialize(id)` BEFORE any query through `db`
3. Never reuse a scope across different tenant contexts — create a new scope per tenant

### File List for This Story

New files to create:
- `src/OneId.Server/Domain/Entities/Tenant.cs` (new)
- `src/OneId.Server/Domain/Entities/User.cs` (new)
- `src/OneId.Server/Infrastructure/Persistence/Configurations/TenantConfiguration.cs` (new)
- `src/OneId.Server/Infrastructure/Persistence/Configurations/UserConfiguration.cs` (new)
- `src/OneId.Server/Infrastructure/Middleware/ExceptionHandlingMiddleware.cs` (new)
- `src/OneId.Server/Infrastructure/Persistence/Migrations/{timestamp}_AddTenantAndUserEntities.cs` (generated)
- `tests/OneId.Server.IntegrationTests/DevSeederIntegrationTests.cs` (new)
- `tests/OneId.Server.IntegrationTests/ConcurrencyConflictTests.cs` (new)

Files to update:
- `src/OneId.Server/Infrastructure/Persistence/AppDbContext.cs` (add ITenantContext param, DbSets, ApplyConfigurationsFromAssembly, global filter)
- `src/OneId.Server/Program.cs` (wire ExceptionHandlingMiddleware first in pipeline)

### Previous Story Learnings (From Story 1.3a)

- `TreatWarningsAsErrors` applies to ALL projects in the tree — no nullable annotation sloppiness in test code
- `TestAuthHandler` / `TestTenantId` pattern established in `RegistrationOrderIntegrationTests.cs` — reuse the same pattern for new factories
- `WebApplicationFactory.ConfigureWebHost.Configure(app => {...})` REPLACES Program.cs middleware pipeline entirely — only the service registrations from Program.cs are preserved
- Integration test factory's `builder.Configure` requires `using Microsoft.AspNetCore.Builder;` for extension methods
- EF InMemory reference: `Microsoft.EntityFrameworkCore.InMemory` is already in `OneId.Server.IntegrationTests.csproj` — no new package needed
- `dotnet sln` is not needed for new test files — the projects are already in the solution

### References

- [Source: epics.md#Story 1.3b] — acceptance criteria, entity list, AR-14 comment text
- [Source: architecture.md#Process Patterns] — soft-delete pattern, tenant isolation via EF Core global query filters, `IsInitialized` guard implication
- [Source: architecture.md#All Implementation Agents MUST] — Rule 2: IEntityTypeConfiguration per entity; Rule 3: soft-delete on all tenant-scoped entities; Rule 4: UseXminAsConcurrencyToken
- [Source: architecture.md#Complete Project Directory Structure] — `Domain/Entities/`, `Infrastructure/Persistence/Configurations/`, middleware location
- [Source: architecture.md#Core Architectural Decisions] — AR-5 (EF Core filter order), AR-14 (xmin)
- [Source: architecture.md#Data Architecture] — PostgreSQL-first, `UseXminAsConcurrencyToken()` is a named migration risk for HANA Cloud
- [Source: implementation-artifacts/1-3a-tenant-context-middleware-and-registration-order-enforcement.md#Completion Notes] — integration test factory requires `FrameworkReference` + `using Microsoft.AspNetCore.Builder`
- [Source: implementation-artifacts/1-3a-tenant-context-middleware-and-registration-order-enforcement.md#Dev Notes/TenantId Type] — TenantId is Guid, confirmed by story 1.3a

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- `UseXminAsConcurrencyToken()` removed in Npgsql EF Core 10.x — replaced with manual `builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken()` in both entity configurations.
- Migration C# code includes `xmin` as `table.Column<uint>(type: "xid", rowVersion: true)` but Npgsql's `SystemColumnNames` suppresses it from the actual DDL — no `xmin` column created, correct behavior confirmed.
- xUnit 2.x parallel class execution caused two failures: (a) Serilog static `ReloadableLogger` race condition across concurrent `WebApplicationFactory<Program>` startups; (b) `RemoveAll<DbContextOptions<T>>()` does NOT remove internal `IDbContextOptionsConfiguration<T>` registrations, causing "two EF Core providers" error. Fixed by: (a) `[Collection("Integration")]` on all three test classes; (b) `TenantIsolationServiceFactory` uses plain `ServiceCollection` instead of `WebApplicationFactory<Program>`.

### Completion Notes List

- All 4 ACs satisfied: (1) `User` global query filter via `ITenantContext.IsInitialized` guard; (2) `xmin` concurrency tokens on `Tenant` + `User` using manual property builder; (3) cross-tenant isolation proven by 2 integration tests; (4) `ExceptionHandlingMiddleware` maps `DbUpdateConcurrencyException` → 409 Problem Details, proven by integration test.
- `TenantIsolationServiceFactory` uses plain `ServiceCollection` (no `WebApplicationFactory`) to avoid Serilog and dual-provider issues — documented pattern for future test authors.
- All 5 integration tests + 6 unit tests pass; 1 unit test remains skipped (as per Story 1.3a, requires real filesystem).

### File List

New:
- `src/OneId.Server/Domain/Entities/Tenant.cs`
- `src/OneId.Server/Domain/Entities/User.cs`
- `src/OneId.Server/Infrastructure/Persistence/Configurations/TenantConfiguration.cs`
- `src/OneId.Server/Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- `src/OneId.Server/Infrastructure/Middleware/ExceptionHandlingMiddleware.cs`
- `src/OneId.Server/Infrastructure/Persistence/Migrations/20260522134750_AddTenantAndUserEntities.cs`
- `src/OneId.Server/Infrastructure/Persistence/Migrations/20260522134750_AddTenantAndUserEntities.Designer.cs`
- `tests/OneId.Server.IntegrationTests/DevSeederIntegrationTests.cs`
- `tests/OneId.Server.IntegrationTests/ConcurrencyConflictTests.cs`

Modified:
- `src/OneId.Server/Infrastructure/Persistence/AppDbContext.cs`
- `src/OneId.Server/Program.cs`
- `tests/OneId.Server.IntegrationTests/RegistrationOrderIntegrationTests.cs`
- `src/OneId.Server/Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`

## Review Findings

*Source: Epic 1 code review, 2026-05-23*

- [x] [Review][Patch] Added `catch (Exception ex) when not OperationCanceledException` fallback with `LogError` + 500 Problem Details; extracted `WriteProblemAsync` helper [ExceptionHandlingMiddleware.cs]
- [x] [Review][Patch] Added `context.Response.HasStarted` guard in both catch blocks [ExceptionHandlingMiddleware.cs:InvokeAsync]
- [x] [Review][Defer] Guid.Empty row data-leak vector: a row with `tenant_id = '00000000...'` would be visible to all uninitialized contexts — deferred, requires actively bad data; `TenantContext.Initialize` rejects Guid.Empty on normal paths [AppDbContext.cs]
- [x] [Review][Defer] No FK constraint `users.tenant_id → tenants.id` — deferred, may be intentional design choice for multi-tenant flexibility; revisit in Epic 3 when Tenant lifecycle is built [UserConfiguration.cs]
- [x] [Review][Defer] Cross-tenant isolation integration tests (AC3) not in Group 1 diff — deferred, verify present in Group 2 test file review [DevSeederIntegrationTests.cs]

## Change Log

- 2026-05-22: Story 1.3b implemented — entity stubs, global query filters, xmin concurrency tokens, ExceptionHandlingMiddleware, migration, and integration tests. `UseXminAsConcurrencyToken()` replaced with manual property builder (removed in Npgsql v10). All tests pass.
