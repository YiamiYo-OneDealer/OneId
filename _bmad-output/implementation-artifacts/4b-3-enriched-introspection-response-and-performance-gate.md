# Story 4b.3: Enriched Introspection Response and Performance Gate

**Status:** done
**Epic:** 4b — Token Evaluation & Overrides
**Story ID:** 4b-3
**Prerequisite:** Story 4b-2 complete ✓ (`PermissionEvaluator`, `PermissionEvaluationEnricher`, permissions in JWT)

---

## Story

As a resource server (OneDealer v2),
I want the introspection endpoint to return the user's full resolved authorization state — permissions, dimensional attributes, and license status — within the 50ms performance budget,
So that OneDealer v2 can make authorization decisions from a single introspection call without additional round trips.

---

## Acceptance Criteria

1. **Given** a valid token is submitted to `POST /connect/introspect`
   **When** introspection runs with the full enrichment pipeline active
   **Then** the response includes (in addition to the existing `active`, `sub`, `jti`, etc.):
   - `permissions` (array of effective permission ID strings, DENY-evaluated, expiry-filtered) — already in JWT from 4b-2
   - `dimensional_attributes` (object keyed by axis name, value is array of strings — e.g. `{ "Company": ["OneDealer GmbH"], "Location": [] }`)
   - `license` (object: `{ "status": "active", "seats_used": 0, "max_seats": 0 }` — stub until Phase 6 licensing stories 3-3/3-5)
   **And** all five dimension axes (`Company`, `Location`, `Branch`, `Make`, `MarketSegment`) are always present in `dimensional_attributes` — axes with no assignments return an empty array, never omitted
   **And** `IntrospectionTests.ActiveToken_IntrospectionReturnsActiveTrue` is updated to assert these three new fields

2. **Given** a User has no Group assignments
   **When** their token is introspected
   **Then** `permissions` is an empty array and `dimensional_attributes` has all five axes as empty arrays
   **And** the response is still `active: true` (no permissions ≠ invalid token)
   **And** a new `IntrospectionTests.NoAssignments_EnrichedFieldsAreEmpty` test covers this case

3. **Given** `TestTokenFactoryContractTests.cs` currently has a skipped placeholder test
   **When** the enrichment pipeline from this story is active
   **Then** the skip is removed and the test is implemented to:
   - Issue a real token via the full `ITokenClaimsEnricher` pipeline (password + MFA flow)
   - Introspect it and assert the response contains `permissions` (array), `dimensional_attributes` (object with all 5 axes), and `license` (object with `status`/`seats_used`/`max_seats` keys)
   **And** this test lives in the `[Collection("IntegrationTests")]` collection and uses `IntegrationTestBase`

