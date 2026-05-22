# Story 1.7a: ArchUnit Boundary Enforcement and Cache Abstraction

Status: review

## Story

As a developer,
I want namespace boundary rules and a cache abstraction enforced from day one,
so that architectural constraints are machine-checked and no service bypasses the caching contract.

## Acceptance Criteria

1. **Given** `InternalBoundaryTests.cs` runs (ArchUnit)
   **When** any type in the `Application/Internal/` namespace is referenced
   **Then** the ArchUnit rule asserts it is NOT injected or instantiated by any type outside `Application/Internal/`
   **And** the rule is expressed as a fluent API assertion (not a convention test or manual scan)
   **And** a deliberate violation test case — injecting an `Application/Internal/` type (`InternalAdminContext`) into a controller in `Application/Tenant/` — causes the assertion to fail, proving the rule has teeth

2. **Given** any application service needs to cache a value
   **When** it accesses the cache
   **Then** it uses `ICacheService` only — no direct `IMemoryCache` injection is permitted outside `Infrastructure/Caching/`
   **And** this is enforced by an ArchUnit rule: "types outside `Infrastructure/Caching/` MUST NOT depend on `IMemoryCache` directly"
   **And** `ICacheService` is implemented by `MemoryCacheService` using cache key format: `{entity}:{userId}:{tenantId}` (e.g., `user:abc123:tenant456`)

## Tasks / Subtasks

- [x] Task 1: Create `InternalAdminContext.cs` marker class (AC: 1)
  - [x] Create `src/OneId.Server/Application/Common/InternalAdminContext.cs` — sealed marker class with no properties, namespace `OneId.Server.Application.Common`

- [x] Task 2: Complete `ICacheService` interface and implement `MemoryCacheService` (AC: 2)
  - [x] Update `src/OneId.Server/Application/Common/ICacheService.cs` — add `Get<T>`, `Set<T>`, and `Remove` methods with XML docs describing the key format
  - [x] Create `src/OneId.Server/Infrastructure/Caching/MemoryCacheService.cs` — implements `ICacheService` wrapping `IMemoryCache`
  - [x] Register `AddMemoryCache()` and `ICacheService`→`MemoryCacheService` in `Program.cs`

- [x] Task 3: Add ArchUnit package and write boundary tests (AC: 1, 2)
  - [x] Add `NetArchTest.Rules` NuGet package to `tests/OneId.Server.IntegrationTests/OneId.Server.IntegrationTests.csproj`
  - [x] Create `tests/OneId.Server.IntegrationTests/Architecture/InternalBoundaryTests.cs`
  - [x] Write the `InternalAdminContext_MustOnlyBeUsedInApplicationInternal` test (positive rule — production assembly passes)
  - [x] Write the `ViolatingController_Using_InternalAdminContext_CausesRuleFailure` test (violation proof — test assembly fails)
  - [x] Write the `IMemoryCache_MustOnlyBeReferencedInInfrastructureCaching` test (cache boundary rule — production assembly passes)

- [x] Task 4: Verify build passes (AC: 1, 2)
  - [x] `dotnet build OneId.slnx` — zero warnings, zero errors
  - [x] `dotnet test OneId.slnx` — all tests pass including new ArchUnit tests

## Dev Notes

### CRITICAL: Solution File Is `.slnx` Not `.sln`

Use `OneId.slnx` for all dotnet commands. `OneId.sln` does not exist.

### CRITICAL: `TreatWarningsAsErrors=true` Is Global

`Directory.Build.props` sets `TreatWarningsAsErrors=true` for all projects. Every new class file must compile without any warnings: no unused variables, no nullable warnings, no `CS0169` (unused fields). The deliberate violation class in the test file must reference its `InternalAdminContext` field to avoid the unused-field warning.

### File Structure — What Exists vs. What to Create

**Exists (DO NOT recreate):**
- `src/OneId.Server/Application/Common/ICacheService.cs` — stub exists at this path. Read it before editing. It currently has a `TODO` comment — replace the entire file content with the completed interface.
- `src/OneId.Server/Infrastructure/Caching/.gitkeep` — placeholder. Delete the `.gitkeep` and create `MemoryCacheService.cs` in that folder.
- `src/OneId.Server/Application/Internal/.gitkeep` — placeholder only. Do NOT create service files here — that is Epic 3's job.
- `src/OneId.Server/Application/Common/` — existing namespace. `InternalAdminContext.cs` goes here.

