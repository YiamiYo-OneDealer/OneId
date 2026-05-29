# Story gap-1: Effective Permissions & Token Revoke — Backend Endpoints

**Status:** draft
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
