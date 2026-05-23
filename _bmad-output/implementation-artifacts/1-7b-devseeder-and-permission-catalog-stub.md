# Story 1.7b: DevSeeder and Permission Catalog Stub

Status: done

## Story

As a developer,
I want seed data and the Permission catalog skeleton active from day one,
so that the development environment is immediately usable and the Epic 4a wiring point is visible in CI.

## Acceptance Criteria

1. **Given** `OneId.Server` starts in the `Development` environment
   **When** the DevSeeder executes (after EF Core global query filters are confirmed active per Story 1.3b)
   **Then** the following seed data is present:
   - Dev tenant (`name: "Dev Tenant"`) with a stable well-known ID
   - Admin user (`email: admin@oneid.dev`, Argon2id-hashed password for `Admin123!`)
   - OpenIddict test client (`client_id: oneid-dev-client`, `redirect_uri: http://localhost:3000/callback`) — **NOTE: see Dev Notes; client seeding is structurally deferred to Story 2.1 when OpenIddict is registered**
   **And** DevSeeder is idempotent — running twice does not create duplicate records (uses find-or-create, not blind insert)
   **And** Note: the pre-provisioned federated test user is NOT seeded here — deferred to Epic 6 Story 6.1

2. **Given** `PermissionCatalog.cs` and `Permissions` static class exist
   **When** the solution compiles
   **Then** `Permissions` class compiles with zero constants (populated in Epic 4a)
   **And** `PermissionCatalogSyncTests.cs` contains: `[Fact(Skip = "Wired in Epic 4a — remove Skip in Story 4a.1")]` with body `Assert.Fail("PermissionCatalog sync not yet enforced — wire in Epic 4a")`
   **And** this skip is visible as a known gap in CI test reports (counts toward AR-15 deferred-skip cap — this is the 3rd and final permitted skip)

## Tasks / Subtasks

- [x] Task 1: Add `PasswordHash` to User entity + migration (AC: 1)
  - [x] Add `public string? PasswordHash { get; set; }` to `src/OneId.Server/Domain/Entities/User.cs`
  - [x] Update `src/OneId.Server/Infrastructure/Persistence/Configurations/UserConfiguration.cs` — add `builder.Property(u => u.PasswordHash).HasMaxLength(500);`
  - [x] Generate migration: `dotnet ef migrations add AddPasswordHashToUser --project src/OneId.Server/OneId.Server.csproj`
  - [x] Verify migration compiles and `dotnet build OneId.slnx` passes with zero warnings

- [x] Task 2: Create DevSeeder (AC: 1)
  - [x] Delete `src/OneId.Server/Infrastructure/Persistence/Seeds/.gitkeep`
  - [x] Create `src/OneId.Server/Infrastructure/Persistence/Seeds/DevSeeder.cs` — seeds dev tenant + admin user; includes TODO stub for OpenIddict test client (see Dev Notes for exact structure)
  - [x] Wire DevSeeder call in `Program.cs` inside the `IsDevelopment || IsEnvironment("Docker")` block, after `db.Database.MigrateAsync()`

- [x] Task 3: Create PermissionCatalog.cs (AC: 2)
  - [x] Create `src/OneId.Server/Infrastructure/Persistence/Seeds/PermissionCatalog.cs` — empty class with no seed rows (populated in Story 4a.1)

- [x] Task 4: Create PermissionCatalogSyncTests.cs (AC: 2)
  - [x] Create `tests/OneId.Server.IntegrationTests/PermissionCatalogSyncTests.cs` — single `[Fact(Skip = "Wired in Epic 4a — remove Skip in Story 4a.1")]`
  - [x] Verify `dotnet test OneId.slnx` passes; the new skip appears in test output but does not fail CI

- [x] Task 5: Final build verification (AC: 1, 2)
  - [x] `dotnet build OneId.slnx` — zero warnings, zero errors
  - [x] `dotnet test OneId.slnx` — all tests pass; 3 deferred skips visible (DevSigningKeyStabilityTest + TestTokenFactoryContractTests + PermissionCatalogSyncTests)