**Create new:**
```
src/OneId.Server/Application/Common/InternalAdminContext.cs   ← NEW
src/OneId.Server/Infrastructure/Caching/MemoryCacheService.cs ← NEW (delete .gitkeep first)
tests/OneId.Server.IntegrationTests/Architecture/             ← NEW directory
tests/OneId.Server.IntegrationTests/Architecture/InternalBoundaryTests.cs ← NEW
```

### ICacheService — Exact Interface Shape

Replace the stub in `src/OneId.Server/Application/Common/ICacheService.cs`:

```csharp
// AR-10: All cache access must go through this interface.
// Direct IMemoryCache injection is forbidden outside Infrastructure/Caching/ — enforced by InternalBoundaryTests.cs.
// Cache key format: {entity}:{userId}:{tenantId} (e.g., "user:abc123:tenant456")
namespace OneId.Server.Application.Common;

public interface ICacheService
{
    T? Get<T>(string key);
    void Set<T>(string key, T value, TimeSpan? expiry = null);
    void Remove(string key);
}
```

No async overloads — `IMemoryCache` is synchronous and in-process. If Redis is swapped in (first staging deploy), the Redis client synchronous paths are acceptable for the POC.

### MemoryCacheService — Exact Implementation

Create `src/OneId.Server/Infrastructure/Caching/MemoryCacheService.cs`:

```csharp
using Microsoft.Extensions.Caching.Memory;
using OneId.Server.Application.Common;

namespace OneId.Server.Infrastructure.Caching;

internal sealed class MemoryCacheService(IMemoryCache cache) : ICacheService
{
    public T? Get<T>(string key)
    {
        cache.TryGetValue(key, out T? value);
        return value;
    }

    public void Set<T>(string key, T value, TimeSpan? expiry = null)
    {
        var options = expiry.HasValue
            ? new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry }
            : new MemoryCacheEntryOptions();
        cache.Set(key, value, options);
    }

    public void Remove(string key) => cache.Remove(key);
}
```

Mark as `internal sealed` — this implementation is an infrastructure detail. Only `ICacheService` is public.

### InternalAdminContext — Exact Class Shape

Create `src/OneId.Server/Application/Common/InternalAdminContext.cs`:

```csharp
// AR-8: Injectable ONLY within Application/Internal/ — enforced by InternalBoundaryTests.cs.
// Services in Application/Internal/ inject this to signal they need cross-tenant data access.
// All other code (Tenant Admin services, controllers) must NOT take this as a constructor dependency.
namespace OneId.Server.Application.Common;

public sealed class InternalAdminContext
{
}
```

This is a marker class for now. Epic 3 will flesh it out when Internal Admin services are created. The ArchUnit rule enforces its usage boundary from day one.

### Program.cs Additions

Add these two lines to `src/OneId.Server/Program.cs` **after** the existing DI registrations (after `ITenantContext` but before `AddDbContext`):

```csharp
// AR-10: All cache access must go through ICacheService — direct IMemoryCache injection is forbidden
// outside Infrastructure/Caching/ and is enforced by InternalBoundaryTests.cs.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
```

Add the required using at the top:
```csharp
using OneId.Server.Infrastructure.Caching;
```

**WHY `AddSingleton`**: `IMemoryCache` is registered as singleton by `AddMemoryCache()`. `MemoryCacheService` wraps it and has no per-request state — singleton is correct. Do NOT use `AddScoped` here.

### ArchUnit Package — NetArchTest.Rules

Add to `tests/OneId.Server.IntegrationTests/OneId.Server.IntegrationTests.csproj`:

```xml
<PackageReference Include="NetArchTest.Rules" Version="1.3.*" />
```

Use the `1.3.*` floating version. `NetArchTest.Rules` targets .NET Standard 2.0 and uses Mono.Cecil to analyze IL — it works with .NET 10 compiled assemblies. Do NOT use `TngTech.ArchUnitNET` — it has a more complex setup and the xUnit integration requires extra configuration.

### InternalBoundaryTests.cs — Exact Structure

The test file goes in `tests/OneId.Server.IntegrationTests/Architecture/InternalBoundaryTests.cs`.

**Three tests required:**

**Test 1 — InternalAdminContext boundary (positive rule, production code must pass):**

