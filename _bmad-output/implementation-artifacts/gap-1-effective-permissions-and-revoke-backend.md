# Story gap-1: Effective Permissions & Token Revoke — Backend Endpoints

**Status:** done
**Epic:** Phase 8 completion
**Story ID:** gap-1
**Prerequisite:** Epic 4b complete ✓ — `PermissionEvaluator`, `UserPermissionOverride`, `UserGroup`/`GroupRole`/`RoleSetRole` tables all stable.

---

## Story

As a frontend developer,
I want backend endpoints that expose a user's resolved permissions (with provenance) and allow token revocation,
so that the SPA can remove all mock data and operate entirely against the real backend.

---

## Context

Four backend endpoints are missing that block Phase 8 frontend work:

1. `GET /api/account/permissions` — the calling user's own permission IDs (needed by `useHasPermission`; currently returns `[]` from a stub in `queries/hooks/usePermissions.ts:64`).
2. `GET /api/tenant/users/{userId}/effective-permissions` — a Tenant Admin inspecting another user's full permission set with provenance chains (needed by `EffectivePermissionsPanel` live mode, `features/users/api.ts:12`).
3. `POST /api/tenant/effective-permissions/preview` — hypothetical permission evaluation given a set of group IDs (needed by `EffectivePermissionsPanel` preview mode, `features/users/api.ts:27`).
4. `POST /api/tenant/users/{userId}/revoke-tokens` — revoke all active JTIs for a specific user (needed by `DenyOverrideSheet` "Force Re-authenticate"; the service from Story 2.6 already exists but has no HTTP endpoint).

The existing `PermissionEvaluator` returns only a flat `IReadOnlySet<string>`. Endpoints 2 and 3 require a richer response including provenance chains. A new `EffectivePermissionsQuery` handler is needed for those.

**Frontend type contract** (do not change these — they are already live in `src/OneId.Web/src/features/users/schemas.ts`):
```typescript
interface ProvenanceNode { nodeType: 'user'|'group'|'roleSet'|'role'|'permission'; id: string; label: string; href: string }
interface PermissionEntry { id: string; label: string; isDenied: boolean; provenanceChain: ProvenanceNode[]; diffStatus?: 'added'|'removed'|'unchanged' }
interface EffectivePermissionsResponse { userId: string; resolvedAt: string; hasGroupAssignments: boolean; permissions: PermissionEntry[] }
```

---

## Acceptance Criteria

### AC1 — `GET /api/account/permissions` (current user's own permissions)

**Given** an authenticated user calls `GET /api/account/permissions`
**When** the request is processed
**Then** the response is HTTP 200 with `{ "permissions": ["od.crm.read", "od.finance.read", ...] }` — a flat string array of the calling user's effective permission IDs
**And** the evaluation uses `PermissionEvaluator.EvaluateAsync(userId, tenantId)` — the same evaluator used for token issuance (including DENY overrides and expiry filtering)
**And** `userId` and `tenantId` are read from the authenticated JWT's `sub` and `tid` claims respectively
**And** a user with no group assignments receives HTTP 200 with `{ "permissions": [] }` — not 404
**And** the endpoint is secured — unauthenticated requests return HTTP 401
**And** no `TenantAdmin` role is required — any authenticated user can call this endpoint

### AC2 — `GET /api/tenant/users/{userId}/effective-permissions` (admin inspects user)

**Given** a Tenant Admin calls `GET /api/tenant/users/{userId}/effective-permissions`
**When** the request is processed for a user in the Tenant Admin's tenant
**Then** the response is HTTP 200 with an `EffectivePermissionsResponse` body matching this shape:
```json
{
  "userId": "...",
  "resolvedAt": "2026-05-29T10:00:00Z",
  "hasGroupAssignments": true,
  "permissions": [
    {
      "id": "od.crm.read",
      "label": "",
      "isDenied": false,
      "provenanceChain": [
        { "nodeType": "user",       "id": "...", "label": "Jane Smith",     "href": "" },
        { "nodeType": "group",      "id": "...", "label": "Sales Team",     "href": "" },
        { "nodeType": "role",       "id": "...", "label": "CRM Viewer",     "href": "" },
        { "nodeType": "permission", "id": "od.crm.read", "label": "", "href": "" }
      ]
    }
  ]
}
```
**And** DENY-overridden permissions are included in the response with `"isDenied": true` — they are NOT filtered out; the frontend renders them with a `DenyOverrideBadge`
**And** `hasGroupAssignments` is `true` if the user belongs to at least one group, `false` otherwise
**And** `label` fields on `ProvenanceNode` and `PermissionEntry` are set to `""` (empty string) — the frontend resolves human-readable labels from `getPermissionLabel()` and entity names
**And** `href` on all `ProvenanceNode` entries is `""` — the frontend constructs navigation URLs from nodeType + id
**And** if `userId` does not belong to the calling Tenant Admin's tenant, the response is HTTP 404
**And** the endpoint requires `TenantAdmin` role