4. **Given** introspection is measured end-to-end (permission union + override filter + dimension lookup + license stub)
   **When** `TokenEvaluationPerformanceTests.cs` runs with the full enrichment pipeline active
   **Then** 95th percentile introspection time (measured client-side excluding network, same method as `IntrospectionTests.IntrospectionPerformanceTest_P95_Under50ms`) is **under 40ms** (10ms headroom against NFR-4's 50ms gate)
   **And** the test uses a `Stopwatch` per call, minimum 50 samples, and fails if p95 exceeds 40ms
   **And** `ICacheService` (AR-10) is confirmed used: cache-miss path (first call) and cache-hit path (subsequent calls) are both exercised; the test asserts that second-call p95 is faster than first-call p95 (or separately asserts both pass the 40ms gate)

5. **Given** `IntrospectionEnrichmentRegressionTests.cs` runs
   **When** a User's dimensional attribute assignment changes between two introspection calls
   **Then** after explicitly clearing the in-process `IMemoryCache` between calls, the second introspection reflects the updated dimension values
   **And** the test seeds an initial assignment, introspects, removes the assignment, clears the cache via `IMemoryCache` resolved from `factory.Services`, introspects again, and asserts the dimension is absent

---

## Tasks

- [x] **Task 1: Create `IDimensionEvaluator` interface**
  - Path: `src/OneId.Server/Domain/Services/IDimensionEvaluator.cs`
  - Method: `Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> EvaluateAsync(Guid userId, Guid tenantId, CancellationToken ct = default)`
  - Returns: all 5 axes always present, value is list of string values (empty list if no assignments)
  - Axis keys are the enum names: `"Company"`, `"Location"`, `"Branch"`, `"Make"`, `"MarketSegment"`

- [x] **Task 2: Implement `DimensionEvaluator`**
  - Path: `src/OneId.Server/Application/Dimensions/DimensionEvaluator.cs`
  - Injects: `AppDbContext`, `ICacheService`
  - Cache key: `$"dimensions:{userId}:{tenantId}"` — cache with TTL of 5 minutes
  - Query: `db.UserDimensionAssignments.IgnoreQueryFilters()` + explicit `a.DimensionValue.TenantId == tenantId` filter
  - Must include `.Include(a => a.DimensionValue)` to access `Axis` and `Value`
  - Group by `DimensionValue.Axis`, map to `DimensionAxis` enum names
  - All 5 axes from `Enum.GetValues<DimensionAxis>()` must always appear in the result — use default empty list for absent axes
  - No background sweeper needed; cache TTL is the propagation window (matches OneDealer v2's 5-min consumer cache)

- [x] **Task 3: Add caching to `PermissionEvaluator`**
  - Modify: `src/OneId.Server/Application/Permissions/PermissionEvaluator.cs`
  - Add `ICacheService` constructor injection alongside existing `AppDbContext`
  - Cache key: `$"permissions:{userId}:{tenantId}"`
  - On cache hit: return cached `IReadOnlySet<string>` immediately (skip all DB queries)
  - On cache miss: run existing evaluation logic, `cache.Set(key, result, TimeSpan.FromMinutes(5))`, return result
  - Remove the deferred-caching comment from line 10

- [x] **Task 4: Create `IntrospectionEnricher` OpenIddict handler**
  - Path: `src/OneId.Server/Infrastructure/OpenIddict/IntrospectionEnricher.cs`
  - Implements: `IOpenIddictServerHandler<ApplyIntrospectionResponseContext>`
  - Order: `Order = OpenIddictServerHandlers.ApplyIntrospectionResponse[0].Order - 1` (runs before default handler writes the response)
  - Read `sub` and `tid` from `context.Principal` — if either is missing or not a valid GUID, skip enrichment (log warning, do not throw)
  - Call `IDimensionEvaluator.EvaluateAsync(userId, tenantId, ct)` — serialize result as JSON object keyed by axis name
  - Set `context.Response["dimensional_attributes"] = ...` using `OpenIddictResponse` or direct JSON
  - Set `context.Response["license"] = new { status = "active", seats_used = 0, max_seats = 0 }` (stub — Phase 6 wires real license data)
  - Only enriches when `context.Response["active"]?.Value<bool>() == true` — skip for inactive/revoked tokens
  - Injects: `IDimensionEvaluator` (scoped via context's DI)

- [x] **Task 5: Register `DimensionEvaluator` and `IntrospectionEnricher` in DI**
  - Modify: `src/OneId.Server/Infrastructure/OpenIddict/TokenPipelineExtensions.cs`
  - Add: `services.AddScoped<IDimensionEvaluator, DimensionEvaluator>();`
  - Register `IntrospectionEnricher` with OpenIddict server options in the OpenIddict configuration (Program.cs or wherever `.AddServer()` is called):
    ```csharp
    options.AddEventHandler<ApplyIntrospectionResponseContext>(builder =>
        builder.UseType<IntrospectionEnricher>());
    ```

- [x] **Task 6: Update `IntrospectionTests.cs`**
  - Extend `ActiveToken_IntrospectionReturnsActiveTrue`: assert `permissions`, `dimensional_attributes`, and `license` fields present and have correct JSON shapes (array, object with 5 keys, object with `status`/`seats_used`/`max_seats`)
  - Add `NoAssignments_EnrichedFieldsAreEmpty`: issue token for a user with no group or dimension assignments; assert `permissions` is empty array and all 5 `dimensional_attributes` axes are empty arrays
  - The DevSeeder user (`DevSeeder.TotpUserEmail`) may have assignments from previous stories — use a fresh seeded user with no assignments for the empty-case test

- [x] **Task 7: Update `TestTokenFactoryContractTests.cs`**
  - Remove `[Fact(Skip = ...)]`, change class to `[Collection("IntegrationTests")]`, inherit from `IntegrationTestBase`
  - Implement `TestTokenFactory_ClaimShape_MatchesProductionITokenClaimsEnricher`:
    - Issue a real token via password + MFA flow (reuse `IssueMfaTokenAsync` pattern from `IntrospectionTests`)
    - Introspect the token and assert the response body contains keys: `permissions` (JsonValueKind.Array), `dimensional_attributes` (JsonValueKind.Object with exactly 5 properties), `license` (JsonValueKind.Object with `status`, `seats_used`, `max_seats`)
    - This test catches future drift between `TestTokenFactory` claim shape and the production pipeline

- [x] **Task 8: Create `TokenEvaluationPerformanceTests.cs`**
  - Path: `tests/OneId.Server.IntegrationTests/TokenEvaluationPerformanceTests.cs`
  - Collection: `[Collection("IntegrationTests")]`, inherits `IntegrationTestBase`
  - Test `IntrospectionP95_Under40ms_ColdPath`: issue one token; run 50 introspection calls (cache cold on first, warm after); measure Stopwatch per call; sort; assert p95 index ≤ 40ms
  - Pattern: identical to existing `IntrospectionPerformanceTest_P95_Under50ms` in `IntrospectionTests.cs` but with 40ms ceiling
  - Note: IMemoryCache warms up after first call; the test covers the warm path naturally since the loop reuses the same token (same userId/tenantId)

- [x] **Task 9: Create `IntrospectionEnrichmentRegressionTests.cs`**
  - Path: `tests/OneId.Server.IntegrationTests/IntrospectionEnrichmentRegressionTests.cs`
  - Collection: `[Collection("IntegrationTests")]`, inherits `IntegrationTestBase`
  - Test `DimensionAssignmentChange_ReflectedAfterCacheInvalidation`:
    1. Create a fresh user + seed a `UserDimensionAssignment` (e.g., Company = "Contoso") via `AppDbContext` directly
    2. Issue token for this user (password-only or TestTokenFactory path — see Dev Notes)
    3. Introspect → assert `dimensional_attributes.Company` contains `"Contoso"`
    4. Delete the assignment via `AppDbContext`, then clear the full `IMemoryCache`: `factory.Services.GetRequiredService<IMemoryCache>().Clear()` (or remove the specific key)
    5. Introspect again → assert `dimensional_attributes.Company` is empty array
    **Note:** `IMemoryCache.Clear()` is available in .NET 10; alternatively use `factory.Services.GetRequiredService<ICacheService>()` if a `Clear` method is added, or resolve `IMemoryCache` directly for test purposes (this is acceptable in test infrastructure — the boundary rule is for production code)

---

## Dev Notes

### Cache key mismatch — why no active invalidation on mutations
`MemoryCacheService` auto-prefixes cache keys with `{tenantId}:` when `ITenantContext.IsInitialized == true`. During introspection and token issuance, `ITenantContext` is NOT initialized (these are IDP flows, not tenant-scoped API calls), so evaluator cache keys are stored WITHOUT the tenant prefix (e.g., `permissions:{userId}:{tenantId}`). Mutation handlers (Override CRUD, Group assignment changes) run with TenantContext initialized — calling `cache.Remove("permissions:{userId}:{tenantId}")` from there would produce key `{tenantId}:permissions:{userId}:{tenantId}` → mismatch. 

**Decision:** Do NOT add active cache invalidation to mutation handlers. The 5-minute TTL is the accepted propagation delay per the architecture (same window OneDealer v2 caches introspection results). This is explicitly documented in the architecture. Mutation handlers should NOT call `cache.Remove` for permission/dimension keys.

### OpenIddict `ApplyIntrospectionResponseContext` handler
OpenIddict uses an event pipeline for introspection. The principal available on `context.Principal` has been validated and contains all JWT claims including custom claims (`tid`, `permissions`, etc.) set during token issuance. The `context.Response` is a mutable `OpenIddictResponse` — you can set arbitrary JSON properties on it.

Key pitfall: Only enrich when `context.Response["active"]?.Value<bool>() == true`. When a token is revoked or expired, OpenIddict sets `active: false` and RFC 7662 §2.2 requires no other claims be returned. Enriching an inactive token response violates this requirement.

Check if `context.Principal` is null before accessing claims — it will be null for inactive tokens.

### `permissions` claim is already in the introspection response
`PermissionEvaluationEnricher` adds `permissions` claims to the `ClaimsIdentity` during token issuance. `ConnectController` then calls `claim.SetDestinations(Destinations.AccessToken)` on ALL claims (line 230). OpenIddict serializes JWT claims with `AccessToken` destination into the JWT. During introspection, OpenIddict validates the JWT and reflects its claims into the introspection response. Therefore `permissions` will already be present as a JSON array in the introspection response without any additional work in this story.

### `DimensionEvaluator` query — bypassing global query filter
`UserDimensionAssignment` has a global query filter: `a.DimensionValue.TenantId == tenantContext.TenantId`. The evaluator is called without TenantContext initialized (same issue as `PermissionEvaluator`). Use `IgnoreQueryFilters()` with an explicit `a.DimensionValue.TenantId == tenantId` filter. Single query:

```csharp
var assignments = await db.UserDimensionAssignments
    .IgnoreQueryFilters()
    .Include(a => a.DimensionValue)
    .Where(a => a.UserId == userId && a.DimensionValue.TenantId == tenantId)
    .Select(a => new { a.DimensionValue.Axis, a.DimensionValue.Value })
    .ToListAsync(ct);
```

### All 5 axes always present
Use `Enum.GetValues<DimensionAxis>()` to initialize the result dictionary with all axes, then populate from the query results:

```csharp
var result = Enum.GetValues<DimensionAxis>()
    .ToDictionary(
        axis => axis.ToString(),
        axis => (IReadOnlyList<string>)assignments
            .Where(a => a.Axis == axis)
            .Select(a => a.Value)
            .ToList());
return result;
```

### `IntrospectionEnricher` serialization
`OpenIddictResponse` in OpenIddict v5 accepts `System.Text.Json.Nodes.JsonNode` values. For the `dimensional_attributes` object:

```csharp
var dimensionsNode = new JsonObject();
foreach (var (axis, values) in dimensions)
    dimensionsNode[axis] = new JsonArray(values.Select(v => JsonValue.Create(v)).ToArray<JsonNode?>());

context.Response["dimensional_attributes"] = dimensionsNode;
context.Response["license"] = new JsonObject
{
    ["status"] = "active",
    ["seats_used"] = 0,
    ["max_seats"] = 0
};
```

Verify this against the OpenIddict version in use (OpenIddict v5.x for .NET 10). If `OpenIddictResponse[]` setter doesn't accept `JsonNode` directly, use the `SetParameter` extension method.

### Registering `IntrospectionEnricher` with OpenIddict
The event handler registration goes inside `.AddServer(options => { ... })` in Program.cs or wherever OpenIddict is configured. Find the `.AddServer(...)` call and add:

```csharp
options.AddEventHandler(OpenIddictServerHandlers.ApplyIntrospectionResponse[0].Descriptor);
// OR more directly:
options.AddEventHandler<ApplyIntrospectionResponseContext>(builder =>
    builder.UseScopedHandler<IntrospectionEnricher>());
```

Use `.UseScopedHandler<T>()` (not `.UseType<T>()`) since `IntrospectionEnricher` injects scoped services (`IDimensionEvaluator`).

### TestTokenFactoryContractTests — needs `IntegrationTestBase`
The test class currently has no constructor and no collection. After modification it will need `OneIdWebApplicationFactory` injected. Pattern:

```csharp
[Collection("IntegrationTests")]
public class TestTokenFactoryContractTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task TestTokenFactory_ClaimShape_MatchesProductionITokenClaimsEnricher() { ... }
}
```

### Issuing a token for a fresh user in regression tests
`IntegrationTestBase` uses `DevSeeder.TotpUserEmail` for `IssueMfaTokenAsync`. For the regression test with a fresh user with no assignments, either:
- Create a user with no TOTP secret — use password-only flow if MFA is not required for non-TOTP users (check DevSeeder — there's a `SimpleUserEmail` without TOTP if it was seeded in previous stories)
- OR seed the fresh user with a known TOTP secret and compute the code in the test
- OR use TestTokenFactory if the regression test only needs to verify the REFRESH path (pass the token directly to introspect without going through the login flow) — **preferred for regression tests** since it avoids MFA complexity

### File paths summary (new and modified)
| File | Status |
|------|--------|
| `src/OneId.Server/Domain/Services/IDimensionEvaluator.cs` | NEW |
| `src/OneId.Server/Application/Dimensions/DimensionEvaluator.cs` | NEW |
| `src/OneId.Server/Application/Permissions/PermissionEvaluator.cs` | MODIFY (add caching) |
| `src/OneId.Server/Infrastructure/OpenIddict/IntrospectionEnricher.cs` | NEW |
| `src/OneId.Server/Infrastructure/OpenIddict/TokenPipelineExtensions.cs` | MODIFY (add IDimensionEvaluator registration) |
| `src/OneId.Server/Program.cs` | MODIFY (register IntrospectionEnricher event handler) |
| `tests/OneId.Server.IntegrationTests/OpenIddict/IntrospectionTests.cs` | MODIFY (extend AC1, add AC2 test) |
| `tests/OneId.Server.IntegrationTests/TestTokenFactoryContractTests.cs` | MODIFY (remove Skip, implement) |
| `tests/OneId.Server.IntegrationTests/TokenEvaluationPerformanceTests.cs` | NEW |
| `tests/OneId.Server.IntegrationTests/IntrospectionEnrichmentRegressionTests.cs` | NEW |

---

## Current Codebase State (as of story creation)

### `PermissionEvaluator.cs` — current (no caching)
```csharp
// src/OneId.Server/Application/Permissions/PermissionEvaluator.cs
// Called during token issuance where ITenantContext is not yet initialized.
// Uses explicit tenantId parameter for isolation instead of EF query filters.
// Caching is deferred to Story 4b-3 (where the 40ms performance gate validates it).
public sealed class PermissionEvaluator(AppDbContext db) : IPermissionEvaluator
{
    public async Task<IReadOnlySet<string>> EvaluateAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        // ... [full join-chain logic for direct roles, role set roles, overrides]
    }
}
```

After this story: add `ICacheService cache` to constructor, wrap in cache check/set.

### `TokenPipelineExtensions.cs` — current state
```csharp
public static IServiceCollection AddTokenPipeline(this IServiceCollection services)
{
    services.AddScoped<ITokenClaimsEnricher, RoleClaimsEnricher>();
    services.AddScoped<IPermissionEvaluator, PermissionEvaluator>();
    services.AddScoped<ITokenClaimsEnricher, PermissionEvaluationEnricher>();
    return services;
}
```

After this story: add `services.AddScoped<IDimensionEvaluator, DimensionEvaluator>();`

### `ICacheService` interface (AR-10)
```csharp
// All cache access MUST go through this — IMemoryCache injection forbidden outside Infrastructure/Caching/
public interface ICacheService
{
    T? Get<T>(string key);
    void Set<T>(string key, T value, TimeSpan? expiry = null);
    void Remove(string key);
}
```

`MemoryCacheService` auto-prefixes keys with `{tenantId}:` when `ITenantContext.IsInitialized`. During introspection/token-issuance these flows do NOT initialize TenantContext, so no prefix is added — keys are stored exactly as passed to `ICacheService`.

### `DimensionAxis` enum
```csharp
public enum DimensionAxis { Company = 0, Location = 1, Branch = 2, Make = 3, MarketSegment = 4 }
```

### `UserDimensionAssignment` entity
```csharp
public class UserDimensionAssignment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DimensionValueId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public User User { get; set; } = null!;
    public DimensionValue DimensionValue { get; set; } = null!;
}
```

Global query filter: `a.DimensionValue.TenantId == tenantContext.TenantId` — bypass with `IgnoreQueryFilters()` + explicit tenantId filter.

### `AppDbContext` DbSets relevant to this story
```csharp
public DbSet<DimensionValue> DimensionValues => Set<DimensionValue>();
public DbSet<UserDimensionAssignment> UserDimensionAssignments => Set<UserDimensionAssignment>();
```

---

## Architecture Constraints

- **AR-10:** All cache access via `ICacheService`. Direct `IMemoryCache` injection in production code is forbidden and enforced by `InternalBoundaryTests.cs`. In test code, resolving `IMemoryCache` directly to `.Clear()` the cache is acceptable.
- **NFR-4:** Introspection p95 ≤ 50ms. Story tests at 40ms (10ms headroom).
- **5-min TTL:** Matches OneDealer v2's consumer-side introspection cache. This is the accepted propagation delay — not a bug.
- **License stub:** `Tenant` entity has no seat-count fields yet (Phase 6 stories 3-3/3-5 are `backlog`). Hardcode `seats_used: 0, max_seats: 0, status: "active"` with a TODO comment pointing to Phase 6.
- **RFC 7662 §2.2:** When `active: false`, introspection response MUST contain only the `active` claim. `IntrospectionEnricher` must check `context.Response["active"]` before enriching.

## Completion Checklist

- [x] `IDimensionEvaluator` interface created with correct signature
- [x] `DimensionEvaluator` queries all 5 axes, uses `IgnoreQueryFilters()` + explicit tenantId, caches with 5-min TTL
- [x] `PermissionEvaluator` now injects `ICacheService`, caches with 5-min TTL under `permissions:{userId}:{tenantId}`
- [x] `IntrospectionEnricher` only enriches when `active: true`, adds `dimensional_attributes` and `license`
- [x] `IntrospectionEnricher` registered via `UseScopedHandler` in OpenIddict server options
- [x] `IDimensionEvaluator` registered as scoped in DI
- [x] `IntrospectionTests` updated: AC1 asserts enriched fields, AC2 test added
- [x] `TestTokenFactoryContractTests` Skip removed, class inherits `IntegrationTestBase`, test asserts enriched shape
- [x] `TokenEvaluationPerformanceTests` created with 40ms p95 ceiling
- [x] `IntrospectionEnrichmentRegressionTests` created, tests cache clear → refresh path
- [x] All pre-existing tests still pass (no regressions)

---

## Dev Agent Record

## Review Findings

- [x] [Review][Defer] **F02: `IntrospectionResponseEnricher` registered as Singleton** — intentional; OpenIddict isolates `context.Transaction` per request so there is no captured-state hazard; Singleton is correct here — Stage 2 is registered as Singleton via `AddSingleton` but reads from `context.Transaction` which is per-request. Stage 1 (DataEnricher) is Scoped. OpenIddict's transaction dictionary should isolate per-request, but the inconsistency is architecturally fragile. Decision: should Stage 2 also be Scoped, or is Singleton correct per OpenIddict's event pipeline design? [`TokenPipelineExtensions.cs:591`, `IntrospectionEnricher.cs:522`]
- [x] [Review][Dismiss] **F07: `DimensionEvaluator.cache.Get<Dictionary<>>` type mismatch with stored type** — `cache.Get<Dictionary<string, IReadOnlyList<string>>>` is called but the result is stored via `cache.Set(cacheKey, result, ...)` where `result` is typed `Dictionary<string, IReadOnlyList<string>>`. If `ICacheService` does runtime type-checking, the Get would return null on every call (defeating the cache). Verify the concrete type stored matches the Get type parameter, or align to `IReadOnlyDictionary`. [`DimensionEvaluator.cs:25`]
- [x] [Review][Patch] **F08: `TokenEvaluationPerformanceTests` doesn't exercise or assert cache-miss p95 (AC4)** — AC4 requires "cache-miss and cache-hit paths both exercised." Currently only the warm path is measured (50 samples after one warm-up). Add a separate cold-path sample (clear cache, single timed call) and assert it also meets the 40ms gate. [`TokenEvaluationPerformanceTests.cs`]
- [x] [Review][Defer] **F11: License stub always returns `status: active`** [`IntrospectionEnricher.cs`] — deferred, by design; Phase 6 stories 3-3/3-5 will wire real license data
- [x] [Review][Defer] **F12: Client-credentials token (missing `tid`) → introspection returns `active: true` with no enriched fields** [`IntrospectionEnricher.cs:34-35`] — deferred, expected behavior; client-credentials tokens are not user tokens and enrichment is scoped to user tokens with a valid `tid`

---

### Implementation Notes

**Two-stage introspection enrichment:** OpenIddict's pipeline requires two separate handlers. Stage 1 (`IntrospectionDataEnricher`) runs in `HandleIntrospectionRequestContext` where `GenericTokenPrincipal` is available — evaluates permissions and dimensions, stores on transaction via `OpenIddictServerHelpers.SetProperty`. Stage 2 (`IntrospectionResponseEnricher`) runs in `ApplyIntrospectionResponseContext` — reads from transaction, writes to response.

**Critical: handler order.** Stage 2 must run at order `1_000` (not `int.MaxValue - 50`). The ASP.NET Core transport handler that commits the HTTP response runs at a lower order. Setting Stage 2 to `int.MaxValue - 50` caused it to run AFTER the response was already written.

**Critical: empty array serialization.** `OpenIddictParameter(JsonNode)` internally converts empty `JsonArray` to `ImmutableArray.Empty`, which is stripped during serialization. Use `JsonSerializer.SerializeToElement(permissions.ToArray())` to get a `JsonElement` — the `OpenIddictParameter(JsonElement)` constructor stores it as-is and serializes correctly as `[]`.

**Critical: DI registration.** `UseScopedHandler<T>()` and `UseSingletonHandler<T>()` both require T to be registered in the DI container. Added `IntrospectionDataEnricher` (scoped) and `IntrospectionResponseEnricher` (singleton) to `TokenPipelineExtensions`.

**Cache contamination in tests.** Adding caching to `PermissionEvaluator` caused `PermissionEvaluationPipelineTests` to fail — multiple tests share TotpUser, so cached results from test N poisoned test N+1. Fixed by adding `IMemoryCache.Clear()` to `IntegrationTestBase.InitializeAsync()` alongside the existing DB reset.

### Files Changed

| File | Change |
|------|--------|
| `src/OneId.Server/Domain/Services/IDimensionEvaluator.cs` | NEW |
| `src/OneId.Server/Application/Dimensions/DimensionEvaluator.cs` | NEW |
| `src/OneId.Server/Application/Permissions/PermissionEvaluator.cs` | Added ICacheService caching |
| `src/OneId.Server/Infrastructure/OpenIddict/IntrospectionEnricher.cs` | NEW (Stage 1 + Stage 2) |
| `src/OneId.Server/Infrastructure/OpenIddict/TokenPipelineExtensions.cs` | Added DI registrations |
| `src/OneId.Server/Program.cs` | Added Stage 1 + Stage 2 event handler registrations |
| `tests/OneId.Server.IntegrationTests/Helpers/IntegrationTestBase.cs` | Added cache clear to InitializeAsync |
| `tests/OneId.Server.IntegrationTests/OpenIddict/IntrospectionTests.cs` | Extended AC1, added AC2 test |
| `tests/OneId.Server.IntegrationTests/TestTokenFactoryContractTests.cs` | Removed Skip, implemented |
| `tests/OneId.Server.IntegrationTests/TokenEvaluationPerformanceTests.cs` | NEW |
| `tests/OneId.Server.IntegrationTests/IntrospectionEnrichmentRegressionTests.cs` | NEW |