```csharp
[Fact]
public void InternalAdminContext_MustOnlyBeUsedInsideApplicationInternal()
{
    var assembly = typeof(OneId.Server.Application.Common.InternalAdminContext).Assembly;

    var result = Types.InAssembly(assembly)
        .That()
        .DoNotResideInNamespaceContaining("Application.Internal")
        .ShouldNot()
        .HaveDependencyOnAny("OneId.Server.Application.Common.InternalAdminContext")
        .GetResult();

    Assert.True(result.IsSuccessful,
        $"InternalAdminContext leaked outside Application.Internal: " +
        $"{string.Join(", ", result.FailingTypes.Select(t => t.FullName))}");
}
```

**Test 2 — Violation proof (tests that the rule FAILS when violated):**

The test file itself must contain a deliberately violating class in a "wrong" namespace. Place this class at the BOTTOM of the file, outside the test class, in a deliberately wrong namespace:

```csharp
// Deliberate violation fixture — in wrong namespace to prove InternalBoundaryTests rule has teeth.
// This class lives in the TEST assembly, not the production assembly.
namespace OneId.Server.Application.Tenant.Controllers.TestViolation
{
    using OneId.Server.Application.Common;
    
    internal sealed class ViolatingTenantController(InternalAdminContext context)
    {
        private readonly InternalAdminContext _context = context; // must reference to avoid CS0169
    }
}
```

Then write the test that expects this to FAIL:

```csharp
[Fact]
public void ViolatingController_Using_InternalAdminContext_IsDetectedByRule()
{
    // The test assembly itself contains a deliberate violation (ViolatingTenantController above).
    var testAssembly = System.Reflection.Assembly.GetExecutingAssembly();

    var result = Types.InAssembly(testAssembly)
        .That()
        .DoNotResideInNamespaceContaining("Application.Internal")
        .ShouldNot()
        .HaveDependencyOnAny("OneId.Server.Application.Common.InternalAdminContext")
        .GetResult();

    // MUST fail — test assembly contains ViolatingTenantController
    Assert.False(result.IsSuccessful,
        "Expected the rule to fail because ViolatingTenantController depends on InternalAdminContext " +
        "from outside Application.Internal — rule has no teeth if this passes.");
}
```

**Test 3 — IMemoryCache boundary (positive rule, production code must pass):**

```csharp
[Fact]
public void IMemoryCache_MustOnlyBeReferencedInsideInfrastructureCaching()
{
    var assembly = typeof(OneId.Server.Application.Common.ICacheService).Assembly;

    var result = Types.InAssembly(assembly)
        .That()
        .DoNotResideInNamespaceContaining("Infrastructure.Caching")
        .ShouldNot()
        .HaveDependencyOnAny("Microsoft.Extensions.Caching.Memory.IMemoryCache")
        .GetResult();

    Assert.True(result.IsSuccessful,
        $"IMemoryCache leaked outside Infrastructure.Caching: " +
        $"{string.Join(", ", result.FailingTypes.Select(t => t.FullName))}");
}
```

### Full InternalBoundaryTests.cs File

```csharp
using NetArchTest.Rules;

namespace OneId.Server.IntegrationTests.Architecture;

public class InternalBoundaryTests
{
    [Fact]
    public void InternalAdminContext_MustOnlyBeUsedInsideApplicationInternal()
    {
        var assembly = typeof(OneId.Server.Application.Common.InternalAdminContext).Assembly;

        var result = Types.InAssembly(assembly)
            .That()
            .DoNotResideInNamespaceContaining("Application.Internal")
            .ShouldNot()
            .HaveDependencyOnAny("OneId.Server.Application.Common.InternalAdminContext")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"InternalAdminContext leaked outside Application.Internal: " +
            $"{string.Join(", ", result.FailingTypes.Select(t => t.FullName))}");
    }

    [Fact]
    public void ViolatingController_Using_InternalAdminContext_IsDetectedByRule()
    {
        var testAssembly = System.Reflection.Assembly.GetExecutingAssembly();

        var result = Types.InAssembly(testAssembly)
            .That()
            .DoNotResideInNamespaceContaining("Application.Internal")
            .ShouldNot()
            .HaveDependencyOnAny("OneId.Server.Application.Common.InternalAdminContext")
            .GetResult();

        Assert.False(result.IsSuccessful,
            "Expected the rule to fail because ViolatingTenantController depends on InternalAdminContext " +
            "from outside Application.Internal — rule has no teeth if this passes.");
    }

    [Fact]
    public void IMemoryCache_MustOnlyBeReferencedInsideInfrastructureCaching()
    {
        var assembly = typeof(OneId.Server.Application.Common.ICacheService).Assembly;

        var result = Types.InAssembly(assembly)
            .That()
            .DoNotResideInNamespaceContaining("Infrastructure.Caching")
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.Extensions.Caching.Memory.IMemoryCache")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"IMemoryCache leaked outside Infrastructure.Caching: " +
            $"{string.Join(", ", result.FailingTypes.Select(t => t.FullName))}");
    }
}

// Deliberate violation fixture — proves InternalAdminContext boundary rule has teeth.
// Lives in the TEST assembly in a wrong namespace so Test 2 detects it.
namespace OneId.Server.Application.Tenant.Controllers.TestViolation
{
    using OneId.Server.Application.Common;

    internal sealed class ViolatingTenantController(InternalAdminContext context)
    {
        private readonly InternalAdminContext _context = context;
    }
}
```