**Implementation note:** `PermissionEvaluator.EvaluateAsync` returns only the final permitted set without DENY detail or provenance. This endpoint requires a new `GetEffectivePermissionsHandler` that executes the same query logic but tracks the source chain per permission. The provenance chain per permission follows the shortest path: `User → Group → [RoleSet →] Role → Permission`. If a permission is granted via multiple paths, include only the first path found (deterministic ordering by group name then role name). DENY overrides add a single-node chain: `[{ nodeType: "user", id: userId, label: "" }]`.

**Test:** `EffectivePermissionsIntegrationTest` — seeds a user in Group G1 (Role R1 → Permission `od.crm.read`) and Group G2 (RoleSet RS1 → Role R2 → Permission `od.finance.read`), plus a DENY override on `od.crm.write`. Asserts: `od.crm.read` present with provenance chain `[user, group:G1, role:R1, permission]`; `od.finance.read` present with provenance `[user, group:G2, roleSet:RS1, role:R2, permission]`; `od.crm.write` present with `isDenied: true`; `hasGroupAssignments: true`.

### AC3 — `POST /api/tenant/effective-permissions/preview`

**Given** a Tenant Admin calls `POST /api/tenant/effective-permissions/preview`
**When** the request body is `{ "groupIds": ["guid1", "guid2"] }`
**Then** the response is HTTP 200 with an `EffectivePermissionsResponse`
**And** the evaluation treats the supplied `groupIds` as the hypothetical group memberships — the actual user's current group memberships are NOT used
**And** `userId` in the response is `""` (preview has no real userId)
**And** `hasGroupAssignments` is `true` if `groupIds` is non-empty, `false` if empty
**And** DENY overrides are NOT applied in preview mode (preview shows what group-sourced permissions would be, without per-user overrides)
**And** `groupIds` that do not belong to the calling Tenant Admin's tenant are silently ignored (tenant isolation — cross-tenant group IDs return no permissions, not an error)
**And** an empty `groupIds` array (`{}` or `{ "groupIds": [] }`) returns HTTP 200 with empty `permissions` array

**Also accepted in request body:** `{ "roleSets": ["guid1"] }` and `{ "overrides": [{ "permissionId": "od.crm.read", "effect": "DENY" }] }` — matching the `PreviewPayload` TypeScript type in `schemas.ts`. These fields extend the evaluation accordingly. `roleSets` adds direct role-set permissions (not via a group). `overrides` with `effect: "DENY"` removes the permission from the result.

**Test:** `EffectivePermissionsPreviewIntegrationTest` — calls preview with `groupIds: [G1.id]` and asserts the response contains exactly G1's role permissions. Calls preview with empty groupIds and asserts empty permissions array. Calls preview with a groupId from Tenant B while authenticated as Tenant A admin — asserts it is silently ignored.

### AC4 — `POST /api/tenant/users/{userId}/revoke-tokens`

**Given** a Tenant Admin calls `POST /api/tenant/users/{userId}/revoke-tokens`
**When** the request is processed for a user in the Tenant Admin's tenant
**Then** all active OpenIddict authorization records for that user are revoked (uses the same revocation service from Story 2.6 — `UserTokenRevocationService` or equivalent)
**And** a subsequent introspection call for any of that user's previously-valid tokens returns `active: false`
**And** the response is HTTP 204 (No Content) on success
**And** if `userId` does not belong to the calling Tenant Admin's tenant, the response is HTTP 404
**And** the endpoint requires `TenantAdmin` role

**Test:** `RevokeUserTokensIntegrationTest` — issues a token for a user, calls `POST /api/tenant/users/{userId}/revoke-tokens`, introspects the token, asserts `active: false`.

### AC5 — Placement and routing