## Dev Notes

### CRITICAL: OpenIddict Client Seeding is Deferred to Story 2.1

`IOpenIddictApplicationManager` is the correct way to seed an OpenIddict test client, but it requires `builder.Services.AddOpenIddict()` to have been called first. Story 2.1 registers OpenIddict. Story 1.7b **cannot** seed the OpenIddict client.

The correct pattern:
- Story 1.7b: Create `DevSeeder.cs` with Tenant + User seeding fully implemented. Include a clearly commented stub method `SeedOpenIddictClientAsync()` with a `// TODO Story 2.1: register OpenIddict first, then inject IOpenIddictApplicationManager here` comment.
- Story 2.1: After registering OpenIddict, register `DevSeeder` in DI with the `IOpenIddictApplicationManager` dependency and complete the OpenIddict client seeding.

Do NOT attempt to seed the OpenIddict client directly via raw SQL or by inserting into `openiddict_applications` — use `IOpenIddictApplicationManager` when available.

### CRITICAL: Solution File Is `.slnx` Not `.sln`

Use `OneId.slnx` for all dotnet commands. `OneId.sln` does not exist.

### CRITICAL: `TreatWarningsAsErrors=true` Is Global

`Directory.Build.props` sets `TreatWarningsAsErrors=true`. Every new file must compile with zero warnings: no unused variables, no nullable warnings (enable null-safe patterns), no CS0169.

### CRITICAL: AR-15 Deferred-Skip Cap

Current deferred-skip count is 2 (DevSigningKeyStabilityTest + TestTokenFactoryContractTests). Story 1.7b adds `PermissionCatalogSyncTests` as the **3rd and final** skip — this hits the cap exactly. Do NOT add any other skipped tests in this story.

### User Entity — Add PasswordHash Column

The current `User` entity has no `PasswordHash`. This migration must add it:

```csharp
// In User.cs — add after Email:
public string? PasswordHash { get; set; }
```

In `UserConfiguration.cs` add:
```csharp
builder.Property(u => u.PasswordHash).HasMaxLength(500);
```