### Namespace Notes

- Production assembly namespace root: `OneId.Server` (e.g., `OneId.Server.Application.Common`)
- Unit tests namespace root: `OneId.Server.Tests` (e.g., `OneId.Server.Tests.Infrastructure`)  
- Integration tests namespace root: `OneId.Server.IntegrationTests` (e.g., `OneId.Server.IntegrationTests.Architecture`)

The violation class uses `OneId.Server.Application.Tenant.Controllers.TestViolation` — this is in the test assembly, NOT in `OneId.Server.Application.Internal`, so the rule correctly flags it.

### What `HaveDependencyOnAny` Checks

`NetArchTest.Rules` uses Mono.Cecil to inspect IL-level type references. `HaveDependencyOnAny("FullTypeName")` catches:
- Constructor parameters of that type
- Field declarations of that type
- Method local variables of that type
- Method return types

It does NOT flag calls to extension methods on `IServiceCollection` (e.g., `AddMemoryCache()`) as depending on `IMemoryCache` directly — the IL reference is to `IServiceCollectionExtensions`, not `IMemoryCache`. So `Program.cs` calling `AddMemoryCache()` will NOT trigger the `IMemoryCache` boundary rule.

### AR-15 Deferred-Skip Governance — No New Skips in This Story

The current deferred-skip count is 2 (at the cap of 3 before adding the 3rd):
1. `DevSigningKeyStabilityTest` → Story 2.1
2. `TestTokenFactoryContractTests` → Story 3.5

Story 1.7b (next) will add the 3rd skip (`PermissionCatalogSyncTests` → Story 4a.1). Story 1.7a must NOT introduce any new `[Fact(Skip = ...)]` — doing so would exceed the cap and block sprint planning.

### Previous Story Learnings (Stories 1.1–1.6)

- **Solution file**: `OneId.slnx` — use this in all dotnet commands.
- **xmin concurrency token**: `UseXminAsConcurrencyToken()` was removed from Npgsql v10 — instead use explicit property configuration with `.HasColumnType("xid")` as shown in `TenantConfiguration.cs`. Not relevant for this story but context for future stories.
- **Test namespace convention**: unit tests use `OneId.Server.Tests.*`, integration tests use `OneId.Server.IntegrationTests.*` — the `Architecture/` subdirectory follows this: namespace = `OneId.Server.IntegrationTests.Architecture`.
- **InternalsVisibleTo**: `OneId.Server.csproj` already grants `InternalsVisibleToAttribute` to both `OneId.Server.UnitTests` and `OneId.Server.IntegrationTests`. The `MemoryCacheService` is marked `internal sealed` but the test assembly can still reference it for any future testing.
- **ImplicitUsings enabled**: `Microsoft.Extensions.Caching.Memory` must be explicitly imported in `MemoryCacheService.cs` unless it's in the global usings. Add `using Microsoft.Extensions.Caching.Memory;` at the top of the file to be safe.

### Project Structure Alignment

```
src/OneId.Server/
├── Application/
│   ├── Common/
│   │   ├── InternalAdminContext.cs   ← NEW (Task 1)
│   │   ├── ICacheService.cs          ← UPDATE: add Get/Set/Remove methods
│   │   ├── TenantContext.cs          ← unchanged
│   │   └── ITenantContext.cs         ← unchanged
│   └── Internal/
│       └── .gitkeep                  ← keep as-is; services come in Epic 3
└── Infrastructure/
    └── Caching/
        ├── .gitkeep                  ← delete this
        └── MemoryCacheService.cs     ← NEW (Task 2)

tests/OneId.Server.IntegrationTests/
└── Architecture/                     ← NEW directory
    └── InternalBoundaryTests.cs      ← NEW (Task 3)
```

