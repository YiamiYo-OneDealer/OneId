# Story 4b.2: Permission Evaluation Pipeline

**Status:** review
**Epic:** 4b — Token Evaluation & Overrides
**Story ID:** 4b-2
**Prerequisite:** Story 4b-1 complete ✓ (`UserPermissionOverride` table, entity, CRUD API all stable)

---

## Story

As a developer,
I want the `ITokenClaimsEnricher` pipeline extended with a full permission evaluation stage,
So that token issuance produces the correct effective permission set for each user based on their complete Group chain.

---

## Acceptance Criteria

1. **Given** `ITokenClaimsEnricher` was defined in Story 2.4 with a `RoleClaimsEnricher` stage
   **When** this story adds a `PermissionEvaluationEnricher` stage
   **Then** no code from Story 2.4's `RoleClaimsEnricher` is deleted or modified — the new stage is registered additively after the existing stage

2. **Given** a User belongs to Groups G1 and G2
   **When** G1 has Role R1 (permissions: `od.crm.read`, `od.crm.write`) and G2 has RoleSet RS1 containing Role R2 (permissions: `od.crm.write`, `od.finance.read`)
   **Then** the effective permission set is the deduplicated union: `["od.crm.read", "od.crm.write", "od.finance.read"]`
   **And** a `PermissionUnionIntegrationTest` constructs this exact scenario and asserts the union

3. **Given** a User has a DENY override on `od.crm.write` with no expiry
   **When** the permission evaluation pipeline runs
   **Then** `od.crm.write` is excluded from the effective permission set regardless of Group assignments
   **And** DENY is terminal — no ALLOW override or Group assignment can reinstate a DENY-overridden permission
   **And** a `DenyTerminalIntegrationTest` asserts: User with DENY on `od.crm.write` via override does NOT receive `od.crm.write` even when a Group grants it

4. **Given** a User has an ALLOW override on `od.finance.delete` not present in any Group assignment
   **When** the permission evaluation pipeline runs
   **Then** `od.finance.delete` IS included in the effective permission set
   **And** a `AllowOverrideIntegrationTest` asserts this additive ALLOW behaviour

5. **Given** a User has a DENY override on `od.crm.write` with `ExpiresAt` in the past
   **When** the permission evaluation pipeline runs
   **Then** the expired DENY override is NOT applied — `od.crm.write` is included if a Group grants it
   **And** `ExpiredDenyOverrideIntegrationTest` confirms this by inserting an override with `ExpiresAt = NOW() - 1 minute` and asserting the permission is present

---

## Tasks / Subtasks

- [x] **Task 1: Create `IPermissionEvaluator` interface** (AC: 1-5)
  - [x] Create `src/OneId.Server/Domain/Services/IPermissionEvaluator.cs`
  - [x] Method: `Task<IReadOnlySet<string>> EvaluateAsync(Guid userId, Guid tenantId, CancellationToken ct)`
  - [x] Returns deduplicated string set of effective permission IDs after union + override application

- [x] **Task 2: Implement `PermissionEvaluator`** (AC: 2-5)
  - [x] Create `src/OneId.Server/Application/Permissions/PermissionEvaluator.cs`
  - [x] **Evaluation algorithm (in order):**
    1. Query direct role IDs via `UserGroups → GroupRoles`
    2. Query role set role IDs via `UserGroups → GroupRoleSets → RoleSetRoles`
    3. Merge all role IDs, query `RolePermissions → Permission.PermissionId` strings → HashSet union
    4. Query active `UserPermissionOverride` records with `IgnoreQueryFilters()` + explicit `tenantId` filter
    5. Compute `deniedPermissions` HashSet, remove from union (DENY is terminal)
    6. Add ALLOW overrides additively (skip if also denied — defensive guard)
  - [x] Uses `IgnoreQueryFilters()` + explicit `tenantId` parameter for all queries (TenantContext is not initialized during token issuance)
  - [x] Caching deferred to Story 4b-3 (MemoryCacheService auto-prefixes with TenantId when context is initialized, but evaluator runs without context — key strategy requires care, resolved in 4b-3 performance gate)