This is nullable because Federated users (Epic 6) will not have a password. The max 500 is safe headroom for Argon2id hashes (they are typically ~96 chars in Base64 format with ASP.NET Core Identity's V3 format, but allow extra).

Generate the migration:
```bash
dotnet ef migrations add AddPasswordHashToUser --project src/OneId.Server/OneId.Server.csproj
```

### DevSeeder — Exact Structure

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Seeds;

internal static class DevSeeder
{
    // Stable well-known IDs for dev environment — idempotency and TestTokenFactory alignment.
    public static readonly Guid DevTenantId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid AdminUserId  = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    // AR-6: DevSeeder runs only after global query filters are active.
    // Called from Program.cs inside the IsDevelopment block, after db.Database.MigrateAsync().
    // AppDbContext.IgnoreQueryFilters() is required for cross-tenant seed ops (Tenant is not tenant-scoped,
    // but User is). The IsInitialized guard in the global filter handles uninitialized context gracefully —
    // seed operations that don't activate a TenantContext will see Guid.Empty tenant filter, so
    // we bypass filters for direct User seeding.
    public static async Task SeedAsync(AppDbContext db)
    {
        await SeedDevTenantAsync(db);
        await SeedAdminUserAsync(db);
        // TODO Story 2.1: after AddOpenIddict() is registered, inject IOpenIddictApplicationManager
        // here and call SeedOpenIddictClientAsync(manager).
    }

    private static async Task SeedDevTenantAsync(AppDbContext db)
    {
        var exists = await db.Tenants.AnyAsync(t => t.Id == DevTenantId);
        if (exists) return;

        db.Tenants.Add(new Tenant
        {
            Id = DevTenantId,
            Name = "Dev Tenant",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedAdminUserAsync(AppDbContext db)
    {
        // IgnoreQueryFilters: User has a TenantId global filter. DevSeeder does not activate
        // ITenantContext — IgnoreQueryFilters avoids the Guid.Empty filter trapping this lookup.
        var exists = await db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Id == AdminUserId);
        if (exists) return;

        var hasher = new PasswordHasher<User>();
        var user = new User
        {
            Id = AdminUserId,
            TenantId = DevTenantId,
            Email = "admin@oneid.dev",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        user.PasswordHash = hasher.HashPassword(user, "Admin123!");

        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    // TODO Story 2.1: Complete this method — requires IOpenIddictApplicationManager injected from DI.
    // Registration: client_id = "oneid-dev-client", redirect_uri = "http://localhost:3000/callback"
    // Use OpenIddictApplicationDescriptor with ClientType = Public (SPA PKCE flow).
    // private static async Task SeedOpenIddictClientAsync(IOpenIddictApplicationManager manager) { ... }
}
```

### Program.cs Wiring

Add the DevSeeder call inside the existing `IsDevelopment || IsEnvironment("Docker")` block, **after** `db.Database.MigrateAsync()`:

```csharp
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DevSeeder.SeedAsync(db);  // ← ADD THIS LINE
}
```

Add the required using at the top of `Program.cs`:
```csharp
using OneId.Server.Infrastructure.Persistence.Seeds;
```

### PermissionCatalog.cs — Exact Structure

Create `src/OneId.Server/Infrastructure/Persistence/Seeds/PermissionCatalog.cs`:

```csharp
// AR-9: Version-controlled source of truth for Permission definitions.
// Populated in Story 4a.1 when the Permission catalog is built.
// PermissionCatalogSyncTests.cs enforces that every entry here has a corresponding
// od.* constant in Application/Common/Permissions.cs.
namespace OneId.Server.Infrastructure.Persistence.Seeds;

internal static class PermissionCatalog
{
    // TODO Story 4a.1: Add all od.* permission seed records here.
    // Each entry maps a dot-notation permission ID (e.g., "crm.invoice.create") to a
    // display name and description, and is applied via HasData() in the Permission entity configuration.
}
```

### Permissions.cs — Already Exists, No Changes

`src/OneId.Server/Application/Common/Permissions.cs` already exists as a stub with zero constants. **Do not modify it.** It is correct as-is for Story 1.7b.

### PermissionCatalogSyncTests.cs — Exact Structure

```csharp
namespace OneId.Server.IntegrationTests;

public class PermissionCatalogSyncTests
{
    [Fact(Skip = "Wired in Epic 4a — remove Skip in Story 4a.1")]
    public void PermissionCatalog_SyncedWith_PermissionsStaticClass()
    {
        Assert.Fail("PermissionCatalog sync not yet enforced — wire in Epic 4a");
    }
}
```

File location: `tests/OneId.Server.IntegrationTests/PermissionCatalogSyncTests.cs`

No `[Collection("...")]` attribute needed — this test class does not use `AppDbContext` or the test container.

### PasswordHasher Usage — No Full Identity Required

`Microsoft.AspNetCore.Identity.PasswordHasher<T>` can be instantiated directly without registering full ASP.NET Core Identity:

```csharp
var hasher = new PasswordHasher<User>();
user.PasswordHash = hasher.HashPassword(user, "Admin123!");
```

This produces a V3 Argon2id hash (ASP.NET Core Identity default for .NET 6+). Story 2.2 (password authentication) configures `AddIdentity<User, IdentityRole>()` and uses the same hasher to verify passwords — no format mismatch.

Add `using Microsoft.AspNetCore.Identity;` at the top of `DevSeeder.cs`.

### DevSeeder Integration Test

The existing `DevSeederIntegrationTests.cs` tests tenant isolation (not the DevSeeder itself). Do NOT add a new test class with that name — it already exists.

If a DevSeeder-specific integration test is needed (verifying dev tenant + admin user appear after seed), it would go in a separate file such as `DevSeedVerificationTests.cs`. This is optional for this story — the AC does not explicitly require a new test. The DevSeeder will be validated by the integration test infrastructure in Story 2.2 when authentication is tested against the seeded admin user.

### Namespace and Project Structure Alignment

```
src/OneId.Server/
└── Infrastructure/
    └── Persistence/
        └── Seeds/
            ├── DevSeeder.cs           ← NEW (delete .gitkeep first)
            └── PermissionCatalog.cs   ← NEW

tests/OneId.Server.IntegrationTests/
└── PermissionCatalogSyncTests.cs      ← NEW
```

Namespaces:
- `DevSeeder.cs` → `OneId.Server.Infrastructure.Persistence.Seeds`
- `PermissionCatalog.cs` → `OneId.Server.Infrastructure.Persistence.Seeds`
- `PermissionCatalogSyncTests.cs` → `OneId.Server.IntegrationTests`

### Previous Story Learnings (Stories 1.1–1.7a)

- **Solution file**: `OneId.slnx` — use in all dotnet commands.
- **xmin concurrency token**: `UseXminAsConcurrencyToken()` removed from Npgsql v10. Use explicit `.HasColumnType("xid")` as done in `TenantConfiguration.cs` / `UserConfiguration.cs`. New entities or columns in this story do NOT need xmin.
- **Test namespaces**: Unit tests → `OneId.Server.Tests.*`; Integration tests → `OneId.Server.IntegrationTests.*`.
- **ImplicitUsings enabled**: `Microsoft.AspNetCore.Identity` must be explicitly imported in `DevSeeder.cs`.
- **IgnoreQueryFilters() for seeding**: The global query filter on `Users` uses `tenantContext.IsInitialized ? tenantContext.TenantId : Guid.Empty`. Without activating `ITenantContext`, any User lookup will use `Guid.Empty` as the filter — meaning no users are found. Use `.IgnoreQueryFilters()` on User queries in the DevSeeder to bypass this. Tenant entity has no query filter so no bypass needed.
- **InternalsVisibleTo**: Both `OneId.Server.UnitTests` and `OneId.Server.IntegrationTests` already have InternalsVisibleTo. `DevSeeder` marked `internal static` is fine.
- **AR-15 cap**: Current count = 2. This story adds 1 more = 3 (at cap). No further skips until at least one existing skip is closed by its owning story.

### References

- [Source: epics.md#Story 1.7b] — acceptance criteria, seed data spec, PermissionCatalogSyncTests skip text
- [Source: epics.md#Epic 1 AR-6] — DevSeeder runs after global query filters; must respect isolation
- [Source: epics.md#Epic 1 AR-9] — PermissionCatalog.cs as version-controlled truth; Permissions static class
- [Source: epics.md#Epic 1 AR-15] — Deferred-skip governance; 3rd skip permitted, cap reached
- [Source: architecture.md#Complete Project Directory Structure] — Seeds folder location, DevSeeder path
- [Source: architecture.md#Process Patterns] — Tenant isolation / IgnoreQueryFilters requirement
- [Source: architecture.md#All Implementation Agents MUST] — Rule 7: InternalAdminContext only in Application/Internal/
- [Source: src/OneId.Server/Infrastructure/Persistence/AppDbContext.cs] — global query filter with IsInitialized guard; IgnoreQueryFilters() usage
- [Source: src/OneId.Server/Infrastructure/Persistence/Migrations/20260522134750_AddTenantAndUserEntities.cs] — User table schema (no PasswordHash column yet — must be added)
- [Source: src/OneId.Server/Application/Common/Permissions.cs] — already exists as stub with zero constants, no changes needed
- [Source: tests/OneId.Server.IntegrationTests/TestTokenFactoryContractTests.cs] — deferred-skip pattern to replicate exactly
- [Source: 1-7a story Dev Notes] — xmin, TreatWarningsAsErrors, namespace conventions, IgnoreQueryFilters

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- EF Core migration tooling emits `HostAbortedException` during `dotnet ef migrations add` — this is expected design-time behaviour, not an error. Migration was generated correctly.

### Completion Notes List

- Added `PasswordHash` (nullable string, max 500) to `User` entity and `UserConfiguration`. Generated migration `20260522222043_AddPasswordHashToUser` — adds `password_hash` nullable varchar(500) column to `users` table.
- Created `DevSeeder.cs` with two seeding methods: `SeedDevTenantAsync` (find-or-create by stable GUID) and `SeedAdminUserAsync` (find-or-create by stable GUID, `IgnoreQueryFilters()` to bypass TenantId global filter, `PasswordHasher<User>` for Argon2id hash of `Admin123!`). Includes clearly commented TODO stub for OpenIddict client seeding deferred to Story 2.1.
- Wired `DevSeeder.SeedAsync(db)` in `Program.cs` immediately after `db.Database.MigrateAsync()` inside the `IsDevelopment || IsEnvironment("Docker")` block.
- Created `PermissionCatalog.cs` skeleton in `Seeds/` — empty static class with TODO for Story 4a.1.
- Created `PermissionCatalogSyncTests.cs` with the 3rd deferred skip. AR-15 cap is now exactly at 3 (DevSigningKeyStabilityTest + TestTokenFactoryContractTests + PermissionCatalogSyncTests). No additional skips may be added until an existing skip is closed.
- All tests: 17 passing, 3 skipped (as expected), 0 failed.

### File List

- src/OneId.Server/Domain/Entities/User.cs (modified — added PasswordHash)
- src/OneId.Server/Infrastructure/Persistence/Configurations/UserConfiguration.cs (modified — added PasswordHash configuration)
- src/OneId.Server/Infrastructure/Persistence/Migrations/20260522222043_AddPasswordHashToUser.cs (new)
- src/OneId.Server/Infrastructure/Persistence/Migrations/20260522222043_AddPasswordHashToUser.Designer.cs (new)
- src/OneId.Server/Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs (modified — auto-updated by EF)
- src/OneId.Server/Infrastructure/Persistence/Seeds/DevSeeder.cs (new)
- src/OneId.Server/Infrastructure/Persistence/Seeds/PermissionCatalog.cs (new)
- src/OneId.Server/Infrastructure/Persistence/Seeds/.gitkeep (deleted)
- src/OneId.Server/Program.cs (modified — added Seeds using + DevSeeder.SeedAsync call)
- tests/OneId.Server.IntegrationTests/PermissionCatalogSyncTests.cs (new)

## Review Findings

*Source: Epic 1 code review, 2026-05-23*

- [x] [Review][Patch] `AppDbContext.Users` property now emits `LogWarning` when `ITenantContext.IsInitialized == false`, making the Guid.Empty fallback visible in logs [AppDbContext.cs:Users]
- [x] [Review][Patch] `DevSeeder.SeedDevTenantAsync` now calls `.IgnoreQueryFilters()` on tenant lookup to bypass soft-delete filter [DevSeeder.cs:SeedDevTenantAsync]
- [x] [Review][Defer] DevSeeder hard-codes `"Admin123!"` in source control — deferred, dev-only seeder by spec design; protected by `IsDevelopment()` guard; well-known pattern for dev seeds [DevSeeder.cs]
- [x] [Review][Defer] `User.PasswordHash` nullable creates semantic ambiguity between "no password set" vs "federated user" vs "pre-migration user" — deferred, intentional per spec (Epic 6 federated users); auth logic in Epic 2 will define the contract [User.cs]
- [x] [Review][Defer] `DevSeeder` has no wrapping transaction — deferred, stable well-known IDs make partial-seed re-runs idempotent in practice [DevSeeder.cs]
- [x] [Review][Defer] `SeedAdminUserAsync` checks by `AdminUserId` but a manually-inserted user with same email + different ID would cause unique-constraint violation at startup — deferred, dev-only, requires manual DB interference [DevSeeder.cs]