### References

- [Source: epics.md#Story 1.7a] — acceptance criteria, ArchUnit rule descriptions, cache key format, violation test requirement
- [Source: architecture.md#Process Patterns] — `InternalAdminContext` usage restriction, `ICacheService` wrapping pattern, cache key format `{entity}:{userId}:{tenantId}`
- [Source: architecture.md#All Implementation Agents MUST] — Rule 7: `InternalAdminContext` only under `Application/Internal/`; Rule 10: cache via `ICacheService` only
- [Source: architecture.md#Project Directory Structure] — `Application/Common/InternalAdminContext.cs` location, `Infrastructure/Caching/CacheService.cs` location
- [Source: architecture.md#Project Directory Structure] — ArchUnit tests at `tests/OneId.Server.IntegrationTests/Architecture/InternalBoundaryTests.cs`
- [Source: epics.md#Epic 1 AR-8] — ArchUnit boundary enforcement description
- [Source: epics.md#Epic 1 AR-10] — Cache abstraction via `ICacheService` wrapping `IMemoryCache`
- [Source: epics.md#Epic 1 AR-15] — Deferred-skip governance; current count is 2, must stay ≤3 until an existing skip closes
- [Source: src/OneId.Server/Application/Common/ICacheService.cs] — existing stub to replace
- [Source: src/OneId.Server/Application/Common/TenantContext.cs] — namespace + sealed pattern to follow
- [Source: src/OneId.Server/Infrastructure/Persistence/Configurations/TenantConfiguration.cs] — internal sealed pattern example
- [Source: tests/OneId.Server.IntegrationTests/OneId.Server.IntegrationTests.csproj] — target project for NetArchTest.Rules package
- [Source: Directory.Build.props] — `TreatWarningsAsErrors=true`, `net10.0`, `Nullable=enable`, `ImplicitUsings=enable`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Initial ArchUnit tests failed with `ArgumentNullException: Value cannot be null (Parameter 'source')` because `result.FailingTypes` is null when no violations exist in NetArchTest.Rules 1.3.2. Fixed by guarding the failing-type enumeration inside an `if (!result.IsSuccessful)` block so it only runs when there are actual violations.

### Completion Notes List

- Created `InternalAdminContext.cs` as a sealed marker class in `Application/Common/`. No methods — serves as an injectable sentinel type whose presence in a constructor signals cross-tenant access intent.
- Completed `ICacheService` interface with `Get<T>`, `Set<T>`, and `Remove` methods. Key format documented in comment: `{entity}:{userId}:{tenantId}`.
- Created `MemoryCacheService` as `internal sealed` in `Infrastructure/Caching/`, wrapping `IMemoryCache`. Uses `AbsoluteExpirationRelativeToNow` when expiry is provided.
- Registered `AddMemoryCache()` + `AddSingleton<ICacheService, MemoryCacheService>()` in `Program.cs` before `ITenantContext` registration, with AR-10 comment.
- Added `NetArchTest.Rules 1.3.2` to integration tests project (uses Mono.Cecil 0.11.3 for IL analysis — works with .NET 10 compiled assemblies).
- Created `InternalBoundaryTests.cs` with 3 tests: (1) positive rule on production assembly for `InternalAdminContext` isolation, (2) violation-proof test that deliberately injects `InternalAdminContext` into a `ViolatingTenantController` in the test assembly and asserts the rule FAILS, (3) positive rule on production assembly for `IMemoryCache` isolation.
- All tests pass: 9 unit tests (1 skipped — existing `DevSigningKeyStabilityTest`), 8 integration tests (1 skipped — existing `TestTokenFactoryContractTests`). Zero new deferred skips — AR-15 cap maintained at 2.

### File List

- src/OneId.Server/Application/Common/InternalAdminContext.cs (new)
- src/OneId.Server/Application/Common/ICacheService.cs (modified — added Get/Set/Remove methods)
- src/OneId.Server/Infrastructure/Caching/MemoryCacheService.cs (new)
- src/OneId.Server/Program.cs (modified — added AddMemoryCache + ICacheService registration, using for Infrastructure.Caching)
- tests/OneId.Server.IntegrationTests/Architecture/InternalBoundaryTests.cs (new)
- tests/OneId.Server.IntegrationTests/OneId.Server.IntegrationTests.csproj (modified — added NetArchTest.Rules 1.3.2)