**Given** the four new endpoints are added
**When** a developer inspects the controllers
**Then** `GET /api/account/permissions` is on a new `AccountPermissionsController` (or added to the existing `AccountController`)
**And** `GET /api/tenant/users/{userId}/effective-permissions` is added to `TenantUsersController` as `[HttpGet("{id:guid}/effective-permissions")]`
**And** `POST /api/tenant/effective-permissions/preview` is on a new `TenantEffectivePermissionsController` at route `api/tenant/effective-permissions`
**And** `POST /api/tenant/users/{userId}/revoke-tokens` is added to `TenantUsersController` as `[HttpPost("{id:guid}/revoke-tokens")]`

### AC6 — No performance regression

**Given** `GET /api/tenant/users/{userId}/effective-permissions` is called
**When** the user has ≤20 groups and ≤200 permissions
**Then** the response time is under 300ms p95 (excluding network) — the provenance query adds joins but must not scan the full permission table
**And** the endpoint does NOT use `ICacheService` for this release — provenance data must always be fresh (cache can be added later)
**And** `GET /api/account/permissions` DOES use `ICacheService` with the same TTL as `PermissionEvaluator` (5 minutes) since it returns the same flat set

---

## Out of Scope

- Frontend wiring (covered in gap-2)
- Seat usage in the response (covered in Phase 6 licensing stories)
- Dimensional attributes in the response (these are in the introspection response; not needed for the EffectivePermissionsPanel which only shows permissions)

---

## Tasks / Subtasks

- [x] Task 1: Create EffectivePermissionsDto types (ProvenanceNodeDto, PermissionEntryDto, EffectivePermissionsResponse)
- [x] Task 2: Implement GetEffectivePermissionsHandler (AC2 — user inspect with provenance)
- [x] Task 3: Implement EffectivePermissionsPreviewHandler (AC3 — preview mode)
- [x] Task 4: Add AccountPermissionsController with GET /api/account/permissions (AC1)
- [x] Task 5: Add effective-permissions + revoke-tokens actions to TenantUsersController (AC2, AC4)
- [x] Task 6: Create TenantEffectivePermissionsController with POST /api/tenant/effective-permissions/preview (AC3)
- [x] Task 7: Register new handlers in TenantServiceExtensions
- [x] Task 8: Add missing Permissions constants (CrmRead, CrmWrite, FinanceRead, FinanceWrite) and catalog entries (fixes pre-existing compile error in PermissionEvaluationPipelineTests)
- [x] Task 9: Write integration tests — EffectivePermissionsIntegrationTests, EffectivePermissionsPreviewIntegrationTests, RevokeUserTokensIntegrationTests
- [x] Task 10: Verify all new tests pass (13/13 pass), no regression in unit tests (11/11 pass)

### Review Findings

- [x] [Review][Defer] Tenant isolation in `GetEffectivePermissionsHandler`: user existence check relies on EF query filter; DENY path uses `IgnoreQueryFilters()` + explicit `tenantId` — refactor to EF Core 10 named query filters (`"tenant"` / `"softDelete"`) to make isolation bypass-safe across all entities. [`GetEffectivePermissionsHandler.cs`] — deferred, refactor after poc
- [x] [Review][Patch] Add ALLOW overrides to `GetEffectivePermissionsHandler` — query both DENY and ALLOW overrides to mirror `PermissionEvaluator`; ALLOW-only permissions get provenance `[{ nodeType: "user", id: userId }]` with `isDenied: false` [`GetEffectivePermissionsHandler.cs`]
- [x] [Review][Dismiss] `GET /api/account/permissions` caching — `IPermissionEvaluator` already uses `ICacheService` internally; spec requirement met transitively. No controller-level cache needed.
- [x] [Review][Dismiss] Preview endpoint `TenantAdmin` requirement — intent confirmed; spec's "Given a Tenant Admin calls…" language implies the gate; all tenant-scoped data endpoints consistently require TenantAdmin.
- [x] [Review][Patch] `HasGroupAssignments` in preview uses `request.GroupIds.Count > 0` instead of `validGroupIds.Count > 0` — fixed to use `validGroupIds.Count > 0` [`EffectivePermissionsPreviewHandler.cs`]
- [x] [Review][Patch] `validRoleSetIds.Select(rs => rs.Id).Contains(rsr.RoleSetId)` inside LINQ-to-SQL — pre-materialized to `rsIds` list [`EffectivePermissionsPreviewHandler.cs`]
- [x] [Review][Patch] No size cap on `PreviewRequest.GroupIds`, `RoleSets`, `Overrides` — added 100-item guard in controller returning 400 [`TenantEffectivePermissionsController.cs`]
- [x] [Review][Patch] `PreviewOverrideEntry.Effect` not validated — null/empty PermissionId guard added in handler; Effect="DENY" enforcement added in controller returning 400 [`EffectivePermissionsPreviewHandler.cs`, `TenantEffectivePermissionsController.cs`]
- [x] [Review][Defer] Soft-deleted users: `RevokeTokens` returns 204, `GetEffectivePermissions` returns 404 — pre-existing inconsistency between `GetUserHandler` (IgnoreQueryFilters + explicit tenantId check) and `db.Users.AnyAsync` (EF query filter excludes deleted users) [`TenantUsersController.cs`] — deferred, pre-existing