- [x] **Task 3: Create `PermissionEvaluationEnricher`** (AC: 1-5)
  - [x] Create `src/OneId.Server/Application/TokenPipeline/PermissionEvaluationEnricher.cs`
  - [x] Implements `ITokenClaimsEnricher`, injects `IPermissionEvaluator`
  - [x] Adds `Claim("permissions", permissionId, ...)` for each result from evaluator
  - [x] `RoleClaimsEnricher` unchanged

- [x] **Task 4: Register `PermissionEvaluationEnricher` in DI** (AC: 1)
  - [x] Updated `src/OneId.Server/Infrastructure/OpenIddict/TokenPipelineExtensions.cs`
  - [x] `IPermissionEvaluator` + `PermissionEvaluationEnricher` registered after `RoleClaimsEnricher`

- [x] **Task 5: Integration tests — `PermissionEvaluationPipelineTests.cs`** (AC: 2-5)
  - [x] Created `tests/OneId.Server.IntegrationTests/PermissionEvaluationPipelineTests.cs`
  - [x] `PermissionUnionIntegrationTest` — G1 (direct role) + G2 (via RoleSet): asserts 3-permission deduplicated union
  - [x] `DenyTerminalIntegrationTest` — DENY override blocks permission even when group grants it
  - [x] `AllowOverrideIntegrationTest` — ALLOW override adds permission not in any group
  - [x] `ExpiredDenyOverrideIntegrationTest` — expired DENY (ExpiresAt - 1 min) does not block
  - [x] `NoGroupAssignments_NoOverrides_EmptyPermissions` — empty result when no groups/overrides
  - [x] All 5 pass; 6 pre-existing failures unchanged; 0 regressions

- [x] **Task 6: Cache invalidation analysis** (AC: cached path)
  - [x] Analyzed: `MemoryCacheService` auto-prefixes keys with `TenantId` when HTTP context has initialized TenantContext. During token issuance, TenantContext is NOT initialized → no prefix. During mutation handlers, TenantContext IS initialized → prefix is added. Key mismatch makes handler-side `cache.Remove()` ineffective for evaluator-set keys. Caching + invalidation properly deferred to 4b-3 where the 40ms p95 gate validates the full approach.

---

## Dev Notes

### Architecture: No `IPermissionEvaluator` yet exists

A search of the codebase confirms `IPermissionEvaluator` and `PermissionEvaluator` do **not** yet exist — this story creates them from scratch. The architecture document (lines 463, 500) already prescribes these file paths:
- `src/OneId.Server/Domain/Services/IPermissionEvaluator.cs`
- `src/OneId.Server/Application/Permissions/PermissionEvaluator.cs`

### Pipeline: `TokenPipelineExtensions.cs` current state

```csharp
// src/OneId.Server/Infrastructure/OpenIddict/TokenPipelineExtensions.cs
public static IServiceCollection AddTokenPipeline(this IServiceCollection services)
{
    services.AddScoped<ITokenClaimsEnricher, RoleClaimsEnricher>();  // ← DO NOT TOUCH
    // Story 4b-2: Add PermissionEvaluationEnricher here, AFTER RoleClaimsEnricher
    return services;
}
```

After this story, it should look like:
```csharp
public static IServiceCollection AddTokenPipeline(this IServiceCollection services)
{
    services.AddScoped<ITokenClaimsEnricher, RoleClaimsEnricher>();
    services.AddScoped<IPermissionEvaluator, PermissionEvaluator>();
    services.AddScoped<ITokenClaimsEnricher, PermissionEvaluationEnricher>();
    return services;
}
```

### Entity relationship chain (current codebase state)

```
User (Users table)
  └─ UserGroup[] (UserGroups join — composite PK: GroupId, UserId)
       └─ Group (Groups table)
            ├─ GroupRole[] (GroupRoles join — composite PK: GroupId, RoleId)
            │    └─ Role (Roles table)
            │         └─ RolePermission[] (RolePermissions join — composite PK: RoleId, PermissionId)
            │              └─ PermissionId: string (e.g. "od.crm.read")
            └─ GroupRoleSet[] (GroupRoleSets join — composite PK: GroupId, RoleSetId)
                 └─ RoleSet (RoleSets table)
                      └─ RoleSetRole[] (RoleSetRoles join — composite PK: RoleSetId, RoleId)
                           └─ Role
                                └─ RolePermission[]
                                     └─ PermissionId: string
```