---

## Dev Notes

- `GetEffectivePermissionsHandler` uses EF query filters (ITenantContext is initialized on TenantAdmin requests) for Group, Role, RoleSet isolation. UserPermissionOverride uses IgnoreQueryFilters + explicit tenantId (same pattern as PermissionEvaluator).
- Provenance: first-path-wins, deterministic order (group name → role name). Direct-role paths take precedence over roleSet paths.
- DENY overrides always produce isDenied:true. If also group-granted, the group provenance chain is kept with isDenied:true set. If DENY-only, provenance is a single user node.
- Preview mode applies DENY only from the request body (no DB overrides). Cross-tenant groupIds are silently ignored via the EF query filter on db.Groups.
- Revoke tokens delegates to IUserTokenRevoker.RevokeAllUserTokensAsync (same service used by password reset in AccountController).
- Tests seed in SystemTenantId (TotpUser's tenant) to ensure tenant isolation works correctly.

---

## Dev Agent Record

### Completion Notes

Implemented all 4 endpoints satisfying AC1–AC6:
- `GET /api/account/permissions` on `AccountPermissionsController` — delegates to `IPermissionEvaluator` (caching already built-in with 5-min TTL)
- `GET /api/tenant/users/{id}/effective-permissions` on `TenantUsersController` via `GetEffectivePermissionsHandler` — returns `EffectivePermissionsResponse` with full provenance chains
- `POST /api/tenant/effective-permissions/preview` on `TenantEffectivePermissionsController` via `EffectivePermissionsPreviewHandler` — hypothetical group evaluation, no DB overrides, tenant isolation via query filter
- `POST /api/tenant/users/{id}/revoke-tokens` on `TenantUsersController` — delegates to `IUserTokenRevoker`

Also fixed pre-existing compile error in `PermissionEvaluationPipelineTests`: added `Permissions.CrmRead/Write/FinanceRead/Write` constants and `PermissionCatalog` entries.

13 new integration tests all pass. 11 unit tests all pass. Pre-existing test failures (45) are unrelated to this story.

---

## File List

- `src/OneId.Server/Application/Permissions/EffectivePermissionsDto.cs` (new)
- `src/OneId.Server/Application/Permissions/GetEffectivePermissionsHandler.cs` (new)
- `src/OneId.Server/Application/Permissions/EffectivePermissionsPreviewHandler.cs` (new)
- `src/OneId.Server/Controllers/AccountPermissionsController.cs` (new)
- `src/OneId.Server/Controllers/TenantEffectivePermissionsController.cs` (new)
- `src/OneId.Server/Controllers/TenantUsersController.cs` (modified — added 2 actions + handler injections)
- `src/OneId.Server/Application/TenantAdmin/TenantServiceExtensions.cs` (modified — registered 2 handlers)
- `src/OneId.Server/Application/Common/Permissions.cs` (modified — added 4 constants)
- `src/OneId.Server/Infrastructure/Persistence/Seeds/PermissionCatalog.cs` (modified — added 4 entries)
- `tests/OneId.Server.IntegrationTests/EffectivePermissionsIntegrationTests.cs` (new)
- `tests/OneId.Server.IntegrationTests/EffectivePermissionsPreviewIntegrationTests.cs` (new)
- `tests/OneId.Server.IntegrationTests/RevokeUserTokensIntegrationTests.cs` (new)

---

## Change Log

- 2026-05-29: Implemented gap-1 — 4 backend endpoints for effective permissions and token revocation. Added provenance handler, preview handler, and 3 integration test suites (13 tests). Fixed pre-existing `PermissionEvaluationPipelineTests` compile error by adding missing permission constants.