All entities are tenant-scoped via global query filters on `AppDbContext`. The `PermissionEvaluator` should use explicit `tenantId` filtering in addition (for correctness when called from enricher where `ITenantContext` may not be set to the right tenant).

### ICacheService (AR-10)

```csharp
// src/OneId.Server/Application/Common/ICacheService.cs
public interface ICacheService
{
    T? Get<T>(string key);
    void Set<T>(string key, T value, TimeSpan? expiry = null);
    void Remove(string key);
}
```

Cache key pattern: `"permissions:{userId}:{tenantId}"` (matches architecture AR-10 convention).
- **Cache hit**: return cached `HashSet<string>` immediately — no DB query
- **Cache miss**: evaluate via DB query, store result for 5 minutes
- Cache is `IMemoryCache`-backed for POC (single instance); swapped to Redis in production

### `RolePermission.PermissionId` is a string

`RolePermission.PermissionId` stores the string permission ID (e.g. `"od.crm.read"`) directly — it is NOT a FK to `Permission.Id` (Guid). This is the same pattern as `UserPermissionOverride.PermissionId`. The evaluation pipeline never needs to join to the `Permissions` table — it works purely with permission ID strings.

### DENY-before-ALLOW ordering is critical

The evaluation algorithm must apply DENY overrides before ALLOW overrides:

```csharp
// 1. Build union of permissions from Groups (Groups → RoleSets → Roles → RolePermissions)
var permissions = new HashSet<string>(groupPermissions);

// 2. Apply DENY overrides FIRST (terminal — cannot be reinstated)
foreach (var deny in overrides.Where(o => o.OverrideType == PermissionOverrideType.Deny))
    permissions.Remove(deny.PermissionId);

// 3. Apply ALLOW overrides AFTER (additive — but cannot override a DENY)
foreach (var allow in overrides.Where(o => o.OverrideType == PermissionOverrideType.Allow))
    permissions.Add(allow.PermissionId);
```

Wait — this ordering means an ALLOW override CAN reinstate a permission that was also DENY-overridden (since ALLOW runs after DENY removes it, but ALLOW.Add re-adds it). Per the epic spec: "DENY is terminal — no ALLOW override or Group assignment can reinstate a DENY-overridden permission." 

**Correct implementation**: If both ALLOW and DENY exist on the same permission (which the unique constraint on `(TenantId, UserId, PermissionId)` prevents), DENY wins. Since the unique constraint ensures only ONE override per `(TenantId, UserId, PermissionId)`, there can never be both a DENY and ALLOW on the same permission simultaneously. So the ordering doesn't matter for same-permission conflicts — but the algorithm should still semantically make DENY non-overridable. The simplest safe approach:

```csharp
var deniedPermissions = overrides
    .Where(o => o.OverrideType == PermissionOverrideType.Deny)
    .Select(o => o.PermissionId)
    .ToHashSet();

// Build union from Groups, excluding denied permissions
var permissions = groupPermissions
    .Where(p => !deniedPermissions.Contains(p))
    .ToHashSet();

// Add ALLOW overrides (only if not in denied set — defensive guard)
foreach (var allow in overrides.Where(o => o.OverrideType == PermissionOverrideType.Allow))
    if (!deniedPermissions.Contains(allow.PermissionId))
        permissions.Add(allow.PermissionId);
```

### Efficient EF Core query for permission union

Prefer a single query with `SelectMany` rather than N+1 per group:

```csharp
// Efficient: one query to get all permissions via Group chain
var groupPermissions = await db.UserGroups
    .Where(ug => ug.UserId == userId)
    .SelectMany(ug => ug.Group.GroupRoles.Select(gr => gr.Role))
    .Union(db.UserGroups
        .Where(ug => ug.UserId == userId)
        .SelectMany(ug => ug.Group.GroupRoleSets
            .SelectMany(grs => grs.RoleSet.RoleSetRoles.Select(rsr => rsr.Role))))
    .SelectMany(r => r.RolePermissions.Select(rp => rp.PermissionId))
    .Distinct()
    .ToListAsync(ct);
```

Check if navigation properties `GroupRoles`, `GroupRoleSets`, `RoleSetRoles`, `RolePermissions` are configured as collections on the entities (they are, per entity inspection). If EF query translation fails, fall back to explicit joins.

### Integration test setup pattern

Follow the existing `UserOverrideIntegrationTests.cs` (from 4b-1) pattern for test setup:
- Use `WebApplicationFactory<Program>` or `IntegrationTestBase`
- Seed via `AppDbContext` directly in test setup
- Use `TestTokenFactory` to produce tokens for auth headers
- Assert by decoding the issued JWT claims

Look at `tests/OneId.Server.IntegrationTests/` for existing base classes — do NOT recreate infrastructure that already exists.

### Files from 4b-1 that this story interacts with

- `src/OneId.Server/Domain/Entities/UserPermissionOverride.cs` — read overrides from this entity
- `src/OneId.Server/Domain/Enums/PermissionOverrideType.cs` — `Allow = 0`, `Deny = 1`
- `src/OneId.Server/Application/TenantAdmin/UserOverrides/Commands/CreateUserOverrideHandler.cs` — may need `cache.Remove` call added here
- `src/OneId.Server/Application/TenantAdmin/UserOverrides/Commands/DeleteUserOverrideHandler.cs` — same

---

## File List

**New files:**
- `src/OneId.Server/Domain/Services/IPermissionEvaluator.cs`
- `src/OneId.Server/Application/Permissions/PermissionEvaluator.cs`
- `src/OneId.Server/Application/TokenPipeline/PermissionEvaluationEnricher.cs`
- `tests/OneId.Server.IntegrationTests/PermissionEvaluationPipelineTests.cs`

**Modified files:**
- `src/OneId.Server/Infrastructure/OpenIddict/TokenPipelineExtensions.cs` — register `IPermissionEvaluator` + `PermissionEvaluationEnricher` after `RoleClaimsEnricher`

---

## Change Log

- **2026-05-27:** Implemented story 4b-2 — `IPermissionEvaluator` interface, `PermissionEvaluator` service, `PermissionEvaluationEnricher` enricher, 5 integration tests. All 5 new tests pass; 6 pre-existing failures unchanged; 0 regressions.

---

## Dev Agent Record

**Implementation notes:**
- `PermissionEvaluator` uses explicit junction-table joins (`db.UserGroups`, `db.GroupRoles`, `db.GroupRoleSets`, `db.RoleSetRoles`, `db.RolePermissions`) rather than navigation property traversal. This avoids triggering `Group`, `Role`, `RoleSet` query filters which would throw when `TenantContext` is not initialized during token issuance.
- `UserPermissionOverride` query uses `IgnoreQueryFilters()` + explicit `o.TenantId == tenantId` filter for the same reason.
- `RolePermission.PermissionId` is a `Guid` FK to `Permission.Id` (not a string FK). The evaluator joins to `Permission` to select `Permission.PermissionId` (the string like `"od.crm.read"`).
- Caching deferred to 4b-3: `MemoryCacheService` auto-prefixes keys with `TenantId` when HTTP context has an initialized TenantContext. During token issuance, TenantContext is not initialized (no `tid` claim in the incoming request) → no prefix. This means any cache set by the evaluator cannot be reliably invalidated from mutation handlers (different prefix state). 4b-3 must address this before claiming the 40ms p95 gate.
- `PermissionEvaluationEnricher` adds one `Claim("permissions", permissionId)` per string in the result set. OpenIddict serializes multiple claims of the same type as a JSON array in the JWT.
- `RoleClaimsEnricher` is completely unchanged — the new enricher is registered additively.
- 5 integration tests use the real Postgres Testcontainers DB, seed Groups/Roles/RoleSets via DbContext directly, issue real tokens via the 2-step auth flow, and decode the JWT payload to assert on the `permissions` claim.

---

## Completion Note

Story ready-for-dev. All prerequisites satisfied:
- `ITokenClaimsEnricher` pipeline is operational (Story 2.4)
- `UserPermissionOverride` entity and table exist (Story 4b-1)
- All Group, Role, RoleSet, RolePermission, UserGroup entities are stable (Epic 4a)
- `ICacheService` is available (AR-10, implemented in Epic 1)
